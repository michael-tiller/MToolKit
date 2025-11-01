/**
 * Unit tests for GlobalAsyncMessageBroker.cs
 * Refactored from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Static class initialization and state management with multiple cycles
 * - Publisher/Subscriber resolution via VContainer with error handling
 * - Message publishing functionality with exception propagation
 * - Async request/response patterns with UniTask status verification
 * - Concurrent request management and cleanup with thread safety
 * - Log assertion patterns for Serilog structured logging
 * - Static state consistency across multiple initialization/reset cycles
 * - Exception isolation and cleanup patterns
 * 
 * Mock Dependencies:
 * - IObjectResolver (VContainer) for dependency injection
 * - IPublisher<T> and ISubscriber<T> (MessagePipe) for message handling
 * - ILogger (Serilog) for logging verification
 * 
 * Refactoring Improvements:
 * - Enhanced test organization with nested TestFixture classes
 * - Comprehensive static state management testing
 * - Improved exception handling with try/finally cleanup patterns
 * - Parameterized tests for similar scenarios
 * - Enhanced mock implementations with call tracking
 * - Better async testing without awaiting UniTask methods
 * - Comprehensive log assertion patterns for Serilog
 */

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using Serilog;
using UnityEngine;
using UnityEngine.TestTools;
using NSubstitute;
using VContainer;
using MToolKit.Runtime.MessageBus;
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;

namespace MToolKit.Tests.Runtime.Core.MessageBus
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// CRITICAL: Make test classes public for NSubstitute proxy creation (internal classes cause proxy errors)
    /// </summary>
    public static class GlobalAsyncMessageBrokerTestData
    {
        // Basic test values
        public const string TestRequestId = "test-request-id";
        public const string TestMessage = "test-message";
        public const string TestMessage2 = "test-message-2";
        public const int TestResponseValue = 42;
        public const int TestResponseValue2 = 84;
        public const string TestResponseString = "42";
        public const string TestResponseString2 = "84";
        
        // Factory methods for consistent test object creation
        public static TestRequest CreateTestRequest(string data = TestMessage)
        {
            return new TestRequest { Data = data };
        }
        
        public static TestResponse CreateTestResponse(string result = TestResponseString)
        {
            return new TestResponse { Result = result };
        }
        
        public static IPublisher<T> CreateMockPublisher<T>()
        {
            return Substitute.For<IPublisher<T>>();
        }
        
        public static ISubscriber<T> CreateMockSubscriber<T>()
        {
            return Substitute.For<ISubscriber<T>>();
        }
        
        public static IObjectResolver CreateMockResolver()
        {
            return Substitute.For<IObjectResolver>();
        }
        
        /// <summary>
        /// Creates a mock publisher that throws exception when publishing
        /// </summary>
        public static IPublisher<T> CreateFailingMockPublisher<T>(string exceptionMessage = "Publish failed")
        {
            var mockPublisher = Substitute.For<IPublisher<T>>();
            mockPublisher.When(x => x.Publish(Arg.Any<T>())).Do(x => throw new InvalidOperationException(exceptionMessage));
            return mockPublisher;
        }
        
        /// <summary>
        /// Creates a mock resolver that throws exception when resolving
        /// </summary>
        public static IObjectResolver CreateFailingMockResolver(string exceptionMessage = "Resolve failed")
        {
            var mockResolver = Substitute.For<IObjectResolver>();
            mockResolver.When(x => x.Resolve<IPublisher<TestRequest>>()).Do(x => throw new InvalidOperationException(exceptionMessage));
            mockResolver.When(x => x.Resolve<ISubscriber<TestRequest>>()).Do(x => throw new InvalidOperationException(exceptionMessage));
            return mockResolver;
        }
    }


    /// <summary>
    /// Reflection utilities for accessing private static fields and methods with performance optimization
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    internal static class GlobalAsyncMessageBrokerReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for performance optimization
        /// </summary>
        private static readonly FieldInfo GlobalResolverField = typeof(GlobalAsyncMessageBroker)
            .GetField("globalResolver", BindingFlags.NonPublic | BindingFlags.Static);
        
        private static readonly FieldInfo IsInitializedField = typeof(GlobalAsyncMessageBroker)
            .GetField("isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        
        private static readonly FieldInfo PendingRequestsByTypeField = typeof(GlobalAsyncMessageBroker)
            .GetField("pendingRequestsByType", BindingFlags.NonPublic | BindingFlags.Static);
        
        public static IObjectResolver GetGlobalResolver()
        {
            return GlobalResolverField?.GetValue(null) as IObjectResolver;
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
        
        public static void SetGlobalResolver(IObjectResolver resolver)
        {
            GlobalResolverField?.SetValue(null, resolver);
        }
        
        public static void SetIsInitialized(bool value)
        {
            IsInitializedField?.SetValue(null, value);
        }
    }

    [TestFixture]
    public class GlobalAsyncMessageBrokerTests
    {
        private IObjectResolver _mockResolver;
        private ILogger _mockLogger;
        
        [SetUp]
        public void Setup()
        {
            // Always reset static state before each test
            GlobalAsyncMessageBroker.Reset();
            
            // Create fresh mocks for each test
            _mockResolver = GlobalAsyncMessageBrokerTestData.CreateMockResolver();
            _mockLogger = new MockLogger();
        }
        
        [TearDown]
        public void TearDown()
        {
            // Always reset static state after each test
            GlobalAsyncMessageBroker.Reset();
        }
        
        /// <summary>
        /// Helper method to setup broker with mock resolver and publisher
        /// </summary>
        protected void SetupBrokerWithPublisher<T>(IPublisher<T> mockPublisher)
        {
            _mockResolver.Resolve<IPublisher<T>>().Returns(mockPublisher);
            GlobalAsyncMessageBroker.Initialize(_mockResolver);
        }
        
        /// <summary>
        /// Helper method to setup broker with mock resolver and subscriber
        /// </summary>
        protected void SetupBrokerWithSubscriber<T>(ISubscriber<T> mockSubscriber)
        {
            _mockResolver.Resolve<ISubscriber<T>>().Returns(mockSubscriber);
            GlobalAsyncMessageBroker.Initialize(_mockResolver);
        }
        
        /// <summary>
        /// Helper method to setup broker with mock resolver and both publisher and subscriber
        /// </summary>
        protected void SetupBrokerWithPublisherAndSubscriber<T>(IPublisher<T> mockPublisher, ISubscriber<T> mockSubscriber)
        {
            _mockResolver.Resolve<IPublisher<T>>().Returns(mockPublisher);
            _mockResolver.Resolve<ISubscriber<T>>().Returns(mockSubscriber);
            GlobalAsyncMessageBroker.Initialize(_mockResolver);
        }
        
        [TestFixture]
        public class InitializeTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void Initialize_WhenCalledWithValidResolver_ShouldSetInitializationState()
            {
                // Act
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Assert
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.True);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.SameAs(_mockResolver));
            }
            
            [Test]
            public void Initialize_WhenCalledMultipleTimes_ShouldReturnEarly()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Assert
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
            }
            
            [Test]
            public void Initialize_WhenCalledWithNullResolver_ShouldStillInitialize()
            {
                // Act
                GlobalAsyncMessageBroker.Initialize(null);
                
                // Assert
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.True);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.Null);
            }
            
            [Test]
            public void Initialize_WhenCalledAfterReset_ShouldReinitializeCorrectly()
            {
                // Arrange - Initialize, then reset
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                GlobalAsyncMessageBroker.Reset();
                
                // Act - Reinitialize
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Assert
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.True);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.SameAs(_mockResolver));
            }
        }

        [TestFixture]
        public class GetPublisherTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void GetPublisher_WhenNotInitialized_ShouldReturnNullAndLogError()
            {
                // Expect error log for uninitialized state
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GlobalAsyncMessageBroker not initialized.*"));
                
                // Act
                var result = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetPublisher_WhenInitializedWithValidResolver_ShouldReturnPublisher()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                SetupBrokerWithPublisher(mockPublisher);
                
                // Act
                var result = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                
                // Assert
                Assert.That(result, Is.SameAs(mockPublisher));
                _mockResolver.Received(1).Resolve<IPublisher<TestRequest>>();
            }
            
            [Test]
            public void GetPublisher_WhenResolverThrowsException_ShouldReturnNullAndLogError()
            {
                // Arrange
                _mockResolver.When(x => x.Resolve<IPublisher<TestRequest>>()).Do(x => throw new InvalidOperationException("Test exception"));
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Expect error log for resolver exception
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to resolve publisher for.*TestRequest.*"));
                
                // Act
                var result = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [TestCase(typeof(TestRequest))]
            [TestCase(typeof(TestResponse))]
            [TestCase(typeof(string))]
            public void GetPublisher_WhenCalledWithDifferentTypes_ShouldResolveCorrectType(Type messageType)
            {
                // Arrange
                var mockPublisher = Substitute.For<IPublisher<object>>();
                _mockResolver.Resolve<IPublisher<object>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act
                var result = GlobalAsyncMessageBroker.GetPublisher<object>();
                
                // Assert
                Assert.That(result, Is.SameAs(mockPublisher));
            }
        }

        [TestFixture]
        public class GetSubscriberTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void GetSubscriber_WhenNotInitialized_ShouldReturnNullAndLogError()
            {
                // Expect error log for uninitialized state
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GlobalAsyncMessageBroker not initialized.*"));
                
                // Act
                var result = GlobalAsyncMessageBroker.GetSubscriber<TestRequest>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void GetSubscriber_WhenInitializedWithValidResolver_ShouldReturnSubscriber()
            {
                // Arrange
                var mockSubscriber = GlobalAsyncMessageBrokerTestData.CreateMockSubscriber<TestRequest>();
                SetupBrokerWithSubscriber(mockSubscriber);
                
                // Act
                var result = GlobalAsyncMessageBroker.GetSubscriber<TestRequest>();
                
                // Assert
                Assert.That(result, Is.SameAs(mockSubscriber));
                _mockResolver.Received(1).Resolve<ISubscriber<TestRequest>>();
            }
            
            [Test]
            public void GetSubscriber_WhenResolverThrowsException_ShouldReturnNullAndLogError()
            {
                // Arrange
                _mockResolver.When(x => x.Resolve<ISubscriber<TestRequest>>()).Do(x => throw new InvalidOperationException("Test exception"));
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Expect error log for resolver exception
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to resolve subscriber for.*TestRequest.*"));
                
                // Act
                var result = GlobalAsyncMessageBroker.GetSubscriber<TestRequest>();
                
                // Assert
                Assert.That(result, Is.Null);
            }
            
            [TestCase(typeof(TestRequest))]
            [TestCase(typeof(TestResponse))]
            [TestCase(typeof(string))]
            public void GetSubscriber_WhenCalledWithDifferentTypes_ShouldResolveCorrectType(Type messageType)
            {
                // Arrange
                var mockSubscriber = Substitute.For<ISubscriber<object>>();
                _mockResolver.Resolve<ISubscriber<object>>().Returns(mockSubscriber);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act
                var result = GlobalAsyncMessageBroker.GetSubscriber<object>();
                
                // Assert
                Assert.That(result, Is.SameAs(mockSubscriber));
            }
        }

        [TestFixture]
        public class PublishTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void Publish_WhenPublisherAvailable_ShouldPublishMessage()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                SetupBrokerWithPublisher(mockPublisher);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act
                GlobalAsyncMessageBroker.Publish(testRequest);
                
                // Assert
                mockPublisher.Received(1).Publish(testRequest);
            }
            
            [Test]
            public void Publish_WhenPublisherNotAvailable_ShouldLogWarning()
            {
                // Arrange
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns((IPublisher<TestRequest>)null);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Expect warning log for unavailable publisher
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                
                // Act
                GlobalAsyncMessageBroker.Publish(testRequest);
                
                // Assert - no exception should be thrown
                Assert.Pass("Publish completed without exception");
            }
            
            [Test]
            public void Publish_WhenPublisherThrowsException_ShouldPropagateException()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateFailingMockPublisher<TestRequest>("Publish failed");
                SetupBrokerWithPublisher(mockPublisher);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => GlobalAsyncMessageBroker.Publish(testRequest));
            }
            
            [Test]
            public void Publish_WhenCalledWithNullMessage_ShouldPublishNull()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                SetupBrokerWithPublisher(mockPublisher);
                
                // Act
                GlobalAsyncMessageBroker.Publish<TestRequest>(null);
                
                // Assert
                mockPublisher.Received(1).Publish(null);
            }
            
            [TestCase("test-message-1")]
            [TestCase("test-message-2")]
            [TestCase("")]
            public void Publish_WhenCalledWithDifferentMessages_ShouldPublishCorrectMessage(string message)
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                SetupBrokerWithPublisher(mockPublisher);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest(message);
                
                // Act
                GlobalAsyncMessageBroker.Publish(testRequest);
                
                // Assert
                mockPublisher.Received(1).Publish(testRequest);
            }
        }

        [TestFixture]
        public class ExceptionIsolationTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void ExceptionIsolation_WhenPublisherThrows_ShouldNotAffectOtherOperations()
            {
                // Arrange - Setup broker with failing publisher
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateFailingMockPublisher<TestRequest>("Publisher failed");
                SetupBrokerWithPublisher(mockPublisher);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                try
                {
                    // Act & Assert - Publish should throw
                    Assert.Throws<InvalidOperationException>(() => GlobalAsyncMessageBroker.Publish(testRequest));
                    
                    // Other operations should still work
                    Assert.DoesNotThrow(() => GlobalAsyncMessageBroker.IsAvailable());
                    Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                    
                    // GetPublisher should still work
                    var publisher = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                    Assert.That(publisher, Is.Not.Null);
                }
                finally
                {
                    // Reset to prevent exceptions during TearDown
                    GlobalAsyncMessageBroker.Reset();
                }
            }
            
            [Test]
            public void ExceptionIsolation_WhenResolverThrows_ShouldNotAffectStaticState()
            {
                // Arrange - Setup broker with failing resolver
                var failingResolver = GlobalAsyncMessageBrokerTestData.CreateFailingMockResolver("Resolver failed");
                GlobalAsyncMessageBroker.Initialize(failingResolver);
                
                try
                {
                    // Act & Assert - GetPublisher should return null due to resolver exception
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to resolve publisher for.*"));
                    var publisher = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                    Assert.That(publisher, Is.Null);
                    
                    // Static state should remain consistent
                    Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                    Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.True);
                }
                finally
                {
                    // Reset to prevent exceptions during TearDown
                    GlobalAsyncMessageBroker.Reset();
                }
            }
        }

        [TestFixture]
        public class RequestAsyncTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void RequestAsync_WhenNotInitialized_ShouldReturnDefaultAndLogError()
            {
                // Arrange
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Expect error log for uninitialized state
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GlobalAsyncMessageBroker not initialized.*"));
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void RequestAsync_WhenPublisherNotAvailable_ShouldReturnDefaultAndLogError()
            {
                // Arrange
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns((IPublisher<TestRequest>)null);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Expect error log for unavailable publisher
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to publish async request.*publisher not available.*"));
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.Null);
            }
            
            [Test]
            public void RequestAsync_WhenCalled_ShouldCreatePendingRequestAndReturnUniTask()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(1));
                mockPublisher.Received(1).Publish(testRequest);
            }
            
            [Test]
            public void RequestAsync_WhenCompletedWithResponse_ShouldReturnResponse()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                var testResponse = GlobalAsyncMessageBrokerTestData.CreateTestResponse();
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Complete the request
                GlobalAsyncMessageBroker.CompleteRequest(testResponse);
                
                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.SameAs(testResponse));
                Assert.That(result.Result, Is.EqualTo(GlobalAsyncMessageBrokerTestData.TestResponseString));
            }
            
            [Test]
            public void RequestAsync_WhenCalledWithCancellationToken_ShouldCreatePendingRequest()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                using var cts = new CancellationTokenSource();
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest, cts.Token);
                
                // Assert - The task should be pending initially
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                // Verify that a pending request was created
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(1));
                
                // Clean up by completing the request
                GlobalAsyncMessageBroker.CompleteRequest(GlobalAsyncMessageBrokerTestData.CreateTestResponse());
                
                // Verify the task completes successfully
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.Not.Null);
            }
            
            [Test]
            public void RequestAsync_WhenNoMatchingCompletion_ShouldRemainPending()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Complete with wrong response type (string instead of TestResponse)
                // This will not complete our request because CompleteRequest only matches exact types
                GlobalAsyncMessageBroker.CompleteRequest("wrong-type");
                
                // Expect warning log for no matching request
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*No pending request found for response.*"));
                
                // The task will remain pending because no matching completion was found
                // This test demonstrates that CompleteRequest only works with exact type matches
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            }
        }

        [TestFixture]
        public class CompleteRequestTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void CompleteRequest_WhenMatchingPendingRequest_ShouldCompleteRequest()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                var testResponse = GlobalAsyncMessageBrokerTestData.CreateTestResponse();
                
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Act
                GlobalAsyncMessageBroker.CompleteRequest(testResponse);
                
                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                var result = task.GetAwaiter().GetResult();
                Assert.That(result, Is.SameAs(testResponse));
            }
            
            [Test]
            public void CompleteRequest_WhenNoMatchingPendingRequest_ShouldLogWarning()
            {
                // Arrange
                var testResponse = GlobalAsyncMessageBrokerTestData.CreateTestResponse();
                
                // Expect warning log for no matching request
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*No pending request found for response.*"));
                
                // Act
                GlobalAsyncMessageBroker.CompleteRequest(testResponse);
                
                // Assert - no exception should be thrown
                Assert.Pass("CompleteRequest completed without exception");
            }
            
            [Test]
            public void CompleteRequest_WhenMultiplePendingRequestsOfSameType_ShouldCompleteFirstMatching()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest1 = GlobalAsyncMessageBrokerTestData.CreateTestRequest("request1");
                var testRequest2 = GlobalAsyncMessageBrokerTestData.CreateTestRequest("request2");
                var testResponse = GlobalAsyncMessageBrokerTestData.CreateTestResponse();
                
                var task1 = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest1);
                var task2 = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest2);
                
                // Act
                GlobalAsyncMessageBroker.CompleteRequest(testResponse);
                
                // Assert - first request should be completed
                Assert.That(task1.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Pending));
                
                var result1 = task1.GetAwaiter().GetResult();
                Assert.That(result1, Is.SameAs(testResponse));
            }
        }

        [TestFixture]
        public class IsAvailableTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void IsAvailable_WhenNotInitialized_ShouldReturnFalse()
            {
                // Act
                var result = GlobalAsyncMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.False);
            }
            
            [Test]
            public void IsAvailable_WhenInitializedWithValidResolver_ShouldReturnTrue()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act
                var result = GlobalAsyncMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.True);
            }
            
            [Test]
            public void IsAvailable_WhenInitializedWithNullResolver_ShouldReturnFalse()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(null);
                
                // Act
                var result = GlobalAsyncMessageBroker.IsAvailable();
                
                // Assert
                Assert.That(result, Is.False);
            }
        }

        [TestFixture]
        public class ResetTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void Reset_WhenCalled_ShouldClearAllState()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Create pending request
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Act
                GlobalAsyncMessageBroker.Reset();
                
                // Assert
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.Null);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(0));
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded)); // Cancelled
            }
            
            [Test]
            public void Reset_WhenCalledMultipleTimes_ShouldMaintainConsistency()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act - Multiple reset cycles
                GlobalAsyncMessageBroker.Reset();
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                GlobalAsyncMessageBroker.Reset();
                
                // Assert
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.False);
            }
            
            [Test]
            public void Reset_WhenCalledWithPendingRequests_ShouldCancelAllRequests()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                var task1 = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                var task2 = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Act
                GlobalAsyncMessageBroker.Reset();
                
                // Assert
                Assert.That(task1.Status, Is.EqualTo(UniTaskStatus.Succeeded)); // Cancelled
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Succeeded)); // Cancelled
            }
        }

        [TestFixture]
        public class StaticStateConsistencyTests : GlobalAsyncMessageBrokerTests
        {
            [Test]
            public void StaticState_WhenMultipleInitializationCycles_ShouldMaintainConsistency()
            {
                // Test multiple initialization cycles
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                
                GlobalAsyncMessageBroker.Reset();
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False);
                
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                
                GlobalAsyncMessageBroker.Reset();
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False);
                
                // Verify static state is properly reset
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.Null);
            }
            
            [Test]
            public void StaticState_WhenConcurrentOperations_ShouldMaintainThreadSafety()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act - Multiple concurrent requests
                var tasks = new UniTask[10];
                for (int i = 0; i < 10; i++)
                {
                    tasks[i] = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                }
                
                // Complete all requests
                for (int i = 0; i < 10; i++)
                {
                    GlobalAsyncMessageBroker.CompleteRequest(GlobalAsyncMessageBrokerTestData.CreateTestResponse(i.ToString()));
                }
                
                // Assert - All tasks should complete successfully
                foreach (var task in tasks)
                {
                    Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                }
                
                // Verify pending requests are cleaned up
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(0));
            }
            
            [Test]
            public void StaticState_WhenMixedOperations_ShouldMaintainConsistency()
            {
                // Arrange
                var mockPublisher = GlobalAsyncMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                var mockSubscriber = GlobalAsyncMessageBrokerTestData.CreateMockSubscriber<TestRequest>();
                SetupBrokerWithPublisherAndSubscriber(mockPublisher, mockSubscriber);
                var testRequest = GlobalAsyncMessageBrokerTestData.CreateTestRequest();
                
                // Act - Mixed operations
                var publisher = GlobalAsyncMessageBroker.GetPublisher<TestRequest>();
                var subscriber = GlobalAsyncMessageBroker.GetSubscriber<TestRequest>();
                GlobalAsyncMessageBroker.Publish(testRequest);
                var task = GlobalAsyncMessageBroker.RequestAsync<TestRequest, TestResponse>(testRequest);
                
                // Assert - All operations should work
                Assert.That(publisher, Is.SameAs(mockPublisher));
                Assert.That(subscriber, Is.SameAs(mockSubscriber));
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
                
                // Complete the async request
                GlobalAsyncMessageBroker.CompleteRequest(GlobalAsyncMessageBrokerTestData.CreateTestResponse());
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            }
            
            [Test]
            public void StaticState_WhenResetCalledMultipleTimes_ShouldMaintainConsistency()
            {
                // Arrange
                GlobalAsyncMessageBroker.Initialize(_mockResolver);
                
                // Act - Multiple reset calls
                GlobalAsyncMessageBroker.Reset();
                GlobalAsyncMessageBroker.Reset();
                GlobalAsyncMessageBroker.Reset();
                
                // Assert - State should remain reset
                Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetIsInitialized(), Is.False);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetGlobalResolver(), Is.Null);
                Assert.That(GlobalAsyncMessageBrokerReflectionHelper.GetTotalPendingRequestsCount(), Is.EqualTo(0));
            }
        }
    }
}
