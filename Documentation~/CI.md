# Licensed Windows menu CI

This lane verifies the hierarchical action-menu feature using Unity 6000.3.10f1 on Michael's licensed Windows host. It is prepared infrastructure until a real Actions job completes. Local Pester tests do not establish hosted CI, Unity licensing, graphical correctness, or player acceptance.

## Trust boundary

The workflow uses `pull_request`, so its first same-repository PR can run before the workflow reaches the default branch. Only a PR raised/synchronized by `michael-tiller`, whose head repository equals the base repository, can queue the menu job. There is no `pull_request_target`, fork checkout, admin token, dependency cache, or persistent runner service.

YAML conditions are defense in depth. The operator starts a fresh, repository-scoped ephemeral runner for one exact reviewed run ID and attempt. It has no default labels; its only label is `licensed-menu-<run-id>-<attempt>`. An external `ACTIONS_RUNNER_HOOK_JOB_STARTED` script rejects any other repository, actor, PR, head, merge commit, workflow commit, job, or event before job steps. It reads an approval file beside the installed tools, outside the runner application and workspace. It also verifies copied tool hashes and the private host configuration hash. Never enroll a general-purpose `self-hosted` runner on the public MTK repository.

Review the exact GitHub synthetic merge commit and workflow before starting the runner. The same-repository branch must contain trusted code: this executes project and test code with the licensed interactive user's access. Ephemeral registration and a new integration checkout reduce reuse; they are not an OS sandbox or protection against a deliberately approved malicious change. GitHub warns about self-hosted runners on public repositories. Fork code is not eligible for this lane.

## Host preparation

The hook writes its two approved tool paths through the runner-provided `GITHUB_ENV` file. GitHub's hook provider processes these file commands after the hook returns, making them available to later job steps. It does not rely on a child process changing its parent environment. A local test launches the real writer in a hidden child, checks that the parent is unchanged, and applies the resulting file entries exactly as the runner handoff expects. This is a file-command dry run, not an enrolled Actions run.


1. Keep the dependency-complete primary Dirigible project available. It supplies complete ignored roots, including sibling `.meta` files: `Assets/ThirdParty`, `Assets/Plugins`, `Assets/Packages`, `Assets/ExternalDependencyManager`, `Assets/TextMesh Pro`, and `Packages/com.ivanmurzak.unity.mcp`. No vendor assets are committed or uploaded.
2. Use the logged-in, licensed `metag` desktop session, never a Windows service or the Codex sandbox identity. Confirm the exact `WindowsIdentity.GetCurrent().Name`; set `ExpectedIdentity` to that value. Session 0 is rejected. Unity licensing is ultimately verified by the fresh Unity process and log.
3. Copy `host-config.example.json` to a private location outside both repositories. Replace every placeholder. Pin `ConsumerRevision` to the reviewed private Dirigible commit used for MTK CI. List every external `file:` dependency. Git packages require exact commits matching the consumer's committed dependency lock; archives require SHA256. Tracked embedded packages retain their contents from the tested commit. Missing/mismatched pins fail before Unity; do not silently rewrite the source lock.
4. Use separate private `RunsRoot`, `ControlRoot`, and `RunnersRoot` directories. They must not contain junctions/symlinks. These are disposable CI storage, separate from task worktrees and the primary editor. Disk quota/retention is an operator responsibility: the scripts preserve failed evidence and never recursively delete directories.
5. Download and verify the official current Windows x64 Actions runner using GitHub's repository Settings → Actions → Runners instructions and release checksum. Extract a fresh unconfigured copy under `RunnersRoot` for this run attempt. This setup does not download, install, or enroll it automatically. No service installation, auto-update disabling, machine environment edits, or Unity license export is needed.

Keep Actions' approval restrictions for outside collaborators enabled. No secret or private project checkout is supplied to public workflows. Only the official upload action is used, pinned to an exact commit.

## Queue and enroll one run

Push the trusted branch and open/synchronize its same-repository PR. The workflow queues the job while the per-run runner is offline. Read the canonical metadata from that queued run: repository, run ID, attempt, PR number, actor, head SHA, merge SHA, and workflow SHA. Fetch the PR merge commit into an authorized local source repository, inspect its workflow and source, and retain that exact commit locally. This preparation must use actual GitHub metadata, not a branch name or guessed SHA.

Run the reviewed `Start-LicensedMenuRunner.ps1` in the interactive user session. For example, with actual values substituted:

```powershell
$parameters = @{
  ReviewedSourceRoot = 'E:/ReviewedSource'
  HostConfigPath = 'E:/LicensedMenuCI/host-config.json'
  ControlDirectory = 'E:/LicensedMenuCI/control/repo-run-attempt'
  RunnerDirectory = 'E:/LicensedMenuCI/runners/repo-run-attempt'
  Repository = 'michael-tiller/MToolKit'
  RunId = '<actual numeric ID>'
  RunAttempt = '1'
  PullRequest = 123
  MergeSha = '<actual 40-character merge SHA>'
  WorkflowSha = '<same reviewed workflow commit SHA>'
  HeadSha = '<actual PR head SHA>'
}
# Optional offline preparation: creates a reviewable plan, registers nothing.
& ./Tools/CI/Start-LicensedMenuRunner.ps1 @parameters
```

Preparation writes the reviewed tool snapshot, approval, vendor root hashes, copied host configuration, and runner plan to the fresh control directory. It reads ignored vendor contents only to identify the exact approved snapshot.

For actual registration/start, use a fresh control directory with the same reviewed run values, obtain a short-lived repository registration token from GitHub Settings, and provide it only as the process environment variable `ACTIONS_RUNNER_INPUT_TOKEN`. Invoke with `-StartRunner`. The token is passed through the configuration child's environment, not a command-line argument or checked-in file. The run process does not inherit that token. No runner is registered without this explicit switch.

The listener is launched hidden as the interactive user, processes one job, and GitHub automatically deregisters the ephemeral runner. Retain `runner-plan.json`, private listener logs and job evidence. Review/retire the exact owned runner process and storage afterward; do not stop foreign Editors or remove unrelated runners. A plan-only invocation cannot later reuse its populated directory; use a fresh control directory for actual enrollment. Each rerun attempt requires new metadata and a fresh runner.

## What executes

The workflow calls the approved external tools; it does not checkout or execute arbitrary workflow-branch scripts. The tools clone exact committed Git snapshots into a new per-run integration root. They hydrate every required ignored dependency root and sibling metadata as a whole, including roots with tracked candidate files: only missing files are copied, identical existing files remain untouched, and conflicting contents fail before that root is copied. Directory ignore checks use the trailing slash and inspect rules independently of tracked index entries. Links are rejected. `Library`, `Temp`, editor caches, and a linked worktree's `.git` file are never copied.

Git checkout line endings are the sole content-equivalence exception. An existing tracked file must be designated text/EOL by Git attributes, decode as strict UTF-8 without NUL bytes, and differ only by CRLF versus LF. The candidate's original bytes are preserved. The private hydration receipt records both raw hashes and the canonical hash for that equivalence. Enrollment and copied-root manifests retain raw digests separately and compare canonical digests that normalize only these tracked Git text paths. Ignored copied files and binary data remain byte-exact; other differences fail.

All external Git packages are cloned at their pinned commits; local package archives are copied after digest verification. Only the generated integration manifest is redirected to these isolated paths. The committed dependency lock remains unchanged. For an MTK PR, the explicit candidate replaces the consumer's locked MTK revision, and the private input record records both values. Dirigible CI uses the MTK revision from its committed dependency lock.

The candidate's complete saved sample is staged once at `Assets/Samples/MToolKit/<package-version>/HierarchicalActionMenus`. Existing imported menu samples and their sibling metadata are parked outside the integration project under the run's private directory after ownership/link checks; their original paths and hashes are retained before moving. The source checkout and package assets are never moved or overwritten. This prevents duplicate script GUIDs and assembly definitions while testing the candidate's sample changes.

CI requires exactly one saved `ActionMenuGallery` scene in the candidate sample before staging. A staging-only Editor entry validates and opens that saved scene; it does not regenerate prefabs, save an untitled scene, or call global `SaveAssets`. Missing authored assets fail with a useful diagnostic. The lane then runs:

- MTK context-menu EditMode tests and gallery-builder EditMode tests.
- Dirigible context-menu, priority-fan, and accessible-input regressions for Dirigible PRs.
- The saved menu gallery's PlayMode tests using real uGUI/Input System rendering and input, plus the focused host/canvas lifetime PlayMode tests.

This adds no full-suite, IL2CPP, build, release, or gameplay-smoke gate. Existing declared acceptance and Captain gates remain independent.

Unity uses hidden `cmd.exe /d /s /c start "" /wait`, never `-quit` with tests. Child `ALLUSERSPROFILE` is filled only when missing/blank. Rendered PlayMode checks keep graphics enabled; 1600×1000 dimensions are requested. Every stage has a bounded timeout and unique result/log paths. A passing stage requires a zero launcher exit and fresh, nonempty XML containing only passed tests from the declared selection. Empty, skipped, stale, incomplete, foreign-selection, licensing, package-resolution, and compilation results fail. Timeouts stop only the exact owned child process tree.

## Evidence and privacy

Private Unity logs, XML (including failure text/stack traces), source, ignored assets, runner credentials, and dependency manifests stay on the host. They are never attached to public MTK Actions. The script prints fixed status lines and publishes only fixed-schema aggregate JSON: public repository/revision, public MTK revision, stage, counts, exit and outcome. The private Dirigible integration revision and paths are omitted from public MTK evidence. Only `menu-ci-public/*.json` is uploaded, for seven days. Failure details are read locally by the operator; a missing aggregate is not a pass.

Run the non-Unity checks from either repository:

```powershell
Invoke-Pester ./Tools/CI/Tests/LicensedMenuCI.Tests.ps1
```

Pester 3.4 and later are supported. Use process-scoped execution policy only if the local shell requires it; do not change machine/user policy. Tests cover path/command boundaries, exact job ownership, full dependency copies/digests, safe evidence, and fresh Unity XML handling. Neither these tests nor source validation enrolls a runner or launches Unity.

## References

- [GitHub secure use of self-hosted runners](https://docs.github.com/en/actions/reference/security/secure-use)
- [Ephemeral runners and routing](https://docs.github.com/en/actions/reference/runners/self-hosted-runners)
- [Job-start hooks and nonzero-exit rejection](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/run-scripts)
- [Workflow events and pull requests](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#pull_request)
- [Official runner environment input handling](https://github.com/actions/runner/blob/main/src/Runner.Listener/CommandSettings.cs)
