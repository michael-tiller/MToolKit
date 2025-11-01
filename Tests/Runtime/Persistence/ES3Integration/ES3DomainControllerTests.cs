using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.ES3Integration;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using Serilog;
using UnityEngine.TestTools;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Slog;
using UnityEngine;

namespace MToolKit.Tests.Runtime.Persistence.ES3Integration
{
    /// <summary>
    /// Comprehensive unit tests for ES3DomainController
    /// Tests save/load operations, cancellation handling, exception propagation, and domain-specific behavior
    /// CRITICAL: Uses LogAssert.ignoreFailingMessages for Serilog integration in exception-throwing tests
    /// CRITICAL: Follows MDC patterns for proper test structure and error handling
    /// </summary>
    [TestFixture]
    public class ES3DomainControllerTests
    {
        private TestES3Service testES3Service;
        private TestSaveable testSaveable1;
        private TestSaveable testSaveable2;
        private List<ISaveable> saveables;
        private ES3DomainController controller;
        private CancellationToken cancellationToken;
        private PersistenceTestHelper testHelper;

        /// <summary>
        /// Sets up test dependencies and creates fresh instances for each test
        /// CRITICAL: Creates new instances to avoid test interference
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // Initialize test helper to manage SlogLoader state
            testHelper = new PersistenceTestHelper();
            
            // Mock SlogLoader as initialized to prevent Bootstrapper timeout issues
            testHelper.MockSlogLoaderInitialized(true);
            testHelper.MockFlushSlogOnQuitFound(true);
            
            testES3Service = new TestES3Service();
            testSaveable1 = new TestSaveable("test_saveable_1");
            testSaveable2 = new TestSaveable("test_saveable_2");
            
            saveables = new List<ISaveable> { testSaveable1, testSaveable2 };
            controller = new ES3DomainController(ESaveDomain.Player, saveables, testES3Service);
            cancellationToken = CancellationToken.None;
        }

        /// <summary>
        /// Cleans up test resources after each test
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // Clean up test helper
            testHelper?.Cleanup();
        }

        /// <summary>
        /// Tests that ES3DomainController constructor creates a valid instance with correct domain
        /// </summary>
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var domainController = new ES3DomainController(ESaveDomain.Player, saveables, testES3Service);
            
            // Assert
            Assert.That(domainController, Is.Not.Null);
            Assert.That(domainController.Domain, Is.EqualTo(ESaveDomain.Player));
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when saveables parameter is null
        /// </summary>
        [Test]
        public void Constructor_WithNullSaveables_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ES3DomainController(ESaveDomain.Player, null, testES3Service));
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when es3Service parameter is null
        /// </summary>
        [Test]
        public void Constructor_WithNullES3Service_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ES3DomainController(ESaveDomain.Player, saveables, null));
        }

        [Test]
        public async Task SaveAsync_WithValidSaveables_ShouldSaveAllData()
        {
            // Arrange
            testSaveable1.SetTestData(new { Value = "test1" });
            testSaveable2.SetTestData(new { Value = "test2" });
            
            // Act
            await controller.SaveAsync(cancellationToken);
            
            // Assert
            Assert.That(testSaveable1.SaveCallCount, Is.EqualTo(1));
            Assert.That(testSaveable2.SaveCallCount, Is.EqualTo(1));
            
            Assert.That(testES3Service.SavedKeys.Contains("player_test_saveable_1"), Is.True);
            Assert.That(testES3Service.SavedKeys.Contains("player_test_saveable_2"), Is.True);
        }

        /// <summary>
        /// Tests that SaveAsync stops saving when cancellation is requested before the operation starts
        /// CRITICAL: Verifies that no saveables are called when cancellation is requested upfront
        /// </summary>
        [Test]
        public async Task SaveAsync_WhenCancellationRequestedBeforeStart_ShouldStopSaving()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before starting
            testSaveable1.SetTestData(new { Value = "test1" });
            
            // Act
            await controller.SaveAsync(cts.Token);
            
            // Assert - when cancellation is requested before starting, no saveables should be called
            Assert.That(testSaveable1.SaveCallCount, Is.EqualTo(0));
            Assert.That(testSaveable2.SaveCallCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Tests that SaveAsync stops saving when cancellation is requested during the save operation
        /// CRITICAL: Uses CancellationTriggeringTestSaveable to reliably trigger cancellation after first saveable
        /// </summary>
        [Test]
        public async Task SaveAsync_WhenCancellationRequestedDuringSave_ShouldStopSaving()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            testSaveable1.SetTestData(new { Value = "test1" });
            testSaveable2.SetTestData(new { Value = "test2" });
            
            // Create a custom saveable that will trigger cancellation after first save
            var customSaveable1 = new CancellationTriggeringTestSaveable("custom_saveable_1", cts);
            customSaveable1.SetTestData(new { Value = "test1" });
            
            var customSaveable2 = new TestSaveable("custom_saveable_2");
            customSaveable2.SetTestData(new { Value = "test2" });
            
            var customSaveables = new List<ISaveable> { customSaveable1, customSaveable2 };
            var customController = new ES3DomainController(ESaveDomain.Player, customSaveables, testES3Service);
            
            // Disable log message checking for this test since Serilog messages can't be expected with LogAssert
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Act & Assert - should throw OperationCanceledException when cancellation is requested
                try
                {
                    await customController.SaveAsync(cts.Token);
                    Assert.Fail("Expected OperationCanceledException to be thrown");
                }
                catch (OperationCanceledException)
                {
                    // Expected behavior - cancellation should be propagated
                }
                
                // Assert - first saveable should be called and trigger cancellation, second should not be called
                Assert.That(customSaveable1.SaveCallCount, Is.EqualTo(1));
                Assert.That(customSaveable2.SaveCallCount, Is.EqualTo(0));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public async Task SaveAsync_WhenSaveableThrowsException_ShouldPropagateException()
        {
            // Arrange
            testSaveable1.SetShouldThrowException(true);
            
            // Disable log message checking for this test since Serilog messages can't be expected with LogAssert
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Act & Assert
                try
                {
                    await controller.SaveAsync(cancellationToken);
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.Not.Null);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public async Task SaveAsync_WhenES3ServiceThrowsException_ShouldPropagateException()
        {
            // Arrange
            testES3Service.SetShouldThrowException(true);
            testSaveable1.SetTestData(new { Value = "test" });
            
            // Disable log message checking for this test since Serilog messages can't be expected with LogAssert
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Act & Assert
                try
                {
                    await controller.SaveAsync(cancellationToken);
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.Not.Null);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public async Task LoadAsync_WithExistingKeys_ShouldLoadAllData()
        {
            // Arrange
            testES3Service.SetKeyExists("player_test_saveable_1", true);
            testES3Service.SetKeyExists("player_test_saveable_2", true);
            testES3Service.SetLoadData("player_test_saveable_1", new { Value = "test1" });
            testES3Service.SetLoadData("player_test_saveable_2", new { Value = "test2" });
            
            // Act
            await controller.LoadAsync(cancellationToken);
            
            // Assert
            Assert.That(testSaveable1.LoadCallCount, Is.EqualTo(1));
            Assert.That(testSaveable2.LoadCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task LoadAsync_WithNonExistingKeys_ShouldSkipLoading()
        {
            // Arrange
            testES3Service.SetKeyExists("player_test_saveable_1", false);
            testES3Service.SetKeyExists("player_test_saveable_2", false);
            
            // Act
            await controller.LoadAsync(cancellationToken);
            
            // Assert
            Assert.That(testSaveable1.LoadCallCount, Is.EqualTo(0));
            Assert.That(testSaveable2.LoadCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task LoadAsync_WithNullData_ShouldNotCallLoadAsyncOnSaveable()
        {
            // Arrange
            testES3Service.SetKeyExists("player_test_saveable_1", true);
            testES3Service.SetLoadData("player_test_saveable_1", null);
            
            // Act
            await controller.LoadAsync(cancellationToken);
            
            // Assert
            Assert.That(testSaveable1.LoadCallCount, Is.EqualTo(0));
        }


        [Test]
        public async Task LoadAsync_WhenSaveableThrowsException_ShouldPropagateException()
        {
            // Arrange
            testES3Service.SetKeyExists("player_test_saveable_1", true);
            testES3Service.SetLoadData("player_test_saveable_1", new { Value = "test" });
            testSaveable1.SetShouldThrowLoadException(true);
            
            // Disable log message checking for this test since Serilog messages can't be expected with LogAssert
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Act & Assert
                try
                {
                    await controller.LoadAsync(cancellationToken);
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.Not.Null);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public async Task LoadAsync_WhenES3ServiceThrowsException_ShouldPropagateException()
        {
            // Arrange
            testES3Service.SetKeyExists("player_test_saveable_1", true);
            testES3Service.SetShouldThrowLoadException(true);
            
            // Disable log message checking for this test since Serilog messages can't be expected with LogAssert
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Act & Assert
                try
                {
                    await controller.LoadAsync(cancellationToken);
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.Not.Null);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Domain_ShouldReturnCorrectDomain()
        {
            // Arrange
            var playerController = new ES3DomainController(ESaveDomain.Player, saveables, testES3Service);
            var worldController = new ES3DomainController(ESaveDomain.World, saveables, testES3Service);
            
            // Assert
            Assert.That(playerController.Domain, Is.EqualTo(ESaveDomain.Player));
            Assert.That(worldController.Domain, Is.EqualTo(ESaveDomain.World));
        }

        [Test]
        public async Task SaveAsync_WithDifferentDomains_ShouldUseCorrectPrefixes()
        {
            // Arrange - Create separate saveables for each controller to avoid shared state
            var playerSaveables = new List<ISaveable> { new TestSaveable("test_saveable_1"), new TestSaveable("test_saveable_2") };
            var worldSaveables = new List<ISaveable> { new TestSaveable("test_saveable_1"), new TestSaveable("test_saveable_2") };
            
            var playerController = new ES3DomainController(ESaveDomain.Player, playerSaveables, testES3Service);
            var worldController = new ES3DomainController(ESaveDomain.World, worldSaveables, testES3Service);
            
            // Set test data for each controller's saveables
            ((TestSaveable)playerSaveables[0]).SetTestData(new { Value = "player_test" });
            ((TestSaveable)worldSaveables[0]).SetTestData(new { Value = "world_test" });
            
            // Act
            await playerController.SaveAsync(cancellationToken);
            await worldController.SaveAsync(cancellationToken);
            
            // Assert
            Assert.That(testES3Service.SavedKeys.Contains("player_test_saveable_1"), Is.True);
            Assert.That(testES3Service.SavedKeys.Contains("world_test_saveable_1"), Is.True);
            
            // Verify that each controller's saveables were saved
            var playerSaveable1 = (TestSaveable)playerSaveables[0];
            var worldSaveable1 = (TestSaveable)worldSaveables[0];
            
            Assert.That(playerSaveable1.SaveCallCount, Is.EqualTo(1));
            Assert.That(worldSaveable1.SaveCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task LoadAsync_WithDifferentDomains_ShouldUseCorrectPrefixes()
        {
            // Arrange - Create separate saveables for each controller to avoid shared state
            var playerSaveables = new List<ISaveable> { new TestSaveable("test_saveable_1"), new TestSaveable("test_saveable_2") };
            var worldSaveables = new List<ISaveable> { new TestSaveable("test_saveable_1"), new TestSaveable("test_saveable_2") };
            
            var playerController = new ES3DomainController(ESaveDomain.Player, playerSaveables, testES3Service);
            var worldController = new ES3DomainController(ESaveDomain.World, worldSaveables, testES3Service);
            
            testES3Service.SetKeyExists("player_test_saveable_1", true);
            testES3Service.SetKeyExists("world_test_saveable_1", true);
            testES3Service.SetLoadData("player_test_saveable_1", new { Value = "test" });
            testES3Service.SetLoadData("world_test_saveable_1", new { Value = "test" });
            
            // Act
            await playerController.LoadAsync(cancellationToken);
            await worldController.LoadAsync(cancellationToken);
            
            // Assert
            Assert.That(testES3Service.KeyExistsCallCount, Is.GreaterThanOrEqualTo(2));
            
            // Verify that each controller's saveables were loaded
            var playerSaveable1 = (TestSaveable)playerSaveables.First();
            var worldSaveable1 = (TestSaveable)worldSaveables.First();
            
            Assert.That(playerSaveable1.LoadCallCount, Is.EqualTo(1));
            Assert.That(worldSaveable1.LoadCallCount, Is.EqualTo(1));
        }
    }

    // Test helper classes are now defined in ES3DomainControllerPropertyTests.cs to avoid duplication

    public static class ES3DomainControllerTestData
    {
        public static IEnumerable<ESaveDomain> AllDomains => Enum.GetValues(typeof(ESaveDomain)).Cast<ESaveDomain>();
        
        public static IEnumerable<object[]> DomainPrefixTestCases()
        {
            yield return new object[] { ESaveDomain.Player, "player_" };
            yield return new object[] { ESaveDomain.World, "world_" };
            yield return new object[] { ESaveDomain.Settings, "settings_" };
        }
        
        public static TestSaveable CreateTestSaveable(string key)
        {
            return new TestSaveable(key);
        }
        
        public static List<ISaveable> CreateSaveablesList(params string[] keys)
        {
            var saveables = new List<ISaveable>();
            foreach (var key in keys)
            {
                saveables.Add(CreateTestSaveable(key));
            }
            return saveables;
        }
    }

    /// <summary>
    /// Test saveable that triggers cancellation after being called once
    /// CRITICAL: Provides reliable cancellation testing without timing dependencies
    /// CRITICAL: Used for testing cancellation behavior during save operations
    /// </summary>
    public class CancellationTriggeringTestSaveable : ISaveable
    {
        private object testData;
        private readonly CancellationTokenSource cancellationTokenSource;
        
        public string Key { get; }
        public int SaveCallCount { get; private set; }
        public int LoadCallCount { get; private set; }

        /// <summary>
        /// Initializes a new instance of CancellationTriggeringTestSaveable
        /// </summary>
        /// <param name="key">The saveable key</param>
        /// <param name="cts">The cancellation token source to trigger after first save</param>
        public CancellationTriggeringTestSaveable(string key, CancellationTokenSource cts)
        {
            Key = key;
            cancellationTokenSource = cts;
        }

        /// <summary>
        /// Sets the test data to be returned by SaveAsync
        /// </summary>
        /// <param name="data">The test data</param>
        public void SetTestData(object data)
        {
            testData = data;
        }

        /// <summary>
        /// Saves the test data and triggers cancellation after first call
        /// CRITICAL: Triggers cancellation on first call to test cancellation during save operations
        /// </summary>
        /// <returns>The test data</returns>
        public async UniTask<object> SaveAsync()
        {
            SaveCallCount++;
            
            // Trigger cancellation after first save
            if (SaveCallCount == 1)
            {
                cancellationTokenSource.Cancel();
            }
                
            await UniTask.CompletedTask;
            return testData;
        }

        /// <summary>
        /// Loads the provided data (test implementation)
        /// </summary>
        /// <param name="data">The data to load</param>
        public async UniTask LoadAsync(object data)
        {
            LoadCallCount++;
            await UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test implementation of IES3Service with SavedKeys tracking for domain controller tests
    /// </summary>
    public class TestES3Service : IES3Service
    {
        private readonly Dictionary<string, bool> keyExistsMap = new();
        private readonly Dictionary<string, object> loadDataMap = new();
        private readonly HashSet<string> savedKeys = new();
        
        public bool ShouldThrowException { get; set; }
        public bool ShouldThrowLoadException { get; set; }
        public int KeyExistsCallCount { get; private set; }
        
        public HashSet<string> SavedKeys => savedKeys;

        public ReactiveProperty<bool> IsSaving { get; } = new(false);
        public ReactiveProperty<bool> IsLoading { get; } = new(false);
        public ReactiveProperty<string> LastSaveTime { get; } = new(string.Empty);
        public ReactiveProperty<string> LastLoadTime { get; } = new(string.Empty);
        public ReactiveProperty<int> SaveCounter { get; } = new(0);

        public void SetKeyExists(string key, bool exists)
        {
            keyExistsMap[key] = exists;
        }

        public void SetLoadData(string key, object data)
        {
            loadDataMap[key] = data;
        }

        public void SetShouldThrowException(bool shouldThrow)
        {
            ShouldThrowException = shouldThrow;
        }

        public void SetShouldThrowLoadException(bool shouldThrow)
        {
            ShouldThrowLoadException = shouldThrow;
        }

        public bool KeyExists(string key)
        {
            KeyExistsCallCount++;
            return keyExistsMap.GetValueOrDefault(key, false);
        }

        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            if (ShouldThrowException)
                throw new InvalidOperationException("Test exception");
            
            // Check cancellation before delay to avoid hanging
            ct.ThrowIfCancellationRequested();
            IsSaving.Value = true;
            await UniTask.Delay(1, cancellationToken: ct);
            IsSaving.Value = false;
            SaveCounter.Value++;
            LastSaveTime.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            if (ShouldThrowLoadException)
                throw new InvalidOperationException("Test load exception");
            
            // Check cancellation before delay to avoid hanging
            ct.ThrowIfCancellationRequested();
            IsLoading.Value = true;
            await UniTask.Delay(1, cancellationToken: ct);
            IsLoading.Value = false;
            LastLoadTime.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public UniTask SaveAsync(string key, object value, CancellationToken ct = default)
        {
            if (ShouldThrowException)
                throw new InvalidOperationException("Test exception");
            
            // Check cancellation before delay to avoid hanging
            ct.ThrowIfCancellationRequested();
            savedKeys.Add(key);
            loadDataMap[key] = value;
            return UniTask.CompletedTask;
        }

        public UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
        {
            if (ShouldThrowLoadException)
                throw new InvalidOperationException("Test load exception");
            
            // Check cancellation before delay to avoid hanging
            ct.ThrowIfCancellationRequested();
            // Removed delay for testing - not needed and causes UniTask sync issues
            return UniTask.FromResult(loadDataMap.TryGetValue(key, out var value) ? (T)value : defaultValue);
        }

        public void DeleteKey(string key)
        {
            savedKeys.Remove(key);
            loadDataMap.Remove(key);
            keyExistsMap.Remove(key);
        }

        public void DeleteFile()
        {
            savedKeys.Clear();
            loadDataMap.Clear();
            keyExistsMap.Clear();
        }

        public string GetSaveFormatVersion()
        {
            return "1.0.0";
        }

        public string GetSavedFormatVersion()
        {
            return loadDataMap.TryGetValue("_saveFormatVersion", out var version) ? version.ToString() : "1.0.0";
        }

        public bool CreateBackup()
        {
            return true;
        }

        public bool RestoreFromBackup()
        {
            return true;
        }

        public string[] GetAvailableBackups()
        {
            return new string[] { "backup1", "backup2" };
        }
    }
}
