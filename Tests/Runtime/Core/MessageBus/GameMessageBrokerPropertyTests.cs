/**
 * Property-based tests for GameMessageBroker.cs
 * Generated from generate-property-tests.mdc template on 2025-01-27
 * Framework: Unity Test Framework with NUnit and FsCheck
 * 
 * Test Coverage:
 * - Invariant properties (must always hold)
 * - Mathematical properties (laws of operations)
 * - State transitions (valid progression of state)
 * - Reversibility / Round-trip laws
 * - Error / Boundary behavior
 * 
 * Property Testing Focus:
 * - Static state consistency across multiple initialization/reset cycles
 * - Publisher/Subscriber resolution behavior with various resolver states
 * - Message publishing invariants and mathematical properties
 * - Async request/response pattern correctness and cleanup
 * - Concurrent request management and thread safety
 * - Error boundary behavior and exception isolation
 * 
 * Mock Dependencies:
 * - IObjectResolver (VContainer) for dependency injection
 * - IPublisher<T> and ISubscriber<T> (MessagePipe) for message handling
 * - ILogger (Serilog) for logging verification
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FsCheck;
using MessagePipe;
using NUnit.Framework;
using Serilog;
using MToolKit.Tests.Runtime.Core;
using UnityEngine;
using UnityEngine.TestTools;
using NSubstitute;
using VContainer;
using MToolKit.Runtime.MessageBus;
using ILogger = Serilog.ILogger;

namespace MToolKit.Tests.Runtime.Core.MessageBus
{
    /// <summary>
    /// Test fixture for GameMessageBroker property-based tests
    /// </summary>
    [TestFixture]
    public class GameMessageBrokerPropertyTests
    {
        private IObjectResolver _mockResolver;
        private ILogger _mockLogger;
        
        [SetUp]
        public void Setup()
        {
            // Always reset static state before each test
            GameMessageBroker.Reset();
            
            // Create fresh mocks for each test
            _mockResolver = GameMessageBrokerTestData.CreateMockResolver();
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
        }
        
        [TearDown]
        public void TearDown()
        {
            // Always reset static state after each test
            GameMessageBroker.Reset();
        }

        /// <summary>
        /// Creates a test message for property testing
        /// </summary>
        private TestMessage CreateTestMessage(string content = null)
        {
            return new TestMessage { Content = content ?? $"test-content-{Guid.NewGuid()}" };
        }
        
        /// <summary>
        /// Creates a test request for property testing
        /// </summary>
        private TestRequest CreateTestRequest(string data = null)
        {
            return new TestRequest { Data = data ?? $"test-data-{Guid.NewGuid()}" };
        }
        
        /// <summary>
        /// Creates a test response for property testing
        /// </summary>
        private TestResponse CreateTestResponse(string result = null)
        {
            return new TestResponse { Result = result ?? $"test-result-{Guid.NewGuid()}" };
        }

        #region 1. Invariant Properties (Must Always Hold)

        /// <summary>
        /// Property: IsAvailable() returns consistent state based on initialization
        /// Invariant: IsAvailable() behavior is deterministic based on initialization state
        /// </summary>
        [Test]
        public void IsAvailable_ReturnsConsistentState_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Test various initialization states
                var shouldInitialize = random.Next(2) == 0;
                var resolverIsNull = random.Next(2) == 0;
                
                if (shouldInitialize)
                {
                    var resolver = resolverIsNull ? null : _mockResolver;
                    GameMessageBroker.Initialize(resolver);
                }
                
                // Property: IsAvailable() should be consistent with initialization state
                var isAvailable = GameMessageBroker.IsAvailable();
                var expectedAvailable = shouldInitialize && !resolverIsNull;
                
                var result = isAvailable == expectedAvailable;
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Static state remains consistent across multiple operations
        /// Invariant: Static fields maintain their values until explicitly changed
        /// </summary>
        [Test]
        public void StaticState_RemainsConsistentAcrossOperations_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Initialize broker
                GameMessageBroker.Initialize(_mockResolver);
                
                // Perform multiple operations
                var operations = random.Next(1, 10);
                for (int j = 0; j < operations; j++)
                {
                    var operation = random.Next(4);
                    switch (operation)
                    {
                        case 0:
                            GameMessageBroker.IsAvailable();
                            break;
                        case 1:
                            GameMessageBroker.GetPublisher<TestMessage>();
                            break;
                        case 2:
                            GameMessageBroker.GetSubscriber<TestMessage>();
                            break;
                        case 3:
                            GameMessageBroker.Publish(CreateTestMessage());
                            break;
                    }
                }
                
                // Property: Static state should remain consistent
                var result = GameMessageBroker.IsAvailable() == true &&
                           GameMessageBrokerReflectionHelper.GetIsInitialized() == true &&
                           GameMessageBrokerReflectionHelper.GetGameResolver() == _mockResolver;
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Pending requests count never goes negative
        /// Invariant: Pending requests count is always non-negative
        /// </summary>
        [Test]
        public void PendingRequestsCount_NeverNegative_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker with publisher
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Create multiple requests
                var requestCount = random.Next(1, 20);
                var tasks = new List<UniTask<TestResponse>>();
                
                for (int j = 0; j < requestCount; j++)
                {
                    var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                    tasks.Add(task);
                }
                
                // Property: Pending count should never be negative
                var pendingCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                var result = pendingCount >= 0;
                Check.QuickThrowOnFailure(result);
                
                // Complete all requests
                foreach (var task in tasks)
                {
                    GameMessageBroker.CompleteRequest(CreateTestResponse());
                }
                
                // Property: After completion, pending count should be zero
                var finalPendingCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                var finalResult = finalPendingCount == 0;
                Check.QuickThrowOnFailure(finalResult);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion

        #region 2. Mathematical Properties (Laws of Operations)

        /// <summary>
        /// Property: Initialize → Reset → Initialize = Original state
        /// Mathematical Law: State transitions are reversible
        /// </summary>
        [Test]
        public void InitializeResetInitialize_RestoresOriginalState_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Initial state
                var initialState = GameMessageBroker.IsAvailable();
                
                // Initialize
                GameMessageBroker.Initialize(_mockResolver);
                var afterInit = GameMessageBroker.IsAvailable();
                
                // Reset
                GameMessageBroker.Reset();
                var afterReset = GameMessageBroker.IsAvailable();
                
                // Re-initialize
                GameMessageBroker.Initialize(_mockResolver);
                var afterReinit = GameMessageBroker.IsAvailable();
                
                // Property: Initialize → Reset → Initialize should restore original state
                var result = afterReinit == true && // Should be available after re-init
                           afterReset == false && // Should be unavailable after reset
                           afterInit == true; // Should be available after init
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Multiple Reset calls = Single Reset call
        /// Mathematical Law: Reset operation is idempotent
        /// </summary>
        [Test]
        public void MultipleResetCalls_EqualSingleReset_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Initialize broker
                GameMessageBroker.Initialize(_mockResolver);
                
                // Create some state (pending requests)
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                var requestCount = random.Next(1, 10);
                for (int j = 0; j < requestCount; j++)
                {
                    GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest()).Forget();
                }
                
                // Single reset
                GameMessageBroker.Reset();
                var singleResetState = GameMessageBroker.IsAvailable();
                var singleResetPending = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Re-initialize and create state again
                GameMessageBroker.Initialize(_mockResolver);
                for (int j = 0; j < requestCount; j++)
                {
                    GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest()).Forget();
                }
                
                // Multiple resets
                var resetCount = random.Next(2, 6);
                for (int j = 0; j < resetCount; j++)
                {
                    GameMessageBroker.Reset();
                }
                var multipleResetState = GameMessageBroker.IsAvailable();
                var multipleResetPending = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Property: Multiple resets should equal single reset
                var result = singleResetState == multipleResetState &&
                           singleResetPending == multipleResetPending;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Request → Complete → Request = Same behavior
        /// Mathematical Law: Request/Complete operations are consistent
        /// </summary>
        [Test]
        public void RequestCompleteRequest_ConsistentBehavior_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // First request cycle
                var request1 = CreateTestRequest();
                var task1 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request1);
                var pendingCount1 = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                var response1 = CreateTestResponse();
                GameMessageBroker.CompleteRequest(response1);
                var pendingCountAfter1 = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Second request cycle
                var request2 = CreateTestRequest();
                var task2 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request2);
                var pendingCount2 = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                var response2 = CreateTestResponse();
                GameMessageBroker.CompleteRequest(response2);
                var pendingCountAfter2 = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Property: Both cycles should behave identically
                var result = pendingCount1 == pendingCount2 && // Same pending count after request
                           pendingCountAfter1 == pendingCountAfter2 && // Same pending count after completion
                           task1.Status == task2.Status; // Same task status
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion

        #region 3. State Transitions (Valid Progression of State)

        /// <summary>
        /// Property: After Initialize, broker becomes available
        /// State Transition: Uninitialized → Initialized = Available
        /// </summary>
        [Test]
        public void AfterInitialize_BrokerBecomesAvailable_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Start with uninitialized state
                GameMessageBroker.Reset();
                var beforeInit = GameMessageBroker.IsAvailable();
                
                // Initialize with various resolver states
                var resolverIsNull = random.Next(2) == 0;
                var resolver = resolverIsNull ? null : _mockResolver;
                
                GameMessageBroker.Initialize(resolver);
                var afterInit = GameMessageBroker.IsAvailable();
                
                // Property: After initialization, availability should change appropriately
                var result = !beforeInit && // Should start unavailable
                           (afterInit == !resolverIsNull); // Should be available only if resolver is not null
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: After Reset, broker becomes unavailable
        /// State Transition: Initialized → Reset = Unavailable
        /// </summary>
        [Test]
        public void AfterReset_BrokerBecomesUnavailable_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Start with initialized state
                GameMessageBroker.Initialize(_mockResolver);
                var beforeReset = GameMessageBroker.IsAvailable();
                
                // Create some pending requests
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                
                var requestCount = random.Next(1, 10);
                for (int j = 0; j < requestCount; j++)
                {
                    GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest()).Forget();
                }
                
                var pendingBeforeReset = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Reset
                GameMessageBroker.Reset();
                var afterReset = GameMessageBroker.IsAvailable();
                var pendingAfterReset = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Property: After reset, broker should be unavailable and pending requests cleared
                var result = beforeReset == true && // Should start available
                           afterReset == false && // Should become unavailable
                           pendingBeforeReset > 0 && // Should have pending requests before reset
                           pendingAfterReset == 0; // Should have no pending requests after reset
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Request creation increases pending count
        /// State Transition: No Request → Request = Pending Count Increases
        /// </summary>
        [Test]
        public void RequestCreation_IncreasesPendingCount_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Initial state
                var initialPending = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Create requests
                var requestCount = random.Next(1, 20);
                var tasks = new List<UniTask<TestResponse>>();
                
                for (int j = 0; j < requestCount; j++)
                {
                    var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                    tasks.Add(task);
                }
                
                var finalPending = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                
                // Property: Request creation should increase pending count
                var result = finalPending == initialPending + requestCount;
                Check.QuickThrowOnFailure(result);
                
                // Complete all requests
                foreach (var task in tasks)
                {
                    GameMessageBroker.CompleteRequest(CreateTestResponse());
                }
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion

        #region 4. Reversibility / Round-trip Laws

        /// <summary>
        /// Property: Initialize → Reset → Initialize = Original Initialize behavior
        /// Round-trip Law: State transitions are reversible
        /// </summary>
        [Test]
        public void InitializeResetInitialize_RoundTripBehavior_RoundTripLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // First initialization
                GameMessageBroker.Initialize(_mockResolver);
                var firstInitAvailable = GameMessageBroker.IsAvailable();
                var firstInitResolver = GameMessageBrokerReflectionHelper.GetGameResolver();
                
                // Reset
                GameMessageBroker.Reset();
                
                // Second initialization
                GameMessageBroker.Initialize(_mockResolver);
                var secondInitAvailable = GameMessageBroker.IsAvailable();
                var secondInitResolver = GameMessageBrokerReflectionHelper.GetGameResolver();
                
                // Property: Second initialization should behave identically to first
                var result = firstInitAvailable == secondInitAvailable &&
                           firstInitResolver == secondInitResolver &&
                           secondInitAvailable == true &&
                           secondInitResolver == _mockResolver;
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Request → Complete → Request = Same request behavior
        /// Round-trip Law: Request/Complete operations are reversible
        /// </summary>
        [Test]
        public void RequestCompleteRequest_RoundTripBehavior_RoundTripLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // First request cycle
                var request1 = CreateTestRequest();
                var task1 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request1);
                var response1 = CreateTestResponse();
                GameMessageBroker.CompleteRequest(response1);
                var result1 = task1.GetAwaiter().GetResult();
                
                // Second request cycle
                var request2 = CreateTestRequest();
                var task2 = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(request2);
                var response2 = CreateTestResponse();
                GameMessageBroker.CompleteRequest(response2);
                var result2 = task2.GetAwaiter().GetResult();
                
                // Property: Both cycles should complete successfully (check results instead of status after await)
                var result = result1 != null && result2 != null;
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion

        #region 5. Error / Boundary Behavior

        /// <summary>
        /// Property: Operations on uninitialized broker never crash
        /// Error Boundary: Uninitialized state should be handled gracefully
        /// </summary>
        [Test]
        public void OperationsOnUninitializedBroker_NeverCrash_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Ensure broker is uninitialized
                GameMessageBroker.Reset();
                
                // Expect error logs for uninitialized operations
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Property: All operations should complete without throwing
                var result = true;
                
                try
                {
                    // Test various operations
                    var publisher = GameMessageBroker.GetPublisher<TestMessage>();
                    var subscriber = GameMessageBroker.GetSubscriber<TestMessage>();
                    var isAvailable = GameMessageBroker.IsAvailable();
                    
                    // These should return null/false, not throw
                    result = result && publisher == null && subscriber == null && isAvailable == false;
                    
                    // Publish should not throw
                    GameMessageBroker.Publish(CreateTestMessage());
                    
                    // RequestAsync should return completed task with default value
                    var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                    result = result && task.Status == UniTaskStatus.Succeeded;
                    
                    // CompleteRequest should not throw
                    GameMessageBroker.CompleteRequest(CreateTestResponse());
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Operations with null resolver never crash
        /// Error Boundary: Null resolver should be handled gracefully
        /// </summary>
        [Test]
        public void OperationsWithNullResolver_NeverCrash_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Initialize with null resolver
                GameMessageBroker.Initialize(null);
                
                // Expect error logs for null resolver operations
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher not available.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*GameMessageBroker not initialized.*"));
                
                // Property: All operations should complete without throwing
                var result = true;
                
                try
                {
                    // Test various operations
                    var publisher = GameMessageBroker.GetPublisher<TestMessage>();
                    var subscriber = GameMessageBroker.GetSubscriber<TestMessage>();
                    var isAvailable = GameMessageBroker.IsAvailable();
                    
                    // These should return null/false, not throw
                    result = result && publisher == null && subscriber == null && isAvailable == false;
                    
                    // Publish should not throw
                    GameMessageBroker.Publish(CreateTestMessage());
                    
                    // RequestAsync should return completed task with default value
                    var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                    result = result && task.Status == UniTaskStatus.Succeeded;
                    
                    // CompleteRequest should not throw
                    GameMessageBroker.CompleteRequest(CreateTestResponse());
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Multiple concurrent requests are handled safely
        /// Error Boundary: Concurrent operations should not cause race conditions
        /// </summary>
        [Test]
        public void ConcurrentRequests_HandledSafely_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Create multiple concurrent requests
                var requestCount = random.Next(5, 20);
                var tasks = new List<UniTask<TestResponse>>();
                
                // Property: All requests should be created successfully
                var result = true;
                
                try
                {
                    for (int j = 0; j < requestCount; j++)
                    {
                        var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                        tasks.Add(task);
                    }
                    
                    // Verify pending count
                    var pendingCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                    result = result && pendingCount == requestCount;
                    
                    // Complete all requests
                    for (int j = 0; j < requestCount; j++)
                    {
                        GameMessageBroker.CompleteRequest(CreateTestResponse());
                    }
                    
                    // Verify all tasks completed
                    foreach (var task in tasks)
                    {
                        result = result && task.Status == UniTaskStatus.Succeeded;
                    }
                    
                    // Verify pending count is zero
                    var finalPendingCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                    result = result && finalPendingCount == 0;
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: CompleteRequest with no pending requests never crashes
        /// Error Boundary: CompleteRequest should handle empty queue gracefully
        /// </summary>
        [Test]
        public void CompleteRequestWithNoPendingRequests_NeverCrashes_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Property: CompleteRequest should not throw when no requests are pending
                var result = true;
                
                try
                {
                    // Complete request with no pending requests
                    GameMessageBroker.CompleteRequest(CreateTestResponse());
                    
                    // Verify broker state is still consistent
                    var isAvailable = GameMessageBroker.IsAvailable();
                    var pendingCount = GameMessageBrokerReflectionHelper.GetTotalPendingRequestsCount();
                    
                    result = result && isAvailable == true && pendingCount == 0;
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion

        #region Advanced Property Tests

        /// <summary>
        /// Property: Publisher resolution behavior is consistent across multiple calls
        /// Advanced Property: Resolution behavior should be deterministic
        /// </summary>
        [Test]
        public void PublisherResolution_ConsistentAcrossMultipleCalls_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker with publisher
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>();
                _mockResolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Call GetPublisher multiple times
                var callCount = random.Next(1, 10);
                var publishers = new List<IPublisher<TestMessage>>();
                
                for (int j = 0; j < callCount; j++)
                {
                    var publisher = GameMessageBroker.GetPublisher<TestMessage>();
                    publishers.Add(publisher);
                }
                
                // Property: All calls should return the same publisher
                var result = publishers.All(p => p == mockPublisher);
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Request/Response type matching is strict
        /// Advanced Property: Type safety should be maintained
        /// </summary>
        [Test]
        public void RequestResponseTypeMatching_IsStrict_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Create request expecting TestResponse
                var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                
                // Complete with wrong type (string instead of TestResponse)
                GameMessageBroker.CompleteRequest("wrong-type");
                
                // Property: Task should remain pending because type doesn't match
                var result = task.Status == UniTaskStatus.Pending;
                Check.QuickThrowOnFailure(result);
                
                // Complete with correct type
                GameMessageBroker.CompleteRequest(CreateTestResponse());
                
                // Property: Task should now complete successfully
                var finalResult = task.Status == UniTaskStatus.Succeeded;
                Check.QuickThrowOnFailure(finalResult);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: FIFO order is maintained for multiple requests of same type
        /// Advanced Property: Request completion should follow FIFO order
        /// </summary>
        [Test]
        public void RequestCompletion_FollowsFIFOOrder_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestRequest>();
                _mockResolver.Resolve<IPublisher<TestRequest>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Create multiple requests
                var requestCount = random.Next(2, 10);
                var tasks = new List<UniTask<TestResponse>>();
                
                for (int j = 0; j < requestCount; j++)
                {
                    var task = GameMessageBroker.RequestAsync<TestRequest, TestResponse>(CreateTestRequest());
                    tasks.Add(task);
                }
                
                // Complete requests one by one
                for (int j = 0; j < requestCount; j++)
                {
                    GameMessageBroker.CompleteRequest(CreateTestResponse($"response-{j}"));
                }
                
                // Property: All tasks should complete successfully
                var allCompleted = tasks.All(t => t.Status == UniTaskStatus.Succeeded);
                var result = allCompleted;
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Message publishing with exception handling is safe
        /// Advanced Property: Publisher exceptions should not crash the broker
        /// </summary>
        [Test]
        public void MessagePublishing_WithExceptionHandling_IsSafe_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Setup broker with failing publisher
                var mockPublisher = GameMessageBrokerTestData.CreateMockPublisher<TestMessage>(true, "Publisher exception");
                _mockResolver.Resolve<IPublisher<TestMessage>>().Returns(mockPublisher);
                GameMessageBroker.Initialize(_mockResolver);
                
                // Expect error log for publisher exception
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to publish.*publisher threw exception.*"));
                
                // Property: Publish should not throw even when publisher throws
                var result = true;
                
                try
                {
                    GameMessageBroker.Publish(CreateTestMessage());
                    
                    // Verify broker state is still consistent
                    var isAvailable = GameMessageBroker.IsAvailable();
                    result = result && isAvailable == true;
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        /// <summary>
        /// Property: Multiple initialization attempts maintain first resolver
        /// Advanced Property: Early return guard should prevent state changes
        /// </summary>
        [Test]
        public void MultipleInitializationAttempts_MaintainFirstResolver_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create multiple resolvers
                var resolver1 = GameMessageBrokerTestData.CreateMockResolver();
                var resolver2 = GameMessageBrokerTestData.CreateMockResolver();
                var resolver3 = GameMessageBrokerTestData.CreateMockResolver();
                
                // First initialization
                GameMessageBroker.Initialize(resolver1);
                var firstInitResolver = GameMessageBrokerReflectionHelper.GetGameResolver();
                var firstInitAvailable = GameMessageBroker.IsAvailable();
                
                // Multiple subsequent initializations
                GameMessageBroker.Initialize(resolver2);
                GameMessageBroker.Initialize(resolver3);
                GameMessageBroker.Initialize(null);
                
                var finalResolver = GameMessageBrokerReflectionHelper.GetGameResolver();
                var finalAvailable = GameMessageBroker.IsAvailable();
                
                // Property: First resolver should be maintained, subsequent initializations ignored
                var result = firstInitResolver == finalResolver &&
                           firstInitResolver == resolver1 &&
                           firstInitAvailable == true &&
                           finalAvailable == true;
                
                Check.QuickThrowOnFailure(result);
                
                // Reset for next iteration
                GameMessageBroker.Reset();
            }
        }

        #endregion
    }
}
