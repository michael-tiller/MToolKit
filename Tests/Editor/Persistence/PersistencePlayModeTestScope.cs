using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.Persistence
{
  internal static class PersistencePlayModeTestScope
  {
    private const string StateKey = "MToolKit.Tests.Editor.Persistence.PlayModeSceneSetup";

    [Serializable]
    private sealed class SavedState
    {
      public SavedScene[] scenes;
      public string playModeStartScene;
    }

    [Serializable]
    private sealed class SavedScene
    {
      public string path;
      public bool isLoaded;
      public bool isActive;
    }

    public static void PrepareEmptyScene()
    {
      if (Application.isPlaying) throw new InvalidOperationException("Expected EditMode test setup.");
      if (!string.IsNullOrEmpty(SessionState.GetString(StateKey, string.Empty)))
        throw new InvalidOperationException("The previous persistence test scene setup has not been restored.");
      for (int i = 0; i < SceneManager.sceneCount; i++)
        if (SceneManager.GetSceneAt(i).isDirty)
          throw new InvalidOperationException("Persistence tests must not discard unsaved scene changes.");

      SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
      var state = new SavedState
      {
        // The EditMode runner's initial untitled camera/light scene is generated state;
        // Unity's runner separately retains the user's original scene setup for job cleanup.
        scenes = setup.Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SavedScene
        {
          path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive
        }).ToArray(),
        playModeStartScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
      };
      // SessionState survives EnterPlayMode's domain reload without changing global EditorPrefs.
      SessionState.SetString(StateKey, JsonUtility.ToJson(state));
      EditorSceneManager.playModeStartScene = null;
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    public static void RestoreSceneSetup()
    {
      string json = SessionState.GetString(StateKey, string.Empty);
      if (string.IsNullOrEmpty(json)) return;
      if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before restoring test scenes.");
      SavedState state = JsonUtility.FromJson<SavedState>(json);
      if (state.scenes.Length > 0)
        EditorSceneManager.RestoreSceneManagerSetup(state.scenes.Select(scene => new SceneSetup
        {
          path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive
        }).ToArray());
      else
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
      EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.playModeStartScene)
        ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.playModeStartScene);
      SessionState.EraseString(StateKey);
    }
  }

  internal sealed class ExpectedPersistenceErrorScope : IDisposable
  {
    private readonly Func<string, bool> matchesExpectedError;

    public ExpectedPersistenceErrorScope(params string[] messages)
      : this(message => messages.Any(message.Contains)) { }

    private ExpectedPersistenceErrorScope(Func<string, bool> matchesExpectedError)
    {
      this.matchesExpectedError = matchesExpectedError;
      Application.logMessageReceived += OnLogMessage;
    }

    public static ExpectedPersistenceErrorScope ForControllerLoadFailure(string controllerType)
      => new(message => message.Contains("Save blocked: required controller failed during load;")
        || (message.Contains("Save controller") && message.Contains(controllerType)
          && message.Contains("failed during")));

    public static ExpectedPersistenceErrorScope ForDurableCompletionFailure(string controllerType)
      => new(message => message.Contains("Durable save completion handler failed for")
        && message.Contains(controllerType) && message.Contains("completion failed"));

    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
      // Register only the diagnostic from this intentionally failing operation, when a host
      // actually has a Unity sink. Other errors and native exceptions remain test failures.
      if (type == LogType.Error && matchesExpectedError(message)) LogAssert.Expect(type, message);
    }

    public void Dispose() => Application.logMessageReceived -= OnLogMessage;
  }
}
