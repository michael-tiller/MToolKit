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
    /// Tests for LocalizationSystem singleton behavior in isolation
    /// </summary>
    [TestFixture]
    public class LocalizationSystemSingletonTests
    {
        private List<Locale> _mockLocales;
        private Locale _mockEnglishLocale;
        private Locale _mockSpanishLocale;
        private Locale _mockFrenchLocale;

        [SetUp]
        public void SetUp()
        {
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
            // Clean up mock locales
            if (_mockEnglishLocale != null) UnityEngine.Object.DestroyImmediate(_mockEnglishLocale);
            if (_mockSpanishLocale != null) UnityEngine.Object.DestroyImmediate(_mockSpanishLocale);
            if (_mockFrenchLocale != null) UnityEngine.Object.DestroyImmediate(_mockFrenchLocale);

            // Clear PlayerPrefs
            PlayerPrefs.DeleteKey("Language");
        }

        [Test]
        public void SingletonBehavior_ShouldWorkCorrectly()
        {
            // Test singleton behavior - the RuntimeInitializeOnLoadMethod might have already created an instance
            var existingInstance = LocalizationSystem.Instance;
            
            // Create additional instances to test singleton behavior
            var gameObject1 = new GameObject("Test1");
            var gameObject2 = new GameObject("Test2");
            var instance1 = gameObject1.AddComponent<LocalizationSystem>();
            var instance2 = gameObject2.AddComponent<LocalizationSystem>();

            // Act & Assert - Test that the static Instance property returns the same instance
            Assert.AreSame(LocalizationSystem.Instance, LocalizationSystem.Instance);
            Assert.IsNotNull(LocalizationSystem.Instance);
            
            // Test that the existing instance (or first created) is the one that survives
            Assert.AreSame(existingInstance, LocalizationSystem.Instance);
            
            // Verify that the new instances are not the singleton (they should be destroyed)
            Assert.AreNotSame(instance1, LocalizationSystem.Instance);
            Assert.AreNotSame(instance2, LocalizationSystem.Instance);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject1);
            UnityEngine.Object.DestroyImmediate(gameObject2);
        }

        [Test]
        public void SingletonBehavior_ShouldDestroyDuplicates()
        {
            // Test that duplicate instances are properly destroyed
            var existingInstance = LocalizationSystem.Instance;
            
            var gameObject1 = new GameObject("FirstInstance");
            var gameObject2 = new GameObject("SecondInstance");
            var gameObject3 = new GameObject("ThirdInstance");
            
            var instance1 = gameObject1.AddComponent<LocalizationSystem>();
            var instance2 = gameObject2.AddComponent<LocalizationSystem>();
            var instance3 = gameObject3.AddComponent<LocalizationSystem>();

            // Act & Assert - The existing instance should survive, new ones should be destroyed
            Assert.AreSame(existingInstance, LocalizationSystem.Instance);
            Assert.AreNotSame(instance1, LocalizationSystem.Instance);
            Assert.AreNotSame(instance2, LocalizationSystem.Instance);
            Assert.AreNotSame(instance3, LocalizationSystem.Instance);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject1);
            UnityEngine.Object.DestroyImmediate(gameObject2);
            UnityEngine.Object.DestroyImmediate(gameObject3);
        }

        [Test]
        public void SingletonBehavior_ShouldBeConsistent()
        {
            // Test that singleton instance remains consistent
            // Note: RuntimeInitializeOnLoadMethod might have already created an instance
            var existingInstance = LocalizationSystem.Instance;
            
            var gameObject = new GameObject("SingletonTest");
            var instance = gameObject.AddComponent<LocalizationSystem>();

            // Act & Assert - Multiple calls should return the same instance
            var instance1 = LocalizationSystem.Instance;
            var instance2 = LocalizationSystem.Instance;
            var instance3 = LocalizationSystem.Instance;

            Assert.AreSame(instance1, instance2);
            Assert.AreSame(instance2, instance3);
            Assert.AreSame(existingInstance, instance1); // Should be the existing instance

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SingletonBehavior_ShouldBeInitialized()
        {
            // Test that the singleton instance is properly initialized
            // Note: RuntimeInitializeOnLoadMethod might have already created an instance
            var existingInstance = LocalizationSystem.Instance;
            
            var gameObject = new GameObject("SingletonTest");
            var instance = gameObject.AddComponent<LocalizationSystem>();

            // Act & Assert - Singleton should be initialized
            Assert.IsTrue(LocalizationSystem.Instance.IsInitialized);
            Assert.IsNotNull(LocalizationSystem.Instance.Language);
            Assert.AreSame(existingInstance, LocalizationSystem.Instance);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SingletonBehavior_ShouldHandleMultipleAccess()
        {
            // Test that multiple access to singleton doesn't cause issues
            // Note: RuntimeInitializeOnLoadMethod might have already created an instance
            var existingInstance = LocalizationSystem.Instance;
            
            var gameObject = new GameObject("SingletonTest");
            var instance = gameObject.AddComponent<LocalizationSystem>();

            // Act - Access singleton multiple times
            for (int i = 0; i < 100; i++)
            {
                var currentInstance = LocalizationSystem.Instance;
                Assert.AreSame(existingInstance, currentInstance);
                Assert.IsTrue(currentInstance.IsInitialized);
            }

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
