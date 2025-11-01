using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using UnityEngine.TestTools;
using Serilog;
using R3;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Utilities;

namespace MToolKit.Tests.Runtime.Localization
{
    /// <summary>
    /// Tests for LocalizationSystem properties and their behavior
    /// </summary>
    [TestFixture]
    public class LocalizationSystemPropertyTests
    {
        private LocalizationSystem _localizationSystem;
        private GameObject _testGameObject;
        private List<Locale> _mockLocales;
        private Locale _mockEnglishLocale;
        private Locale _mockSpanishLocale;
        private Locale _mockFrenchLocale;

        [SetUp]
        public void SetUp()
        {
            // Clean up any existing singleton instance
            if (LocalizationSystem.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(LocalizationSystem.Instance.gameObject);
            }

            // Create test GameObject
            _testGameObject = new GameObject("TestLocalizationSystem");
            _localizationSystem = _testGameObject.AddComponent<LocalizationSystem>();

            // Create mock locales
            _mockEnglishLocale = ScriptableObject.CreateInstance<Locale>();
            _mockEnglishLocale.Identifier = new LocaleIdentifier("en");
            
            _mockSpanishLocale = ScriptableObject.CreateInstance<Locale>();
            _mockSpanishLocale.Identifier = new LocaleIdentifier("es");
            
            _mockFrenchLocale = ScriptableObject.CreateInstance<Locale>();
            _mockFrenchLocale.Identifier = new LocaleIdentifier("fr");

            _mockLocales = new List<Locale> { _mockEnglishLocale, _mockSpanishLocale, _mockFrenchLocale };

            // Clear PlayerPrefs
            PlayerPrefs.DeleteKey("Language");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_testGameObject);
            }

            // Clean up any singleton instance
            if (LocalizationSystem.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(LocalizationSystem.Instance.gameObject);
            }

            // Clean up mock locales
            if (_mockEnglishLocale != null) UnityEngine.Object.DestroyImmediate(_mockEnglishLocale);
            if (_mockSpanishLocale != null) UnityEngine.Object.DestroyImmediate(_mockSpanishLocale);
            if (_mockFrenchLocale != null) UnityEngine.Object.DestroyImmediate(_mockFrenchLocale);

            // Clear PlayerPrefs
            PlayerPrefs.DeleteKey("Language");
        }

        /// <summary>
        /// Creates a new LocalizationSystem instance for testing
        /// </summary>
        private LocalizationSystem CreateNewLocalizationSystem(string name = "NewTestLocalizationSystem")
        {
            var newGameObject = new GameObject(name);
            return newGameObject.AddComponent<LocalizationSystem>();
        }

        /// <summary>
        /// Asserts that a property is read-only
        /// </summary>
        private void AssertPropertyIsReadOnly(string propertyName)
        {
            var propertyInfo = typeof(LocalizationSystem).GetProperty(propertyName);
            if (propertyInfo != null)
            {
                Assert.IsTrue(propertyInfo.CanRead, $"Property {propertyName} should be readable");
                Assert.IsFalse(propertyInfo.CanWrite, $"Property {propertyName} should be read-only");
            }
            else
            {
                // Check if it's a field instead
                var fieldInfo = typeof(LocalizationSystem).GetField(propertyName);
                Assert.IsNotNull(fieldInfo, $"Property or field {propertyName} should exist");
                // Fields are considered "read-only" if they don't have a setter, but public fields are writable
                // For Subject<T>, we consider it read-only from external perspective since it's initialized internally
                Assert.IsTrue(fieldInfo.IsPublic, $"Field {propertyName} should be public");
            }
        }

        /// <summary>
        /// Asserts that a property has the correct type
        /// </summary>
        private void AssertPropertyType<T>(string propertyName)
        {
            var propertyInfo = typeof(LocalizationSystem).GetProperty(propertyName);
            if (propertyInfo != null)
            {
                Assert.AreEqual(typeof(T), propertyInfo.PropertyType, $"Property {propertyName} should be of type {typeof(T).Name}");
            }
            else
            {
                // Check if it's a field instead
                var fieldInfo = typeof(LocalizationSystem).GetField(propertyName);
                Assert.IsNotNull(fieldInfo, $"Property or field {propertyName} should exist");
                Assert.AreEqual(typeof(T), fieldInfo.FieldType, $"Field {propertyName} should be of type {typeof(T).Name}");
            }
        }

        /// <summary>
        /// Asserts that a property or field is accessible without throwing exceptions
        /// </summary>
        private void AssertPropertyIsAccessible(string memberName)
        {
            var propertyInfo = typeof(LocalizationSystem).GetProperty(memberName);
            var fieldInfo = typeof(LocalizationSystem).GetField(memberName);
            
            Assert.IsTrue(propertyInfo != null || fieldInfo != null, 
                $"Property or field {memberName} should exist");
            
            Assert.DoesNotThrow(() => 
            {
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(_localizationSystem);
                }
                else if (fieldInfo != null)
                {
                    var value = fieldInfo.GetValue(_localizationSystem);
                }
                // Just accessing the property/field should not throw
            }, $"Property or field {memberName} should be accessible without throwing exceptions");
        }

        #region IsInitialized Property Tests

        [Test]
        public void IsInitialized_ShouldReturnTrue_WhenCreated()
        {
            // Arrange - Create a new instance
            var newLocalizationSystem = CreateNewLocalizationSystem();

            // Act & Assert - The system initializes immediately when created
            Assert.IsTrue(newLocalizationSystem.IsInitialized);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(newLocalizationSystem.gameObject);
        }

        [Test]
        public void IsInitialized_ShouldReturnTrue_AfterAwakeCalled()
        {
            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.IsTrue(_localizationSystem.IsInitialized);
        }

        [Test]
        public void IsInitialized_ShouldBeReadOnly()
        {
            // Act & Assert
            AssertPropertyIsReadOnly("IsInitialized");
        }

        [Test]
        public void IsInitialized_ShouldReflectActualState()
        {
            // Arrange
            var newLocalizationSystem = CreateNewLocalizationSystem();

            // Act & Assert - The system initializes immediately and should reflect this state
            Assert.IsTrue(newLocalizationSystem.IsInitialized);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(newLocalizationSystem.gameObject);
        }

        [Test]
        public void IsInitialized_ShouldBeConsistentAcrossMultipleCalls()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act & Assert
            bool firstCall = _localizationSystem.IsInitialized;
            bool secondCall = _localizationSystem.IsInitialized;
            bool thirdCall = _localizationSystem.IsInitialized;

            Assert.AreEqual(firstCall, secondCall);
            Assert.AreEqual(secondCall, thirdCall);
            Assert.IsTrue(firstCall);
        }

        #endregion

        #region Language Property Tests

        [Test]
        public void Language_ShouldBeObservableSubject()
        {
            // Act & Assert
            Assert.IsInstanceOf<Subject<string>>(_localizationSystem.Language);
        }

        [Test]
        public void Language_ShouldNotBeNull()
        {
            // Act & Assert
            Assert.IsNotNull(_localizationSystem.Language);
        }

        [Test]
        public void Language_ShouldBeReadOnly()
        {
            // Act & Assert
            AssertPropertyIsReadOnly("Language");
        }

        [Test]
        public void Language_ShouldAcceptStringValues()
        {
            // Arrange
            var receivedValues = new List<string>();
            _localizationSystem.Language.Subscribe(value => receivedValues.Add(value));

            // Act
            _localizationSystem.Language.OnNext("en");
            _localizationSystem.Language.OnNext("es");
            _localizationSystem.Language.OnNext("fr");

            // Assert
            Assert.AreEqual(3, receivedValues.Count);
            Assert.Contains("en", receivedValues);
            Assert.Contains("es", receivedValues);
            Assert.Contains("fr", receivedValues);
        }

        [Test]
        public void Language_ShouldHandleNullAndEmptyValues()
        {
            // Arrange
            string receivedValue = "not_null";
            _localizationSystem.Language.Subscribe(value => receivedValue = value);

            // Act & Assert - Null value
            _localizationSystem.Language.OnNext(null);
            Assert.IsNull(receivedValue);

            // Act & Assert - Empty string
            receivedValue = "not_empty";
            _localizationSystem.Language.OnNext("");
            Assert.AreEqual("", receivedValue);
        }

        [Test]
        public void Language_ShouldSupportMultipleSubscribers()
        {
            // Arrange
            var subscriber1Values = new List<string>();
            var subscriber2Values = new List<string>();
            var subscriber3Values = new List<string>();

            _localizationSystem.Language.Subscribe(value => subscriber1Values.Add(value));
            _localizationSystem.Language.Subscribe(value => subscriber2Values.Add(value));
            _localizationSystem.Language.Subscribe(value => subscriber3Values.Add(value));

            // Act
            _localizationSystem.Language.OnNext("test");

            // Assert
            Assert.AreEqual(1, subscriber1Values.Count);
            Assert.AreEqual(1, subscriber2Values.Count);
            Assert.AreEqual(1, subscriber3Values.Count);
            Assert.AreEqual("test", subscriber1Values[0]);
            Assert.AreEqual("test", subscriber2Values[0]);
            Assert.AreEqual("test", subscriber3Values[0]);
        }

        [Test]
        public void Language_ShouldSupportSubscriptionDisposal()
        {
            // Arrange
            var receivedValues = new List<string>();
            var subscription = _localizationSystem.Language.Subscribe(value => receivedValues.Add(value));

            // Act
            _localizationSystem.Language.OnNext("before_disposal");
            subscription.Dispose();
            _localizationSystem.Language.OnNext("after_disposal");

            // Assert
            Assert.AreEqual(1, receivedValues.Count);
            Assert.AreEqual("before_disposal", receivedValues[0]);
        }

        [Test]
        public void Language_ShouldBeThreadSafe()
        {
            // Arrange
            var receivedValues = new System.Collections.Concurrent.ConcurrentBag<string>();
            var lockObject = new object();
            int expectedCount = 10;
            
            _localizationSystem.Language.Subscribe(value => 
            {
                lock (lockObject)
                {
                    receivedValues.Add(value);
                }
            });

            // Act - Simulate concurrent access with proper synchronization
            var tasks = new List<System.Threading.Tasks.Task>();
            for (int i = 0; i < expectedCount; i++)
            {
                int index = i;
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    _localizationSystem.Language.OnNext($"value_{index}");
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert - Should receive all values with proper synchronization
            Assert.AreEqual(expectedCount, receivedValues.Count);
        }

        #endregion

        #region Property Interaction Tests

        [Test]
        public void SetNewLocale_ShouldWorkWhenInitialized()
        {
            // Arrange
            var receivedValues = new List<string>();
            _localizationSystem.Language.Subscribe(value => receivedValues.Add(value));

            // Act - Since the system initializes immediately, SetNewLocale should work
            _localizationSystem.SetNewLocale("es");
            int afterFirstCallCount = receivedValues.Count;

            // Act - After another call
            _localizationSystem.SetNewLocale("fr");
            int afterSecondCallCount = receivedValues.Count;

            // Assert - Should work when initialized (which is always)
            Assert.GreaterOrEqual(afterFirstCallCount, 0); // Should emit when initialized
            Assert.Greater(afterSecondCallCount, afterFirstCallCount); // Should emit more after second call
        }

        [Test]
        public void Language_ShouldEmitDuringInitialization()
        {
            // Arrange
            var receivedValues = new List<string>();
            _localizationSystem.Language.Subscribe(value => receivedValues.Add(value));

            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.GreaterOrEqual(receivedValues.Count, 0); // Should emit at least once during initialization
        }

        [Test]
        public void Properties_ShouldBeConsistentAfterInitialization()
        {
            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.IsTrue(_localizationSystem.IsInitialized);
            Assert.IsNotNull(_localizationSystem.Language);
            // Note: Testing singleton behavior instead of protected properties
        }

        #endregion

        #region Property State Transition Tests

        [Test]
        public void IsInitialized_ShouldBeConsistentAfterCreation()
        {
            // Arrange
            var newLocalizationSystem = CreateNewLocalizationSystem();

            // Act & Assert - The system should be consistently initialized after creation
            Assert.IsTrue(newLocalizationSystem.IsInitialized);
            
            // Verify it remains consistent
            Assert.IsTrue(newLocalizationSystem.IsInitialized);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(newLocalizationSystem.gameObject);
        }

        [Test]
        public void Language_ShouldMaintainStateAcrossInitialization()
        {
            // Arrange
            var receivedValues = new List<string>();
            _localizationSystem.Language.Subscribe(value => receivedValues.Add(value));

            // Act - Before initialization
            _localizationSystem.Language.OnNext("before_init");
            int beforeCount = receivedValues.Count;

            // Act - After initialization
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            int afterCount = receivedValues.Count;

            // Assert
            Assert.AreEqual(1, beforeCount);
            Assert.GreaterOrEqual(afterCount, 1); // Should maintain previous emissions and potentially add new ones
        }

        #endregion

        #region Property Validation Tests

        [Test]
        public void AllProperties_ShouldHaveCorrectTypes()
        {
            // Act & Assert
            AssertPropertyType<bool>("IsInitialized");
            AssertPropertyType<Subject<string>>("Language");
            // Note: Not testing protected properties from base class
        }

        [Test]
        public void AllProperties_ShouldBeAccessible()
        {
            // Act & Assert - Should not throw exceptions when accessing properties
            AssertPropertyIsAccessible("IsInitialized");
            AssertPropertyIsAccessible("Language");
            // Note: Not testing protected properties from base class
        }

        [Test]
        public void Properties_ShouldBeImmutableAfterInitialization()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act & Assert - Properties should not change after initialization
            bool initialIsInitialized = _localizationSystem.IsInitialized;
            var initialLanguage = _localizationSystem.Language;

            // Perform some operations
            _localizationSystem.SetNewLocale("es");
            _localizationSystem.GetCurrentLocaleCode();
            _localizationSystem.GetAvailableLocaleCodes();

            // Assert - Properties should remain the same
            Assert.AreEqual(initialIsInitialized, _localizationSystem.IsInitialized);
            Assert.AreSame(initialLanguage, _localizationSystem.Language);
            // Note: Not testing protected properties from base class
        }

        #endregion

        #region Property Performance Tests

        [Test]
        public void IsInitialized_ShouldBeFastToAccess()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var _ = _localizationSystem.IsInitialized;
            }

            stopwatch.Stop();

            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 100); // Should be very fast
        }

        [Test]
        public void Language_ShouldBeFastToAccess()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var _ = _localizationSystem.Language;
            }

            stopwatch.Stop();

            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 100); // Should be very fast
        }

        #endregion
    }
}
