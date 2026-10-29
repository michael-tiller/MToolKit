using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MToolKit.Runtime.Settings.Ini;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class IniConfigPathResolverTests
  {
    private readonly List<IniConfig> configs = new();
    private IDisposable owner;

    [TearDown]
    public void ReleaseOwnedResolverAndConfigs()
    {
      owner?.Dispose();
      owner = null;
      foreach (var config in configs) Object.DestroyImmediate(config);
      configs.Clear();
    }

    [TestCase(true, "", "config.ini")]
    [TestCase(false, "nested settings", "controls.ini")]
    public void NoResolverPreservesConfiguredPathSelection(bool persistent, string directory, string fileName)
    {
      var config = Config(persistent, directory, fileName);
      string basePath = persistent ? Application.persistentDataPath : Application.dataPath;
      Assert.That(config.GetIniFilePath(), Is.EqualTo(Path.Combine(basePath, directory, fileName)));
      Assert.That(config.GetIniDirectoryPath(), Is.EqualTo(Path.Combine(basePath, directory)));
    }

    [Test]
    public void InstalledResolverSelectsOwnedAbsolutePathForEveryConfigWithoutMutatingAssets()
    {
      var first = Config(true, "original", "first.ini");
      var second = Config(false, "another", "second.ini");
      string root = Path.GetFullPath(Path.Combine(Application.temporaryCachePath, "MToolKit-IniConfig-path-tests", Guid.NewGuid().ToString("N")));
      var seen = new List<IniConfig>();
      var defaults = new List<string>();
      owner = Install((selected, normal) => { seen.Add(selected); defaults.Add(normal); return Path.Combine(root, selected.IniFileName); });
      Assert.That(first.GetIniFilePath(), Is.EqualTo(Path.Combine(root, "first.ini")));
      Assert.That(second.GetIniFilePath(), Is.EqualTo(Path.Combine(root, "second.ini")));
      Assert.That(seen, Is.EqualTo(new[] { first, second }));
      Assert.That(defaults, Is.EqualTo(new[]
      {
        Path.Combine(Application.persistentDataPath, "original", "first.ini"),
        Path.Combine(Application.dataPath, "another", "second.ini")
      }), "The resolver receives the normal path directly without recursive config calls.");
      Assert.That(first.IniDirectory, Is.EqualTo("original"));
      Assert.That(first.UsePersistentDataPath, Is.True);
      Assert.That(second.IniDirectory, Is.EqualTo("another"));
      Assert.That(second.UsePersistentDataPath, Is.False);
    }

    [Test]
    public void ConflictingOwnerIsRejectedWithoutReplacingTheInstalledResolver()
    {
      var config = Config();
      string expected = OwnedPath("first.ini");
      owner = Install((selected, normal) => expected);
      Assert.Throws<InvalidOperationException>(() => Install((selected, normal) => OwnedPath("other.ini")));
      Assert.That(config.GetIniFilePath(), Is.EqualTo(expected));
    }

    [Test]
    public void NullResolverIsRejectedWithoutTakingOwnership()
    {
      Assert.Throws<ArgumentNullException>(() => Install(null));
      string expected = OwnedPath("owned.ini");
      owner = Install((selected, normal) => expected);
      Assert.That(Config().GetIniFilePath(), Is.EqualTo(expected));
    }

    [Test]
    public void DisposalRestoresNormalSelectionAndAnOldOwnerCannotClearItsSuccessor()
    {
      var config = Config(false, "normal", "original.ini");
      string normal = config.GetIniFilePath();
      var previous = Install((selected, normalPath) => OwnedPath("first.ini"));
      owner = previous;
      Assert.That(config.GetIniFilePath(), Is.Not.EqualTo(normal));
      previous.Dispose();
      Assert.That(config.GetIniFilePath(), Is.EqualTo(normal));
      string next = OwnedPath("second.ini");
      owner = Install((selected, normalPath) => next);
      previous.Dispose();
      Assert.That(config.GetIniFilePath(), Is.EqualTo(next));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("relative/config.ini")]
    [TestCase(@"C:relative.ini")]
    [TestCase(@"\root-relative.ini")]
    public void InvalidResolverPathFailsClosedIncludingDefaultConfigFactoryRetry(string invalidPath)
    {
      int calls = 0;
      owner = Install((selected, normal) => { calls++; return invalidPath; });
      var supplied = Config(false, "specific", "supplied.ini");
      Assert.Throws<InvalidOperationException>(() => new IniService(supplied),
        "Invalid isolation must fail before IniService opens any production path.");
      var fallback = Config();
      Assert.Throws<InvalidOperationException>(() => new IniService(fallback),
        "A factory retry with a default config must retain the resolver and fail closed too.");
      Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void ResolverExceptionPropagatesIncludingDefaultConfigFactoryRetry()
    {
      var expected = new IOException("Owned path could not be selected.");
      int calls = 0;
      owner = Install((selected, normal) => { calls++; throw expected; });
      Assert.That(Assert.Throws<IOException>(() => new IniService(Config(false, "custom", "custom.ini"))), Is.SameAs(expected));
      Assert.That(Assert.Throws<IOException>(() => new IniService(Config())), Is.SameAs(expected));
      Assert.That(calls, Is.EqualTo(2), "A failed resolver must not be silently removed for the fallback config.");
    }

    [Test]
    public void SubsystemRegistrationResetsOwnershipWithoutLettingStaleDisposalClearANewOwner()
    {
      var config = Config();
      string normal = config.GetIniFilePath();
      var previous = Install((selected, normalPath) => OwnedPath("before-reset.ini"));
      owner = previous;
      var reset = typeof(IniConfig).GetMethod("ResetScopedPathResolver", BindingFlags.Static | BindingFlags.NonPublic);
      Assert.That(reset, Is.Not.Null);
      var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
      Assert.That(attribute, Is.Not.Null);
      Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
      reset.Invoke(null, null);
      Assert.That(config.GetIniFilePath(), Is.EqualTo(normal));
      string next = OwnedPath("after-reset.ini");
      owner = Install((selected, normalPath) => next);
      previous.Dispose();
      Assert.That(config.GetIniFilePath(), Is.EqualTo(next));
    }

    private IniConfig Config(bool persistent = true, string directory = "", string fileName = "config.ini")
    {
      var config = ScriptableObject.CreateInstance<IniConfig>();
      configs.Add(config);
      const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
      typeof(IniConfig).GetField("usePersistentDataPath", fields).SetValue(config, persistent);
      typeof(IniConfig).GetField("iniDirectory", fields).SetValue(config, directory);
      typeof(IniConfig).GetField("iniFileName", fields).SetValue(config, fileName);
      return config;
    }

    private static string OwnedPath(string fileName)
      => Path.GetFullPath(Path.Combine(Application.temporaryCachePath, "MToolKit-IniConfig-path-tests", fileName));

    private static IDisposable Install(Func<IniConfig, string, string> resolver)
    {
      var method = typeof(IniConfig).GetMethod("InstallScopedPathResolver", BindingFlags.Public | BindingFlags.Static,
        null, new[] { typeof(Func<IniConfig, string, string>) }, null);
      Assert.That(method, Is.Not.Null, "A scoped development/test resolver must select the INI path before normal service construction.");
      try { return (IDisposable)method.Invoke(null, new object[] { resolver }); }
      catch (TargetInvocationException error) when (error.InnerException != null)
      {
        ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        throw;
      }
    }
  }
}
