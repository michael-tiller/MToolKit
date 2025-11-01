#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MToolKit.Editor
{
  /// <summary>
  /// Scene auto loader.
  /// </summary>
  /// <description>
  /// This class adds a File > Scene Autoload menu containing options to select
  /// a "master scene" enable it to be auto-loaded when the user presses play
  /// in the editor. When enabled, the selected scene will be loaded on play,
  /// then the original scene will be reloaded on stop.
  ///
  /// Based on an idea on this thread:
  /// http://forum.unity3d.com/threads/157502-Executing-first-scene-in-build-Config-when-pressing-play-button-in-editor
  /// 
  /// Note: Unity 2019.4+ also provides EditorSceneManager.playModeStartScene as an alternative approach.
  /// </description>
  [InitializeOnLoad]
  public static class SceneAutoLoader
  {
    public const string EditorMenuText = "File/Scene Autoload/Load Default Scene On Play";

    private static bool LoadMasterOnPlay
    {
      get { return EditorPrefs.GetBool(nameof(LoadMasterOnPlay), false); }
      set { EditorPrefs.SetBool(nameof(LoadMasterOnPlay), value); }
    }

    private static string DefaultScene
    {
      get { return EditorPrefs.GetString(nameof(DefaultScene)); }
      set { EditorPrefs.SetString(nameof(DefaultScene), value); }
    }

    private static string PreviousScene
    {
      get { return EditorPrefs.GetString(nameof(PreviousScene), EditorSceneManager.GetActiveScene().path); }
      set { EditorPrefs.SetString(nameof(PreviousScene), value); }
    }

    // Static constructor binds a playmode-changed callback.
    // [InitializeOnLoad] above makes sure this gets executed.
    static SceneAutoLoader()
    {
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // Check if the test runner is currently active
    private static bool IsTestRunnerActive()
    {
      // Method 1: Check if we're actually running tests by looking for test execution context
      try
      {
        // Check if we're in a test execution context
        var testRunnerType = System.Type.GetType("UnityEngine.TestTools.TestRunner.TestRunnerApi, UnityEngine.TestRunner");
        if (testRunnerType != null)
        {
          var isRunningTestsProperty = testRunnerType.GetProperty("isRunningTests", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
          if (isRunningTestsProperty != null)
          {
            var isRunningTests = (bool)isRunningTestsProperty.GetValue(null);
            if (isRunningTests)
            {
              Debug.Log("[SceneAutoLoader] TestRunnerApi.isRunningTests is true");
              return true;
            }
          }
        }
      }
      catch
      {
        // Ignore exceptions when checking for test runner types
      }

      // Method 2: Check for test execution in the call stack
      try
      {
        var stackTrace = new System.Diagnostics.StackTrace();
        for (int i = 0; i < stackTrace.FrameCount; i++)
        {
          var frame = stackTrace.GetFrame(i);
          var method = frame.GetMethod();
          if (method != null)
          {
            var declaringType = method.DeclaringType;
            if (declaringType != null)
            {
              var typeName = declaringType.FullName;
              // Check if we're in test execution methods
              if (typeName != null && (
                  typeName.Contains("UnityEngine.TestTools.TestRunner") ||
                  typeName.Contains("UnityEditor.TestTools.TestRunner") ||
                  typeName.Contains("NUnit.Framework") ||
                  typeName.Contains("UnityTest")))
              {
                Debug.Log($"[SceneAutoLoader] Found test execution in call stack: {typeName}.{method.Name}");
                return true;
              }
            }
          }
        }
      }
      catch
      {
        // Ignore exceptions when checking stack trace
      }

      // Method 3: Check for test runner window being active (less reliable but useful fallback)
      try
      {
        var testRunnerWindowType = System.Type.GetType("UnityEditor.TestTools.TestRunner.TestRunnerWindow, UnityEditor.TestRunner");
        if (testRunnerWindowType != null)
        {
          var focusedWindowProperty = typeof(EditorWindow).GetProperty("focusedWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
          if (focusedWindowProperty != null)
          {
            var focusedWindow = focusedWindowProperty.GetValue(null) as EditorWindow;
            if (focusedWindow != null && focusedWindow.GetType() == testRunnerWindowType)
            {
              Debug.Log("[SceneAutoLoader] Test Runner window is focused");
              return true;
            }
          }
        }
      }
      catch
      {
        // Ignore exceptions when checking for test runner window
      }
      return false;
    }

    // Menu items to select the "default" scene and control whether or not to load it.
    [MenuItem("File/Scene Autoload/Select Default Scene")]
    private static void SelectDefaultScene()
    {
      string defaultScene = EditorUtility.OpenFilePanel("Select the default Scene", Application.dataPath, "unity");
      defaultScene = defaultScene.Replace(Application.dataPath, "Assets");  //project relative instead of absolute path
      if (!string.IsNullOrEmpty(defaultScene))
      {
        DefaultScene = defaultScene;
        LoadMasterOnPlay = true;
        Debug.Log($"[SceneAutoLoader] Set Default Scene: {DefaultScene}, LoadMasterOnPlay: {LoadMasterOnPlay}");
      }
      else
      {
        Debug.LogWarning("[SceneAutoLoader] No scene selected or selection cancelled");
      }
    }

    [MenuItem(EditorMenuText)]
    static void OnClickMenu()
    {
      // Check/Uncheck menu.
      bool isChecked = !Menu.GetChecked(EditorMenuText);
      Menu.SetChecked(EditorMenuText, isChecked);

      // Save to EditorPrefs.
      EditorPrefs.SetBool(EditorMenuText, isChecked);

      LoadMasterOnPlay = isChecked;
      Debug.Log($"[SceneAutoLoader] LoadMasterOnPlay set to: {isChecked}, DefaultScene: {DefaultScene}");
    }

    [MenuItem(EditorMenuText, true)]
    static bool Valid()
    {
      // Check/Uncheck menu from EditorPrefs.
      Menu.SetChecked(EditorMenuText, EditorPrefs.GetBool(EditorMenuText, false));
      return true;
    }

    // Play mode change callback handles the scene load/reload.
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
      Debug.Log($"[SceneAutoLoader] PlayModeStateChanged: {state}, LoadMasterOnPlay: {LoadMasterOnPlay}, DefaultScene: {DefaultScene}");
      
      if (!LoadMasterOnPlay)
      {
        Debug.Log("[SceneAutoLoader] LoadMasterOnPlay is false, skipping");
        return;
      }

      if (string.IsNullOrEmpty(DefaultScene))
      {
        Debug.LogWarning("[SceneAutoLoader] DefaultScene is not set, skipping");
        return;
      }

      // Skip auto-loading if test runner is active
      if (IsTestRunnerActive())
      {
        Debug.Log("[SceneAutoLoader] Test runner is active, skipping");
        return;
      }

      // Handle entering play mode
      if (state == PlayModeStateChange.ExitingEditMode)
      {
        Debug.Log("[SceneAutoLoader] ExitingEditMode - preparing to load default scene");
        PreviousScene = EditorSceneManager.GetActiveScene().path;
        
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
          try
          {
            Debug.Log($"[SceneAutoLoader] Loading default scene: {DefaultScene}");
            EditorSceneManager.OpenScene(DefaultScene);
          }
          catch (System.Exception ex)
          {
            Debug.LogError($"[SceneAutoLoader] Failed to load scene {DefaultScene}: {ex.Message}");
            EditorApplication.isPlaying = false;
          }
        }
        else
        {
          Debug.Log("[SceneAutoLoader] User cancelled save operation, cancelling play");
          EditorApplication.isPlaying = false;
        }
      }
      // Handle exiting play mode
      else if (state == PlayModeStateChange.EnteredEditMode)
      {
        Debug.Log($"[SceneAutoLoader] EnteredEditMode - reloading previous scene: {PreviousScene}");
        try
        {
          if (!string.IsNullOrEmpty(PreviousScene))
          {
            EditorSceneManager.OpenScene(PreviousScene);
          }
        }
        catch (System.Exception ex)
        {
          Debug.LogError($"[SceneAutoLoader] Failed to reload previous scene {PreviousScene}: {ex.Message}");
        }
      }
    }
  }
}

#endif
