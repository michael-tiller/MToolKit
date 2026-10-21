using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.Migration;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PreparationContext = MToolKit.Runtime.Persistence.SavePreparationContext;
using RequiredParticipant = MToolKit.Runtime.Persistence.Interfaces.IRequiredSavePreparationParticipant;

namespace MToolKit.Tests.Editor.Persistence
{

    [TestFixture]
    public sealed class SaveSystemCoordinatorPreparationTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.Instance;

        private static void RequireRuntimeContracts()
        {
            var assembly = typeof(SaveSystemCoordinator).Assembly;
            Assert.That(assembly.GetType("MToolKit.Runtime.Persistence.Interfaces.IRequiredSavePreparationParticipant"),
                Is.Not.Null, "VD5 requires the real runtime ordered async preparation contract.");
            Assert.That(assembly.GetType("MToolKit.Runtime.Persistence.SavePreparationContext"), Is.Not.Null);
            Assert.That(assembly.GetType("MToolKit.Runtime.Persistence.PreparedSaveCaptureScope"), Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator RestoreScenes()
        {
            try { if (Application.isPlaying) yield return new ExitPlayMode(); }
            finally { PersistencePlayModeTestScope.RestoreSceneSetup(); }
        }

        [Test]
        public async Task Preparation_OrderThenStableId_PrecedesEveryProvisionalSnapshot()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            session.Register("b", 0); session.Register("z", -1); session.Register("a", 0);
            session.AddCaptureController();
            var scope = (IDisposable)await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            Assert.That(session.Events, Is.EqualTo(new[] { "prepare:z", "prepare:a", "prepare:b", "snapshot" }));
            Assert.That(session.ActiveAdmissions, Is.EqualTo(3));
            scope.Dispose(); scope.Dispose();
            Assert.That(session.Events, Is.EqualTo(new[] { "prepare:z", "prepare:a", "prepare:b", "snapshot",
                "unpin", "release:b", "release:a", "release:z" }));
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.That(session.Es3.FlushCount, Is.Zero);
        }

        [Test]
        public async Task PreparedNestedSave_ReusesPreparationAndKeepsAdmissionUntilScopeDisposal()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("actor-falls", 0);
            object scope = await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            try
            {
                int notified = 0;
                session.Coordinator.DurableSaveCompleted += _ =>
                { Assert.That(session.ActiveAdmissions, Is.EqualTo(1)); notified++; };
                object receipt = await InvokeAsync(session.Coordinator, "SavePreparedAsync", scope, CancellationToken.None);
                Assert.That(receipt, Is.InstanceOf<ES3FlushReceipt>());
                Assert.That(participant.Calls, Is.EqualTo(1));
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
                Assert.That(session.Es3.FlushCount, Is.EqualTo(1));
                Assert.That(notified, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => session.Coordinator.BeginLocalSessionAfterDiscard());
            }
            finally { ((IDisposable)scope).Dispose(); }
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.DoesNotThrow(() => session.Coordinator.BeginLocalSessionAfterDiscard());
        }

        [Test]
        public async Task PreparedScope_AllowsCandidateProfileReceiptAndRetainsAdmissionThroughSourceRestoration()
        {
            RequireRuntimeContracts();
            using var session = new Session(profileContext: true);
            Assert.That(session.Profiles.CreateProfile("source"), Is.True);
            Assert.That(await session.Profiles.LoadProfileAsync("source"), Is.True);
            var participant = session.Register("actor-falls", 0);
            ES3SaveContext sourceContext = session.ProfileContextService.GetSaveContext();
            object scope = await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            try
            {
                Assert.That(session.Profiles.CloneProfile("source", "candidate").Success, Is.True);
                Assert.That(await session.Profiles.LoadProfileAsync("candidate"), Is.True);
                ES3SaveContext candidateContext = session.ProfileContextService.GetSaveContext();
                Assert.That(candidateContext.SaveFilePath, Is.Not.EqualTo(sourceContext.SaveFilePath));
                var receipt = (ES3FlushReceipt)await InvokeAsync(session.Coordinator, "SavePreparedAsync", scope, CancellationToken.None);
                Assert.That(receipt.Context, Is.EqualTo(candidateContext));
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
                Assert.That(participant.Calls, Is.EqualTo(1));
                Assert.That(await session.Profiles.LoadProfileAsync("source"), Is.True);
                Assert.That(session.ProfileContextService.GetSaveContext(), Is.EqualTo(sourceContext));
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1), "Restoring the source must precede admission release.");
            }
            finally { ((IDisposable)scope).Dispose(); }
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.That(session.Es3.FlushCount, Is.EqualTo(1));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task Preparation_ModeChangeWhileAwaitingRejectsBeforeSnapshotsOrWrite(bool capture, bool switchToLocal)
        {
            RequireRuntimeContracts();
            using var session = new Session();
            if (switchToLocal) session.Coordinator.SwitchToGlobalSystem();
            var pending = new UniTaskCompletionSource();
            session.Register("pending", 0, (_, _) => pending.Task);
            CaptureController controller = capture ? session.AddCaptureController() : null;
            Task<object> capturing = capture ? InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None) : null;
            Task operation = capturing ?? SaveDirect(session.Coordinator, receipt: true);
            try
            {
                Assert.That(operation.IsCompleted, Is.False);
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
                if (switchToLocal) session.Coordinator.SwitchToLocalSystem();
                else session.Coordinator.SwitchToGlobalSystem();
                pending.TrySetResult();
                Assert.That(await Failure(operation), Is.Not.Null, "An awaited preparation must not cross persistence sessions.");
                Assert.That(controller?.Snapshots ?? 0, Is.Zero);
                Assert.That(session.Es3.FlushCount, Is.Zero);
                Assert.That(session.ActiveAdmissions, Is.Zero);
            }
            finally
            {
                pending.TrySetResult();
                if (capturing != null && capturing.Status == TaskStatus.RanToCompletion)
                    ((IDisposable)capturing.Result).Dispose();
            }
        }

        [Test]
        public async Task PreparedScope_ModeChangeRejectsOldScopeWithoutWriting()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            session.Register("actor-falls", 0);
            object scope = await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            try
            {
                session.Coordinator.SwitchToGlobalSystem();
                Assert.That(await Failure(InvokeAsync(session.Coordinator, "SavePreparedAsync", scope, CancellationToken.None)), Is.Not.Null);
                Assert.That(session.Es3.FlushCount, Is.Zero);
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
            }
            finally { ((IDisposable)scope).Dispose(); }
            Assert.That(session.ActiveAdmissions, Is.Zero);
        }

        [Test]
        public async Task PreparationFailure_UnwindsAdmissionBeforeAnySnapshotOrFlushAndAllowsRetry()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("broken", 0,
                (_, _) => UniTask.FromException(new IOException("injected preparation failure")));
            var controller = session.AddCaptureController();
            Exception failure = await Failure(InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None));
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.ToString(), Does.Contain("injected preparation failure"));
            Assert.That(controller.Snapshots, Is.Zero);
            Assert.That(session.Es3.FlushCount, Is.Zero);
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.That(session.Coordinator.IsSaveBlocked.Value, Is.False);
            session.Unregister(participant);
            using var retry = (IDisposable)await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            Assert.That(controller.Snapshots, Is.EqualTo(1));
        }

        [Test]
        public async Task Cancellation_ReleasesPreparationAndRejectsLateAdmissionWithoutSnapshots()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            using var cancellation = new CancellationTokenSource();
            var pending = new UniTaskCompletionSource();
            PreparationContext captured = null;
            session.Register("pending", 0, (context, _) => { captured = context; return pending.Task; });
            var subsequent = session.Register("must-not-run", 1);
            var controller = session.AddCaptureController();
            Task<object> preparing = InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", cancellation.Token);
            try
            {
                Assert.That(captured, Is.Not.Null);
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
                cancellation.Cancel();
                Assert.That(await Task.WhenAny(preparing, Task.Delay(1500)), Is.SameAs(preparing));
                Assert.That(await Failure(preparing), Is.InstanceOf<OperationCanceledException>());
                Assert.That(session.ActiveAdmissions, Is.Zero);
                var late = new CountingLease(() => { });
                captured.HoldAdmission(late);
                Assert.That(late.Disposals, Is.EqualTo(1));
                Assert.That(controller.Snapshots, Is.Zero);
                Assert.That(session.Es3.FlushCount, Is.Zero);
                Assert.That(session.Coordinator.IsSaveBlocked.Value, Is.False);
            }
            finally { pending.TrySetResult(); await Task.WhenAny(preparing, Task.Delay(1000)); }
            await Task.Delay(50);
            Assert.That(subsequent.Calls, Is.Zero, "Late participant completion must not resume preparation.");
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.That(controller.Snapshots, Is.Zero);
            Assert.That(session.Es3.FlushCount, Is.Zero);
        }

        [Test]
        public async Task TwoSecondTimeout_UsesWallTimeAtZeroTimeScaleAndDisposesLateAdmission()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var pending = new UniTaskCompletionSource();
            PreparationContext captured = null;
            session.Register("never-finishes", 0, (context, _) => { captured = context; return pending.Task; });
            var subsequent = session.Register("must-not-run", 1);
            var controller = session.AddCaptureController();
            float originalScale = Time.timeScale;
            Time.timeScale = 0;
            Task<object> preparing = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                preparing = InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
                Assert.That(await Task.WhenAny(preparing, Task.Delay(4500)), Is.SameAs(preparing));
                Exception failure = await Failure(preparing);
                Assert.That(failure, Is.Not.Null);
                Assert.That(Reason(failure), Does.Contain("Timeout"));
                Assert.That(stopwatch.Elapsed.TotalSeconds, Is.InRange(1.7d, 4d));
                Assert.That(session.ActiveAdmissions, Is.Zero);
                var late = new CountingLease(() => { });
                captured.HoldAdmission(late);
                Assert.That(late.Disposals, Is.EqualTo(1));
                Assert.That(controller.Snapshots, Is.Zero);
                Assert.That(session.Es3.FlushCount, Is.Zero);
                Assert.That(session.Coordinator.IsSaveBlocked.Value, Is.False);
            }
            finally
            {
                Time.timeScale = originalScale;
                pending.TrySetResult();
                if (preparing != null) await Task.WhenAny(preparing, Task.Delay(1000));
            }
            await Task.Delay(50);
            Assert.That(subsequent.Calls, Is.Zero, "Late participant completion must not resume preparation.");
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.That(controller.Snapshots, Is.Zero);
            Assert.That(session.Es3.FlushCount, Is.Zero);
        }

        [Test]
        public void LegacySynchronousCapture_RejectsRequiredPreparationBeforeSnapshot()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("actor-falls", 0);
            var controller = session.AddCaptureController();
            IDisposable unexpected = null;
            try
            {
                var failure = Assert.Catch(() => unexpected = session.Coordinator.BeginProvisionalSaveCapture());
                Assert.That(Reason(failure), Does.Contain("PreparationRequired"));
                Assert.That(participant.Calls, Is.Zero);
                Assert.That(controller.Snapshots, Is.Zero);
                Assert.That(session.ActiveAdmissions, Is.Zero);
            }
            finally { unexpected?.Dispose(); }
        }

        [Test]
        public void LegacySynchronousCapture_RemainsCompatibleWithoutRequiredParticipants()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var controller = session.AddCaptureController();
            using (session.Coordinator.BeginProvisionalSaveCapture())
                Assert.That(controller.Snapshots, Is.EqualTo(1));
            Assert.That(session.Events, Is.EqualTo(new[] { "snapshot", "unpin" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task UnrelatedDirectSave_DuringPreparedScopeReturnsBusyWithoutBorrowing(bool receipt)
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("actor-falls", 0);
            using var scope = (IDisposable)await InvokeAsync(session.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            Exception failure = await Failure(SaveDirect(session.Coordinator, receipt));
            Assert.That(failure, Is.Not.Null);
            Assert.That(Reason(failure), Does.Contain("Busy"));
            Assert.That(session.Es3.FlushCount, Is.Zero);
            Assert.That(participant.Calls, Is.EqualTo(1));
            Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task PreparedSave_RejectsForeignOrDisposedScopeBeforeWriting(bool disposed)
        {
            RequireRuntimeContracts();
            using var source = new Session();
            using var other = new Session();
            object scope = await InvokeAsync(source.Coordinator, "BeginPreparedProvisionalSaveCaptureAsync", CancellationToken.None);
            try
            {
                if (disposed) ((IDisposable)scope).Dispose();
                Exception failure = await Failure(InvokeAsync(disposed ? source.Coordinator : other.Coordinator,
                    "SavePreparedAsync", scope, CancellationToken.None));
                Assert.That(failure, Is.Not.Null);
                Assert.That(source.Es3.FlushCount + other.Es3.FlushCount, Is.Zero);
            }
            finally { ((IDisposable)scope).Dispose(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task DirectSave_HoldsAdmissionAndOperationUntilDurableReceiptCompletes(bool receipt)
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("actor-falls", 0);
            var flush = new UniTaskCompletionSource<ES3FlushReceipt>();
            session.Es3.PendingFlush = flush;
            Task saving = SaveDirect(session.Coordinator, receipt);
            try
            {
                Assert.That(saving.IsCompleted, Is.False);
                Assert.That(participant.Calls, Is.EqualTo(1));
                Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => session.Coordinator.BeginLocalSessionAfterDiscard());
            }
            finally
            {
                flush.TrySetResult(new ES3FlushReceipt(session.Es3.GetSaveContext(), new string('a', 64), DateTime.UtcNow));
                await saving;
            }
            Assert.That(session.ActiveAdmissions, Is.Zero);
            Assert.DoesNotThrow(() => session.Coordinator.BeginLocalSessionAfterDiscard());
        }

        [UnityTest]
        public IEnumerator DirectSave_PreparesBeforeControllerSnapshots()
        {
            RequireRuntimeContracts();
            PersistencePlayModeTestScope.PrepareEmptyScene();
            yield return new EnterPlayMode();
            yield return DirectSnapshotOrder(false).ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ReceiptSave_PreparesBeforeControllerSnapshots()
        {
            RequireRuntimeContracts();
            PersistencePlayModeTestScope.PrepareEmptyScene();
            yield return new EnterPlayMode();
            yield return DirectSnapshotOrder(true).ToCoroutine();
        }

        private static async UniTask DirectSnapshotOrder(bool receipt)
        {
            using var session = new Session();
            session.Register("actor-falls", 0);
            var controller = session.AddCaptureController();
            controller.BeforeSave = () => Assert.That(session.ActiveAdmissions, Is.EqualTo(1));
            if (receipt) await session.Coordinator.SaveWithReceiptAsync();
            else await session.Coordinator.SaveAsync();
            Assert.That(session.Events, Is.EqualTo(new[] { "prepare:actor-falls", "save", "release:actor-falls" }));
            Assert.That(session.Es3.FlushCount, Is.EqualTo(1));
            Assert.That(session.ActiveAdmissions, Is.Zero);
        }

        [Test]
        public void UnregisteringRequiredParticipant_RemovesItsPreparationAndLegacyCaptureRestriction()
        {
            RequireRuntimeContracts();
            using var session = new Session();
            var participant = session.Register("actor-falls", 0);
            session.Unregister(participant);
            var controller = session.AddCaptureController();
            using (session.Coordinator.BeginProvisionalSaveCapture())
                Assert.That(controller.Snapshots, Is.EqualTo(1));
            Assert.That(participant.Calls, Is.Zero);
        }

        private static string Reason(Exception failure)
            => failure?.GetType().GetProperty("Reason")?.GetValue(failure)?.ToString() ?? failure?.Message ?? "no failure";
        private static async Task<Exception> Failure(Task task)
        { try { await task; return null; } catch (Exception failure) { return failure; } }
        private static async Task SaveDirect(SaveSystemCoordinator coordinator, bool receipt)
        { if (receipt) await coordinator.SaveWithReceiptAsync(); else await coordinator.SaveAsync(); }
        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(InstanceFlags)
                .SingleOrDefault(m => m.Name == name && m.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, "Missing real runtime API: " + name);
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }
        private static async Task<object> InvokeAsync(object target, string name, params object[] arguments)
        {
            object operation = Invoke(target, name, arguments);
            if (operation is UniTask simple) { await simple; return null; }
            Type type = operation.GetType();
            Assert.That(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UniTask<>), Is.True);
            var awaiter = typeof(SaveSystemCoordinatorPreparationTests).GetMethod(nameof(AwaitGeneric),
                BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(type.GetGenericArguments()[0]);
            return await (Task<object>)awaiter.Invoke(null, new[] { operation });
        }
        private static async Task<object> AwaitGeneric<T>(object operation) => await (UniTask<T>)operation;

        private sealed class Participant : RequiredParticipant
        {
            private readonly Session session;
            private readonly Func<PreparationContext, CancellationToken, UniTask> callback;
            public string ParticipantId { get; }
            public int Order { get; }
            public int Calls;
            public Participant(Session session, string id, int order, Func<PreparationContext, CancellationToken, UniTask> callback)
            { this.session = session; ParticipantId = id; Order = order; this.callback = callback; }
            public UniTask PrepareForSaveAsync(PreparationContext context, CancellationToken ct)
            {
                Calls++; session.Record("prepare:" + ParticipantId);
                Interlocked.Increment(ref session.ActiveAdmissions);
                context.HoldAdmission(new CountingLease(() =>
                { Interlocked.Decrement(ref session.ActiveAdmissions); session.Record("release:" + ParticipantId); }));
                return callback?.Invoke(context, ct) ?? UniTask.CompletedTask;
            }
        }
        private sealed class CountingLease : IDisposable
        {
            private readonly Action dispose;
            public int Disposals;
            public CountingLease(Action dispose) => this.dispose = dispose;
            public void Dispose() { Interlocked.Increment(ref Disposals); dispose(); }
        }
        private sealed class CaptureController : ISaveDomainController, IProvisionalSaveCaptureParticipant
        {
            private readonly Session session;
            public ESaveDomain Domain => ESaveDomain.World;
            public int Snapshots;
            public Action BeforeSave;
            public CaptureController(Session session) => this.session = session;
            public bool HasSaveData() => true;
            public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct = default)
            { BeforeSave?.Invoke(); session.Record("save"); return UniTask.CompletedTask; }
            public IDisposable BeginProvisionalSaveCapture()
            { Snapshots++; session.Record("snapshot"); return new CountingLease(() => session.Record("unpin")); }
        }
        private sealed class Session : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "mtk_preparation_" + Guid.NewGuid().ToString("N"));
            private readonly ES3SaveConfig config;
            private readonly GameSettingsModule settings = new();
            private readonly ProfileManager profiles;
            public ProfileManager Profiles => profiles;
            public ProfileContextES3Service ProfileContextService { get; }
            private readonly List<string> events = new();
            public string[] Events { get { lock (events) return events.ToArray(); } }
            public int ActiveAdmissions;
            public readonly MemoryES3Service Es3 = new();
            public SaveSystemCoordinator Coordinator { get; }
            public Session(bool profileContext = false)
            {
                config = ScriptableObject.CreateInstance<ES3SaveConfig>();
                SetConfig("usePersistentDataPath", false); SetConfig("usePlatformSpecificPaths", false);
                SetConfig("saveDirectory", directory); settings.AutoSave.Value = false; SetConfig("gameSettings", settings);
                profiles = new ProfileManager(Es3, config);
                if (profileContext) ProfileContextService = new ProfileContextES3Service(Es3, profiles);
                Coordinator = new SaveSystemCoordinator((IES3Service)ProfileContextService ?? Es3, new SaveDomainControllerRegistry(),
                    new SaveDomainControllerRegistry(), config, profiles, new TruncationReporter());
                Coordinator.Start(); Coordinator.SwitchToLocalSystem();
            }
            public void Record(string message) { lock (events) events.Add(message); }
            public Participant Register(string id, int order, Func<PreparationContext, CancellationToken, UniTask> callback = null)
            { var participant = new Participant(this, id, order, callback); Invoke(Coordinator, "RegisterRequiredSavePreparationParticipant", participant); return participant; }
            public void Unregister(Participant participant) => Invoke(Coordinator, "UnregisterRequiredSavePreparationParticipant", participant);
            public CaptureController AddCaptureController()
            { var controller = new CaptureController(this); Coordinator.RegisterLocalController(controller); return controller; }
            private void SetConfig(string name, object value)
                => typeof(ES3SaveConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
            public void Dispose()
            {
                Coordinator.Shutdown(); profiles.Dispose(); settings.OnShutdown(); UnityEngine.Object.DestroyImmediate(config);
                Es3.IsSaving.Dispose(); Es3.IsLoading.Dispose(); Es3.LastSaveTime.Dispose();
                Es3.LastLoadTime.Dispose(); Es3.SaveCounter.Dispose();
                string absolute = Path.GetFullPath(directory), temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                if (!absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test directory.");
                if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
            }
        }

        // Only the selected profile's receipt identity varies; the existing memory backend
        // still supplies all writes and counters. This isolates coordinator scope ownership.
        private sealed class ProfileContextES3Service : IDurableES3Service
        {
            private readonly MemoryES3Service inner;
            private readonly IProfileManager profiles;
            private readonly Dictionary<string, Guid> identities = new(StringComparer.OrdinalIgnoreCase);
            public ProfileContextES3Service(MemoryES3Service inner, IProfileManager profiles)
            { this.inner = inner; this.profiles = profiles; }
            public R3.ReactiveProperty<bool> IsSaving => inner.IsSaving;
            public R3.ReactiveProperty<bool> IsLoading => inner.IsLoading;
            public R3.ReactiveProperty<string> LastSaveTime => inner.LastSaveTime;
            public R3.ReactiveProperty<string> LastLoadTime => inner.LastLoadTime;
            public R3.ReactiveProperty<int> SaveCounter => inner.SaveCounter;
            public ES3SaveContext GetSaveContext()
            {
                string profile = profiles.CurrentProfile.Value;
                if (string.IsNullOrEmpty(profile)) return inner.GetSaveContext();
                string path = profiles.GetProfileFilePath(profile);
                if (!identities.TryGetValue(path, out Guid identity)) identities.Add(path, identity = Guid.NewGuid());
                return new ES3SaveContext(identity, path);
            }
            public async UniTask<ES3FlushReceipt> FlushSaveBatchAsync(CancellationToken ct = default)
            {
                ES3FlushReceipt receipt = await inner.FlushSaveBatchAsync(ct);
                return new ES3FlushReceipt(GetSaveContext(), receipt.FileSha256, receipt.CompletedUtc);
            }
            public UniTask SaveAsync(CancellationToken ct = default) => inner.SaveAsync(ct);
            public UniTask LoadAsync(CancellationToken ct = default) => inner.LoadAsync(ct);
            public UniTask SaveAsync(string key, object value, CancellationToken ct = default) => inner.SaveAsync(key, value, ct);
            public UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default) => inner.LoadAsync(key, defaultValue, ct);
            public bool KeyExists(string key) => inner.KeyExists(key);
            public void DeleteKey(string key) => inner.DeleteKey(key);
            public void DeleteFile() => inner.DeleteFile();
            public string GetSaveFormatVersion() => inner.GetSaveFormatVersion();
            public string GetSavedFormatVersion() => inner.GetSavedFormatVersion();
            public bool CreateBackup() => inner.CreateBackup();
            public bool RestoreFromBackup() => inner.RestoreFromBackup();
            public string[] GetAvailableBackups() => inner.GetAvailableBackups();
        }
    }
}
