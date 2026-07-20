using System;
using System.IO;
using System.Reflection;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.Persistence
{
  /// <summary>
  ///   ProfileManager.WorldSettingsJson persistence: the field must survive the SaveProfile
  ///   reconstruction (a NEW ProfileMetaData is built on every save, see SaveProfile) across
  ///   sequential saves and a fresh ProfileManager instance re-reading the same file on disk.
  ///   Real ES3 file I/O against a temp directory — SaveProfile writes via the static ES3 API
  ///   regardless of the injected IES3Service, so a fake service alone would not exercise this.
  /// </summary>
  [TestFixture]
  public class ProfileManagerWorldSettingsTests
  {
    private string tempDirectory;

    [SetUp]
    public void SetUp()
    {
      tempDirectory = Path.Combine(Path.GetTempPath(), $"profile_manager_test_{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
      if (Directory.Exists(tempDirectory))
        Directory.Delete(tempDirectory, recursive: true);
    }

    private ProfileManager CreateManager()
    {
      var config = ScriptableObject.CreateInstance<TestES3SaveConfig>();
      config.SetSaveDirectory(tempDirectory);
      return new ProfileManager(new MemoryES3Service(), config);
    }

    [Test]
    public void SetWorldSettingsJson_ThenSave_PersistsThroughGetProfileMetaData()
    {
      using var manager = CreateManager();
      manager.CreateProfile("TestProfile");
      manager.LoadProfileAsync("TestProfile").GetAwaiter().GetResult();

      manager.SetWorldSettingsJson("{\"Seed\":42}");
      manager.SaveProfile("TestProfile");

      var metadata = manager.GetProfileMetaData("TestProfile");
      Assert.AreEqual("{\"Seed\":42}", metadata.WorldSettingsJson,
        "WorldSettingsJson must survive the SaveProfile reconstruction, not just live in memory");
    }

    [Test]
    public void SaveProfile_SecondSaveWithoutNewWrite_PreservesPreviousWorldSettingsJson()
    {
      using var manager = CreateManager();
      manager.CreateProfile("TestProfile");
      manager.LoadProfileAsync("TestProfile").GetAwaiter().GetResult();

      manager.SetWorldSettingsJson("{\"Seed\":42}");
      manager.SaveProfile("TestProfile");
      manager.SaveProfile("TestProfile"); // sequential save, no new SetWorldSettingsJson call

      var metadata = manager.GetProfileMetaData("TestProfile");
      Assert.AreEqual("{\"Seed\":42}", metadata.WorldSettingsJson,
        "a save-merge bug would silently drop this on the second reconstruction");
    }

    [Test]
    public void FreshProfileManagerInstance_ReadsWorldSettingsJsonFromDisk()
    {
      using (var writer = CreateManager())
      {
        writer.CreateProfile("TestProfile");
        writer.LoadProfileAsync("TestProfile").GetAwaiter().GetResult();
        writer.SetWorldSettingsJson("{\"Seed\":42}");
        writer.SaveProfile("TestProfile");
      }

      using var reader = CreateManager();
      var metadata = reader.GetProfileMetaData("TestProfile");

      Assert.AreEqual("{\"Seed\":42}", metadata.WorldSettingsJson,
        "must actually be on disk, not just held by the writer's in-memory pending field");
    }

    [Test]
    public void LegacyProfile_WithoutWorldSettingsJson_LoadsAsEmpty()
    {
      using var manager = CreateManager();
      manager.CreateProfile("TestProfile");
      manager.LoadProfileAsync("TestProfile").GetAwaiter().GetResult();
      // Never call SetWorldSettingsJson — simulates a profile saved before this feature existed.
      manager.SaveProfile("TestProfile");

      var metadata = manager.GetProfileMetaData("TestProfile");
      Assert.AreEqual(string.Empty, metadata.WorldSettingsJson);
    }

    /// <summary>Minimal reflection-backed test double, mirroring the pattern already used for ES3SaveConfig in dirigible2D's ProfileAwareES3ServiceTests.</summary>
    private class TestES3SaveConfig : ES3SaveConfig
    {
      private static readonly FieldInfo SaveDirectoryField = typeof(ES3SaveConfig)
        .GetField("saveDirectory", BindingFlags.NonPublic | BindingFlags.Instance);

      private static readonly FieldInfo UsePersistentDataPathField = typeof(ES3SaveConfig)
        .GetField("usePersistentDataPath", BindingFlags.NonPublic | BindingFlags.Instance);

      public void SetSaveDirectory(string directory)
      {
        UsePersistentDataPathField?.SetValue(this, false);
        SaveDirectoryField?.SetValue(this, directory);
      }
    }
  }
}
