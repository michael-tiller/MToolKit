using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Quest
{
  /// <summary>
  ///   Full-lifecycle CHARACTERIZATION of <see cref="QuestManager" /> — it had zero tests before this. These
  ///   pin what the code DOES today (start/objective/complete/claim/abandon/message-entry/save+restore), so the
  ///   9.0.2b refactor onto <c>IGraphContext</c> is provably behavior-preserving. Assertions were discovered by
  ///   running, not aspiration; several deliberately pin quirks (empty serialized graph-state; the two distinct
  ///   objective-progress paths; restore re-publishing Started only for active quests).
  ///
  ///   <para>Quests are built with NULL objective/quest graphs so <c>StartQuestAsync</c> skips graph loading and
  ///   completes synchronously (no <c>WaitForEndOfFrame</c> to deadlock an EditMode test); the executor that
  ///   writes <c>objective_{guid}</c> is simulated by writing that key directly.</para>
  /// </summary>
  [TestFixture]
  public sealed class QuestManagerCharacterizationTests : UnityObjectCleanup
  {
    private QuestManagerHarness harness;
    private QuestManager Manager => harness.Manager;

    [SetUp]
    public void SetUp()
    {
      LogAssert.ignoreFailingMessages = true;
      NodeDebugEvents.ClearAllSubscribers();
      harness = new QuestManagerHarness();
    }

    [TearDown]
    public void TearDownHarness()
    {
      // Runs before the base UnityObjectCleanup TearDown (NUnit: derived teardown first) — so the manager and
      // broker are torn down before the quest ScriptableObjects are DestroyImmediate'd.
      harness?.Dispose();
    }

    // ── builders ──────────────────────────────────────────────────────────────────────────────────

    private QuestObjective Objective(string guid, int required = 1, bool optional = false)
    {
      var o = Track(ScriptableObject.CreateInstance<QuestObjective>());
      o.Guid = guid;
      o.DisplayName = guid;
      o.RequiredProgress = required;
      o.Optional = optional;
      // ObjectiveGraph deliberately left null → StartQuestAsync logs + skips the load (no async yield).
      return o;
    }

    private QuestDefinition Quest(string guid, bool abandonable = true, params QuestObjective[] objectives)
    {
      var q = Track(ScriptableObject.CreateInstance<QuestDefinition>());
      q.Guid = guid;
      q.DisplayName = guid;
      q.IsAbandonable = abandonable;
      q.Objectives = objectives.ToList();
      return q;
    }

    private static bool Start(QuestManager m, QuestDefinition q) =>
      m.StartQuestAsync(q).GetAwaiter().GetResult();

    // ── start ─────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void StartQuest_MovesToActive_StoresQuestContextKeys_PublishesStarted()
    {
      var started = new List<string>();
      using var sub = harness.Subscriber<QuestStartedMessage>().Subscribe(m => started.Add(m.QuestGuid));

      var quest = Quest("q1", true, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest), "StartQuestAsync must report success for a fresh quest.");

      Assert.IsTrue(Manager.IsQuestActive("q1"));
      Assert.That(Manager.GetActiveQuests().Count, Is.EqualTo(1));

      var state = Manager.GetQuestState("q1").GraphState;
      Assert.IsTrue(state.TryGet<string>("__quest_guid", out var g) && g == "q1",
        "StartQuestAsync stores __quest_guid in the quest's graph state.");
      Assert.IsTrue(state.TryGet<QuestDefinition>("__quest_definition", out var def) && def == quest,
        "StartQuestAsync stores the __quest_definition reference.");

      Assert.That(started, Is.EqualTo(new[] { "q1" }), "exactly one QuestStartedMessage carrying the guid.");
    }

    [Test]
    public void StartQuest_AlreadyActive_ReturnsFalse()
    {
      var quest = Quest("q1b", true, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsFalse(Start(Manager, quest), "Re-starting an already-active quest is a no-op false.");
      Assert.That(Manager.GetActiveQuests().Count, Is.EqualTo(1));
    }

    // ── objective progress: the two distinct paths ─────────────────────────────────────────────────

    [Test]
    public void Completion_ExecutorPath_WritesObjectiveKey_DrivesCompletionPercentage()
    {
      var quest = Quest("q2", true, Objective("oA", required: 1));
      Assert.IsTrue(Start(Manager, quest));
      Assert.That(Manager.GetQuestCompletionPercentage("q2"), Is.EqualTo(0f),
        "no objective_{guid} written yet → 0% complete.");

      // Simulate the objective executor / raw-state path: it writes `objective_{guid}` (read by
      // QuestDefinition.GetObjectiveProgress → drives completion%). This key is NOT QuestManager's.
      var state = Manager.GetQuestState("q2").GraphState;
      state.Set("objective_oA", new QuestObjectiveProgress("oA", 1) { Current = 1 });

      Assert.That(Manager.GetQuestCompletionPercentage("q2"), Is.EqualTo(1f),
        "objective_oA marked complete → the single required objective is done → 100%.");
    }

    [Test]
    public void ObjectiveProgressMessage_WritesOnlyMirrorKey_DoesNotTouchObjectiveKeyOrCompletion()
    {
      var quest = Quest("q3", true, Objective("oB", required: 5));
      Assert.IsTrue(Start(Manager, quest));

      // The progress-MESSAGE handler writes ONLY the mirror key `__objective_{guid}_progress` (= Current),
      // never `objective_{guid}` — so completion% (which reads objective_{guid}) is unaffected.
      GameMessageBroker.Publish(new QuestObjectiveProgressMessage(
        "q3", "oB", objective: null, current: 3, required: 5, percentage: 0.6f, isComplete: false));

      var state = Manager.GetQuestState("q3").GraphState;
      Assert.IsTrue(state.TryGet<int>("__objective_oB_progress", out var mirror) && mirror == 3,
        "handler writes the mirror key from message.Current.");
      Assert.IsFalse(state.Contains("objective_oB"),
        "handler must NOT write the executor-owned objective_{guid} key.");
      Assert.That(Manager.GetQuestCompletionPercentage("q3"), Is.EqualTo(0f),
        "completion% reads objective_{guid} (absent) → unchanged by the progress message.");
    }

    // ── complete / claim / abandon ─────────────────────────────────────────────────────────────────

    [Test]
    public void CompleteQuest_MovesActiveToCompletedUnclaimed_PublishesCompleted()
    {
      var completed = new List<string>();
      using var sub = harness.Subscriber<QuestCompletedMessage>().Subscribe(m => completed.Add(m.QuestGuid));

      var quest = Quest("q4", true, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsTrue(Manager.CompleteQuest("q4"));

      Assert.IsFalse(Manager.IsQuestActive("q4"));
      Assert.IsTrue(Manager.IsQuestCompleted("q4"));
      Assert.That(Manager.GetCompletedUnclaimedQuests().Count, Is.EqualTo(1));
      Assert.That(Manager.GetQuestCompletionPercentage("q4"), Is.EqualTo(1f), "completed quest reports 100%.");
      Assert.That(completed, Is.EqualTo(new[] { "q4" }));
    }

    [Test]
    public void CompleteQuest_NotActive_ReturnsFalse()
    {
      Assert.IsFalse(Manager.CompleteQuest("nope"), "Completing a non-active quest is false.");
    }

    [Test]
    public void ClaimQuest_MovesCompletedToClaimed_PublishesClaimed()
    {
      var claimed = new List<string>();
      using var sub = harness.Subscriber<QuestClaimedMessage>().Subscribe(m => claimed.Add(m.QuestGuid));

      var quest = Quest("q5", true, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsTrue(Manager.CompleteQuest("q5"));
      Assert.IsTrue(Manager.ClaimQuest("q5"));

      Assert.IsTrue(Manager.IsQuestClaimed("q5"));
      Assert.That(Manager.GetCompletedUnclaimedQuests().Count, Is.EqualTo(0));
      Assert.That(Manager.GetClaimedQuestGuids(), Does.Contain("q5"));
      Assert.That(claimed, Is.EqualTo(new[] { "q5" }));
    }

    [Test]
    public void AbandonQuest_RemovesActive_PublishesAbandonedWithProgress_NotCompleted()
    {
      var abandoned = new List<(string guid, float progress)>();
      using var sub = harness.Subscriber<QuestAbandonedMessage>()
        .Subscribe(m => abandoned.Add((m.QuestGuid, m.ProgressWhenAbandoned)));

      var quest = Quest("q6", true, Objective("oC", required: 1));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsTrue(Manager.AbandonQuest("q6"));

      Assert.IsFalse(Manager.IsQuestActive("q6"));
      Assert.IsFalse(Manager.IsQuestCompleted("q6"), "abandon does NOT route to completed/claimed.");
      Assert.That(abandoned.Count, Is.EqualTo(1));
      Assert.That(abandoned[0].guid, Is.EqualTo("q6"));
      Assert.That(abandoned[0].progress, Is.EqualTo(0f), "abandoned at 0% (no objective complete).");
    }

    [Test]
    public void AbandonQuest_NonAbandonable_ReturnsFalse_StaysActive()
    {
      var quest = Quest("q7", abandonable: false, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsFalse(Manager.AbandonQuest("q7"), "a non-abandonable quest refuses abandon.");
      Assert.IsTrue(Manager.IsQuestActive("q7"), "and stays active.");
    }

    // ── message-driven entry ───────────────────────────────────────────────────────────────────────

    [Test]
    public void StartQuestRequestMessage_StartsTheQuest()
    {
      var started = new List<string>();
      using var sub = harness.Subscriber<QuestStartedMessage>().Subscribe(m => started.Add(m.QuestGuid));

      var quest = Quest("q8", true, Objective("o1"));
      GameMessageBroker.Publish(new StartQuestRequestMessage(quest));

      Assert.IsTrue(Manager.IsQuestActive("q8"), "the request handler starts the quest synchronously (null graphs).");
      Assert.That(started, Does.Contain("q8"));
    }

    [Test]
    public void ClaimQuestRequestMessage_ClaimsTheQuest()
    {
      var quest = Quest("q9", true, Objective("o1"));
      Assert.IsTrue(Start(Manager, quest));
      Assert.IsTrue(Manager.CompleteQuest("q9"));

      var claimed = new List<string>();
      using var sub = harness.Subscriber<QuestClaimedMessage>().Subscribe(m => claimed.Add(m.QuestGuid));

      GameMessageBroker.Publish(new ClaimQuestRequestMessage("q9"));

      Assert.IsTrue(Manager.IsQuestClaimed("q9"));
      Assert.That(claimed, Does.Contain("q9"));
    }

    // ── persistence: pin the ACTUAL (limited) behavior ─────────────────────────────────────────────

    [Test]
    public void GetSaveData_CapturesQuestLists_ButGraphStateSerializesEmpty()
    {
      var quest = Quest("q10", true, Objective("oD", required: 2));
      Assert.IsTrue(Start(Manager, quest));
      // Even with objective state present, GetSaveData() does NOT serialize it (SerializeGraphState → "").
      Manager.GetQuestState("q10").GraphState.Set("objective_oD", new QuestObjectiveProgress("oD", 2) { Current = 1 });

      var active = Manager.GetSaveData();
      Assert.That(active.ActiveQuests.Select(a => a.QuestGuid), Is.EqualTo(new[] { "q10" }));
      Assert.That(active.ActiveQuests[0].SerializedGraphState, Is.Empty,
        "graph-state is persisted by GraphStateSaveController, NOT GetSaveData — this stays empty by design.");
      Assert.That(active.CompletedUnclaimedQuests, Is.Empty);
      Assert.That(active.ClaimedQuestGuids, Is.Empty);

      // After completion the quest moves into the completed-unclaimed list of the save data.
      Assert.IsTrue(Manager.CompleteQuest("q10"));
      var done = Manager.GetSaveData();
      Assert.That(done.ActiveQuests, Is.Empty);
      Assert.That(done.CompletedUnclaimedQuests.Select(c => c.QuestGuid), Is.EqualTo(new[] { "q10" }));
    }

    [Test]
    public void Restore_ActiveQuestRepublishesStarted_CompletedUnclaimedDoesNot()
    {
      var qActive = Quest("qActive", true, Objective("o1"));
      var qCompleted = Quest("qCompleted", true, Objective("o1"));
      // Registry must resolve both synchronously, else RestoreSaveDataAsync falls into a real-time retry loop.
      GraphDefinitionRegistry.RegisterQuestDefinition(qActive);
      GraphDefinitionRegistry.RegisterQuestDefinition(qCompleted);

      var saveData = new QuestManagerSaveData
      {
        ActiveQuests = { new ActiveQuestSaveData { QuestGuid = "qActive", SerializedGraphState = "" } },
        CompletedUnclaimedQuests = { new ActiveQuestSaveData { QuestGuid = "qCompleted", SerializedGraphState = "" } }
      };

      var started = new List<string>();
      using var sub = harness.Subscriber<QuestStartedMessage>().Subscribe(m => started.Add(m.QuestGuid));

      Manager.RestoreSaveDataAsync(saveData).GetAwaiter().GetResult();

      Assert.IsTrue(Manager.IsQuestActive("qActive"), "active quest is restored via StartQuestAsync.");
      Assert.IsTrue(Manager.IsQuestCompleted("qCompleted"), "completed quest is restored directly to completed-unclaimed.");
      Assert.That(started, Does.Contain("qActive"),
        "active-quest restore re-publishes QuestStartedMessage (it routes through StartQuestAsync).");
      Assert.That(started, Does.Not.Contain("qCompleted"),
        "completed-unclaimed restore is direct and intentionally publishes no Started message.");
    }
  }
}
