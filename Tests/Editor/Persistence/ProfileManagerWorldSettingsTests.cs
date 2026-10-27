using System;
using System.IO;
using System.Reflection;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using R3;
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
    private const string ProfileAJson = "{\"RaidFrequencyPercent\":0,\"HostileSpawnRatePercent\":0}";
    private const string ProfileBJson = "{\"RaidFrequencyPercent\":150,\"HostileSpawnRatePercent\":150}";
    private const string EditedJson = "{\"RaidFrequencyPercent\":1000,\"HostileSpawnRatePercent\":200}";
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

    [Test]
    public void SwitchingProfiles_DoesNotWritePreviousProfilesSettingsOrSeed()
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(ProfileAJson);
      manager.SetWorldSeed(111);
      Assert.IsTrue(manager.SaveProfile("A"));

      Assert.IsTrue(manager.LoadProfileAsync("B").GetAwaiter().GetResult());
      Assert.AreEqual(ProfileBJson, manager.CurrentProfileMetadata.Value.WorldSettingsJson,
        "The destination must initially load its own settings before the subsequent save.");
      Assert.IsTrue(manager.SaveProfile("B"));

      AssertPersisted("B", ProfileBJson, 222);
    }

    [Test]
    public void ProfilePublicationCallback_CanQueueNewWorldSettingsAndSeed()
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(ProfileAJson);
      manager.SetWorldSeed(111);
      Assert.IsTrue(manager.CreateProfile("NewWorld"));

      // The actual new-world view model writes its draft from this notification.
      // A transition reset performed after publishing would erase these values.
      using var creation = manager.CurrentProfile.Subscribe(name =>
      {
        if (name != "NewWorld") return;
        manager.SetWorldSettingsJson(EditedJson);
        manager.SetWorldSeed(909);
      });
      Assert.IsTrue(manager.LoadProfileAsync("NewWorld").GetAwaiter().GetResult());
      Assert.AreEqual(EditedJson, manager.CurrentProfileMetadata.Value.WorldSettingsJson);
      Assert.AreEqual(909, manager.CurrentProfileMetadata.Value.WorldSeed);
      Assert.IsTrue(manager.SaveProfile("NewWorld"));

      AssertPersisted("NewWorld", EditedJson, 909);
    }

    [Test]
    public void SavingAnotherProfile_DoesNotConsumeOrWriteCurrentProfilesPendingValues()
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(EditedJson);
      manager.SetWorldSeed(909);

      Assert.IsTrue(manager.SaveProfile("B"));
      AssertPersisted("B", ProfileBJson, 222);
      Assert.AreEqual("A", manager.CurrentProfile.Value);
      Assert.IsTrue(manager.SaveProfile("A"));
      AssertPersisted("A", EditedJson, 909);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LeavingCurrentProfile_DoesNotCarryPendingValuesToNextProfile(bool deleteCurrent)
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(EditedJson);
      manager.SetWorldSeed(909);

      if (deleteCurrent)
        Assert.IsTrue(manager.DeleteProfile("A"));
      else
        manager.CloseCurrentProfile();
      Assert.AreEqual(string.Empty, manager.CurrentProfile.Value);
      Assert.IsTrue(manager.LoadProfileAsync("B").GetAwaiter().GetResult());
      Assert.IsTrue(manager.SaveProfile("B"));

      AssertPersisted("B", ProfileBJson, 222);
    }

    [Test]
    public void FailedProfileLoad_PreservesCurrentProfilesPendingValues()
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(EditedJson);
      manager.SetWorldSeed(909);

      Assert.IsFalse(manager.LoadProfileAsync("Missing").GetAwaiter().GetResult());
      Assert.AreEqual("A", manager.CurrentProfile.Value);
      Assert.IsTrue(manager.SaveProfile("A"));

      AssertPersisted("A", EditedJson, 909);
    }

    [Test]
    public void ReloadingCurrentProfile_PreservesItsPendingValues()
    {
      using var manager = CreateManager();
      CreateSavedProfiles(manager);
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(EditedJson);
      manager.SetWorldSeed(909);

      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      Assert.IsTrue(manager.SaveProfile("A"));

      AssertPersisted("A", EditedJson, 909);
    }

    [Test]
    public void ValuesQueuedBeforeFirstProfileLoad_AreSavedToThatProfile()
    {
      using var manager = CreateManager();
      Assert.AreEqual(string.Empty, manager.CurrentProfile.Value);
      manager.SetWorldSettingsJson(EditedJson);
      manager.SetWorldSeed(909);

      Assert.IsTrue(manager.CreateProfile("First"));
      Assert.IsTrue(manager.LoadProfileAsync("First").GetAwaiter().GetResult());
      Assert.IsTrue(manager.SaveProfile("First"));

      AssertPersisted("First", EditedJson, 909);
    }

    private static void CreateSavedProfiles(ProfileManager manager)
    {
      Assert.IsTrue(manager.CreateProfile("A"));
      Assert.IsTrue(manager.LoadProfileAsync("A").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(ProfileAJson);
      manager.SetWorldSeed(111);
      Assert.IsTrue(manager.SaveProfile("A"));
      Assert.IsTrue(manager.CreateProfile("B"));
      Assert.IsTrue(manager.LoadProfileAsync("B").GetAwaiter().GetResult());
      manager.SetWorldSettingsJson(ProfileBJson);
      manager.SetWorldSeed(222);
      Assert.IsTrue(manager.SaveProfile("B"));
    }

    private void AssertPersisted(string profileName, string expectedJson, int expectedSeed)
    {
      using var reader = CreateManager();
      var metadata = reader.GetProfileMetaData(profileName);
      Assert.IsNotNull(metadata);
      Assert.AreEqual(expectedJson, metadata.WorldSettingsJson,
        "A fresh manager must read only the target profile's world settings from disk.");
      Assert.AreEqual(expectedSeed, metadata.WorldSeed,
        "The pending seed must follow the same profile boundary as the settings JSON.");
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
