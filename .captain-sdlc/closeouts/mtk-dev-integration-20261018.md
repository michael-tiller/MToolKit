# MTK dev integration - 2026-10-18

Task: MTK-DEV-INTEGRATION-20261018. Owner: codex-unified-game-actions-20261013.
Authority: Michael's instruction, "Bring it to dev at least. Let's itemize."

## Delivery and identities

Consolidate the complete reconciled MTK history on canonical local dev, retaining the fetched interceptor squash as a merge parent. This integration contains no new runtime behavior.

- Canonical Git directory: E:/Personal/MToolKit-stray-backup-20260911/.git.
- Owned branch: integration/mtk-dev-20261018.
- Owned checkout: E:/Personal/MToolKit-dev-integration-20261018.
- Local dev before: 130d38a5da24b361425cd4bf06d0d07d64367488.
- Fetched origin/dev: 8565e919d4cd50b82fccb64be01625122a396eab.
- Reconciled source / first parent: 2e744386193e5a81e4b79a227c8fc20ea4c67d0a, version 1.6.2, tree e9620cb570591582f4f200f4f53910abbca1a743.
- Result version: 1.6.3. The integration revision is the merge commit containing this record, identified by trailer Implements: MTK-DEV-INTEGRATION-20261018. Exact post-integration OID is also recorded in the operator handoff.
- Source history: 34 commits beyond the previous local dev. The fetched squash duplicates earlier interceptor implementation; its history is preserved rather than discarded.

## Incoming code, itemized

| Group | Retained changes |
| --- | --- |
| Message bus | Common broker core and scopes; priority-banded pre/post interceptors and cancellation; lifecycle diagnostics and deduplication; characterization/interceptor regression tests. |
| Persistence | ES3 batching/off-thread flush; ordered save/load transactions; fail-closed initialization and controller isolation; load generations and failed-controller reporting; durable save contexts/receipts; save-before-clone and discarded-local-restore recovery; cancellable frame-separated serialization. |
| VG substrate | Shared typed literal syntax; scoped arithmetic/logic; value transforms, map/combine/extract/format/select; event-derived subscriptions; ordered isolated game rules; owned event teardown and compatible default interface members; async fault preservation. |
| Input | Pointer/button/cancel edges; enabled-backend bootstrap fallback; safe keyboard/mouse handling when devices are absent. |
| Audio, startup and services | Audio cue assets and UI components; bootstrap destroy-race and scene matching repairs; resolver/plugin lifecycle isolation; logging/console improvements; quest Addressables validation. |
| Tests and documentation | The associated package tests, fixtures, contracts and prior release records travel with their source history. |

## Merge resolution and verification

Four squash-related conflicts were reconciled in CHANGELOG.md and the GameMessageBroker, GlobalAsyncMessageBroker and MessageBrokerCore files. The source already contains the remote interceptor behavior plus later lifecycle reporting; the later source postimages are retained. GlobalInstaller's automatic merge is checked against the same source to preserve async scene matching and plugin isolation.

All tracked runtime, editor and test blobs must remain identical to source 2e744386193e5a81e4b79a227c8fc20ea4c67d0a, apart from trailing whitespace removed from three empty YAML values in each of Tests/Editor/MessageBus.meta and Tests/Editor/MessageBus/Support.meta. GUIDs and all non-whitespace metadata remain unchanged. VERSION, package.json and the version-linked changelog advance together to 1.6.3. This record is the only additional file.

Pre-integration checks: no unresolved entries; incoming whitespace clean; explicit staged-path review; version agreement; both merge parents preserved; source blob equivalence; target ancestry and compare-and-swap fast-forward. The target ref is advanced only after these checks. The operator's post-integration record retains the exact final hash and clean-state results.

## Retained proof and limits

No runtime/test implementation changed during this integration. Existing proof is retained at its actual revision; it is not represented as a newly executed 1.6.3 test, build or CI run.

- Dirigible handoff E:/Personal/dirigible2D/.captain-sdlc/handoffs/unified-game-actions-closure.md records focused green: Hotbar 9, Autosave 6, PerfCounters 12, ordered load 11, recovery 29, VG dispatch 11, legacy interfaces 4, Bootstrap 9, MTK logging 8 and ES3 PlayMode 4. The committed 1.6.2 tree exactly matches the exercised staged union; original receipt source associations remain intact.
- Development build: E:/Personal/dirigible2D/.captain-sdlc/worktrees/intent-918adb0a-9010-4b6e-8d0e-5c20cd19ca7f/Artifacts/DirigibleEaVerifier/build-development.json. Consumer revision c722d57763c5220c1ad42c2f190711c3330e93e3, Unity 6000.3.10f1, product 2.610.7, Mono, result Succeeded, errors 0, warnings 9.
- The committed consumer dependency lock pins exact MTK 2e744386193e5a81e4b79a227c8fc20ea4c67d0a. The retained executable matches SHA256 a984c472ff891f45f0ab9be083e100916ed1c5db9cdcb98138174782f7071dc7; the build receipt and dependency lock establish its source association, rather than the generic launcher hash alone.
- Signed consumer integration merge-dfb8e943-7bbd-49be-b319-6f13e45d45e5 retained its passing Build gate at c722d577. See E:/Personal/captain-sdlc/.captain-sdlc/closeouts/merge-back-20261018.md.
- MTK has no committed Captain project policy: this is normal Git integration, not new signed Captain complete/green proof. No fresh MTK 1.6.3 runtime, CI, performance or human acceptance claim is made. Earlier performance failures remain separate follow-ups.

## Retained work and follow-ups for human review

- Preserve checkpoint/mtk-local-overlays-20261012 and its stash: selected repairs already exist in a7e8402 and the reconciled union.
- Preserve checkpoint/mtk-test-assembly-rename-20261012 and its stash: a filename-only assembly rename, with no missing feature established.
- Preserve feat/mtk-interceptor and its original checkout: its input fallback is superseded by the retained null-safe backend handling; its original commit is not claimed as an ancestor.
- Preserve detached save-context checkout at 8b2bf9590ebbacc2a7f9bc6f6c61fa70adbc046c: functionality is ported into the union, not an ancestry merge of that original commit.
- Preserve the historical sound-fix stash and source union's tests-only red-checkpoint stash. Their relevant fixes/tests are already represented; no deletion is authorized.
- main, remote publication, source-checkout relocation and Dirigible dependency-path changes are outside this integration. Existing primary/detached checkouts, stashes and dependency pins remain intact.

Next owner: Michael selects any subsequent main/publication or dependency relocation task. No new feature work is included here.
