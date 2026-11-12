# Rebinding settings transaction — v1.8.1

Owner: Captain, under Michael's instruction to fix Cancel and verify rebinding in a worktree. Registered ordinary checkout: E:/Personal/dirigible2D/.captain-sdlc/MToolKit/.captain-sdlc/worktrees/rebinding-transactions-20261028; branch captain/rebinding-transactions-20261028; base dfd4efc49343d2171bd1d26b88684ec41a520439 (1.8.0). The previously shared checkout is unchanged. This repository has no committed Captain runtime policy; its registered ordinary Git workflow applies.

Delivered: optional canonical InputActionAsset before asynchronous INI initialization; module-owned BindingOverridesJson draft/applied baseline; Apply/Cancel/confirmed Defaults; restore notifications; serialized immutable applied INI snapshots; exact action enablement around rebinding; scoped conflicts and preservation of the prior custom override on rejection. Rows notify the transaction owner when reset. Legacy RegisterAction infers its asset when available. Assetless standalone actions are outside this canonical-asset persistence verification.

Tests were written before implementation. The consuming game's signed RED reproduced F10 Apply -> F11 draft -> Cancel incorrectly restoring Escape. Its final focused class passes 3/3, including the authored canonical asset reference and tab/shared-service lifecycle.

Package proof covers 30 distinct settings cases: 28 passed in the initial 29-case namespace run, then the corrected Defaults assertion and a new actual interactive-conflict completion regression passed in the focused 2-case run. The generic INI getter returns null for empty strings; the test correctly uses its raw string getter. A stale-assembly recheck failure is retained separately; assets-refresh and a loaded-method fence preceded the successful recheck. Fifteen cases cover input transactions and fifteen cover existing settings regressions. Independent review found no task blocker.

Consuming game run: intent-b1172f74-3de1-45cf-bbf4-494a6b17299b, primary E:/Personal/dirigible2D, task checkout beneath its .captain-sdlc/worktrees/. Exact failed-Begin recovery succeeded through installed Captain 0.9.6; the original unstarted reservation was cancelled, not bypassed.

Runtime evidence in the game checkout's Artifacts/RebindingVerification20261028/:

- producer-receipt.json: all pass in Editor PID 40272. Actual authored binding row and manually wired real SettingsView buttons/modal confirmations; F10 Apply, F11 draft/Cancel, visible keys, dirty button state, tabs, Defaults, INI preservation and cleanup. Fixture uses a cloned canonical action asset and isolated INI; no full-player or human acceptance claim.
- fresh-reader-receipt.json: all pass in separate Editor PID 54088. Applied F10 and Defaults load before opening settings; F10 triggers and Escape does not; Cancel preserves the applied baseline; files and input/display/audio state are restored. Focus-independent synthetic input temporarily configures and strictly restores Editor input routing and preexisting device enablement.
- settings-tests-first.xml, defaults-and-conflict.xml, signed-green-receipt.json, screenshots and diagnostic failures retain actual provenance. Earlier failures are diagnosis, not passes.

Runtime proof used the working implementation while package metadata was 1.8.0 and game metadata 2.670.10. The v1.8.1 edits only change version/changelog/handoff metadata; behavior source is unchanged. The consuming game owns its later LOW development build and final closure at .captain-sdlc/closeouts/input-rebinding-20261028.md.

Next owner/action: Captain pins this verified package commit in the owned game dependency lock, commits that task and runs the captured development build. Retain this package worktree/branch for separately authorized integration; no merge or push is claimed.
