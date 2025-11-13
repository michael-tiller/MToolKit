#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MToolKit.Editor
{
  /// <summary>
  ///   Scene auto loader.
  /// </summary>
  /// <description>
  ///   This class adds a File > Scene Autoload menu containing options to select
  ///   a "master scene" enable it to be auto-loaded when the user presses play
  ///   in the editor. When enabled, the selected scene will be loaded on play,
  ///   then the original scene will be reloaded on stop.
  ///   Based on an idea on this thread:
  ///   http://forum.unity3d.com/threads/157502-Executing-first-scene-in-build-Config-when-pressing-play-button-in-editor
  ///   Note: Unity 2019.4+ also provides EditorSceneManager.playModeStartScene as an alternative approach.
  /// </description>
  [InitializeOnLoad]
  public static class SceneAutoLoader
  {
    public const string EditorMenuText = "File/Scene Autoload/Load Default Scene On Play";

    private static bool isLoadMasterOnPlay
    {
      get => EditorPrefs.GetBool(nameof(isLoadMasterOnPlay), false);
      set => EditorPrefs.SetBool(nameof(isLoadMasterOnPlay), value);
    }

    private static string defaultScene
    {
      get => EditorPrefs.GetString(nameof(defaultScene));
      set => EditorPrefs.SetString(nameof(defaultScene), value);
    }

    private static string previousScene
    {
      // ReSharper disable once AccessToStaticMemberViaDerivedType
      get => EditorPrefs.GetString(nameof(previousScene), EditorSceneManager.GetActiveScene().path);
      set => EditorPrefs.SetString(nameof(previousScene), value);
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
        Type testRunnerType = Type.GetType("UnityEngine.TestTools.TestRunner.TestRunnerApi, UnityEngine.TestRunner");
        if (testRunnerType != null)
        {
          PropertyInfo isRunningTestsProperty = testRunnerType.GetProperty("isRunningTests", BindingFlags.Public | BindingFlags.Static);
          if (isRunningTestsProperty != null)
          {
            bool isRunningTests = (bool)isRunningTestsProperty.GetValue(null);
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
        StackTrace stackTrace = new();
        for (int i = 0; i < stackTrace.FrameCount; i++)
        {
          StackFrame frame = stackTrace.GetFrame(i);
          MethodBase method = frame.GetMethod();
          if (method != null)
          {
            Type declaringType = method.DeclaringType;
            if (declaringType != null)
            {
              string typeName = declaringType.FullName;
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
        Type testRunnerWindowType = Type.GetType("UnityEditor.TestTools.TestRunner.TestRunnerWindow, UnityEditor.TestRunner");
        if (testRunnerWindowType != null)
        {
          PropertyInfo focusedWindowProperty = typeof(EditorWindow).GetProperty("focusedWindow", BindingFlags.Public | BindingFlags.Static);
          if (focusedWindowProperty != null)
          {
            EditorWindow focusedWindow = focusedWindowProperty.GetValue(null) as EditorWindow;
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
      string defaultScenePath = EditorUtility.OpenFilePanel("Select the default Scene", Application.dataPath, "unity");
      defaultScenePath = defaultScenePath.Replace(Application.dataPath, "Assets"); //project relative instead of absolute path
      if (!string.IsNullOrEmpty(defaultScenePath))
      {
        defaultScene = defaultScenePath;
        isLoadMasterOnPlay = true;
        Debug.Log($"[SceneAutoLoader] Set Default Scene: {defaultScene}, LoadMasterOnPlay: {isLoadMasterOnPlay}");
      }
      else
      {
        Debug.LogWarning("[SceneAutoLoader] No scene selected or selection cancelled");
      }
    }

    [MenuItem(EditorMenuText)]
    private static void OnClickMenu()
    {
      // Check/Uncheck menu.
      bool isChecked = !Menu.GetChecked(EditorMenuText);
      Menu.SetChecked(EditorMenuText, isChecked);

      // Save to EditorPrefs.
      EditorPrefs.SetBool(EditorMenuText, isChecked);

      isLoadMasterOnPlay = isChecked;
      Debug.Log($"[SceneAutoLoader] LoadMasterOnPlay set to: {isChecked}, DefaultScene: {defaultScene}");
    }

    [MenuItem(EditorMenuText, true)]
    private static bool Valid()
    {
      // Check/Uncheck menu from EditorPrefs.
      Menu.SetChecked(EditorMenuText, EditorPrefs.GetBool(EditorMenuText, false));
      return true;
    }

    // Play mode change callback handles the scene load/reload.
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
      Debug.Log($"[SceneAutoLoader] PlayModeStateChanged: {state}, LoadMasterOnPlay: {isLoadMasterOnPlay}, DefaultScene: {defaultScene}");

      if (!isLoadMasterOnPlay)
      {
        Debug.Log("[SceneAutoLoader] LoadMasterOnPlay is false, skipping");
        return;
      }

      if (string.IsNullOrEmpty(defaultScene))
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
        // ReSharper disable once AccessToStaticMemberViaDerivedType
        previousScene = EditorSceneManager.GetActiveScene().path;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
          try
          {
            Debug.Log($"[SceneAutoLoader] Loading default scene: {defaultScene}");
            EditorSceneManager.OpenScene(defaultScene);
          }
          catch (Exception ex)
          {
            Debug.LogError($"[SceneAutoLoader] Failed to load scene {defaultScene}: {ex.Message}");
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
        Debug.Log($"[SceneAutoLoader] EnteredEditMode - reloading previous scene: {previousScene}");
        try
        {
          if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene);
        }
        catch (Exception ex)
        {
          Debug.LogError($"[SceneAutoLoader] Failed to reload previous scene {previousScene}: {ex.Message}");
        }
      }
    }
  }
}

#endif