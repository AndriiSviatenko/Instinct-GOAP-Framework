using System;
using Instinct.GOAP.Samples.Stalker;
using NUnit.Framework;

namespace Instinct.GOAP.Samples.Stalker.Tests
{
    public class StalkerPlannerTests
    {
        private static GoapPlanner Planner => new GoapPlanner(maxIterations: 400, maxDepth: 8);

        private static WorldState Base() => WorldState.For<StalkerFacts>()
            .Set(StalkerFacts.AtLocation, StalkerLocation.Campfire)
            .Set(StalkerFacts.Activity, StalkerActivity.Idle)
            .Set(StalkerFacts.Health, 100)
            .Set(StalkerFacts.Hunger, 30)
            .Set(StalkerFacts.Energy, 100)
            .Set(StalkerFacts.Money, 150)
            .Set(StalkerFacts.Artifacts, 0)
            .Set(StalkerFacts.PatrolPointsVisited, 0)
            .Set(StalkerFacts.HasWeapon, true)
            .Set(StalkerFacts.HasFood, false)
            .Set(StalkerFacts.HasMedkit, true)
            .Set(StalkerFacts.EmissionActive, false)
            .Set(StalkerFacts.SafeFromEmission, false)
            .Set(StalkerFacts.EnemyVisible, false)
            .Set(StalkerFacts.MutantVisible, false)
            .Set(StalkerFacts.DistanceToThreat, 99f)
            .Set(StalkerFacts.ThreatDealt, false)
            .Set(StalkerFacts.SafeFromThreat, false)
            .Set(StalkerFacts.AnomalyNearby, false)
            .Set(StalkerFacts.AnomalyScanned, false)
            .Set(StalkerFacts.ArtifactCollected, false);

        private static StalkerBlackboard BaseBoard() => new StalkerBlackboard
        {
            HasWeapon = true,
            HasMedkit = true,
            Money = 150,
            Hunger = 30,
            Energy = 100,
        };

        private static void AssertPlanReachesGoal(IPlan plan, WorldState start, IGoal goal)
        {
            Assert.IsNotNull(plan, $"Plan should not be null for {goal.NameOf()}");
            var state = start;
            foreach (var action in plan.Actions)
            {
                Assert.IsTrue(action.PreconditionsSatisfied(state),
                    $"Precondition failed for {action.NameOf()} in {plan.Goal.NameOf()} plan");
                state = action.ApplyTo(state);
            }
            Assert.IsTrue(goal.IsSatisfiedBy(state),
                $"Plan for {goal.NameOf()} should reach the goal");
        }

        [Test]
        public void DomainValidatesClean()
        {
            Assert.IsNull(StalkerAgent.ValidateDomain(), "Domain should have no issues");
        }


        [Test]
        public void PlansShelterAndWaitsOutEmission()
        {
            var state = Base().Set(StalkerFacts.EmissionActive, true);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.SurviveEmission(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.SurviveEmission());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.GoToShelter);
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.WaitOutEmission);
        }

        [Test]
        public void AlreadySafeNeedsNoPlan()
        {
            var planner = new GoapPlanner(maxIterations: 400, maxDepth: 8);
            var state = Base()
                .Set(StalkerFacts.EmissionActive, true)
                .Set(StalkerFacts.SafeFromEmission, true);

            var plan = planner.BuildPlan(StalkerActions.All(), StalkerGoals.SurviveEmission(), state);

            Assert.IsNull(plan);
            Assert.AreEqual(PlanFailure.AlreadySatisfied, planner.LastFailure);
        }


        [Test]
        public void PlansHungerRun()
        {
            var state = Base().Set(StalkerFacts.Hunger, 90);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.SatisfyHunger(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.SatisfyHunger());
            Assert.AreEqual(StalkerActionKeys.GoToStash, plan.Actions[0].Key);
            Assert.AreEqual(StalkerActionKeys.EatFood, plan.Actions[plan.Actions.Count - 1].Key);
        }

        [Test]
        public void EatsCarriedFoodWithoutGoingToStash()
        {
            var state = Base().Set(StalkerFacts.Hunger, 90).Set(StalkerFacts.HasFood, true);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.SatisfyHunger(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.SatisfyHunger());
            CollectionAssert.DoesNotContain(PlanKeys(plan), StalkerActionKeys.GoToStash);
        }

        [Test]
        public void PlansSleepWhenExhausted()
        {
            var state = Base().Set(StalkerFacts.Energy, 10);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.Rest(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.Rest());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.SleepAtCampfire);
        }

        [Test]
        public void PlansMedkitWhenInjured()
        {
            var state = Base().Set(StalkerFacts.Health, 40);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.Heal(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.Heal());
            Assert.AreEqual(StalkerActionKeys.UseMedkit, plan.Actions[plan.Actions.Count - 1].Key);
        }


        [Test]
        public void ArmedStalkerFightsEnemy()
        {
            var state = Base()
                .Set(StalkerFacts.EnemyVisible, true)
                .Set(StalkerFacts.DistanceToThreat, 10f);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.Defend(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.Defend());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.ChaseThreat);
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.AttackThreat);
        }

        [Test]
        public void UnarmedStalkerFleesMutant()
        {
            var state = Base()
                .Set(StalkerFacts.HasWeapon, false)
                .Set(StalkerFacts.MutantVisible, true)
                .Set(StalkerFacts.DistanceToThreat, 5f);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.Defend(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.Defend());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.FleeThreat);
        }


        [Test]
        public void PlansArtifactExtraction()
        {
            var state = Base().Set(StalkerFacts.AnomalyNearby, true);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.CollectArtifact(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.CollectArtifact());
            Assert.AreEqual(StalkerActionKeys.ExtractArtifact, plan.Actions[plan.Actions.Count - 1].Key);
        }

        [Test]
        public void PlansArtifactSale()
        {
            var state = Base().Set(StalkerFacts.Artifacts, 1);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.TradeArtifacts(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.TradeArtifacts());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.SellArtifacts);
        }

        [Test]
        public void PlansSupplyRun()
        {
            var state = Base()
                .Set(StalkerFacts.Money, 300)
                .Set(StalkerFacts.HasFood, false)
                .Set(StalkerFacts.HasMedkit, false);
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.Restock(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.Restock());
            CollectionAssert.Contains(PlanKeys(plan), StalkerActionKeys.BuySupplies);
        }

        [Test]
        public void PlansRoamWhenCalm()
        {
            var state = Base();
            var plan = Planner.BuildPlan(StalkerActions.All(), StalkerGoals.RoamZone(), state);

            AssertPlanReachesGoal(plan, state, StalkerGoals.RoamZone());
            Assert.AreEqual(StalkerActionKeys.GoToField, plan.Actions[0].Key);
        }


        [Test]
        public void AgentChoosesEmissionOverHunger()
        {
            var board = BaseBoard();
            board.Hunger = 90;
            var agent = new StalkerAgent(board);

            var first = agent.Tick();
            Assert.AreEqual(StalkerGoalKeys.SatisfyHunger, agent.CurrentGoal.Key);
            Assert.AreEqual(StalkerCommandKind.MoveTo, first.Kind);

            board.EmissionActive = true;
            board.EmissionSafe = false;

            agent.Tick();
            Assert.AreEqual(StalkerGoalKeys.SurviveEmission, agent.CurrentGoal.Key,
                "Emission must preempt any other goal");
        }

        [Test]
        public void AgentEatsWhenHungry()
        {
            var board = BaseBoard();
            board.Hunger = 90;
            var agent = new StalkerAgent(board);

            Assert.AreEqual(StalkerCommandKind.MoveTo, agent.Tick().Kind);
            agent.NotifyActionComplete(true);

            Assert.AreEqual(StalkerActionKeys.TakeFood, agent.CurrentAction.Key);
            agent.NotifyActionComplete(true);

            Assert.AreEqual(StalkerActionKeys.EatFood, agent.CurrentAction.Key);
            agent.NotifyActionComplete(true);

            Assert.LessOrEqual(board.Hunger, 30, "Hunger should be satisfied after eating");
        }

        [Test]
        public void LonerSurvivesASimulatedDay()
        {
            var board = BaseBoard();
            var agent = new StalkerAgent(board);
            int mealsEaten = 0;

            for (int i = 0; i < 300; i++)
            {
                board.Hunger = Math.Min(100, board.Hunger + 1);
                board.Energy = Math.Max(0, board.Energy - 1);

                var cmd = agent.Tick();
                if (cmd.Kind == StalkerCommandKind.Idle) continue;

                if (cmd.Source == StalkerActionKeys.EatFood && cmd.Kind == StalkerCommandKind.Interact)
                    mealsEaten++;

                agent.NotifyActionComplete(true);
            }

            Assert.GreaterOrEqual(mealsEaten, 1, "Stalker should have eaten during the simulated day");
            Assert.Greater(board.Health, 0, "Stalker should stay alive");
        }

        private static System.Collections.Generic.IReadOnlyCollection<ActionKey> PlanKeys(IPlan plan)
        {
            var keys = new System.Collections.Generic.List<ActionKey>();
            if (plan == null) return keys;
            foreach (var a in plan.Actions) keys.Add(a.Key);
            return keys;
        }
    }
}
