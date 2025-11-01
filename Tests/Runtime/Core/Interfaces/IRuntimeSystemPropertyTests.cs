/**
 * Property-based tests for IRuntimeSystem.cs
 * Generated using FsCheck property-based testing patterns
 * Framework: Unity Test Framework with NUnit + FsCheck
 * 
 * Property Test Coverage:
 * - Invariant properties (must always hold)
 * - Mathematical properties (laws of operations)
 * - State transitions (valid progression of state)
 * - Reversibility / Round-trip laws
 * - Error / Boundary behavior
 * 
 * Key Properties Tested:
 * - Lifecycle method call consistency across random sequences
 * - Delta time parameter handling across edge cases
 * - Exception isolation between methods
 * - State preservation across multiple lifecycle cycles
 * - Method call order independence
 * - Parameter boundary validation
 * 
 * Critical Lessons Applied:
 * - Use concrete TestRuntimeSystem implementations with proper setup
 * - Test actual system behavior, not assumed behavior
 * - Use iterative random testing with System.Random (50 iterations)
 * - Test both mathematical laws AND real object implementation
 * - Avoid lambda expressions and explicit type parameters in FsCheck
 * - Focus on the five essential property categories
 */

using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;
using Serilog;
using NSubstitute;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core.Interfaces
{
    /// <summary>
    /// Property-based test fixture for IRuntimeSystem interface
    /// Tests fundamental laws and invariants across wide input ranges
    /// </summary>
    [TestFixture]
    public class IRuntimeSystemPropertyTests : RuntimeInterfaceTestBase<IRuntimeSystem>
    {
        /// <summary>
        /// Property: Call counts always match actual method invocations
        /// Invariant: Internal state tracking remains consistent with external calls
        /// </summary>
        [Test]
        public void MethodCallCounts_AlwaysMatchInvocations_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var expectedStartCalls = random.Next(0, 10);
                var expectedTickCalls = random.Next(0, 20);
                var expectedLateTickCalls = random.Next(0, 20);
                var expectedFixedTickCalls = random.Next(0, 20);
                var expectedShutdownCalls = random.Next(0, 10);

                // Execute random number of calls for each method
                for (int j = 0; j < expectedStartCalls; j++) system.Start();
                for (int j = 0; j < expectedTickCalls; j++) system.Tick(0.016f);
                for (int j = 0; j < expectedLateTickCalls; j++) system.LateTick(0.016f);
                for (int j = 0; j < expectedFixedTickCalls; j++) system.FixedTick(0.016f);
                for (int j = 0; j < expectedShutdownCalls; j++) system.Shutdown();

                // Property: Call counts must match actual invocations
                var result = system.StartCallCount == expectedStartCalls &&
                           system.TickCallCount == expectedTickCalls &&
                           system.LateTickCallCount == expectedLateTickCalls &&
                           system.FixedTickCallCount == expectedFixedTickCalls &&
                           system.ShutdownCallCount == expectedShutdownCalls;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Delta time values are preserved across all tick methods
        /// Invariant: Parameter values are correctly stored and retrievable
        /// </summary>
        [Test]
        public void DeltaTimeValues_ArePreservedAcrossTickMethods_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTimes = new List<float>();
                
                // Generate random delta times
                for (int j = 0; j < random.Next(1, 10); j++)
                {
                    var deltaTime = (float)(random.NextDouble() * 2.0); // 0.0 to 2.0
                    deltaTimes.Add(deltaTime);
                    
                    system.Tick(deltaTime);
                    system.LateTick(deltaTime);
                    system.FixedTick(deltaTime);
                }

                // Property: All delta times should be preserved
                var result = deltaTimes.All(dt => 
                    system.TickDeltaTimes.Contains(dt) &&
                    system.LateTickDeltaTimes.Contains(dt) &&
                    system.FixedTickDeltaTimes.Contains(dt));

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Multiple Start calls are idempotent (same effect as single call)
        /// Mathematical Property: Start^n = Start^1 for n > 0
        /// </summary>
        [Test]
        public void MultipleStartCalls_AreIdempotent_MathematicalProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system1 = IRuntimeSystemTestData.CreateSystem();
                var system2 = IRuntimeSystemTestData.CreateSystem();
                
                var callCount = random.Next(1, 10);
                
                // Single start call
                system1.Start();
                
                // Multiple start calls
                for (int j = 0; j < callCount; j++)
                {
                    system2.Start();
                }

                // Property: Both systems should be in started state
                var result = system1.StartCalled == system2.StartCalled &&
                           system1.StartCalled == true;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Complete lifecycle sequence preserves state consistency
        /// State Transition: Start → Tick → LateTick → FixedTick → Shutdown maintains invariants
        /// </summary>
        [Test]
        public void CompleteLifecycleSequence_PreservesStateConsistency_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTime = (float)(random.NextDouble() * 1.0); // 0.0 to 1.0

                // Complete lifecycle sequence
                system.Start();
                system.Tick(deltaTime);
                system.LateTick(deltaTime);
                system.FixedTick(deltaTime);
                system.Shutdown();

                // Property: All methods should have been called exactly once
                var result = system.StartCallCount == 1 &&
                           system.TickCallCount == 1 &&
                           system.LateTickCallCount == 1 &&
                           system.FixedTickCallCount == 1 &&
                           system.ShutdownCallCount == 1 &&
                           system.TickDeltaTimes.Contains(deltaTime) &&
                           system.LateTickDeltaTimes.Contains(deltaTime) &&
                           system.FixedTickDeltaTimes.Contains(deltaTime);

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Method call order does not affect final state
        /// Mathematical Property: Commutativity of independent operations
        /// </summary>
        [Test]
        public void MethodCallOrder_DoesNotAffectFinalState_MathematicalProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system1 = IRuntimeSystemTestData.CreateSystem();
                var system2 = IRuntimeSystemTestData.CreateSystem();
                var deltaTime = (float)(random.NextDouble() * 1.0);

                // Order 1: Start → Tick → LateTick → FixedTick → Shutdown
                system1.Start();
                system1.Tick(deltaTime);
                system1.LateTick(deltaTime);
                system1.FixedTick(deltaTime);
                system1.Shutdown();

                // Order 2: Tick → Start → FixedTick → LateTick → Shutdown
                system2.Tick(deltaTime);
                system2.Start();
                system2.FixedTick(deltaTime);
                system2.LateTick(deltaTime);
                system2.Shutdown();

                // Property: Both systems should have same final call counts
                var result = system1.StartCallCount == system2.StartCallCount &&
                           system1.TickCallCount == system2.TickCallCount &&
                           system1.LateTickCallCount == system2.LateTickCallCount &&
                           system1.FixedTickCallCount == system2.FixedTickCallCount &&
                           system1.ShutdownCallCount == system2.ShutdownCallCount;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Edge case delta times are handled consistently
        /// Boundary Behavior: Zero, negative, and large delta times don't crash
        /// </summary>
        [Test]
        public void EdgeCaseDeltaTimes_AreHandledConsistently_BoundaryBehavior()
        {
            var random = new System.Random();
            var edgeCases = new[] { 0f, -0.016f, 1f, 10f, float.MaxValue / 2 };
            
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTime = edgeCases[random.Next(edgeCases.Length)];

                // Property: All tick methods should handle edge cases without crashing
                var result = true;
                
                try
                {
                    system.Tick(deltaTime);
                    system.LateTick(deltaTime);
                    system.FixedTick(deltaTime);
                    
                    // Verify delta time was recorded
                    result = result && system.TickDeltaTimes.Contains(deltaTime) &&
                                   system.LateTickDeltaTimes.Contains(deltaTime) &&
                                   system.FixedTickDeltaTimes.Contains(deltaTime);
                }
                catch (Exception)
                {
                    result = false;
                }

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Exception in one method doesn't affect other methods
        /// Error Isolation: Method-specific exceptions are properly isolated
        /// </summary>
        [Test]
        public void MethodSpecificExceptions_DoNotAffectOtherMethods_ErrorIsolation()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                var deltaTime = (float)(random.NextDouble() * 1.0);
                
                // Configure one method to throw
                var methodToThrow = random.Next(5);
                switch (methodToThrow)
                {
                    case 0: system.ShouldThrowOnStart = true; break;
                    case 1: system.ShouldThrowOnTick = true; break;
                    case 2: system.ShouldThrowOnLateTick = true; break;
                    case 3: system.ShouldThrowOnFixedTick = true; break;
                    case 4: system.ShouldThrowOnShutdown = true; break;
                }

                var result = true;
                
                try
                {
                    // Test all methods - only one should throw
                    if (system.ShouldThrowOnStart)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Start());
                    }
                    else
                    {
                        system.Start();
                        result = result && system.StartCalled;
                    }

                    if (system.ShouldThrowOnTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Tick(deltaTime));
                    }
                    else
                    {
                        system.Tick(deltaTime);
                        result = result && system.TickCalled;
                    }

                    if (system.ShouldThrowOnLateTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.LateTick(deltaTime));
                    }
                    else
                    {
                        system.LateTick(deltaTime);
                        result = result && system.LateTickCalled;
                    }

                    if (system.ShouldThrowOnFixedTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.FixedTick(deltaTime));
                    }
                    else
                    {
                        system.FixedTick(deltaTime);
                        result = result && system.FixedTickCalled;
                    }

                    if (system.ShouldThrowOnShutdown)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Shutdown());
                    }
                    else
                    {
                        system.Shutdown();
                        result = result && system.ShutdownCalled;
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Multiple lifecycle cycles maintain consistent behavior
        /// Reversibility: Start → Shutdown → Start = Original behavior
        /// </summary>
        [Test]
        public void MultipleLifecycleCycles_MaintainConsistentBehavior_Reversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var cycleCount = random.Next(1, 5);
                var deltaTime = (float)(random.NextDouble() * 1.0);

                // Execute multiple complete cycles
                for (int cycle = 0; cycle < cycleCount; cycle++)
                {
                    system.Start();
                    system.Tick(deltaTime);
                    system.LateTick(deltaTime);
                    system.FixedTick(deltaTime);
                    system.Shutdown();
                }

                // Property: Call counts should be multiplied by cycle count
                var result = system.StartCallCount == cycleCount &&
                           system.TickCallCount == cycleCount &&
                           system.LateTickCallCount == cycleCount &&
                           system.FixedTickCallCount == cycleCount &&
                           system.ShutdownCallCount == cycleCount &&
                           system.TickDeltaTimes.Count == cycleCount &&
                           system.LateTickDeltaTimes.Count == cycleCount &&
                           system.FixedTickDeltaTimes.Count == cycleCount;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Delta time parameter ranges are handled correctly
        /// Boundary Behavior: All valid float values are accepted
        /// </summary>
        [Test]
        public void DeltaTimeParameterRanges_AreHandledCorrectly_BoundaryBehavior()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                
                // Generate delta times across various ranges
                var deltaTimes = new[]
                {
                    (float)(random.NextDouble() * 0.001),           // Very small
                    (float)(random.NextDouble() * 0.1),             // Small
                    (float)(random.NextDouble() * 1.0),             // Normal
                    (float)(random.NextDouble() * 10.0),            // Large
                    (float)(random.NextDouble() * 100.0),           // Very large
                    (float)(random.NextDouble() * 1000.0)           // Extremely large
                };

                var result = true;
                
                foreach (var deltaTime in deltaTimes)
                {
                    try
                    {
                        system.Tick(deltaTime);
                        system.LateTick(deltaTime);
                        system.FixedTick(deltaTime);
                        
                        // Verify delta time was recorded
                        result = result && system.TickDeltaTimes.Contains(deltaTime) &&
                                       system.LateTickDeltaTimes.Contains(deltaTime) &&
                                       system.FixedTickDeltaTimes.Contains(deltaTime);
                    }
                    catch (Exception)
                    {
                        result = false;
                        break;
                    }
                }

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: System state remains consistent across random method sequences
        /// Invariant: Internal state tracking never becomes inconsistent
        /// </summary>
        [Test]
        public void RandomMethodSequences_MaintainConsistentState_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var methodCount = random.Next(10, 50);
                var deltaTime = (float)(random.NextDouble() * 1.0);

                // Execute random sequence of methods
                for (int j = 0; j < methodCount; j++)
                {
                    var method = random.Next(5);
                    switch (method)
                    {
                        case 0: system.Start(); break;
                        case 1: system.Tick(deltaTime); break;
                        case 2: system.LateTick(deltaTime); break;
                        case 3: system.FixedTick(deltaTime); break;
                        case 4: system.Shutdown(); break;
                    }
                }

                // Property: State should remain consistent
                var result = system.StartCallCount >= 0 &&
                           system.TickCallCount >= 0 &&
                           system.LateTickCallCount >= 0 &&
                           system.FixedTickCallCount >= 0 &&
                           system.ShutdownCallCount >= 0 &&
                           system.TickDeltaTimes.Count == system.TickCallCount &&
                           system.LateTickDeltaTimes.Count == system.LateTickCallCount &&
                           system.FixedTickDeltaTimes.Count == system.FixedTickCallCount;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Exception handling preserves system integrity
        /// Error Boundary: Exceptions don't corrupt internal state
        /// </summary>
        [Test]
        public void ExceptionHandling_PreservesSystemIntegrity_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);
                var deltaTime = (float)(random.NextDouble() * 1.0);

                var result = true;
                
                try
                {
                    // Attempt operations that will throw exceptions
                    system.Start();
                }
                catch (InvalidOperationException)
                {
                    // Expected exception - verify state is preserved
                    result = result && system.StartCallCount == 1 &&
                                   system.LastThrownException != null;
                }

                try
                {
                    system.Tick(deltaTime);
                }
                catch (InvalidOperationException)
                {
                    // Expected exception - verify state is preserved
                    result = result && system.TickCallCount == 1 &&
                                   system.LastThrownException != null;
                }

                try
                {
                    system.LateTick(deltaTime);
                }
                catch (InvalidOperationException)
                {
                    // Expected exception - verify state is preserved
                    result = result && system.LateTickCallCount == 1 &&
                                   system.LastThrownException != null;
                }

                try
                {
                    system.FixedTick(deltaTime);
                }
                catch (InvalidOperationException)
                {
                    // Expected exception - verify state is preserved
                    result = result && system.FixedTickCallCount == 1 &&
                                   system.LastThrownException != null;
                }

                try
                {
                    system.Shutdown();
                }
                catch (InvalidOperationException)
                {
                    // Expected exception - verify state is preserved
                    result = result && system.ShutdownCallCount == 1 &&
                                   system.LastThrownException != null;
                }

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Method-specific exception configuration works correctly
        /// State Transition: Exception flags control behavior without side effects
        /// </summary>
        [Test]
        public void MethodSpecificExceptionConfiguration_WorksCorrectly_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                var deltaTime = (float)(random.NextDouble() * 1.0);

                // Configure specific methods to throw
                system.ShouldThrowOnStart = random.Next(2) == 0;
                system.ShouldThrowOnTick = random.Next(2) == 0;
                system.ShouldThrowOnLateTick = random.Next(2) == 0;
                system.ShouldThrowOnFixedTick = random.Next(2) == 0;
                system.ShouldThrowOnShutdown = random.Next(2) == 0;

                var result = true;
                
                try
                {
                    if (system.ShouldThrowOnStart)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Start());
                    }
                    else
                    {
                        system.Start();
                        result = result && system.StartCalled;
                    }

                    if (system.ShouldThrowOnTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Tick(deltaTime));
                    }
                    else
                    {
                        system.Tick(deltaTime);
                        result = result && system.TickCalled;
                    }

                    if (system.ShouldThrowOnLateTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.LateTick(deltaTime));
                    }
                    else
                    {
                        system.LateTick(deltaTime);
                        result = result && system.LateTickCalled;
                    }

                    if (system.ShouldThrowOnFixedTick)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.FixedTick(deltaTime));
                    }
                    else
                    {
                        system.FixedTick(deltaTime);
                        result = result && system.FixedTickCalled;
                    }

                    if (system.ShouldThrowOnShutdown)
                    {
                        Assert.Throws<InvalidOperationException>(() => system.Shutdown());
                    }
                    else
                    {
                        system.Shutdown();
                        result = result && system.ShutdownCalled;
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Delta time collection maintains chronological order
        /// Invariant: Delta times are recorded in call order
        /// </summary>
        [Test]
        public void DeltaTimeCollection_MaintainsChronologicalOrder_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTimes = new List<float>();
                
                // Generate and record delta times in sequence
                for (int j = 0; j < random.Next(1, 10); j++)
                {
                    var deltaTime = (float)(random.NextDouble() * 1.0);
                    deltaTimes.Add(deltaTime);
                    
                    system.Tick(deltaTime);
                    system.LateTick(deltaTime);
                    system.FixedTick(deltaTime);
                }

                // Property: Delta time collections should match input order
                var result = system.TickDeltaTimes.SequenceEqual(deltaTimes) &&
                           system.LateTickDeltaTimes.SequenceEqual(deltaTimes) &&
                           system.FixedTickDeltaTimes.SequenceEqual(deltaTimes);

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: System name remains constant throughout lifecycle
        /// Invariant: System identity never changes
        /// </summary>
        [Test]
        public void SystemName_RemainsConstantThroughoutLifecycle_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var systemName = $"TestSystem{random.Next(1000)}";
                var system = IRuntimeSystemTestData.CreateSystem(systemName);
                var deltaTime = (float)(random.NextDouble() * 1.0);

                // Execute complete lifecycle
                system.Start();
                system.Tick(deltaTime);
                system.LateTick(deltaTime);
                system.FixedTick(deltaTime);
                system.Shutdown();

                // Property: System name should remain unchanged
                var result = system.Name == systemName;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Multiple systems maintain independent state
        /// Invariant: System instances don't interfere with each other
        /// </summary>
        [Test]
        public void MultipleSystems_MaintainIndependentState_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var system1 = IRuntimeSystemTestData.CreateSystem("System1");
                var system2 = IRuntimeSystemTestData.CreateSystem("System2");
                var deltaTime1 = (float)(random.NextDouble() * 1.0);
                var deltaTime2 = (float)(random.NextDouble() * 1.0);

                // Operate on system1
                system1.Start();
                system1.Tick(deltaTime1);
                system1.LateTick(deltaTime1);
                system1.FixedTick(deltaTime1);
                system1.Shutdown();

                // Operate on system2
                system2.Start();
                system2.Tick(deltaTime2);
                system2.LateTick(deltaTime2);
                system2.FixedTick(deltaTime2);
                system2.Shutdown();

                // Property: Systems should maintain independent state
                var result = system1.Name != system2.Name &&
                           system1.StartCallCount == 1 &&
                           system2.StartCallCount == 1 &&
                           system1.TickDeltaTimes.Contains(deltaTime1) &&
                           system2.TickDeltaTimes.Contains(deltaTime2) &&
                           !system1.TickDeltaTimes.Contains(deltaTime2) &&
                           !system2.TickDeltaTimes.Contains(deltaTime1);

                Check.QuickThrowOnFailure(result);
            }
        }
    }
}
