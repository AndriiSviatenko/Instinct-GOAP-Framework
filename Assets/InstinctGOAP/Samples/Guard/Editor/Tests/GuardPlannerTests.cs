using NUnit.Framework;
using Instinct.GOAP.Samples.Guard;
using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Samples.Guard.Tests
{
    public class GuardPlannerTests
    {
        private static GoapPlanner NewPlanner() => new GoapPlanner(maxIterations: 200, maxDepth: 6);

        private static WorldState BaseState() => WorldState.For<GuardFacts>()
            .Set(GuardFacts.AlertLevel, Alert.Calm)
            .Set(GuardFacts.IntruderVisible, false)
            .Set(GuardFacts.IntruderCaught, false)
            .Set(GuardFacts.HeardNoise, false)
            .Set(GuardFacts.NoiseChecked, false)
            .Set(GuardFacts.AtNoise, false)
            .Set(GuardFacts.AtPost, true)
            .Set(GuardFacts.HasRadio, true)
            .Set(GuardFacts.BackupCalled, false)
            .Set(GuardFacts.WaypointsVisited, 0)
            .Set(GuardFacts.DistanceToIntruder, 999f);

        [Test]
        public void SpottedIntruderIsChasedThenGrabbed()
        {
            var state = BaseState().Set(GuardFacts.IntruderVisible, true).Set(GuardFacts.DistanceToIntruder, 6f);

            var plan = NewPlanner().BuildPlan(GuardActions.All(), GuardGoals.CatchIntruder(), state);

            Assert.IsNotNull(plan, "A visible intruder at distance must be catchable");
            Assert.AreEqual(2, plan.Actions.Count, "Chase then grab");
            Assert.AreEqual(GuardActionKeys.ChaseIntruder, plan.Actions[0].Key);
            Assert.AreEqual(GuardActionKeys.GrabIntruder, plan.Actions[1].Key);
        }

        [Test]
        public void AdjacentIntruderIsGrabbedInOneStep()
        {
            var state = BaseState().Set(GuardFacts.IntruderVisible, true).Set(GuardFacts.DistanceToIntruder, 1.2f);

            var plan = NewPlanner().BuildPlan(GuardActions.All(), GuardGoals.CatchIntruder(), state);

            Assert.IsNotNull(plan);
            Assert.AreEqual(1, plan.Actions.Count);
            Assert.AreEqual(GuardActionKeys.GrabIntruder, plan.Actions[0].Key);
        }

        [Test]
        public void GrabMirrorsCaughtIntoTheBlackboard()
        {
            var board = new GuardBlackboard();

            var grab = new GrabIntruder();
            grab.OnCompleted(board, success: true);

            Assert.IsTrue(board.IntruderCaught, "A completed grab must mark the intruder as caught");
            var state = new GuardStateProvider(board).GetState();
            Assert.IsTrue(GuardGoals.CatchIntruder().IsSatisfiedBy(state), "The catch goal must be satisfied from the world");
        }

        [Test]
        public void PatrolRoundRestartsAfterThreeWaypoints()
        {
            var board = new GuardBlackboard
            {
                Waypoints = new List<Transform> { null, null, null, null },
            };

            board.AdvanceWaypoint();
            board.AdvanceWaypoint();
            Assert.AreEqual(2, board.WaypointsVisited, "Mid-round counter climbs");
            board.AdvanceWaypoint();
            Assert.AreEqual(0, board.WaypointsVisited, "Completing a round resets it so Patrol stays relevant forever");
        }

        [Test]
        public void QuietNightMeansPatrolOnly()
        {
            var state = BaseState();

            Assert.IsTrue(GuardGoals.Patrol().IsRelevant(state), "Nothing happening - walk the ring");
            Assert.IsFalse(GuardGoals.CatchIntruder().IsRelevant(state), "Nothing to catch");
            Assert.IsFalse(GuardGoals.InvestigateNoise().IsRelevant(state), "Nothing heard");
        }

        [Test]
        public void NoiseIsInvestigatedThenSwept()
        {
            var state = BaseState()
                .Set(GuardFacts.HeardNoise, true)
                .Set(GuardFacts.AlertLevel, Alert.Suspicious);

            var plan = NewPlanner().BuildPlan(GuardActions.All(), GuardGoals.InvestigateNoise(), state);

            Assert.IsNotNull(plan, "A heard noise must be reachable");
            Assert.AreEqual(2, plan.Actions.Count, "Walk to the spot, then sweep it");
            Assert.AreEqual(GuardActionKeys.WalkToNoise, plan.Actions[0].Key);
            Assert.AreEqual(GuardActionKeys.SweepArea, plan.Actions[1].Key);
        }

        [Test]
        public void VisibleIntruderAlwaysBeatsNoise()
        {
            var state = BaseState()
                .Set(GuardFacts.IntruderVisible, true)
                .Set(GuardFacts.DistanceToIntruder, 6f)
                .Set(GuardFacts.HeardNoise, true)
                .Set(GuardFacts.AlertLevel, Alert.Hunting);

            Assert.IsTrue(GuardGoals.CatchIntruder().IsRelevant(state));
            Assert.IsFalse(GuardGoals.InvestigateNoise().IsRelevant(state),
                "Noise must not be relevant while the intruder is visible");

            var plan = NewPlanner().BuildPlan(GuardActions.All(), GuardGoals.CatchIntruder(), state);
            Assert.IsNotNull(plan);
            Assert.AreEqual(GuardActionKeys.ChaseIntruder, plan.Actions[0].Key);
        }
    }
}
