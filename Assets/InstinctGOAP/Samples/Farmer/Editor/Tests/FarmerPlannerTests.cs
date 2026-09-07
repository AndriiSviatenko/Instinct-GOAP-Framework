using NUnit.Framework;
using Instinct.GOAP;
using Instinct.GOAP.Samples.Farmer;

namespace Instinct.GOAP.Samples.Farmer.Tests
{
    public class FarmerPlannerTests
    {
        private static GoapPlanner Planner => new GoapPlanner(maxIterations: 200, maxDepth: 6);

        private static GoapDomain<FarmerContext> Domain => FarmerBrain.Build();

        private static WorldState FreshState() => WorldState.For<FarmerFacts>()
            .Set(FarmerFacts.Energy, 100)
            .Set(FarmerFacts.CropsGrown, 5)
            .Set(FarmerFacts.CropsRipe, 6)
            .Set(FarmerFacts.DistanceToHome, 15f)
            .Set(FarmerFacts.DistanceToField, 2f);

        [Test]
        public void FreshFarmerPlansWalkingToFieldAndHarvest()
        {
            var domain = Domain;
            var state = FreshState();
            var plan = Planner.BuildPlan(domain.PlannerActions, domain.Goal(FarmerGoalKeys.WorkTheField), state);

            Assert.IsNotNull(plan, "WorkTheField should be reachable when fresh");
            Assert.AreEqual(FarmerActionKeys.WalkToField, plan.Actions[0].Key, "Must first walk to field");
            Assert.GreaterOrEqual(plan.Actions.Count, 2, "Should walk, then harvest at least once");
            Assert.AreEqual(FarmerActionKeys.Harvest, plan.Actions[1].Key);
        }

        [Test]
        public void WorkTheFieldNeedsRipeCrops()
        {
            var domain = Domain;
            var emptyField = FreshState().Set(FarmerFacts.CropsRipe, 0);

            var plan = Planner.BuildPlan(domain.PlannerActions, domain.Goal(FarmerGoalKeys.WorkTheField), emptyField);

            Assert.IsNull(plan, "Nothing to harvest on an unripe field - the farmer must wait, not stall broken");
        }

        [Test]
        public void TiredFarmerPlansWalkingHomeAndResting()
        {
            var domain = Domain;
            var state = FreshState()
                .Set(FarmerFacts.Energy, 5)
                .Set(FarmerFacts.DistanceToHome, 15f)
                .Set(FarmerFacts.DistanceToField, 0f);

            var plan = Planner.BuildPlan(domain.PlannerActions, domain.Goal(FarmerGoalKeys.Recover), state);

            Assert.IsNotNull(plan);
            Assert.AreEqual(FarmerActionKeys.WalkToHome, plan.Actions[0].Key, "Must first move home");
            Assert.AreEqual(FarmerActionKeys.Rest, plan.Actions[^1].Key, "Must end by resting");
        }

        [Test]
        public void PlanAlwaysReachesGoal()
        {
            var domain = Domain;
            var fresh = FreshState();
            var tired = FreshState().Set(FarmerFacts.Energy, 5).Set(FarmerFacts.DistanceToField, 0f);

            AssertReachesGoal(domain, domain.Goal(FarmerGoalKeys.WorkTheField), fresh);
            AssertReachesGoal(domain, domain.Goal(FarmerGoalKeys.Recover), tired);
        }

        [Test]
        public void DomainHasNoIssues()
        {
            var report = new DomainBuilder()
                .AddActions(Domain.PlannerActions)
                .AddGoals(Domain.Goals)
                .DeclaredActionsIn(typeof(FarmerActionKeys))
                .DeclaredGoalsIn(typeof(FarmerGoalKeys))
                .Describe();

            Assert.IsNull(report, report);
        }

        [Test]
        public void HarvestMirrorsItsEffectsIntoTheWorld()
        {
            var domain = Domain;
            var harvest = domain.Action(FarmerActionKeys.Harvest);

            var ctx = new FarmerContext { Energy = 80, CropsGrown = 2, CropsRipe = 3 };
            var bindings = domain.Bindings;
            var provider = new BoundStateProvider<FarmerContext>(domain.FactsType, bindings, ctx);
            var executor = new RuntimeExecutor<FarmerContext>(ctx, bindings, provider);

            executor.OnCompleted(harvest, null, success: true);

            Assert.AreEqual(55, ctx.Energy, "Harvest costs 25 energy");
            Assert.AreEqual(3, ctx.CropsGrown, "Harvest grows one crop");
            Assert.AreEqual(2, ctx.CropsRipe, "Harvest consumes one ripe crop");
        }

        [Test]
        public void SomeGoalIsRelevantAtEveryEnergyLevel()
        {
            var domain = Domain;

            for (int energy = 0; energy <= 100; energy++)
            {
                var state = FreshState().Set(FarmerFacts.Energy, energy);

                bool any = false;
                foreach (var goal in domain.Goals)
                    if (goal.IsRelevant(state)) { any = true; break; }

                Assert.IsTrue(any, $"No goal is relevant at Energy={energy} - farmer would stall");
            }
        }

        private static void AssertReachesGoal(GoapDomain<FarmerContext> domain, IGoal goal, WorldState start)
        {
            var plan = Planner.BuildPlan(domain.PlannerActions, goal, start);
            Assert.IsNotNull(plan, $"Plan for {goal.NameOf()} should not be null");

            var finalState = start;
            foreach (var action in plan.Actions)
            {
                Assert.IsTrue(action.PreconditionsSatisfied(finalState),
                    $"Precondition failed for {action.NameOf()}");
                finalState = action.ApplyTo(finalState);
            }

            Assert.IsTrue(goal.IsSatisfiedBy(finalState),
                $"{goal.NameOf()} should be satisfied after executing the plan");
        }
    }
}
