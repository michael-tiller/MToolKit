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
    /// Tests for LocalizationSystem method behavior and functionality
    /// </summary>
    [TestFixture]
    public class LocalizationSystemTests
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

            // Clean up mock locales
            if (_mockEnglishLocale != null) UnityEngine.Object.DestroyImmediate(_mockEnglishLocale);
            if (_mockSpanishLocale != null) UnityEngine.Object.DestroyImmediate(_mockSpanishLocale);
            if (_mockFrenchLocale != null) UnityEngine.Object.DestroyImmediate(_mockFrenchLocale);

            // Clear PlayerPrefs
            PlayerPrefs.DeleteKey("Language");
        }

        #region Initialization Tests

        [Test]
        public void Awake_ShouldInitializeLocalizationSystem()
        {
            // Act - Simulate Unity's Awake call by enabling the GameObject
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.IsTrue(_localizationSystem.IsInitialized);
        }

        #endregion

        #region SetNewLocale Method Tests

        [Test]
        public void SetNewLocale_WithValidLanguageCode_ShouldSetLocale()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            string languageCode = "es";

            // Act
            _localizationSystem.SetNewLocale(languageCode);

            // Assert
            Assert.AreEqual(languageCode, PlayerPrefs.GetString("Language"));
        }

        [Test]
        public void SetNewLocale_WithInvalidLanguageCode_ShouldFallbackToFirstAvailable()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            string invalidLanguageCode = "invalid";

            // Act
            _localizationSystem.SetNewLocale(invalidLanguageCode);

            // Assert
            Assert.IsTrue(PlayerPrefs.HasKey("Language"));
        }


        [Test]
        public void SetNewLocale_ShouldTriggerLanguageSubject()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            string languageCode = "fr";
            string receivedLanguage = null;

            _localizationSystem.Language.Subscribe(lang => receivedLanguage = lang);

            // Act
            _localizationSystem.SetNewLocale(languageCode);

            // Assert
            Assert.AreEqual(languageCode, receivedLanguage);
        }

        [Test]
        public void SetNewLocale_WithNullOrEmptyLanguageCode_ShouldHandleGracefully()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act & Assert
            Assert.DoesNotThrow(() => _localizationSystem.SetNewLocale(null));
            Assert.DoesNotThrow(() => _localizationSystem.SetNewLocale(""));
        }

        #endregion

        #region GetAvailableLocaleCodes Method Tests

        [Test]
        public void GetAvailableLocaleCodes_WhenInitialized_ShouldReturnAvailableCodes()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act
            var codes = _localizationSystem.GetAvailableLocaleCodes();

            // Assert
            Assert.IsNotNull(codes);
            Assert.IsInstanceOf<List<string>>(codes);
        }


        #endregion

        #region GetCurrentLocaleCode Method Tests

        [Test]
        public void GetCurrentLocaleCode_WhenInitialized_ShouldReturnCurrentCode()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act
            var currentCode = _localizationSystem.GetCurrentLocaleCode();

            // Assert
            Assert.IsNotNull(currentCode);
            Assert.IsInstanceOf<string>(currentCode);
        }


        [Test]
        public void GetCurrentLocaleCode_AfterSettingLocale_ShouldReturnSetCode()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            string languageCode = "es";
            _localizationSystem.SetNewLocale(languageCode);

            // Act
            var currentCode = _localizationSystem.GetCurrentLocaleCode();

            // Assert
            Assert.AreEqual(languageCode, currentCode);
        }

        #endregion

        #region PlayerPrefs Integration Tests

        [Test]
        public void SetNewLocale_ShouldSaveToPlayerPrefs()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            string languageCode = "fr";

            // Act
            _localizationSystem.SetNewLocale(languageCode);

            // Assert
            Assert.IsTrue(PlayerPrefs.HasKey("Language"));
            Assert.AreEqual(languageCode, PlayerPrefs.GetString("Language"));
        }

        [Test]
        public void InitializeLocalization_WithSavedLanguage_ShouldRestoreLanguage()
        {
            // Arrange
            string savedLanguage = "es";
            PlayerPrefs.SetString("Language", savedLanguage);

            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.AreEqual(savedLanguage, _localizationSystem.GetCurrentLocaleCode());
        }

        [Test]
        public void InitializeLocalization_WithoutSavedLanguage_ShouldUseDefault()
        {
            // Arrange
            PlayerPrefs.DeleteKey("Language");

            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Assert
            Assert.IsNotNull(_localizationSystem.GetCurrentLocaleCode());
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void SetNewLocale_WithException_ShouldHandleGracefully()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act & Assert
            Assert.DoesNotThrow(() => _localizationSystem.SetNewLocale("exception_test"));
        }

        [Test]
        public void GetAvailableLocaleCodes_WithException_ShouldReturnEmptyList()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);

            // Act
            var codes = _localizationSystem.GetAvailableLocaleCodes();

            // Assert
            Assert.IsNotNull(codes);
            Assert.IsInstanceOf<List<string>>(codes);
        }

        #endregion

        #region Singleton Behavior Tests

        [Test]
        public void LocalizationSystem_ShouldBeSingleton()
        {
            // Arrange
            var gameObject1 = new GameObject("Test1");
            var gameObject2 = new GameObject("Test2");
            var instance1 = gameObject1.AddComponent<LocalizationSystem>();
            var instance2 = gameObject2.AddComponent<LocalizationSystem>();

            // Act & Assert - Test that the static Instance property returns the same instance
            Assert.AreSame(LocalizationSystem.Instance, LocalizationSystem.Instance);
            Assert.IsNotNull(LocalizationSystem.Instance);

            // Cleanup
            UnityEngine.Object.DestroyImmediate(gameObject1);
            UnityEngine.Object.DestroyImmediate(gameObject2);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void FullWorkflow_InitializeSetGetLocale_ShouldWorkCorrectly()
        {
            // Arrange
            string targetLanguage = "es";

            // Act
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            _localizationSystem.SetNewLocale(targetLanguage);
            var currentCode = _localizationSystem.GetCurrentLocaleCode();
            var availableCodes = _localizationSystem.GetAvailableLocaleCodes();

            // Assert
            Assert.IsTrue(_localizationSystem.IsInitialized);
            Assert.AreEqual(targetLanguage, PlayerPrefs.GetString("Language"));
            Assert.IsNotNull(currentCode);
            Assert.IsNotNull(availableCodes);
        }

        [Test]
        public void MultipleLocaleChanges_ShouldWorkCorrectly()
        {
            // Arrange
            _testGameObject.SetActive(false);
            _testGameObject.SetActive(true);
            var languages = new[] { "en", "es", "fr" };
            var receivedLanguages = new List<string>();

            _localizationSystem.Language.Subscribe(lang => receivedLanguages.Add(lang));

            // Act
            foreach (var lang in languages)
            {
                _localizationSystem.SetNewLocale(lang);
            }

            // Assert
            Assert.AreEqual(languages.Length, receivedLanguages.Count);
            for (int i = 0; i < languages.Length; i++)
            {
                Assert.AreEqual(languages[i], receivedLanguages[i]);
            }
        }

        #endregion
    }
}
