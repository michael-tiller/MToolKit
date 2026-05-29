# Visual Graphs — Troubleshooting

Symptom-indexed guide. If a graph is behaving wrong, check here first; entries are written so the failure mode you're seeing is the heading, not the architectural concept.

For architecture: see `README.md`, `MESSAGE_DATA_FLOW.md`, `ARCHITECTURE_TYPE_SYSTEM.md`. For roadmap / planned features: `VISUAL_GRAPHS_ROADMAP.md`.

---

## Objective progress jumps to its target on a single event

**Symptom.** A `QuestObjectiveIncrementNode` (Amount = 1) takes progress from `0/N` to `N/N` on the very first matching event in one frame. Diagnostic window or progress logs show `Previous → Current` walking 0→1, 1→2, … N-1→N back-to-back within the same dispatch.

**Cause.** Per-message reentrancy. Many objective graphs are authored with a feedback edge so `Check` re-evaluates after `Increment`:

```
OnEventNode → Check → (Incomplete) → Increment → Check (back-edge)
                   → (Complete) → ...
```

This is **intended authoring** (it lets `Check` route to `Complete` on the same tick the increment satisfies the target). But a single message dispatched into the graph traverses the back-edge until `Check` finally branches to `Complete`, so `Increment` fires `Required` times instead of once.

**Fix.** `GraphRunner.HandleMessageAsync` carries a per-dispatch `HashSet<string> executedThisDispatch`. When dequeuing a node, the runner skips it (Verbose log: "already executed this dispatch, skipping (feedback-edge guard)") if its ID is already in the set. With the guard in place, `Increment` runs once per message; `Check` may re-fire (which is fine — it's a no-op, since it just reads state and routes), and the loop self-terminates.

**Don't.** Strip the back-edge from the graph asset or the importer (`QuestGraphWirer.WireObjectiveGraph` line ~141). The edge is intentional and the runner-level guard is the right place to enforce "once per dispatch".

**Reference.** `GraphRunner.cs` ~line 168 (the guard), `MaxExecutionSteps` (256) is the broader safety net for runaway dispatches but should never trip in practice.

---

## A single global message ticks an objective N times (no feedback edge)

**Symptom.** Different from the above: progress increments by N on a single event, but the graph is *not* using the back-edge pattern. Logs show `RegisterRunner` called multiple times for the same `runnerId`, or `byTypeDomain[message]` contains duplicates.

**Cause.** `GraphEventRouter.RegisterRunner` does not deduplicate. If `LoadObjectiveGraphsAsync` (or any other code path) registers the same runner twice, the dispatch list contains the runner twice and each subscription fires twice per message.

**Fix.** Audit the call sites of `RegisterRunner`. As of writing, `QuestManager.LoadObjectiveGraphsAsync` and `StartQuestAsync` are the canonical callers; `StartQuestAsync` rejects duplicate quest starts up-front (`activeQuests.ContainsKey` check), so this path is safe in normal use. If you see duplicates in production logs, check for paths that bypass the dedup — e.g. campaign restoration, hot-reload flows, or test fixtures.

**Don't confuse this with the reentrancy case above.** The feedback-edge case shows N executions of the *same* node within one dispatch frame; this case shows separate dispatches to multiple registered runner instances. The dispatch-strategy log line in `GraphEventRouter.RouteAsync` is your discriminator: if it says "to N graph(s)" with N > 1 for what should be a single graph, you have a registration leak.

---

## Quest objective progress never moves past 0

**Symptom.** The objective graph receives the message (you see "Graph X received message Y" in logs), but progress stays at `0/N` forever. No `QuestObjectiveIncrementNode` execution log lines.

**Cause options:**
1. `Check` is branching to `Complete` on first tick because state has stale `Current >= Required` from a prior session or a save-restore that leaked across new-game boundaries.
2. The `Incomplete` port has no connections in the graph asset (authoring mistake — would short-circuit before reaching `Increment`).
3. The objective's `RequiredProgress` was set to 0 somewhere, making `IsComplete` true on `0 >= 0`.

**Fix.** Read the graph state at the `objective_{guid}` key — if it shows `N/N (Complete: true)` before any kill, the state is stale (most commonly because `QuestManager` doesn't have an `ISaveDomainController` and isn't reset on new-game). Verify by clearing graph state at session start, or by implementing a reset hook.

**Reference.** `QuestObjectiveCheckNodeExecutor.cs` line ~83 (`var key = $"objective_{objectiveGuid}";`).

---

## Graph subscribes but never receives a message

**Symptom.** `RegisterRunner` log shows the subscription was registered, but `Routing message X` is never followed by a dispatch to that runner.

**Cause options:**
1. **Domain mismatch.** `GraphEventRouter.RouteAsync` first tries exact `(messageType, domain)` match, then falls back to wildcard (empty domain). If your graph's `DomainFilter` is set but the publisher passes `null` or a different domain, the route misses.
2. **Type mismatch.** The graph's `MessageSubscription.MessageType` is resolved via `assemblyQualifiedName`. If the asset stores a stale name (refactored type, moved namespace, removed assembly), the subscription is recorded as invalid (Debug log: "invalid subscription"). Re-import the graph asset.
3. **Broker not registered.** The message type itself must be registered on the broker (`builder.RegisterMessageBroker<T>(options)`). Unregistered types throw at the publish call site, not silently — but a graph that *expects* messages and never sees any could be downstream of an upstream publish failure.

**Fix.** In order of effort: (1) check the publisher's domain string vs the subscription's `DomainFilter`; (2) re-import the offending graph asset; (3) confirm the type is registered with the broker (`DirigibleGameInstaller.cs` ~line 472 in Dirigible's case).

---

*Add new entries above this line as bugs surface. Each entry: **Symptom** (what you'd grep for), **Cause**, **Fix**, optional **Don't** for known anti-fixes, and a **Reference** line pointing to the canonical code path.*
