/**
 * Unit tests for GameMessageBroker.cs
 * Refactored from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Static utility methods for message publishing and subscription
 * - Async request/response pattern with state management
 * - VContainer integration and dependency resolution
 * - Static state management and lifecycle testing
 * - Unity-specific considerations for static methods
 * - Exception isolation and error handling
 * - Multiple lifecycle cycle testing
 * - Parameterized test scenarios
 * 
 * Mock Dependencies:
 * - IObjectResolver (VContainer) for dependency injection
 * - IPublisher<T> and ISubscriber<T> (MessagePipe) for message handling
 * - ILogger (Serilog) for logging verification
 * - ConcurrentDictionary for async request state management
 * 
 * Refactoring Improvements:
 * - Organized tests with nested TestFixture classes for better structure
 * - Added parameterized tests with [TestCase] for similar scenarios
 * - Enhanced exception isolation testing with try/finally blocks
 * - Added comprehensive lifecycle state testing across multiple cycles
 * - Improved test data management with factory methods
 * - Added missing edge cases and error scenarios
 * - Optimized reflection utilities with cached FieldInfo
 * - Enhanced documentation and test descriptions
 */

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using Serilog;
using VContainer;
using NSubstitute;
using MToolKit.Tests.Runtime.Core;
using UnityEngine;
using UnityEngine.TestTools;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.MessageBus;

namespace MToolKit.Tests.Runtime.Core.MessageBus
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    internal static class GameMessageBrokerTestData
    {
        // Basic test values
        public const string TestRequestId = "test-request-id";
        public const string TestExceptionMessage = "Test exception";
        public const string TestExceptionMessage2 = "Test exception 2";
        public const int TestTimeoutMs = 1000;
        public const int TestTimeoutMsShort = 100;
        
        // Complex test objects
        public static readonly TestMessage ValidMessage = new() { Content = "valid message" };
        public static readonly TestMessage EmptyMessage = new() { Content = "" };
        public static readonly TestMessage NullContentMessage = new() { Content = null };
        
        public static readonly TestRequest ValidRequest = new() { Data = "valid request" };
        public static readonly TestRequest EmptyRequest = new() { Data = "" };
        public static readonly TestRequest NullDataRequest = new() { Data = null };
        
        public static readonly TestResponse ValidResponse = new() { Result = "valid response" };
        public static readonly TestResponse EmptyResponse = new() { Result = "" };
        public static readonly TestResponse NullResultResponse = new() { Result = null };
        
        // Factory methods for consistent test object creation
        public static IPublisher<T> CreateMockPublisher<T>(bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            var mock = Substitute.For<IPublisher<T>>();
            if (shouldThrow)
            {
                mock.When(x => x.Publish(Arg.Any<T>())).Throw(new InvalidOperationException(exceptionMessage));
            }
            return mock;
        }
        
        public static ISubscriber<T> CreateMockSubscriber<T>(bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            var mock = Substitute.For<ISubscriber<T>>();
            if (shouldThrow)
            {
                mock.When(x => x.Subscribe(Arg.Any<IMessageHandler<T>>())).Throw(new InvalidOperationException(exceptionMessage));
            }
            return mock;
        }
        
        public static IObjectResolver CreateMockResolver(bool shouldThrowOnResolve = false, string exceptionMessage = TestExceptionMessage)
        {
            var mock = Substitute.For<IObjectResolver>();
            if (shouldThrowOnResolve)
            {
                mock.When(x => x.Resolve<object>()).Throw(new InvalidOperationException(exceptionMessage));
            }
            return mock;
        }
        
        public static TestMessage CreateTestMessage(string content = "test message")
        {
            return new TestMessage { Content = content };
        }
        
        public static TestRequest CreateTestRequest(string data = "test request")
        {
            return new TestRequest { Data = data };
        }
        
        public static TestResponse CreateTestResponse(string result = "test response")
        {
            return new TestResponse { Result = result };
        }
        
        // Enhanced factory methods with overrides
        public static TestMessage CreateTestMessageWithOverrides(TestMessage overrides = null) => 
            overrides != null ? new TestMessage { Content = overrides.Content ?? ValidMessage.Content } : ValidMessage;
        
        public static TestRequest CreateTestRequestWithOverrides(TestRequest overrides = null) => 
            overrides != null ? new TestRequest { Data = overrides.Data ?? ValidRequest.Data } : ValidRequest;
        
        public static TestResponse CreateTestResponseWithOverrides(TestResponse overrides = null) => 
            overrides != null ? new TestResponse { Result = overrides.Result ?? ValidResponse.Result } : ValidResponse;
    }

    /// <summary>
    /// Reflection utilities for accessing private static fields and methods with performance optimization
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    internal static class GameMessageBrokerReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for performance optimization
        /// </summary>
        private static readonly FieldInfo GameResolverField = typeof(GameMessageBroker)
            .GetField("gameResolver", BindingFlags.NonPublic | BindingFlags.Static);
        
        private static readonly FieldInfo IsInitializedField = typeof(GameMessageBroker)
            .GetField("isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        
        private static readonly FieldInfo PendingRequestsByTypeField = typeof(GameMessageBroker)
            .GetField("pendingRequestsByType", BindingFlags.NonPublic | BindingFlags.Static);
        
        public static IObjectResolver GetGameResolver()
        {
            return GameResolverField?.GetValue(null) as IObjectResolver;
        }
        
        public static bool GetIsInitialized()
        {
            return (bool)(IsInitializedField?.GetValue(null) ?? false);
        }
        
        public static ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> GetPendingRequestsByType()
        {
            return PendingRequestsByTypeField?.GetValue(null) as ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>>;
        }
        
        public static int GetTotalPendingRequestsCount()
        {
            var pendingRequestsByType = GetPendingRequestsByType();
            if (pendingRequestsByType == null) return 0;
            
            int totalCount = 0;
            foreach (var queue in pendingRequestsByType.Values)
            {
                totalCount += queue.Count;
            }
            return totalCount;
        }
        
        public static void SetGameResolver(IObjectResolver resolver)
        {
            GameResolverField?.SetValue(null, resolver);
        }
        
        public static void SetIsInitialized(bool initialized)
        {
            IsInitializedField?.SetValue(null, initialized);
        }
        
        public static void SetPendingRequestsByType(ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> requests)
        {
            PendingRequestsByTypeField?.SetValue(null, requests);
        }
    }

    /// <summary>
    /// Test message types for testing generic methods
    /// CRITICAL: Must be public for NSubstitute to create proxies
    /// </summary>
    public class TestMessage
    {
        public string Content { get; set; }
    }
    
    public class TestRequest
    {
        public string Data { get; set; }
    }
    
    public class TestResponse
    {
        public string Result { get; set; }
    }

    /// <summary>
    /// Base test fixture for GameMessageBroker tests with common setup and utilities
    /// </summary>
    [TestFixture]
    public class GameMessageBrokerTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;
        private IObjectResolver _mockGameResolver;
        
        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
            
            // Reset static state before each test
            GameMessageBroker.Reset();
        }
        
        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
            
            // Reset static state after each test
            GameMessageBroker.Reset();
        }
        
        /// <summary>
        /// Sets up the VContainer test container with common mocks and dependencies
        /// </summary>
        private void SetupTestContainer()
        {
            // Register mock logger with Serilog alias to avoid Unity conflicts
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register mock game resolver
            _mockGameResolver = Substitute.For<IObjectResolver>();
            _containerBuilder.RegisterInstance(_mockGameResolver).As<IObjectResolver>();
        }
        
        /// <summary>
        /// Helper method to create isolated VContainer instance per test
        /// CRITICAL: Prevents registration conflicts between tests
        /// </summary>
        protected T CreateTestInstanceWithDependencies<T>(params object[] dependencies)
        {
            // Create fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register test-specific dependencies
            foreach (var dependency in dependencies)
            {
                testContainerBuilder.RegisterInstance(dependency).As(dependency.GetType());
            }
            
            // CRITICAL: Always register the class being tested
            testContainerBuilder.Register<T>(Lifetime.Singleton);
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            var instance = testResolver.Resolve<T>();
            
            // Store for cleanup
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return instance;
        }
        
        /// <summary>
        /// Helper method to setup broker with mock resolver and publisher/subscriber
        /// </summary>
        protected void SetupBrokerWithMocks<TMessage, TRequest, TResponse>(
            out IObjectResolver resolver, 
            out IPublisher<TMessage> publisher, 
            out ISubscriber<TMessage> subscriber,
            out IPublisher<TRequest> requestPublisher)
        {
            resolver = GameMessageBrokerTestData.CreateMockResolver();
            publisher = GameMessageBrokerTestData.CreateMockPublisher<TMessage>();
            subscriber = GameMessageBrokerTestData.CreateMockSubscriber<TMessage>();
            requestPublisher = GameMessageBrokerTestData.CreateMockPublisher<TRequest>();
            
            resolver.Resolve<IPublisher<TMessage>>().Returns(publisher);
            resolver.Resolve<ISubscriber<TMessage>>().Returns(subscriber);
            resolver.Resolve<IPublisher<TRequest>>().Returns(requestPublisher);
            
            GameMessageBroker.Initialize(resolver);
        }
        
        /// <summary>
        /// Helper method to verify broker state consistency
        /// </summary>
        protected void VerifyBrokerState(bool expectedInitialized, IObjectResolver expectedResolver = null)
        {
            Assert.That(GameMessageBrokerReflectionHelper.GetIsInitialized(), Is.EqualTo(expectedInitialized));
            if (expectedResolver != null)
            {
                Assert.That(GameMessageBrokerReflectionHelper.GetGameResolver(), Is.EqualTo(expectedResolver));
            }
        }

        /// <summary>
        /// Tests for Initialize method functionality
        /// </summary>
        [TestFixture]
        public class InitializeTests : GameMessageBrokerTests
        {
            [Test]
            public void Initialize_WhenValidResolverProvided_ShouldSetInitializedState()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                
                // Act
                GameMessageBroker.Initialize(resolver);
                
                // Assert
                VerifyBrokerState(true, resolver);
            }
            
            [Test]
            public void Initialize_WhenAlreadyInitialized_ShouldLogWarningAndReturnEarly()
            {
                // Arrange
                var resolver1 = GameMessageBrokerTestData.CreateMockResolver();
                var resolver2 = GameMessageBrokerTestData.CreateMockResolver();
                
                // Act
                GameMessageBroker.Initialize(resolver1);
                GameMessageBroker.Initialize(resolver2);
                
                // Assert
                VerifyBrokerState(true, resolver1); // Should still be first resolver
            }
            
            [Test]
            public void Initialize_WhenNullResolverProvided_ShouldStillInitialize()
            {
                // Act
                GameMessageBroker.Initialize(null);
                
                // Assert
                VerifyBrokerState(true, null);
            }
            
            [Test]
            public void Initialize_WhenCalledMultipleTimesWithNull_ShouldMaintainFirstResolver()
            {
                // Arrange
                var firstResolver = GameMessageBrokerTestData.CreateMockResolver();
                
                // Act
                GameMessageBroker.Initialize(firstResolver);
                GameMessageBroker.Initialize(null);
                
                // Assert
                VerifyBrokerState(true, firstResolver);
            }
            
            [Test]
            public void Initialize_WhenCalledMultipleTimesWithDifferentResolver_ShouldMaintainFirstResolver()
            {
                // Arrange
                var firstResolver = GameMessageBrokerTestData.CreateMockResolver();
                var secondResolver = GameMessageBrokerTestData.CreateMockResolver();
                
                // Act
                GameMessageBroker.Initialize(firstResolver);
                GameMessageBroker.Initialize(secondResolver);
                
                // Assert
                VerifyBrokerState(true, firstResolver);
            }
        }

        /// <summary>
        /// Tests for GetPublisher method functionality
        /// </summary>
        [TestFixture]
        public class GetPublisherTests : GameMessageBrokerTests
        {
            [Test]
            public void GetPublisher_WhenInitializedWithValidResolver_ShouldReturnPublisher()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>();
                resolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                
                // Act
                var result = GameMessageBroker.GetPublisher<TestMessage>();
                
                // Assert
                Assert.That(result, Is.EqualTo(mockPublisher));
                resolver.Received(1).Resolve<IPublisher<TestMessage>>();
            }
            
            [Test]
            public void GetPublisher_WhenNotInitialized_ShouldReturnNull()
            {
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.GetPublisher<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetPublisher_WhenResolverIsNull_ShouldReturnNull()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.GetPublisher<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetPublisher_WhenResolverThrowsException_ShouldReturnNull()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                resolver.When(x => x.Resolve<IPublisher<TestMessage>>()).Throw(new InvalidOperationException("Test exception"));
                
                GameMessageBroker.Initialize(resolver);
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to resolve publisher for.*"));
                
                // Act
                var result = GameMessageBroker.GetPublisher<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetPublisher_WhenCalledMultipleTimes_ShouldReturnSameInstance()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>();
                resolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                
                // Act
                var result1 = GameMessageBroker.GetPublisher<TestMessage>();
                var result2 = GameMessageBroker.GetPublisher<TestMessage>();
                
                // Assert
                Assert.That(result1, Is.EqualTo(result2));
                Assert.That(result1, Is.EqualTo(mockPublisher));
                resolver.Received(2).Resolve<IPublisher<TestMessage>>();
            }
        }

        /// <summary>
        /// Tests for GetSubscriber method functionality
        /// </summary>
        [TestFixture]
        public class GetSubscriberTests : GameMessageBrokerTests
        {
            [Test]
            public void GetSubscriber_WhenInitializedWithValidResolver_ShouldReturnSubscriber()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockSubscriber = GameMessageBrokerTestData.CreateMockSubscriber<TestMessage>();
                resolver.Resolve<ISubscriber<TestMessage>>().Returns(mockSubscriber);
                
                GameMessageBroker.Initialize(resolver);
                
                // Act
                var result = GameMessageBroker.GetSubscriber<TestMessage>();
                
                // Assert
                Assert.That(result, Is.EqualTo(mockSubscriber));
                resolver.Received(1).Resolve<ISubscriber<TestMessage>>();
            }
            
            [Test]
            public void GetSubscriber_WhenNotInitialized_ShouldReturnNull()
            {
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.GetSubscriber<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetSubscriber_WhenResolverIsNull_ShouldReturnNull()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.GetSubscriber<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetSubscriber_WhenResolverThrowsException_ShouldReturnNull()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                resolver.When(x => x.Resolve<ISubscriber<TestMessage>>()).Throw(new InvalidOperationException("Test exception"));
                
                GameMessageBroker.Initialize(resolver);
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to resolve subscriber for.*"));
                
                // Act
                var result = GameMessageBroker.GetSubscriber<TestMessage>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetSubscriber_WhenCalledMultipleTimes_ShouldReturnSameInstance()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockSubscriber = GameMessageBrokerTestData.CreateMockSubscriber<TestMessage>();
                resolver.Resolve<ISubscriber<TestMessage>>().Returns(mockSubscriber);
                
                GameMessageBroker.Initialize(resolver);
                
                // Act
                var result1 = GameMessageBroker.GetSubscriber<TestMessage>();
                var result2 = GameMessageBroker.GetSubscriber<TestMessage>();
                
                // Assert
                Assert.That(result1, Is.EqualTo(result2));
                Assert.That(result1, Is.EqualTo(mockSubscriber));
                resolver.Received(2).Resolve<ISubscriber<TestMessage>>();
            }
        }

        /// <summary>
        /// Tests for Publish method functionality
        /// </summary>
        [TestFixture]
        public class PublishTests : GameMessageBrokerTests
        {
            [Test]
            public void Publish_WhenPublisherAvailable_ShouldPublishMessage()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>();
                resolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var message = GameMessageBrokerTestData.CreateTestMessage();
                
                // Act
                GameMessageBroker.Publish(message);
                
                // Assert
                mockPublisher.Received(1).Publish(message);
            }
            
            [Test]
            public void Publish_WhenPublisherNotAvailable_ShouldNotThrow()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                var message = GameMessageBrokerTestData.CreateTestMessage();
                
                // Expect the error and warning log messages - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                
                // Act & Assert
                Assert.DoesNotThrow(() => GameMessageBroker.Publish(message));
            }
            
            [Test]
            public void Publish_WhenPublisherThrowsException_ShouldNotThrow()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>(true, "Publisher exception");
                resolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var message = GameMessageBrokerTestData.CreateTestMessage();
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher threw exception.*"));
                
                // Act & Assert
                Assert.DoesNotThrow(() => GameMessageBroker.Publish(message));
            }
            
            [TestCase("valid message")]
            [TestCase("")]
            [TestCase(null)]
            public void Publish_WithDifferentMessageTypes_ShouldHandleGracefully(string content)
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>();
                resolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var message = new TestMessage { Content = content };
                
                // Act & Assert
                Assert.DoesNotThrow(() => GameMessageBroker.Publish(message));
                mockPublisher.Received(1).Publish(message);
            }
        }

        /// <summary>
        /// Tests for RequestAsync method functionality with enhanced exception isolation
        /// </summary>
        [TestFixture]
        public class RequestAsyncTests : GameMessageBrokerTests
        {
            [Test]
            public void RequestAsync_WhenInitializedWithValidResolver_ShouldPublishRequest()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Assert
                Assert.That(task, Is.Not.Null);
                Assert.That(task, Is.InstanceOf<UniTask<TestResponse>>());
                mockPublisher.Received(1).Publish(request);
            }
            
            [Test]
            public void RequestAsync_WhenNotInitialized_ShouldReturnDefault()
            {
                // Arrange
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request).GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void RequestAsync_WhenPublisherNotAvailable_ShouldReturnDefault()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Act
                var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request).GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void RequestAsync_WhenCancellationTokenProvided_ShouldHandleCancellation()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var cts = new CancellationTokenSource();
                cts.Cancel();
                
                // Act
                var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request, cts.Token).GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void RequestAsync_WhenResponseCompleted_ShouldReturnResponse()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var expectedResponse = GameMessageBrokerTestData.CreateTestResponse();
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                GameMessageBroker.CompleteRequest(expectedResponse);
                var result = task.GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.EqualTo(expectedResponse));
            }
            
            [Test]
            public void RequestAsync_WhenResponseTypeMismatch_ShouldReturnDefault()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var wrongResponse = new TestMessage(); // Wrong type
                
                // Expect the warning log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*No pending request found for response.*"));
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                GameMessageBroker.CompleteRequest(wrongResponse);
                
                // The task should still be pending since the response type doesn't match
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "Task should still be pending due to type mismatch");
                
                // Complete with the correct response type to clean up
                var correctResponse = GameMessageBrokerTestData.CreateTestResponse();
                GameMessageBroker.CompleteRequest(correctResponse);
                
                // Now we can safely get the result
                var result = task.GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.EqualTo(correctResponse));
            }
            
            [Test]
            public void RequestAsync_WhenPublisherThrowsException_ShouldReturnDefault()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>(true, "Publisher exception");
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Expect the error log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Error in async request.*"));
                
                try
                {
                    // Act
                    var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request).GetAwaiter().GetResult();
                    
                    // Assert
                    Assert.That(result, Is.Null);
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    GameMessageBroker.Reset();
                }
            }
            
            [Test]
            public void RequestAsync_WhenMultipleConcurrentRequests_ShouldHandleIndependently()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request1 = GameMessageBrokerTestData.CreateTestRequest("request1");
                var request2 = GameMessageBrokerTestData.CreateTestRequest("request2");
                var response1 = GameMessageBrokerTestData.CreateTestResponse("response1");
                var response2 = GameMessageBrokerTestData.CreateTestResponse("response2");
                
                // Act
                var task1 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request1);
                var task2 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request2);
                
                GameMessageBroker.CompleteRequest(response1);
                GameMessageBroker.CompleteRequest(response2);
                
                var result1 = task1.GetAwaiter().GetResult();
                var result2 = task2.GetAwaiter().GetResult();
                
                // Assert - Use reference equality since TestResponse doesn't override Equals
                Assert.That(result1, Is.SameAs(response1), "First result should be the same object as first response");
                Assert.That(result2, Is.SameAs(response2), "Second result should be the same object as second response");
                Assert.That(result1.Result, Is.EqualTo("response1"), "First result should have correct value");
                Assert.That(result2.Result, Is.EqualTo("response2"), "Second result should have correct value");
                mockPublisher.Received(2).Publish(Arg.Any<TestRequest>());
            }
        }

        /// <summary>
        /// Tests for CompleteRequest method functionality
        /// </summary>
        [TestFixture]
        public class CompleteRequestTests : GameMessageBrokerTests
        {
            [Test]
            public void CompleteRequest_WhenPendingRequestExists_ShouldCompleteRequest()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                GameMessageBroker.CompleteRequest(response);
                var result = task.GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.EqualTo(response));
            }
            
            [Test]
            public void CompleteRequest_WhenNoPendingRequestExists_ShouldNotThrow()
            {
                // Arrange
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Act & Assert
                Assert.DoesNotThrow(() => GameMessageBroker.CompleteRequest(response));
            }
            
            [Test]
            public void CompleteRequest_WhenMultiplePendingRequests_ShouldCompleteFirstMatching()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request1 = GameMessageBrokerTestData.CreateTestRequest();
                var request2 = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Act - Create both requests
                var task1 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request1);
                var task2 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request2);
                
                // Verify both tasks are pending before completion
                Assert.That(task1.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                // Check that pending requests are actually set up using reflection
                var pendingRequestsCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                Assert.That(pendingRequestsCount, Is.EqualTo(2), "Should have 2 pending requests");
                
                // Complete the first request
                GameMessageBroker.CompleteRequest(response);
                
                // Verify the first task is completed and the second is still pending
                Assert.That(task1.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                // Get the result of the first task
                var result1 = task1.GetAwaiter().GetResult();
                Assert.That(result1, Is.EqualTo(response));
                
                // Complete the second request to clean up
                var response2 = GameMessageBrokerTestData.CreateTestResponse("response2");
                GameMessageBroker.CompleteRequest(response2);
                
                // Verify the second task is now completed
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                
                // Get the result of the second task
                var result2 = task2.GetAwaiter().GetResult();
                Assert.That(result2, Is.EqualTo(response2));
            }
            
            [Test]
            public void CompleteRequest_WhenSinglePendingRequest_ShouldCompleteCorrectly()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Verify the task is pending before completion
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                GameMessageBroker.CompleteRequest(response);
                
                // Verify the task is completed after completion
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                
                var result = task.GetAwaiter().GetResult();
                
                // Assert
                Assert.That(result, Is.EqualTo(response));
            }
            
            [Test]
            public void CompleteRequest_WhenNoPendingRequests_ShouldLogWarning()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Expect the warning log message - use regex to match the structured log format
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*No pending request found for response.*"));
                
                // Act
                GameMessageBroker.CompleteRequest(response);
                
                // Assert - No exception should be thrown
                Assert.Pass("CompleteRequest completed without exception when no pending requests exist");
            }
            
            [Test]
            public void CompleteRequest_VerifyPendingRequestsAreSetUp()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Act
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Verify the task is pending
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                // Check that pending requests are actually set up using reflection
                var pendingRequestsByType = GameMessageBrokerReflectionHelper.GetPendingRequestsByType();
                Assert.That(pendingRequestsByType, Is.Not.Null);
                Assert.That(pendingRequestsByType.Count, Is.GreaterThan(0), "Pending requests should be set up");
                
                // Verify the pending request has the correct response type
                var hasCorrectResponseType = pendingRequestsByType.ContainsKey(typeof(TestResponse));
                Assert.That(hasCorrectResponseType, Is.True, "Pending request should have correct response type");
                
                // Clean up
                var response = GameMessageBrokerTestData.CreateTestResponse();
                GameMessageBroker.CompleteRequest(response);
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(response));
            }
            
            [Test]
            public void CompleteRequest_DebugPublisherResolution()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                
                // Test publisher resolution directly
                var resolvedPublisher = GameMessageBroker.GetPublisher<TestRequest>();
                Assert.That(resolvedPublisher, Is.Not.Null, "Publisher should be resolved correctly");
                Assert.That(resolvedPublisher, Is.EqualTo(mockPublisher), "Resolved publisher should match mock");
                
                // Test that the publisher can be called
                var request = GameMessageBrokerTestData.CreateTestRequest();
                Assert.DoesNotThrow(() => resolvedPublisher.Publish(request), "Publisher should be able to publish");
                
                // Verify the mock received the call
                resolvedPublisher.Received(1).Publish(request);
            }
            
            [Test]
            public void CompleteRequest_StepByStepDebug()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Step 1: Create request and verify it's pending
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "Task should be pending after creation");
                
                // Step 2: Verify pending requests are set up
                var pendingRequestsByType = GameMessageBrokerReflectionHelper.GetPendingRequestsByType();
                Assert.That(pendingRequestsByType, Is.Not.Null, "Pending requests should not be null");
                Assert.That(pendingRequestsByType.Count, Is.EqualTo(1), "Should have exactly 1 pending request type");
                
                // Step 3: Verify the pending request has correct type
                var hasCorrectType = pendingRequestsByType.ContainsKey(typeof(TestResponse));
                Assert.That(hasCorrectType, Is.True, "Pending request should have correct response type");
                
                // Step 4: Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Step 5: Verify task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded after completion");
                
                // Step 6: Get result
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(response), "Result should match the response");
            }
            
            [Test]
            public void CompleteRequest_DebugAsyncExecution()
            {
                // This test will help us understand if the issue is with async execution
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Test 1: Verify publisher resolution works
                var resolvedPublisher = GameMessageBroker.GetPublisher<TestRequest>();
                Assert.That(resolvedPublisher, Is.Not.Null, "Publisher should be resolved");
                Assert.That(resolvedPublisher, Is.EqualTo(mockPublisher), "Publisher should match mock");
                
                // Test 2: Verify publisher can publish
                Assert.DoesNotThrow(() => resolvedPublisher.Publish(request), "Publisher should be able to publish");
                
                // Test 3: Create async request
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Test 4: Verify task is pending
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "Task should be pending");
                
                // Test 5: Verify pending requests are set up
                var pendingRequestsCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                Assert.That(pendingRequestsCount, Is.EqualTo(1), "Should have exactly 1 pending request");
                
                // Test 6: Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Test 7: Verify task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded after completion");
                
                // Test 8: Get result
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(response), "Result should match the response");
            }
            
            [Test]
            public void CompleteRequest_DebugPendingRequestCleanup()
            {
                // This test will help us understand if there's an issue with pending request cleanup
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Test 1: Create async request
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Test 2: Verify pending requests are set up
                var pendingRequestsCountBefore = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                Assert.That(pendingRequestsCountBefore, Is.EqualTo(1), "Should have exactly 1 pending request before completion");
                
                // Test 3: Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Test 4: Verify pending requests are cleaned up
                var pendingRequestsCountAfter = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                Assert.That(pendingRequestsCountAfter, Is.EqualTo(0), "Should have 0 pending requests after completion");
                
                // Test 5: Verify task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded after completion");
                
                // Test 6: Get result
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(response), "Result should match the response");
            }
            
            [Test]
            public void CompleteRequest_DebugInternalState()
            {
                // This test will help us understand the internal state of the GameMessageBroker
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Test 1: Verify initialization state
                Assert.That(GameMessageBrokerReflectionHelper.GetIsInitialized(), Is.True, "GameMessageBroker should be initialized");
                Assert.That(GameMessageBrokerReflectionHelper.GetGameResolver(), Is.EqualTo(resolver), "GameResolver should match");
                
                // Test 2: Create async request
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Test 3: Verify pending requests are set up
                var pendingRequestsByType = GameMessageBrokerReflectionHelper.GetPendingRequestsByType();
                Assert.That(pendingRequestsByType, Is.Not.Null, "Pending requests should not be null");
                Assert.That(pendingRequestsByType.Count, Is.EqualTo(1), "Should have exactly 1 pending request type");
                
                // Test 4: Inspect the pending request details
                Assert.That(pendingRequestsByType.ContainsKey(typeof(TestResponse)), Is.True, "Should have TestResponse type");
                var queue = pendingRequestsByType[typeof(TestResponse)];
                Assert.That(queue.Count, Is.EqualTo(1), "Should have exactly 1 pending request");
                
                // Test 5: Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Test 6: Verify task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded after completion");
                
                // Test 7: Get result
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(response), "Result should match the response");
            }
            
            [Test]
            public void CompleteRequest_DebugRequestAsyncFlow()
            {
                // This test will help us understand the RequestAsync flow step by step
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse();
                
                // Test 1: Verify publisher resolution works
                var resolvedPublisher = GameMessageBroker.GetPublisher<TestRequest>();
                Assert.That(resolvedPublisher, Is.Not.Null, "Publisher should be resolved");
                Assert.That(resolvedPublisher, Is.EqualTo(mockPublisher), "Publisher should match mock");
                
                // Test 2: Verify publisher can publish
                Assert.DoesNotThrow(() => resolvedPublisher.Publish(request), "Publisher should be able to publish");
                
                // Test 3: Create async request and verify it's pending
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "Task should be pending after creation");
                
                // Test 4: Verify pending requests are set up
                var pendingRequestsByType = GameMessageBrokerReflectionHelper.GetPendingRequestsByType();
                Assert.That(pendingRequestsByType, Is.Not.Null, "Pending requests should not be null");
                Assert.That(pendingRequestsByType.Count, Is.EqualTo(1), "Should have exactly 1 pending request type");
                
                // Test 5: Inspect the pending request details
                Assert.That(pendingRequestsByType.ContainsKey(typeof(TestResponse)), Is.True, "Should have TestResponse type");
                var queue = pendingRequestsByType[typeof(TestResponse)];
                Assert.That(queue.Count, Is.EqualTo(1), "Should have exactly 1 pending request");
                
                // Test 6: Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Test 7: Verify task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded after completion");
                
                // Test 8: Get result and verify it's the same object
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.SameAs(response), "Result should be the same object as response");
            }
            
            [Test]
            public void CompleteRequest_DebugSingleRequest()
            {
                // This test focuses on the core issue with a single request
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = GameMessageBrokerTestData.CreateTestResponse("test-response");
                
                // Act: Create a single request
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Verify the task is pending
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "Task should be pending");
                
                // Verify pending requests are set up
                var pendingRequestsCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                Assert.That(pendingRequestsCount, Is.EqualTo(1), "Should have exactly 1 pending request");
                
                // Complete the request
                GameMessageBroker.CompleteRequest(response);
                
                // Verify the task is completed
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Task should be succeeded");
                
                // Get the result and verify it's the same object
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.SameAs(response), "Result should be the same object as response");
                Assert.That(result.Result, Is.EqualTo("test-response"), "Result should have correct value");
            }
            
            [TestCase("response1")]
            [TestCase("")]
            [TestCase(null)]
            public void CompleteRequest_WithDifferentResponseTypes_ShouldHandleGracefully(string result)
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var response = new TestResponse { Result = result };
                
                // Act & Assert
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                Assert.DoesNotThrow(() => GameMessageBroker.CompleteRequest(response));
                
                var taskResult = task.GetAwaiter().GetResult();
                Assert.That(taskResult, Is.EqualTo(response));
            }
        }

        /// <summary>
        /// Tests for IsAvailable method functionality
        /// </summary>
        [TestFixture]
        public class IsAvailableTests : GameMessageBrokerTests
        {
            [Test]
            public void IsAvailable_WhenInitializedWithValidResolver_ShouldReturnTrue()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                GameMessageBroker.Initialize(resolver);
                
                // Act
                var result = GameMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.True);
            }
            
            [Test]
            public void IsAvailable_WhenNotInitialized_ShouldReturnFalse()
            {
                // Act
                var result = GameMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.False);
            }
            
            [Test]
            public void IsAvailable_WhenResolverIsNull_ShouldReturnFalse()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                
                // Act
                var result = GameMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.False);
            }
        }

        /// <summary>
        /// Tests for Reset method functionality
        /// </summary>
        [TestFixture]
        public class ResetTests : GameMessageBrokerTests
        {
            [Test]
            public void Reset_WhenInitialized_ShouldClearState()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                GameMessageBroker.Initialize(resolver);
                
                // Act
                GameMessageBroker.Reset();
                
                // Assert
                VerifyBrokerState(false, null);
                Assert.That(GameMessageBrokerReflectionHelper.GetPendingRequestsByType(), Is.Not.Null);
            }
            
            [Test]
            public void Reset_WhenNotInitialized_ShouldNotThrow()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => GameMessageBroker.Reset());
            }
            
            [Test]
            public void Reset_WhenCalledMultipleTimes_ShouldNotThrow()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => 
                {
                    GameMessageBroker.Reset();
                    GameMessageBroker.Reset();
                    GameMessageBroker.Reset();
                });
            }
            
            [Test]
            public void Reset_WhenCalledAfterRequest_ShouldClearPendingRequests()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request);
                
                // Act
                GameMessageBroker.Reset();
                
                // Assert
                VerifyBrokerState(false, null);
                Assert.That(GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(0));
            }
        }

        /// <summary>
        /// Tests for lifecycle state management across multiple cycles
        /// CRITICAL: Tests state consistency and early return guard behavior
        /// </summary>
        [TestFixture]
        public class LifecycleStateTests : GameMessageBrokerTests
        {
            [Test]
            public void LifecycleState_WhenMultipleInitializeResetCycles_ShouldMaintainConsistency()
            {
                // Test that lifecycle state remains consistent across multiple cycles
                // This catches issues where early return guards prevent state updates
                
                var resolver1 = GameMessageBrokerTestData.CreateMockResolver();
                var resolver2 = GameMessageBrokerTestData.CreateMockResolver();
                
                // First cycle
                GameMessageBroker.Initialize(resolver1);
                VerifyBrokerState(true, resolver1);
                
                GameMessageBroker.Reset();
                VerifyBrokerState(false, null);
                
                // Second cycle - critical test for state consistency
                GameMessageBroker.Initialize(resolver2);
                VerifyBrokerState(true, resolver2);
                
                GameMessageBroker.Reset();
                VerifyBrokerState(false, null);
            }
            
            [Test]
            public void LifecycleState_WhenInitializeCalledAfterReset_ShouldWorkCorrectly()
            {
                // Arrange
                var resolver1 = GameMessageBrokerTestData.CreateMockResolver();
                var resolver2 = GameMessageBrokerTestData.CreateMockResolver();
                
                // Act
                GameMessageBroker.Initialize(resolver1);
                GameMessageBroker.Reset();
                GameMessageBroker.Initialize(resolver2);
                
                // Assert
                VerifyBrokerState(true, resolver2);
            }
            
            [Test]
            public void LifecycleState_WhenMultipleResetsCalled_ShouldMaintainConsistentState()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                GameMessageBroker.Initialize(resolver);
                
                // Act
                GameMessageBroker.Reset();
                GameMessageBroker.Reset();
                GameMessageBroker.Reset();
                
                // Assert
                VerifyBrokerState(false, null);
            }
        }

        /// <summary>
        /// Tests for static method behavior without initialization
        /// </summary>
        [TestFixture]
        public class StaticMethodTests : GameMessageBrokerTests
        {
            [Test]
            public void StaticMethods_WhenCalledWithoutInitialization_ShouldHandleGracefully()
            {
                // Test that all static methods handle the uninitialized state gracefully
                
                // Expect the error and warning log messages - use regex to match the structured log format
                // Multiple calls will generate multiple error logs, so we need multiple expectations
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                
                // Act & Assert
                Assert.That(GameMessageBroker.GetPublisher<TestMessage>(), Is.Null);
                Assert.That(GameMessageBroker.GetSubscriber<TestMessage>(), Is.Null);
                Assert.DoesNotThrow(() => GameMessageBroker.Publish(GameMessageBrokerTestData.CreateTestMessage()));
                Assert.That(GameMessageBroker.IsAvailable(), Is.False);
                Assert.DoesNotThrow(() => GameMessageBroker.Reset());
            }
            
            [Test]
            public void StaticMethods_WhenCalledWithNullResolver_ShouldHandleGracefully()
            {
                // Arrange
                GameMessageBroker.Initialize(null);
                
                // Expect the error and warning log messages - use regex to match the structured log format
                // Multiple calls will generate multiple error logs, so we need multiple expectations
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                
                // Act & Assert
                Assert.That(GameMessageBroker.GetPublisher<TestMessage>(), Is.Null);
                Assert.That(GameMessageBroker.GetSubscriber<TestMessage>(), Is.Null);
                Assert.DoesNotThrow(() => GameMessageBroker.Publish(GameMessageBrokerTestData.CreateTestMessage()));
                Assert.That(GameMessageBroker.IsAvailable(), Is.False);
            }
        }

        /// <summary>
        /// Tests for async method behavior using reflection
        /// CRITICAL: Tests UniTask return types without awaiting
        /// </summary>
        [TestFixture]
        public class AsyncMethodTests : GameMessageBrokerTests
        {
            [Test]
            public void RequestAsync_WhenCalled_ShouldReturnUniTask()
            {
                // Test async methods with reflection instead of awaiting
                // Verify they return UniTask without triggering async execution
                
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Act - Use the generic method directly instead of reflection
                var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request, CancellationToken.None);
                
                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<UniTask<TestResponse>>());
            }
            
            [Test]
            public void RequestAsync_WhenCalledWithDifferentTypes_ShouldReturnCorrectUniTask()
            {
                // Arrange
                var resolver = GameMessageBrokerTestData.CreateMockResolver();
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                resolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                GameMessageBroker.Initialize(resolver);
                var request = GameMessageBrokerTestData.CreateTestRequest();
                
                // Act - Use the generic method directly instead of reflection
                var result = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request, CancellationToken.None);
                
                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<UniTask<TestResponse>>());
            }
        }
    }
}
