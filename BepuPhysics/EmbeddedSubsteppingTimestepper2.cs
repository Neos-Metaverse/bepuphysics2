﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BepuUtilities;

namespace BepuPhysics
{
    /// <summary>
    /// Updates the simulation in the order of: sleeper -> predict body bounding boxes -> collision detection -> LOOP { contact data update (if on iteration > 0) -> integrate body velocities -> solver -> integrate body poses } -> data structure optimization.
    /// Each inner loop execution simulates a sub-timestep of length dt/substepCount.
    /// Useful for simulations with difficult to solve constraint systems that need shorter timestep durations but which don't require high frequency collision detection.
    /// </summary>
    public class EmbeddedSubsteppingTimestepper2 : ITimestepper
    {
        /// <summary>
        /// Gets or sets the number of substeps to execute during each timestep.
        /// </summary>
        public int SubstepCount { get; set; }

        /// <summary>
        /// Fires after the sleeper completes and before bodies are integrated.
        /// </summary>
        public event TimestepperStageHandler Slept;
        /// <summary>
        /// Fires after bodies have their bounding boxes updated for the frame's predicted motion and before collision detection.
        /// </summary>
        public event TimestepperStageHandler BeforeCollisionDetection;
        /// <summary>
        /// Fires after all collisions have been identified, but before the substep loop begins.
        /// </summary>
        public event TimestepperStageHandler CollisionsDetected;
        /// <summary>
        /// Fires after the solver executes and before the final integration step.
        /// </summary>
        public event TimestepperStageHandler ConstraintsSolved;
        /// <summary>
        /// Fires after all substeps are finished executing and before data structures are incrementally optimized.
        /// </summary>
        public event TimestepperStageHandler SubstepsComplete;

        public EmbeddedSubsteppingTimestepper2(int substepCount)
        {
            SubstepCount = substepCount;
        }

        public void Timestep(Simulation simulation, float dt, IThreadDispatcher threadDispatcher = null)
        {
            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates0);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates0);
            simulation.Solver.ValidateTrailingTypeBatchBodyReferences();
            simulation.Solver.ValidateFallbackBatchEmptySlotReferences();
            simulation.Solver.ValidateFallbackBatchAccessSafety();
            simulation.Solver.ValidateAccumulatedImpulses();
            simulation.Solver.ValidateConstraintMaps();
            simulation.Solver.ValidateConstraintReferenceKinematicity();
            simulation.Solver.ValidateConstrainedKinematicsSet();
            simulation.Solver.ValidateFallbackBodiesAreDynamic();
            //simulation.Solver.ValidateExistingHandles();
            simulation.Sleep(threadDispatcher);
            Slept?.Invoke(dt, threadDispatcher);
            simulation.Solver.ValidateTrailingTypeBatchBodyReferences();
            simulation.Solver.ValidateFallbackBatchEmptySlotReferences();
            simulation.Solver.ValidateFallbackBatchAccessSafety();
            simulation.Solver.ValidateAccumulatedImpulses();
            simulation.Solver.ValidateConstraintMaps();
            simulation.Solver.ValidateConstraintReferenceKinematicity();
            simulation.Solver.ValidateConstrainedKinematicsSet();
            simulation.Solver.ValidateFallbackBodiesAreDynamic();
            //simulation.Solver.ValidateExistingHandles();
            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates1);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates1);

            simulation.PredictBoundingBoxes(dt, threadDispatcher);
            BeforeCollisionDetection?.Invoke(dt, threadDispatcher);

            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates2);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates2);
            simulation.CollisionDetection(dt, threadDispatcher);
            CollisionsDetected?.Invoke(dt, threadDispatcher);
            Debug.Assert(SubstepCount >= 0, "Substep count should be positive.");

            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates3);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates3);
            simulation.Solver.ValidateTrailingTypeBatchBodyReferences();
            simulation.Solver.ValidateFallbackBatchEmptySlotReferences();
            simulation.Solver.ValidateFallbackBatchAccessSafety();
            simulation.Solver.ValidateAccumulatedImpulses();
            simulation.Solver.ValidateConstraintMaps();
            simulation.Solver.ValidateConstraintReferenceKinematicity();
            simulation.Solver.ValidateConstrainedKinematicsSet();
            simulation.Solver.ValidateFallbackBodiesAreDynamic();
            //simulation.Solver.ValidateExistingHandles();

            var constrainedBodySet = simulation.Solver.PrepareConstraintIntegrationResponsibilities(SubstepCount, threadDispatcher);
            simulation.Profiler.Start(simulation.Solver);
            simulation.Solver.SolveStep2(dt, threadDispatcher);
            simulation.Profiler.End(simulation.Solver);
            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates4);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates4);
            ConstraintsSolved?.Invoke(dt, threadDispatcher);
            simulation.Profiler.Start(simulation.PoseIntegrator);
            simulation.PoseIntegrator.IntegrateAfterSubstepping(constrainedBodySet, dt, SubstepCount, threadDispatcher);
            simulation.Profiler.End(simulation.PoseIntegrator);
            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates5);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates5);
            simulation.Solver.DisposeConstraintIntegrationResponsibilities();
            SubstepsComplete?.Invoke(dt, threadDispatcher);


            simulation.Solver.ValidateAccumulatedImpulses();
            simulation.IncrementallyOptimizeDataStructures(threadDispatcher);
            //simulation.Bodies.ValidateAwakeMotionStatesByHash(HashDiagnosticType.AwakeBodyStates6);
            //simulation.Bodies.ValidateAwakeCollidablesByHash(HashDiagnosticType.AwakeBodyCollidableStates6);
        }
    }
}
