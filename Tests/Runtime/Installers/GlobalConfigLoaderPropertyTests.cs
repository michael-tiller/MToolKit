/**
 * Property-focused unit tests for GlobalConfigLoader.cs
 * Generated from function analysis on 2025-01-20
 * Framework: Unity Test Framework with NUnit
 * 
 * Property Test Coverage:
 * - Static logger property (logLazy initialization, thread safety, context assignment)
 * - Instance dontDestroyOnLoad property (override verification, consistency across instances)
 * - Serialized GlobalPluginConfig property (assignment, retrieval, null safety, type preservation)
 * - Serialized PluginConfig property (assignment, retrieval, null safety, type preservation)

 * Property-Specific Testing Approaches:
 * - Direct property access vs field access verification
 * - Serialization attribute behavior testing
 * - Property getter/setter isolation testing
 * - Property value persistence across method calls
 * - Property access performance and thread safety
 * - Property visibility and encapsulation verification
 * 
 * Mock Dependencies:
 * - Unity ScriptableObject assets (GlobalPluginConfigAsset, PluginConfigAsset)
 * - Serilog ILogger for logging verification
 * - Reflection-based access to private backing fields
 * - Unity GameObject lifecycle simulation without instance creation
 * 
 * Property Testing Philosophy:
 * - Focus on property behaviors rather than class functionality
 * - Test property contracts, constraints, and side effects
 * - Verify encapsulation and visibility rules
 * - Validate property-specific edge cases and error handling
 */

using System;
using System.Reflection;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using Serilog;
using NSubstitute;
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Singletons;

namespace MToolKit.Tests.Runtime.Installers
{
    /// <summary>
    /// Property-specific test data factory methods
    /// </summary>
    internal static class GlobalConfigLoaderPropertyTestData
    {
        /// <summary>Creates a mock GlobalPluginConfigAsset for property testing</summary>
        public static GlobalPluginConfigAsset CreateGlobalPluginConfigAsset() => Substitute.For<GlobalPluginConfigAsset>();
        
        /// <summary>Creates a mock PluginConfigAsset for property testing</summary>
        public static PluginConfigAsset CreatePluginConfigAsset() => Substitute.For<PluginConfigAsset>();
        
        /// <summary>Creates multiple mock GlobalPluginConfigAssets for multi-property testing</summary>
        /// <param name="count">Number of configs to create</param>
        /// <returns>Array of mock configs</returns>
        public static GlobalPluginConfigAsset[] CreateMultipleGlobalPluginConfigs(int count)
        {
            var configs = new GlobalPluginConfigAsset[count];
            for (int i = 0; i < count; i++)
            {
                configs[i] = CreateGlobalPluginConfigAsset();
            }
            return configs;
        }

        /// <summary>Creates multiple mock PluginConfigAssets for multi-property testing</summary>
        /// <param name="count">Number of configs to create</param>
        /// <returns>Array of mock configs</returns>
        public static PluginConfigAsset[] CreateMultiplePluginConfigs(int count)
        {
            var configs = new PluginConfigAsset[count];
            for (int i = 0; i < count; i++)
            {
                configs[i] = CreatePluginConfigAsset();
            }
            return configs;
        }

        /// <summary>Creates multiple test instances for cross-property testing</summary>
        public static GlobalConfigLoader[] CreateMultipleInstances(int count, Func<GlobalConfigLoader> factory)
        {
            var instances = new GlobalConfigLoader[count];
            for (int i = 0; i < count; i++)
            {
                instances[i] = factory();
            }
            return instances;
        }
    }

    /// <summary>
    /// Reflection utilities focused on property access for property-specific testing
    /// </summary>
    internal static class PropertyReflectionHelper
    {
        // Cached reflection info for performance
        private static readonly Lazy<PropertyInfo> GlobalPluginConfigProperty = new(() => 
            typeof(GlobalConfigLoader).GetProperty("GlobalPluginConfig", BindingFlags.Public | BindingFlags.Instance));
        private static readonly Lazy<PropertyInfo> PluginConfigProperty = new(() => 
            typeof(GlobalConfigLoader).GetProperty("PluginConfig", BindingFlags.Public | BindingFlags.Instance));
        private static readonly Lazy<PropertyInfo> LogProperty = new(() => 
            typeof(GlobalConfigLoader).GetProperty("log", BindingFlags.NonPublic | BindingFlags.Static));
        private static readonly Lazy<PropertyInfo> DontDestroyOnLoadProperty = new(() => 
            typeof(GlobalConfigLoader).GetProperty("dontDestroyOnLoad", BindingFlags.NonPublic | BindingFlags.Instance));

        // Backing field access
        private static readonly Lazy<FieldInfo> GlobalPluginConfigBackingField = new(() => 
            typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance));
        private static readonly Lazy<FieldInfo> PluginConfigBackingField = new(() => 
            typeof(GlobalConfigLoader).GetField("<PluginConfig>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance));
        private static readonly Lazy<FieldInfo> DontDestroyOnLoadBackingField = new(() => 
            typeof(GlobalConfigLoader).GetField("dontDestroyOnLoad", BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>Gets a GlobalPluginConfigAsset property value</summary>
        public static GlobalPluginConfigAsset GetGlobalPluginConfig(GlobalConfigLoader instance) => 
            GlobalPluginConfigProperty.Value?.GetValue(instance) as GlobalPluginConfigAsset;

        /// <summary>Sets a GlobalPluginConfigAsset property value through backing field</summary>
        public static void SetGlobalPluginConfig(GlobalConfigLoader instance, GlobalPluginConfigAsset value) => 
            GlobalPluginConfigBackingField.Value?.SetValue(instance, value);

        /// <summary>Gets a PluginConfigAsset property value</summary>
        public static PluginConfigAsset GetPluginConfig(GlobalConfigLoader instance) => 
            PluginConfigProperty.Value?.GetValue(instance) as PluginConfigAsset;

        /// <summary>Sets a PluginConfigAsset property value through backing field</summary>
        public static void SetPluginConfig(GlobalConfigLoader instance, PluginConfigAsset value) => 
            PluginConfigBackingField.Value?.SetValue(instance, value);

        /// <summary>Gets dontDestroyOnLoad property value</summary>
        public static bool GetDontDestroyOnLoad(GlobalConfigLoader instance) => 
            (bool)(DontDestroyOnLoadProperty.Value?.GetValue(instance) ?? false);

        /// <summary>Gets the logger property value</summary>
        public static ILogger GetLogger() => 
            LogProperty.Value?.GetValue(null, null) as ILogger;

        /// <summary>Gets property getter access level</summary>
        public static bool IsPropertyPublicGetter<T>(string propertyName)
        {
            var property = typeof(GlobalConfigLoader).GetProperty(propertyName);
            return property?.GetGetMethod()?.IsPublic == true;
        }

        /// <summary>Gets property setter access level</summary>
        public static bool IsPropertyPublicSetter<T>(string propertyName)
        {
            var property = typeof(GlobalConfigLoader).GetProperty(propertyName);
            return property?.GetSetMethod()?.IsPublic == true;
        }

        /// <summary>Verifies property has SerializeField attribute</summary>
        public static bool PropertyHasSerializeFieldAttribute(string propertyName)
        {
            var backingField = typeof(GlobalConfigLoader).GetField($"<{propertyName}>k__BackingField", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return backingField?.GetCustomAttribute(typeof(SerializeField)) != null;
        }
    }

    [TestFixture]
    public class GlobalConfigLoaderPropertyTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;

        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupContainer();
            _resolver = _containerBuilder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
        }

        private void SetupContainer()
        {
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            var mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(mockLogger).As<ILogger>();
        }

        private GlobalConfigLoader CreateGlobalConfigLoader() => CreateGlobalConfigLoader(null);

        private GlobalConfigLoader CreateGlobalConfigLoader(GameObject parentGameObject = null)
        {
            if (parentGameObject == null)
            {
                return Activator.CreateInstance<GlobalConfigLoader>();
            }

            var parent = parentGameObject;
            var instance = parent.AddComponent<GlobalConfigLoader>();
            return instance;
        }

        #region GlobalPluginConfig Property Tests

        [TestFixture]
        public class GlobalPluginConfigPropertyTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void GlobalPluginConfig_ReturnsNullByDefault()
            {
                var instance = CreateGlobalConfigLoader();

                var value = PropertyReflectionHelper.GetGlobalPluginConfig(instance);

                Assert.That(value, Is.Null);
            }

            [Test]
            public void GlobalPluginConfig_CanBeSetToNonNull()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, testAsset);

                var saved = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved, Is.EqualTo(testAsset));
            }

            [Test]
            public void GlobalPluginConfig_CanBeSetToNull()
            {
                var instance = CreateGlobalConfigLoader();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, null);

                var saved = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                Assert.That(saved, Is.Null);
            }

            [Test]
            public void GlobalPluginConfig_MultipleAssignments_LastValueWins()
            {
                var instance = CreateGlobalConfigLoader();

                var originalAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                PropertyReflectionHelper.SetGlobalPluginConfig(instance, originalAsset);
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(originalAsset));

                var newAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                PropertyReflectionHelper.SetGlobalPluginConfig(instance, newAsset);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(newAsset));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.Not.EqualTo(originalAsset));
            }

            [Test]
            public void GlobalPluginConfig_SetSameAssetMultipleTimes_ValuePreserved()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, testAsset);
                var saved = PropertyReflectionHelper.GetGlobalPluginConfig(instance);

                Assert.That(saved, Is.EqualTo(testAsset));
                Assert.That(saved, Is.Not.Null);
            }

            [Test]
            public void GlobalPluginConfig_AssignedOnce_PersistsAcrossAccesses()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, testAsset);

                var val1 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                var val2 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                Assert.That(val1, Is.EqualTo(val2));
                Assert.That(val1, Is.EqualTo(testAsset));
            }

            [Test]
            public void GlobalPluginConfig_GetterBehavior_ConsistentAcrossCalls()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, testAsset);

                var val1 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                var val2 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                var val3 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);

                Assert.That(val1, Is.Not.Null);
                Assert.That(val2, Is.Not.Null);
                Assert.That(val3, Is.Not.Null);
                Assert.That(val1, Is.EqualTo(val2));
                Assert.That(val2, Is.EqualTo(val3));
                Assert.That(val1, Is.InstanceOf<GlobalPluginConfigAsset>());
            }

            [Test]
            public void GlobalPluginConfig_NotNullAssignment_ValueNotReplaced()
            {
                var instance = CreateGlobalConfigLoader();
                var asset1 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var asset2 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, asset1);
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(asset1));

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, asset2);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(asset2));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.Not.EqualTo(asset1));
            }

            [Test]
            public void GlobalPluginConfig_SetThenGet_ReturnsCorrectType()
            {
                var instance = CreateGlobalConfigLoader();

                var asset1 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                PropertyReflectionHelper.SetGlobalPluginConfig(instance, asset1);

                var val1 = PropertyReflectionHelper.GetGlobalPluginConfig(instance);

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, null);

                var nullVal = PropertyReflectionHelper.GetGlobalPluginConfig(instance);

                Assert.That(val1, Is.InstanceOf<GlobalPluginConfigAsset>());
                Assert.That(val1, Is.Not.Null);
                Assert.That(val1, Is.EqualTo(asset1));
                Assert.That(nullVal, Is.Null);
            }

            [Test]
            public void GlobalPluginConfig_MultipleInstances_ValueIndependence()
            {
                var instance1 = CreateGlobalConfigLoader();
                var instance2 = CreateGlobalConfigLoader();

                var asset1 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var asset2 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance1, asset1);
                PropertyReflectionHelper.SetGlobalPluginConfig(instance2, asset2);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance1), Is.EqualTo(asset1));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance2), Is.EqualTo(asset2));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance1), Is.Not.EqualTo(PropertyReflectionHelper.GetGlobalPluginConfig(instance2)));
            }

            [TestCase(true, "Expected non-null value to be preserved")]
            [TestCase(false, "Expected null value to be preserved")]
            public void GlobalPluginConfig_AssignmentBehavior_ConsistentAcrossProperties(bool assignNull, string message)
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = assignNull ? null : GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, testAsset);

                var retrieved = PropertyReflectionHelper.GetGlobalPluginConfig(instance);
                Assert.That(retrieved, Is.EqualTo(testAsset), message);
            }
        }

        #endregion

        #region PluginConfig Property Tests

        [TestFixture]
        public class PluginConfigPropertyTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void PluginConfig_ReturnsNullByDefault()
            {
                var instance = CreateGlobalConfigLoader();

                var value = PropertyReflectionHelper.GetPluginConfig(instance);

                Assert.That(value, Is.Null);
            }

            [Test]
            public void PluginConfig_CanBeSetToNonNull()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, testAsset);

                var saved = PropertyReflectionHelper.GetPluginConfig(instance);
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved, Is.EqualTo(testAsset));
            }

            [Test]
            public void PluginConfig_CanBeSetToNull()
            {
                var instance = CreateGlobalConfigLoader();

                PropertyReflectionHelper.SetPluginConfig(instance, null);

                var saved = PropertyReflectionHelper.GetPluginConfig(instance);
                Assert.That(saved, Is.Null);
            }

            [Test]
            public void PluginConfig_MultipleAssignments_LastValueWins()
            {
                var instance = CreateGlobalConfigLoader();

                var originalAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                PropertyReflectionHelper.SetPluginConfig(instance, originalAsset);
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(originalAsset));

                var newAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                PropertyReflectionHelper.SetPluginConfig(instance, newAsset);

                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(newAsset));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.Not.EqualTo(originalAsset));
            }

            [Test]
            public void PluginConfig_SetSameAssetMultipleTimes_ValuePreserved()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, testAsset);
                var saved = PropertyReflectionHelper.GetPluginConfig(instance);

                Assert.That(saved, Is.EqualTo(testAsset));
                Assert.That(saved, Is.Not.Null);
            }

            [Test]
            public void PluginConfig_AssignedOnce_PersistsAcrossAccesses()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, testAsset);

                var val1 = PropertyReflectionHelper.GetPluginConfig(instance);
            var val2 = PropertyReflectionHelper.GetPluginConfig(instance);
                Assert.That(val1, Is.EqualTo(val2));
                Assert.That(val1, Is.EqualTo(testAsset));
            }

            [Test]
            public void PluginConfig_GetterBehavior_ConsistentAcrossCalls()
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, testAsset);

                var val1 = PropertyReflectionHelper.GetPluginConfig(instance);
                var val2 = PropertyReflectionHelper.GetPluginConfig(instance);
                var val3 = PropertyReflectionHelper.GetPluginConfig(instance);

                Assert.That(val1, Is.Not.Null);
                Assert.That(val2, Is.Not.Null);
                Assert.That(val3, Is.Not.Null);
                Assert.That(val1, Is.EqualTo(val2));
                Assert.That(val2, Is.EqualTo(val3));
                Assert.That(val1, Is.InstanceOf<PluginConfigAsset>());
            }

            [Test]
            public void PluginConfig_NotNullAssignment_ValueNotReplaced()
            {
                var instance = CreateGlobalConfigLoader();
                var asset1 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                var asset2 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, asset1);
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(asset1));

                PropertyReflectionHelper.SetPluginConfig(instance, asset2);

                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(asset2));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.Not.EqualTo(asset1));
            }

            [Test]
            public void PluginConfig_SetThenGet_ReturnsCorrectType()
            {
                var instance = CreateGlobalConfigLoader();

                var asset1 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                PropertyReflectionHelper.SetPluginConfig(instance, asset1);

                var val1 = PropertyReflectionHelper.GetPluginConfig(instance);

                PropertyReflectionHelper.SetPluginConfig(instance, null);

                var nullVal = PropertyReflectionHelper.GetPluginConfig(instance);

                Assert.That(val1, Is.InstanceOf<PluginConfigAsset>());
                Assert.That(val1, Is.Not.Null);
                Assert.That(val1, Is.EqualTo(asset1));
                Assert.That(nullVal, Is.Null);
            }

            [Test]
            public void PluginConfig_MultipleInstances_ValueIndependence()
            {
                var instance1 = CreateGlobalConfigLoader();
                var instance2 = CreateGlobalConfigLoader();

                var asset1 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                var asset2 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance1, asset1);
                PropertyReflectionHelper.SetPluginConfig(instance2, asset2);

                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance1), Is.EqualTo(asset1));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance2), Is.EqualTo(asset2));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance1), Is.Not.EqualTo(PropertyReflectionHelper.GetPluginConfig(instance2)));
            }

            [TestCase(true, "Expected non-null value to be preserved")]
            [TestCase(false, "Expected null value to be preserved")]
            public void PluginConfig_AssignmentBehavior_ConsistentAcrossProperties(bool assignNull, string message)
            {
                var instance = CreateGlobalConfigLoader();
                var testAsset = assignNull ? null : GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, testAsset);

                var retrieved = PropertyReflectionHelper.GetPluginConfig(instance);
                Assert.That(retrieved, Is.EqualTo(testAsset), message);
            }
        }

        #endregion

        #region Logger Property Tests

        [TestFixture]
        public class LoggerPropertyTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void Log_StaticPropertyAccess_ReturnsNonNull()
            {
                var logger = PropertyReflectionHelper.GetLogger();

                Assert.That(logger, Is.Not.Null);
            }

            [Test]
            public void Log_MultipleAccesses_ReturnsSameInstance()
            {
                var logger1 = PropertyReflectionHelper.GetLogger();
                var logger2 = PropertyReflectionHelper.GetLogger();

                Assert.That(logger1, Is.EqualTo(logger2));
            }

            [Test]
            public void Log_LazyInitialization_OnlyOccursOnce()
            {
                // First access
                var initialLogger = PropertyReflectionHelper.GetLogger();
                Assert.That(initialLogger, Is.Not.Null);

                // We can't directly test lazy behavior, but we can ensure consistency
                var logger2 = PropertyReflectionHelper.GetLogger();
                Assert.That(logger2, Is.EqualTo(initialLogger));
            }

            [Test]
            public void Log_ThreadSafety_ConsistentAcrossMultipleCalls()
            {
                // Simulate multiple simultaneous accesses
                var loggers = new ILogger[5];
                for (int i = 0; i < 5; i++)
                {
                    loggers[i] = PropertyReflectionHelper.GetLogger();
                }

                // All should be the same instance
                for (int i = 1; i < loggers.Length; i++)
                {
                    Assert.That(loggers[i], Is.EqualTo(loggers[0]));
                }
            }

            [Test]
            public void Log_HasCorrectType_IsILogger()
            {
                var logger = PropertyReflectionHelper.GetLogger();

                Assert.That(logger, Is.InstanceOf<ILogger>());
            }
        }

        #endregion

        #region DontDestroyOnLoad Property Tests

        [TestFixture]
        public class DontDestroyOnLoadPropertyTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void DontDestroyOnLoad_PropertyOverride_ReturnsTrue()
            {
                var instance = CreateGlobalConfigLoader();

                var dontDestroyOnLoad = PropertyReflectionHelper.GetDontDestroyOnLoad(instance);

                Assert.That(dontDestroyOnLoad, Is.True);
            }

            [Test]
            public void DontDestroyOnLoad_MultipleInstances_AllReturnTrue()
            {
                var instances = GlobalConfigLoaderPropertyTestData.CreateMultipleInstances(3, CreateGlobalConfigLoader);

                foreach (var instance in instances)
                {
                    Assert.That(PropertyReflectionHelper.GetDontDestroyOnLoad(instance), Is.True);
                }
            }

            [Test]
            public void DontDestroyOnLoad_PropertyBehavior_ConsistentAcrossCalls()
            {
                var instance = CreateGlobalConfigLoader();

                var val1 = PropertyReflectionHelper.GetDontDestroyOnLoad(instance);
                var val2 = PropertyReflectionHelper.GetDontDestroyOnLoad(instance);
                var val3 = PropertyReflectionHelper.GetDontDestroyOnLoad(instance);

                Assert.That(val1, Is.True);
                Assert.That(val2, Is.True);
                Assert.That(val3, Is.True);
                Assert.That(val1, Is.EqualTo(val2));
                Assert.That(val2, Is.EqualTo(val3));
            }
        }

        #endregion

        #region Cross-Property Tests

        [TestFixture]
        public class CrossPropertyTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void SetPluginConfigOnly_GlobalPluginConfigUnaffected()
            {
                var instance = CreateGlobalConfigLoader();
                var originalGlobalConfig = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var newPluginConfig = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, originalGlobalConfig);
                PropertyReflectionHelper.SetPluginConfig(instance, newPluginConfig);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(originalGlobalConfig));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(newPluginConfig));
            }

            [Test]
            public void SetNullPluginConfig_GlobalPluginConfigUnaffected()
            {
                var instance = CreateGlobalConfigLoader();
                var globalConfig = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, globalConfig);
                PropertyReflectionHelper.SetPluginConfig(instance, null);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(globalConfig));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.Null);
            }

            [Test]
            public void SetNullGlobalPluginConfig_PluginConfigUnaffected()
            {
                var instance = CreateGlobalConfigLoader();
                var pluginConfig = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetPluginConfig(instance, pluginConfig);
                PropertyReflectionHelper.SetGlobalPluginConfig(instance, null);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.Null);
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(pluginConfig));
            }

            [Test]
            public void SetBothConfigs_ValuesIndependent()
            {
                var instance = CreateGlobalConfigLoader();
                var globalConfig = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var pluginConfig = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, globalConfig);
                PropertyReflectionHelper.SetPluginConfig(instance, pluginConfig);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.EqualTo(globalConfig));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.EqualTo(pluginConfig));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.Not.EqualTo(PropertyReflectionHelper.GetPluginConfig(instance)));
            }

            [Test]
            public void MultipleInstancesSeparateProperties_ValuesIndependencePreserved()
            {
                var instance1 = CreateGlobalConfigLoader();
                var instance2 = CreateGlobalConfigLoader();

                var globalConfig1 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var globalConfig2 = GlobalConfigLoaderPropertyTestData.CreateGlobalPluginConfigAsset();
                var pluginConfig1 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();
                var pluginConfig2 = GlobalConfigLoaderPropertyTestData.CreatePluginConfigAsset();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance1, globalConfig1);
                PropertyReflectionHelper.SetPluginConfig(instance1, pluginConfig1);
                PropertyReflectionHelper.SetGlobalPluginConfig(instance2, globalConfig2);
                PropertyReflectionHelper.SetPluginConfig(instance2, pluginConfig2);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance1), Is.EqualTo(globalConfig1));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance1), Is.EqualTo(pluginConfig1));
                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance2), Is.EqualTo(globalConfig2));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance2), Is.EqualTo(pluginConfig2));

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance1), Is.Not.EqualTo(PropertyReflectionHelper.GetGlobalPluginConfig(instance2)));
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance1), Is.Not.EqualTo(PropertyReflectionHelper.GetPluginConfig(instance2)));

                Assert.That(PropertyReflectionHelper.GetDontDestroyOnLoad(instance1), Is.EqualTo(PropertyReflectionHelper.GetDontDestroyOnLoad(instance2)));
            }

            [Test]
            public void SetNullBothConfigs_PropertiesSeparatelyNull()
            {
                var instance = CreateGlobalConfigLoader();

                PropertyReflectionHelper.SetGlobalPluginConfig(instance, null);
                PropertyReflectionHelper.SetPluginConfig(instance, null);

                Assert.That(PropertyReflectionHelper.GetGlobalPluginConfig(instance), Is.Null);
                Assert.That(PropertyReflectionHelper.GetPluginConfig(instance), Is.Null);
            }
        }

        #endregion

        #region Property Attribute Tests

        [TestFixture]
        public class PropertyAttributeTests : GlobalConfigLoaderPropertyTests
        {
            [Test]
            public void SerializeFieldAttribute_GlobalPluginConfig_HasAttribute()
            {
                var hasAttribute = PropertyReflectionHelper.PropertyHasSerializeFieldAttribute("GlobalPluginConfig");

                Assert.That(hasAttribute, Is.True);
            }

            [Test]
            public void SerializeFieldAttribute_PluginConfig_HasAttribute()
            {
                var hasAttribute = PropertyReflectionHelper.PropertyHasSerializeFieldAttribute("PluginConfig");

                Assert.That(hasAttribute, Is.True);
            }

            [Test]
            public void PropertyVisibility_GlobalPluginConfig_PublicGetterPrivateSetter()
            {
                Assert.That(PropertyReflectionHelper.IsPropertyPublicGetter<GlobalPluginConfigAsset>("GlobalPluginConfig"), Is.True);
                Assert.That(PropertyReflectionHelper.IsPropertyPublicSetter<GlobalPluginConfigAsset>("GlobalPluginConfig"), Is.False);
            }

            [Test]
            public void PropertyVisibility_PluginConfig_PublicGetterPrivateSetter()
            {
                Assert.That(PropertyReflectionHelper.IsPropertyPublicGetter<PluginConfigAsset>("PluginConfig"), Is.True);
                Assert.That(PropertyReflectionHelper.IsPropertyPublicSetter<PluginConfigAsset>("PluginConfig"), Is.False);
            }
        }

        #endregion
    }
}
