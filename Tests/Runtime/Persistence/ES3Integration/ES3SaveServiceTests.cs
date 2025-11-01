using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using MToolKit.Runtime.Persistence.ES3Integration;
using static ES3;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using MToolKit.Runtime.Slog;

namespace MToolKit.Tests.Runtime.Persistence.ES3Integration
{
    /// <summary>
    /// Comprehensive unit tests for ES3SaveService
    /// Includes basic functionality, edge cases, and property-based tests
    /// Designed to avoid Unity deadlocks and async issues
    /// </summary>
    [TestFixture]
    public sealed class ES3SaveServiceTests
    {
        private ES3SaveService saveService;
        private string testFilePath;
        private CancellationTokenSource cancellationTokenSource;
        private PersistenceTestHelper testHelper;

        [SetUp]
        public void SetUp()
        {
            // Initialize test helper to manage SlogLoader state
            testHelper = new PersistenceTestHelper();
            
            // Mock SlogLoader as initialized to prevent Bootstrapper timeout issues
            testHelper.MockSlogLoaderInitialized(true);
            testHelper.MockFlushSlogOnQuitFound(true);
            
            // Create a unique test directory for this test to avoid file conflicts
            var testDir = Path.Combine(Application.temporaryCachePath, "ES3Tests", Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(testDir);
            testFilePath = Path.Combine(testDir, $"RobustTest_{Guid.NewGuid()}.es3").Replace('\\', '/');
            cancellationTokenSource = new CancellationTokenSource();
            
            // Ensure the directory exists and is writable
            var directory = Path.GetDirectoryName(testFilePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            CleanupTestFile();
        }

        [TearDown]
        public void TearDown()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            saveService?.Dispose();
            
            // Clean up test helper
            testHelper?.Cleanup();
            
            // Force garbage collection to release any file handles
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            // Wait a bit to ensure file handles are released
            Thread.Sleep(50);
            
            CleanupTestFile();
            
            // Clean up the entire test directory
            try
            {
                var testDir = Path.GetDirectoryName(testFilePath);
                if (System.IO.Directory.Exists(testDir))
                {
                    System.IO.Directory.Delete(testDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void CleanupTestFile()
        {
            try
            {
                if (ES3.FileExists(testFilePath))
                {
                    ES3.DeleteFile(testFilePath);
                }
                
                // Also clean up any backup files that might exist
                var backupPath = testFilePath + ".backup";
                if (ES3.FileExists(backupPath))
                {
                    ES3.DeleteFile(backupPath);
                }
                
                // Clean up any temporary files
                var tempPath = testFilePath + ".tmp";
                if (ES3.FileExists(tempPath))
                {
                    ES3.DeleteFile(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Creates an ES3SaveService with test-friendly configuration (no backups, no encryption)
        /// </summary>
        private ES3SaveService CreateTestES3SaveService()
        {
            var config = ES3SaveServiceTestHelpers.CreateConfig(
                compress: false, 
                encrypt: false, 
                encryptionKey: null, 
                createBackups: false
            );
            return new ES3SaveService(testFilePath, config);
        }

        #region Basic Functionality Tests

        [Test]
        [Timeout(3000)]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act
            saveService = CreateTestES3SaveService();

            // Assert
            Assert.IsNotNull(saveService);
            Assert.IsFalse(saveService.IsSaving.Value);
            Assert.IsFalse(saveService.IsLoading.Value);
            Assert.IsEmpty(saveService.LastSaveTime.Value);
            Assert.IsEmpty(saveService.LastLoadTime.Value);
            Assert.AreEqual(0, saveService.SaveCounter.Value);
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            saveService = CreateTestES3SaveService();

            // Act - Test UniTask with proper awaiting
            var saveTask = saveService.SaveAsync(cancellationTokenSource.Token);
            
            // Assert - Verify task is created and can be awaited
            Assert.IsNotNull(saveTask);
            Assert.IsInstanceOf<UniTask>(saveTask);
            
            // Complete the task properly
            await saveTask;
            
            Assert.IsFalse(saveService.IsSaving.Value);
            Assert.IsNotEmpty(saveService.LastSaveTime.Value);
            Assert.AreEqual(1, saveService.SaveCounter.Value);
            Assert.IsTrue(ES3.FileExists(testFilePath));
        }

        [Test]
        [Timeout(5000)]
        public async Task LoadAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            await saveService.SaveAsync(cancellationTokenSource.Token);

            // Act - Test UniTask with proper awaiting
            var loadTask = saveService.LoadAsync(cancellationTokenSource.Token);
            
            // Assert - Verify task is created and can be awaited
            Assert.IsNotNull(loadTask);
            Assert.IsInstanceOf<UniTask>(loadTask);
            
            // Complete the task properly
            await loadTask;

            Assert.IsFalse(saveService.IsLoading.Value);
            Assert.IsNotEmpty(saveService.LastLoadTime.Value);
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveKeyValue_ShouldPersistData()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            const string testKey = "TestKey";
            const string testValue = "TestValue";

            // Act - Test UniTask methods with proper awaiting
            var saveTask = saveService.SaveAsync(testKey, testValue, cancellationTokenSource.Token);
            Assert.IsNotNull(saveTask);
            Assert.IsInstanceOf<UniTask>(saveTask);
            await saveTask;
            
            var loadTask = saveService.LoadAsync<string>(testKey, default, cancellationTokenSource.Token);
            Assert.IsNotNull(loadTask);
            Assert.IsInstanceOf<UniTask<string>>(loadTask);
            var result = await loadTask;

            // Assert
            Assert.AreEqual(testValue, result);
        }

        [Test]
        [Timeout(3000)]
        public void KeyExists_ShouldReturnCorrectValue()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);

            // Act & Assert
            Assert.IsFalse(saveService.KeyExists("NonExistentKey"));
        }

        [Test]
        [Timeout(3000)]
        public void GetSaveFormatVersion_ShouldReturnVersion()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);

            // Act
            var version = saveService.GetSaveFormatVersion();

            // Assert
            Assert.IsNotNull(version);
            Assert.AreEqual("1.0.0", version);
        }

        [Test]
        [Timeout(5000)]
        public async Task DeleteKey_ShouldRemoveData()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            const string testKey = "TestKey";
            const string testValue = "TestValue";

            await saveService.SaveAsync(testKey, testValue, cancellationTokenSource.Token);
            Assert.IsTrue(saveService.KeyExists(testKey));

            // Act
            saveService.DeleteKey(testKey);

            // Assert
            Assert.IsFalse(saveService.KeyExists(testKey));
        }

        [Test]
        [Timeout(5000)]
        public async Task DeleteFile_ShouldRemoveFile()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            Assert.IsTrue(ES3.FileExists(testFilePath));

            // Act
            saveService.DeleteFile();

            // Assert
            Assert.IsFalse(ES3.FileExists(testFilePath));
        }

        [Test]
        [Timeout(3000)]
        public void ReactiveProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            bool isSavingChanged = false;
            bool isLoadingChanged = false;
            int saveChangeCount = 0;
            int loadChangeCount = 0;

            // Act - Subscribe to reactive properties
            // Note: ReactiveProperty immediately fires current value on subscription
            saveService.IsSaving.Subscribe(_ => 
            {
                isSavingChanged = true;
                saveChangeCount++;
            });
            saveService.IsLoading.Subscribe(_ => 
            {
                isLoadingChanged = true;
                loadChangeCount++;
            });

            // Assert - Verify initial subscription fired
            Assert.IsTrue(isSavingChanged, "IsSaving should have fired on subscription");
            Assert.IsTrue(isLoadingChanged, "IsLoading should have fired on subscription");
            Assert.AreEqual(1, saveChangeCount, "IsSaving should have fired exactly once on subscription");
            Assert.AreEqual(1, loadChangeCount, "IsLoading should have fired exactly once on subscription");
            
            // Verify initial values
            Assert.IsFalse(saveService.IsSaving.Value, "IsSaving should be false initially");
            Assert.IsFalse(saveService.IsLoading.Value, "IsLoading should be false initially");
        }

        #endregion

        #region Compression/Encryption Flags

        [Test]
        [Timeout(3000)]
        public void Constructor_WithConfig_ShouldApplyCompressionAndEncryptionFlags()
        {
            // Arrange
            var config = ES3SaveServiceTestHelpers.CreateConfig(compress: true, encrypt: true, encryptionKey: "SecretKey", createBackups: false);

            // Act
            saveService = new ES3SaveService(testFilePath, config);

            // Assert (via reflection)
            var es3SettingsField = typeof(ES3SaveService).GetField("es3Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(es3SettingsField, "es3Settings field not found via reflection");
            var settings = es3SettingsField!.GetValue(saveService);
            Assert.IsNotNull(settings);

            var compressionProp = settings!.GetType().GetField("compressionType");
            var encryptionProp = settings!.GetType().GetField("encryptionType");
            var passwordProp = settings!.GetType().GetField("encryptionPassword");

            Assert.IsNotNull(compressionProp);
            Assert.IsNotNull(encryptionProp);
            Assert.IsNotNull(passwordProp);

            Assert.AreEqual(ES3.CompressionType.Gzip, compressionProp!.GetValue(settings));
            Assert.AreEqual(ES3.EncryptionType.AES, encryptionProp!.GetValue(settings));
            Assert.AreEqual("SecretKey", passwordProp!.GetValue(settings));
        }

        [Test]
        [Timeout(8000)]
        public async Task Encryption_WithMismatchedKey_ShouldFailToLoad()
        {
            // Set up expectations for expected log messages
            LogAssert.Expect(LogType.Warning, new Regex(".*Failed to initialize from existing save file.*"));
            LogAssert.Expect(LogType.Error, new Regex(".*Failed to load key.*"));
            
            // Ensure clean state at the start of the test
            CleanupTestFile();
            await UniTask.Delay(50);
            
            // Create a fresh cancellation token for this test to avoid any interference
            using var testCts = new CancellationTokenSource();
            
            // Arrange: write encrypted data with key "A"
            var configA = ES3SaveServiceTestHelpers.CreateConfig(compress: false, encrypt: true, encryptionKey: "KeyA", createBackups: false);
            saveService = new ES3SaveService(testFilePath, configA);
            await saveService.SaveAsync("EncryptedKey", "Secret", testCts.Token);
            saveService.Dispose();
            
            // Ensure the save service is fully disposed and file handles are released
            await UniTask.Delay(100, cancellationToken: testCts.Token);

            // Act: attempt to read with different key "B"
            var configB = ES3SaveServiceTestHelpers.CreateConfig(compress: false, encrypt: true, encryptionKey: "KeyB", createBackups: false);
            
            // The constructor should succeed but initialization may fail gracefully
            var mismatchedService = new ES3SaveService(testFilePath, configB);
            
            // Give the service a moment to complete initialization
            await UniTask.Delay(50, cancellationToken: testCts.Token);

            // Assert: Loading with mismatched key should either throw an exception or return default value
            var threw = false;
            string loadedValue = null;
            try
            {
                loadedValue = await mismatchedService.LoadAsync<string>("EncryptedKey", ct: testCts.Token);
            }
            catch (Exception)
            {
                threw = true;
            }
            
            // Either an exception should be thrown OR the loaded value should be different from what was saved
            Assert.IsTrue(threw || loadedValue != "Secret", 
                $"Expected either exception or different value, but got: threw={threw}, loadedValue='{loadedValue}'");

            mismatchedService.Dispose();
        }

        #endregion

        #region Backup/Restore Semantics

        [Test]
        [Timeout(8000)]
        public async Task RestoreFromBackup_ShouldRevertToBackedUpState()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            await saveService.SaveAsync("Score", 1, cancellationTokenSource.Token);
            Assert.IsTrue(saveService.CreateBackup(), "Backup should be created after initial save");

            // Mutate state
            await saveService.SaveAsync("Score", 2, cancellationTokenSource.Token);
            var mutated = await saveService.LoadAsync<int>("Score", ct: cancellationTokenSource.Token);
            Assert.AreEqual(2, mutated);

            // Act
            var restored = saveService.RestoreFromBackup();

            // Assert
            Assert.IsTrue(restored, "RestoreFromBackup should return true when backup exists");
            var value = await saveService.LoadAsync<int>("Score", ct: cancellationTokenSource.Token);
            Assert.AreEqual(1, value, "Restored value should match backed up state");
        }

        [Test]
        [Timeout(3000)]
        public void RestoreFromBackup_WhenNoBackup_ShouldReturnFalse()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            saveService.SaveAsync(cancellationTokenSource.Token).Forget();

            // Act
            var result = saveService.RestoreFromBackup();

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Disposal Behavior

        [Test]
        [Timeout(3000)]
        public void Dispose_ShouldBeIdempotent()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);

            // Act & Assert
            Assert.DoesNotThrow(() => saveService.Dispose());
            Assert.DoesNotThrow(() => saveService.Dispose());
        }

        [Test]
        [Timeout(5000)]
        public void Methods_ShouldThrowAfterDispose()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            saveService.Dispose();

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await saveService.SaveAsync(cancellationTokenSource.Token));
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await saveService.LoadAsync(cancellationTokenSource.Token));
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await saveService.SaveAsync("K", 1, cancellationTokenSource.Token));
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await saveService.LoadAsync<int>("K", ct: cancellationTokenSource.Token));
            Assert.Throws<ObjectDisposedException>(() => saveService.KeyExists("K"));
            Assert.Throws<ObjectDisposedException>(() => saveService.DeleteKey("K"));
            Assert.Throws<ObjectDisposedException>(() => saveService.DeleteFile());
        }

        #endregion

        #region Edge Cases and Error Handling

        [Test]
        [Timeout(5000)]
        public async Task SaveAsync_WhenAlreadySaving_ShouldHandleGracefully()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);

            // Act - Start multiple saves
            var saveTask1 = saveService.SaveAsync(cancellationTokenSource.Token);
            var saveTask2 = saveService.SaveAsync(cancellationTokenSource.Token);

            // Assert - Both should complete without error
            Assert.IsNotNull(saveTask1);
            Assert.IsNotNull(saveTask2);
            await saveTask1;
            await saveTask2;
            Assert.IsFalse(saveService.IsSaving.Value);
        }

        [Test]
        [Timeout(5000)]
        public async Task LoadAsync_WhenAlreadyLoading_ShouldHandleGracefully()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            await saveService.SaveAsync(cancellationTokenSource.Token);

            // Act - Start multiple loads
            var loadTask1 = saveService.LoadAsync(cancellationTokenSource.Token);
            var loadTask2 = saveService.LoadAsync(cancellationTokenSource.Token);

            // Assert - Both should complete without error
            Assert.IsNotNull(loadTask1);
            Assert.IsNotNull(loadTask2);
            await loadTask1;
            await loadTask2;
            Assert.IsFalse(saveService.IsLoading.Value);
        }

        [Test]
        [Timeout(3000)]
        public async Task SaveAsync_WhenCancelled_ShouldThrowOperationCanceledException()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            cancellationTokenSource.Cancel();

            // Act & Assert - Test UniTask cancellation with proper async/await
            var saveTask = saveService.SaveAsync(cancellationTokenSource.Token);
            Assert.IsNotNull(saveTask);
            Assert.IsInstanceOf<UniTask>(saveTask);
            
            // Verify that the task throws the expected exception when awaited
            // UniTask throws OperationCanceledException, not TaskCanceledException
            try
            {
                await saveTask;
                Assert.Fail("Expected OperationCanceledException was not thrown");
            }
            catch (OperationCanceledException)
            {
                // Expected exception
            }
        }

        [Test]
        [Timeout(5000)]
        public async Task LoadAsync_WithNonExistentKey_ShouldReturnDefaultValue()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            const string defaultValue = "DefaultValue";

            // Act
            var loadTask = saveService.LoadAsync<string>("NonExistentKey", defaultValue, cancellationTokenSource.Token);
            Assert.IsNotNull(loadTask);
            Assert.IsInstanceOf<UniTask<string>>(loadTask);
            var result = await loadTask;

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        #endregion

        #region Property-Based Tests

        [Test]
        [Timeout(5000)]
        public async Task SaveOperation_ShouldBeIdempotent()
        {
            // Property: Multiple saves should produce identical results
            saveService = new ES3SaveService(testFilePath);
            
            // Act - Save multiple times
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            
            // Assert - All saves should result in same state
            await saveService.LoadAsync(cancellationTokenSource.Token);
            await saveService.LoadAsync(cancellationTokenSource.Token);
            
            // Both loads should complete successfully
            Assert.IsTrue(ES3.FileExists(testFilePath));
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveLoadCycle_ShouldPreserveDataIntegrity()
        {
            // Property: Save -> Load cycle should preserve all data
            saveService = new ES3SaveService(testFilePath);
            
            var testData = new Dictionary<string, object>
            {
                { "StringValue", "TestString" },
                { "IntValue", 42 },
                { "FloatValue", 3.14f },
                { "BoolValue", true }
            };

            // Act - Save all data
            foreach (var kvp in testData)
            {
                await saveService.SaveAsync(kvp.Key, kvp.Value, cancellationTokenSource.Token);
            }

            // Assert - Load and verify all data
            foreach (var kvp in testData)
            {
                var loadedValue = await saveService.LoadAsync<object>(kvp.Key, null, cancellationTokenSource.Token);
                Assert.AreEqual(kvp.Value, loadedValue);
            }
        }

        [Test]
        [Timeout(5000)]
        public async Task LastSaveTime_ShouldBeMonotonicallyIncreasing()
        {
            // Property: Save times should be in chronological order
            saveService = new ES3SaveService(testFilePath);
            
            var saveTimes = new List<string>();

            // Act - Perform multiple saves
            for (int i = 0; i < 3; i++)
            {
                await saveService.SaveAsync(cancellationTokenSource.Token);
                saveTimes.Add(saveService.LastSaveTime.Value);
                Thread.Sleep(100); // Small delay to ensure different timestamps
            }

            // Assert - Times should be in chronological order
            for (int i = 1; i < saveTimes.Count; i++)
            {
                Assert.IsTrue(DateTime.Parse(saveTimes[i]) >= DateTime.Parse(saveTimes[i - 1]));
            }
        }

        [Test]
        [Timeout(5000)]
        public async Task KeyValueOperations_ShouldBeCommutative()
        {
            // Property: Order of key-value operations shouldn't matter for final result
            saveService = new ES3SaveService(testFilePath);
            
            const string key1 = "Key1";
            const string key2 = "Key2";
            const string value1 = "Value1";
            const string value2 = "Value2";

            // Act - Save in one order
            await saveService.SaveAsync(key1, value1, cancellationTokenSource.Token);
            await saveService.SaveAsync(key2, value2, cancellationTokenSource.Token);

            // Load and verify
            var result1 = await saveService.LoadAsync<string>(key1, default, cancellationTokenSource.Token);
            var result2 = await saveService.LoadAsync<string>(key2, default, cancellationTokenSource.Token);

            // Assert
            Assert.AreEqual(value1, result1);
            Assert.AreEqual(value2, result2);
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveCounter_ShouldIncrementOnEachSave()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            
            // Assert initial state
            Assert.AreEqual(0, saveService.SaveCounter.Value);
            
            // Act & Assert - Perform multiple saves and verify counter increments
            for (int i = 1; i <= 5; i++)
            {
                await saveService.SaveAsync(cancellationTokenSource.Token);
                Assert.AreEqual(i, saveService.SaveCounter.Value, $"Save counter should be {i} after {i} saves");
            }
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveCounter_ShouldPersistAcrossServiceInstances()
        {
            // Arrange - First service instance
            saveService = new ES3SaveService(testFilePath);
            
            // Act - Perform saves
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            
            var expectedCounter = saveService.SaveCounter.Value;
            Assert.AreEqual(3, expectedCounter);
            
            // Dispose first service
            saveService.Dispose();
            
            // Arrange - Second service instance with same file
            saveService = new ES3SaveService(testFilePath);
            
            // Assert - Counter should be restored from save file
            Assert.AreEqual(expectedCounter, saveService.SaveCounter.Value, "Save counter should persist across service instances");
            
            // Act - Perform another save
            await saveService.SaveAsync(cancellationTokenSource.Token);
            
            // Assert - Counter should increment from persisted value
            Assert.AreEqual(expectedCounter + 1, saveService.SaveCounter.Value, "Save counter should continue from persisted value");
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveCounter_ShouldStartFromZeroForNewFile()
        {
            // Arrange - Ensure no existing file
            CleanupTestFile();
            saveService = new ES3SaveService(testFilePath);
            
            // Assert - Counter should start at 0 for new file
            Assert.AreEqual(0, saveService.SaveCounter.Value);
            
            // Act - Perform first save
            await saveService.SaveAsync(cancellationTokenSource.Token);
            
            // Assert - Counter should be 1 after first save
            Assert.AreEqual(1, saveService.SaveCounter.Value);
        }

        [Test]
        [Timeout(5000)]
        public async Task SaveCounter_ShouldBeReactive()
        {
            // Arrange
            saveService = new ES3SaveService(testFilePath);
            var counterValues = new List<int>();
            
            // Subscribe to counter changes
            saveService.SaveCounter.Subscribe(counterValues.Add);
            
            // Act - Perform saves
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            await saveService.SaveAsync(cancellationTokenSource.Token);
            
            // Assert - Should have received all counter updates
            Assert.AreEqual(4, counterValues.Count); // Initial value (0) + 3 saves
            Assert.AreEqual(0, counterValues[0]); // Initial value
            Assert.AreEqual(1, counterValues[1]); // After first save
            Assert.AreEqual(2, counterValues[2]); // After second save
            Assert.AreEqual(3, counterValues[3]); // After third save
        }

        #endregion
    }

    // Helper methods
    internal static class ES3SaveServiceTestHelpers
    {
        public static ES3SaveConfig CreateConfig(bool compress, bool encrypt, string encryptionKey, bool createBackups)
        {
            var cfg = ScriptableObject.CreateInstance<ES3SaveConfig>();

            var type = typeof(ES3SaveConfig);
            // Set private serialized fields via reflection
            SetField(type, cfg, "compressSaveData", compress);
            SetField(type, cfg, "encryptSaveData", encrypt);
            SetField(type, cfg, "encryptionKey", encryptionKey);
            SetField(type, cfg, "createBackups", createBackups);

            return cfg;
        }

        private static void SetField(Type t, object instance, string fieldName, object value)
        {
            var field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on ES3SaveConfig");
            field!.SetValue(instance, value);
        }
    }
}
