using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.ES3Integration;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using Serilog;
using ILogger = Serilog.ILogger;
using FsCheck;

namespace MToolKit.Tests.Runtime.Persistence.ES3Integration
{
    [TestFixture]
    public class ES3DomainControllerPropertyTests
    {
        private TestES3Service testES3Service;
        private List<TestSaveable> testSaveables;
        private ES3DomainController controller;
        private CancellationToken cancellationToken;

        [SetUp]
        public void SetUp()
        {
            testES3Service = new TestES3Service();
            testSaveables = new List<TestSaveable>();
            cancellationToken = CancellationToken.None;
        }

        private ES3DomainController CreateController(ESaveDomain domain, int saveableCount)
        {
            testSaveables.Clear();
            var saveables = new List<ISaveable>();
            
            for (int i = 0; i < saveableCount; i++)
            {
                var testSaveable = new TestSaveable($"saveable_{i}");
                testSaveables.Add(testSaveable);
                saveables.Add(testSaveable);
            }
            
            return new ES3DomainController(domain, saveables, testES3Service);
        }

        [Test]
        public void Constructor_WithAnyValidParameters_ShouldCreateInstance()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                
                var controller = CreateController(domain, saveableCount);
                var result = controller != null && controller.Domain == domain;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void Constructor_WithNullSaveables_ShouldAlwaysThrowArgumentNullException()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                
                try
                {
                    new ES3DomainController(domain, null, testES3Service);
                    Check.QuickThrowOnFailure(false);
                }
                catch (ArgumentNullException)
                {
                    Check.QuickThrowOnFailure(true);
                }
            }
        }

        [Test]
        public void Constructor_WithNullES3Service_ShouldAlwaysThrowArgumentNullException()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var count = Math.Max(1, Math.Abs(saveableCount) % 10 + 1);
                var saveables = new List<ISaveable>();
                for (int j = 0; j < count; j++)
                {
                    var testSaveable = new TestSaveable($"saveable_{j}");
                    saveables.Add(testSaveable);
                }
                
                try
                {
                    new ES3DomainController(domain, saveables, null);
                    Check.QuickThrowOnFailure(false);
                }
                catch (ArgumentNullException)
                {
                    Check.QuickThrowOnFailure(true);
                }
            }
        }

        [Test]
        public void SaveAsync_WithAnyValidData_ShouldCallSaveOnAllSaveables()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                
                // Setup test data for all saveables
                var testData = new { Value = $"test_{i}" };
                foreach (var saveable in testSaveables)
                {
                    saveable.SetTestData(testData);
                }
                
                // Act
                controller.SaveAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify all saveables were called
                var result = testSaveables.All(s => s.SaveCallCount == 1);
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void SaveAsync_WithAnyDomain_ShouldUseCorrectPrefix()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                var testData = new { Value = "test" };
                var expectedPrefix = $"{domain.ToString().ToLower()}_";
                
                // Setup test data
                foreach (var saveable in testSaveables)
                {
                    saveable.SetTestData(testData);
                }
                
                // Act
                controller.SaveAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify all keys start with correct prefix
                var result = testSaveables.All(saveable =>
                {
                    var expectedKey = $"{expectedPrefix}{saveable.Key}";
                    return testES3Service.SavedKeys.Contains(expectedKey);
                });
                
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void SaveAsync_WithCancellationBeforeStart_ShouldNotCallAnySaveables()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(2, 11);
                var controller = CreateController(domain, saveableCount);
                var cts = new CancellationTokenSource();
                cts.Cancel();
                
                var testData = new { Value = "test" };
                
                // Setup test data
                foreach (var saveable in testSaveables)
                {
                    saveable.SetTestData(testData);
                }
                
                // Act
                controller.SaveAsync(cts.Token).GetAwaiter().GetResult();
                
                // Verify no saveables were called when cancellation is requested before starting
                var noSaveablesCalled = testSaveables.All(s => s.SaveCallCount == 0);
                
                var result = noSaveablesCalled;
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void LoadAsync_WithExistingKeys_ShouldLoadAllData()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                var expectedPrefix = $"{domain.ToString().ToLower()}_";
                
                // Setup mocks - all keys exist
                var testData = new { Value = $"test_{i}" };
                foreach (var saveable in testSaveables)
                {
                    var key = $"{expectedPrefix}{saveable.Key}";
                    testES3Service.SetKeyExists(key, true);
                    testES3Service.SetLoadData(key, testData);
                }
                
                // Act
                controller.LoadAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify all saveables were loaded
                var result = testSaveables.All(s => s.LoadCallCount == 1);
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void LoadAsync_WithNonExistingKeys_ShouldSkipLoading()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                var expectedPrefix = $"{domain.ToString().ToLower()}_";
                
                // Setup mocks - no keys exist
                foreach (var saveable in testSaveables)
                {
                    var key = $"{expectedPrefix}{saveable.Key}";
                    testES3Service.SetKeyExists(key, false);
                }
                
                // Act
                controller.LoadAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify no saveables were loaded
                var result = testSaveables.All(s => s.LoadCallCount == 0);
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void LoadAsync_WithNullData_ShouldNotCallLoadAsyncOnSaveable()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                var expectedPrefix = $"{domain.ToString().ToLower()}_";
                
                // Setup mocks - keys exist but return null data
                foreach (var saveable in testSaveables)
                {
                    var key = $"{expectedPrefix}{saveable.Key}";
                    testES3Service.SetKeyExists(key, true);
                    testES3Service.SetLoadData(key, null);
                }
                
                // Act
                controller.LoadAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify no saveables were loaded
                var result = testSaveables.All(s => s.LoadCallCount == 0);
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void Domain_WithAnyDomain_ShouldReturnCorrectDomain()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                
                var result = controller.Domain == domain;
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void SaveAsync_WithMixedDataTypes_ShouldHandleAllTypes()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(1, 11);
                var controller = CreateController(domain, saveableCount);
                var dataTypes = new object[]
                {
                    "string_data",
                    42,
                    true,
                    new[] { 1, 2, 3 },
                    new Dictionary<string, object> { { "key", "value" } },
                    new { Property = "value" },
                    null
                };
                
                // Setup mocks with different data types
                for (int j = 0; j < testSaveables.Count; j++)
                {
                    var dataType = dataTypes[j % dataTypes.Length];
                    testSaveables[j].SetTestData(dataType);
                }
                
                // Act
                controller.SaveAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify all saveables were called
                var result = testSaveables.All(s => s.SaveCallCount == 1);
                Check.QuickThrowOnFailure(result);
            }
        }

        [Test]
        public void LoadAsync_WithMixedKeyExistence_ShouldHandleCorrectly()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var domain = (ESaveDomain)random.Next(0, Enum.GetValues(typeof(ESaveDomain)).Length);
                var saveableCount = random.Next(2, 11);
                var controller = CreateController(domain, saveableCount);
                var expectedPrefix = $"{domain.ToString().ToLower()}_";
                var testData = new { Value = "test" };
                
                // Setup mocks with alternating key existence
                for (int j = 0; j < testSaveables.Count; j++)
                {
                    var key = $"{expectedPrefix}{testSaveables[j].Key}";
                    var keyExists = j % 2 == 0; // Alternate between existing and non-existing
                    
                    testES3Service.SetKeyExists(key, keyExists);
                    
                    if (keyExists)
                    {
                        testES3Service.SetLoadData(key, testData);
                    }
                }
                
                // Act
                controller.LoadAsync(cancellationToken).GetAwaiter().GetResult();
                
                // Verify only existing keys were loaded
                var correctLoading = true;
                for (int j = 0; j < testSaveables.Count; j++)
                {
                    var shouldBeLoaded = j % 2 == 0;
                    var expectedCount = shouldBeLoaded ? 1 : 0;
                    if (testSaveables[j].LoadCallCount != expectedCount)
                    {
                        correctLoading = false;
                        break;
                    }
                }
                
                Check.QuickThrowOnFailure(correctLoading);
            }
        }
    }
}

    // Test helper classes (reused from unit tests)
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
                
            IsSaving.Value = true;
            await UniTask.CompletedTask;
            IsSaving.Value = false;
            LastSaveTime.Value = DateTime.Now.ToString();
            SaveCounter.Value++;
        }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            if (ShouldThrowLoadException)
                throw new InvalidOperationException("Test load exception");
                
            IsLoading.Value = true;
            await UniTask.CompletedTask;
            IsLoading.Value = false;
            LastLoadTime.Value = DateTime.Now.ToString();
        }

        public async UniTask SaveAsync(string key, object value, CancellationToken ct = default)
        {
            if (ShouldThrowException)
                throw new InvalidOperationException("Test exception");
                
            savedKeys.Add(key);
            await UniTask.CompletedTask;
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
        {
            if (ShouldThrowLoadException)
                throw new InvalidOperationException("Test load exception");
                
            var data = loadDataMap.GetValueOrDefault(key, defaultValue);
            await UniTask.CompletedTask;
            return (T)data;
        }

        public void DeleteKey(string key)
        {
            keyExistsMap.Remove(key);
            loadDataMap.Remove(key);
            savedKeys.Remove(key);
        }

        public void DeleteFile()
        {
            keyExistsMap.Clear();
            loadDataMap.Clear();
            savedKeys.Clear();
        }

        public string GetSaveFormatVersion()
        {
            return "1.0.0";
        }

        public string GetSavedFormatVersion()
        {
            return "1.0.0";
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
            return new string[] { "backup1.es3", "backup2.es3" };
        }
    }

    public class TestSaveable : ISaveable
    {
        private object testData;
        private bool shouldThrowException;
        private bool shouldThrowLoadException;
        
        public string Key { get; }
        public int SaveCallCount { get; private set; }
        public int LoadCallCount { get; private set; }

        public TestSaveable(string key)
        {
            Key = key;
        }

        public void SetTestData(object data)
        {
            testData = data;
        }

        public void SetShouldThrowException(bool shouldThrow)
        {
            shouldThrowException = shouldThrow;
        }

        public void SetShouldThrowLoadException(bool shouldThrow)
        {
            shouldThrowLoadException = shouldThrow;
        }

        public async UniTask<object> SaveAsync()
        {
            SaveCallCount++;
            if (shouldThrowException)
                throw new InvalidOperationException("Test save exception");
                
            await UniTask.CompletedTask;
            return testData;
        }

        public async UniTask LoadAsync(object data)
        {
            LoadCallCount++;
            if (shouldThrowLoadException)
                throw new InvalidOperationException("Test load exception");
                
            await UniTask.CompletedTask;
        }
    }
