using System;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Utilities
{
  /// <summary>
  ///   A generic MonoBehaviour-based Singleton that can auto-generate itself.
  /// </summary>
  /// <typeparam name="T">The concrete type inheriting from Singleton{T}.</typeparam>
  public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<Singleton<T>>().ForFeature("Utility"));

    /// <summary>
    ///   Holds the Singleton instance.
    /// </summary>
    private static T instance;

    /// <summary>
    ///   True if the application is quitting; used to prevent new instance creation.
    /// </summary>
    private static bool isApplicationQuitting;

    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Readonly property indicating whether we have a valid instance.
    /// </summary>
    public static bool HasInstance => instance != null && !isApplicationQuitting;

    /// <summary>
    ///   Access point for the Singleton instance.
    ///   If the instance is null, we search the scene, and if still null, we create one
    ///   (unless quitting).
    /// </summary>
    public static T Instance
    {
      get
      {
        // Guard clause: if the application is quitting, don't create anything.
        if (isApplicationQuitting)
        {
          log.ForMethod(typeof(T).Name).Information("Singleton<{0}> requested after quit. Returning null.", typeof(T).Name);
          return null;
        }

        // If we don't yet have an instance, search for it
        if (instance == null)
        {
          instance = FindFirstObjectByType<T>();

          // If we *still* don't have an instance, optionally create one
          if (instance == null)
          {
            // We need to know if this T is allowed to selfCreate.
            // Because there's no instance yet, we create a TEMPORARY GameObject
            // just so we can read T's selfCreate value from it:
            GameObject go = new(nameof(T));
            T component = go.AddComponent<T>();
            bool canSelfCreate = component.selfCreate;
            DestroyImmediate(go); // immediately nuke the temp

            if (canSelfCreate)
            {
              // Now we know that T is allowed to auto-generate
              log.ForMethod(typeof(T).Name).Information("No instance of {0} found. Creating a new one.", typeof(T).Name);
              GameObject singletonObject = new($"[Singleton] {typeof(T).Name}");
              instance = singletonObject.AddComponent<T>();
            }
            else
            {
              // If not allowed to selfCreate, just return null
              log.ForMethod(typeof(T).Name).Verbose("No instance of {0} found. Returning null (selfCreate=false).", typeof(T).Name);
              return null;
            }
          }
        }

        return instance;
      }
    }

    /// <summary>
    ///   If true, this Singleton instance will persist across scene loads.
    /// </summary>
    protected virtual bool dontDestroyOnLoad => false;

    /// <summary>
    ///   If true, this singleton will create itself automatically if none is found in the scene.
    ///   By default, it's false, but you can override or set it in your derived class or the Inspector.
    /// </summary>
    protected virtual bool selfCreate => false;

    /// <summary>
    ///   Called by Unity when the script instance is being loaded.
    ///   Ensures there is only one valid instance; duplicates will be destroyed.
    /// </summary>
    protected virtual void Awake()
    {
      if (instance == null)
      {
        instance = (T)this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
      }
      else if (instance != this)
      {
        log.ForGameObject(gameObject).ForMethod().Information(
          "Duplicate Singleton<{0}> found. Destroying new instance on '{1}'.",
          typeof(T).Name,
          gameObject.name
          );
        Destroy(gameObject);
      }
    }

    protected virtual void OnDestroy()
    {
      if (HasInstance) instance = null;
    }

    /// <summary>
    ///   Called by Unity when the application is quitting.
    ///   We use this to prevent creation of new instances after quitting.
    /// </summary>
    protected virtual void OnApplicationQuit()
    {
      log.ForGameObject(gameObject).ForMethod(typeof(T).Name).Verbose("{0}: {1}", typeof(T).Name, nameof(OnApplicationQuit));
      isApplicationQuitting = true;
    }
  }
}