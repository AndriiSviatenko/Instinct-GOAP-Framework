using NUnit.Framework;
using Instinct.GOAP.Samples.Chef;

namespace Instinct.GOAP.Samples.Chef.Tests
{
    public class ChefPlannerTests
    {
        private static GoapPlanner NewPlanner() => new GoapPlanner(maxIterations: 200, maxDepth: 10);

        private static WorldState DefaultState() => WorldState.For<ChefFacts>()
            .Set(ChefFacts.ClientHunger, 80)
            .Set(ChefFacts.ClientPresent, true)
            .Set(ChefFacts.HasIngredients, false)
            .Set(ChefFacts.MealReady, false)
            .Set(ChefFacts.DistanceToStove, 5.0f)
            .Set(ChefFacts.DistanceToClient, 5.0f)
            .Set(ChefFacts.DistanceToBreak, 4.0f)
            .Set(ChefFacts.Energy, 100)
            .Set(ChefFacts.ChefState, ChefState.Idle);

        [Test]
        public void PrepareMealPlansFullCookingSequence()
        {
            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.PrepareMeal, DefaultState());

            Assert.IsNotNull(plan, "PrepareMeal should be reachable when hungry and out of meals");
            Assert.GreaterOrEqual(plan.Actions.Count, 3);
            Assert.AreEqual(ChefActionKeys.GetIngredients, plan.Actions[0].Key, "Must fetch ingredients first");
            Assert.AreEqual(ChefActionKeys.CookMeal, plan.Actions[plan.Actions.Count - 1].Key, "Must end by cooking");
        }

        [Test]
        public void ReturnsNullWhenAlreadySatisfied()
        {
            var state = DefaultState().Set(ChefFacts.MealReady, true);
            var planner = NewPlanner();

            var plan = planner.BuildPlan(ChefActions.All(), ChefGoals.PrepareMeal, state);

            Assert.IsNull(plan, "Plan should be null when the goal is already satisfied");
            Assert.AreEqual(PlanFailure.AlreadySatisfied, planner.LastFailure);
        }

        [Test]
        public void PlanReachesGoal()
        {
            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.PrepareMeal, DefaultState());

            Assert.IsNotNull(plan);
            var finalState = DefaultState();
            foreach (var action in plan.Actions)
            {
                Assert.IsTrue(action.PreconditionsSatisfied(finalState),
                    $"Precondition failed for {action.NameOf()}");
                finalState = action.ApplyTo(finalState);
            }
            Assert.IsTrue(ChefGoals.PrepareMeal.IsSatisfiedBy(finalState),
                "Plan should reach the goal");
        }

        [Test]
        public void ChoosesWalkToStoveWhenFar()
        {
            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.PrepareMeal, DefaultState());

            Assert.IsNotNull(plan);
            bool hasWalkToStove = false;
            foreach (var action in plan.Actions)
                if (action.Key == ChefActionKeys.WalkToStove)
                    hasWalkToStove = true;
            Assert.IsTrue(hasWalkToStove, "Plan should include WalkToStove when far from stove");
        }

        [Test]
        public void ServeClientIsOneStepWhenMealReady()
        {
            var state = DefaultState()
                .Set(ChefFacts.MealReady, true)
                .Set(ChefFacts.DistanceToClient, 1.0f);

            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.ServeClient, state);

            Assert.IsNotNull(plan, "ServeClient should be reachable with a ready meal");
            Assert.AreEqual(1, plan.Actions.Count, "One serve feeds the client fully - no re-cooking inside ServeClient");
            Assert.AreEqual(ChefActionKeys.ServeMeal, plan.Actions[0].Key);
        }

        [Test]
        public void ServeClientWalksToTheClientFirst()
        {
            var state = DefaultState().Set(ChefFacts.MealReady, true);

            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.ServeClient, state);

            Assert.IsNotNull(plan, "ServeClient should be reachable: walk then serve");
            Assert.AreEqual(2, plan.Actions.Count, "The chef must cross the room to the client before serving");
            Assert.AreEqual(ChefActionKeys.WalkToClient, plan.Actions[0].Key);
            Assert.AreEqual(ChefActionKeys.ServeMeal, plan.Actions[1].Key);
        }

        [Test]
        public void ChefIgnoresEmptyRoom()
        {
            var state = DefaultState()
                .Set(ChefFacts.ClientPresent, false)
                .Set(ChefFacts.MealReady, true);

            Assert.IsFalse(ChefGoals.ServeClient.IsRelevant(state), "No one to serve when the room is empty");
            Assert.IsFalse(ChefGoals.PrepareMeal.IsRelevant(state), "No cooking for an empty room");
            Assert.IsFalse(ChefGoals.TakeRest.IsRelevant(state), "Rested and done - nothing to do");
        }

        [Test]
        public void RestIsReachableWhenTired()
        {
            var state = DefaultState().Set(ChefFacts.Energy, 10);

            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.TakeRest, state);

            Assert.IsNotNull(plan, "TakeRest must be reachable when tired, or the agent dead-ends");
            Assert.AreEqual(ChefActionKeys.TakeBreak, plan.Actions[plan.Actions.Count - 1].Key,
                "The way back to full energy is TakeBreak");
        }

        [Test]
        public void RestGoesToTheBreakCornerFirst()
        {
            var state = DefaultState().Set(ChefFacts.Energy, 10);

            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.TakeRest, state);

            Assert.IsNotNull(plan);
            Assert.AreEqual(2, plan.Actions.Count, "Walk aside, then rest");
            Assert.AreEqual(ChefActionKeys.WalkToBreak, plan.Actions[0].Key);
            Assert.AreEqual(ChefActionKeys.TakeBreak, plan.Actions[1].Key);
        }

        [Test]
        public void TiredChefPlansBreakBeforeCooking()
        {
            var state = DefaultState().Set(ChefFacts.Energy, 10);

            var plan = NewPlanner().BuildPlan(ChefActions.All(), ChefGoals.PrepareMeal, state);

            Assert.IsNotNull(plan, "Planner should chain a break to afford cooking");
            int breakIdx = -1, cookIdx = -1;
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                if (plan.Actions[i].Key == ChefActionKeys.TakeBreak) breakIdx = i;
                if (plan.Actions[i].Key == ChefActionKeys.CookMeal) cookIdx = i;
            }
            Assert.GreaterOrEqual(breakIdx, 0, "Plan must include a break - cooking requires 25 energy");
            Assert.Greater(cookIdx, breakIdx, "Break must come before cooking");
            Assert.AreEqual(ChefActionKeys.CookMeal, plan.Actions[plan.Actions.Count - 1].Key,
                "The chain still ends with the meal");
        }

        [Test]
        public void GoalsAreRelevantInDistinctStates()
        {
            var hungryWithMeal = DefaultState().Set(ChefFacts.MealReady, true);
            var hungryNoMeal = DefaultState();
            var tired = DefaultState().Set(ChefFacts.Energy, 10);
            var fedAndRested = DefaultState().Set(ChefFacts.ClientHunger, 0);

            Assert.IsTrue(ChefGoals.ServeClient.IsRelevant(hungryWithMeal), "ServeClient: hungry + meal ready");
            Assert.IsFalse(ChefGoals.ServeClient.IsRelevant(hungryNoMeal), "ServeClient: nothing to serve");

            Assert.IsTrue(ChefGoals.PrepareMeal.IsRelevant(hungryNoMeal), "PrepareMeal: hungry + no meal + energy");
            Assert.IsFalse(ChefGoals.PrepareMeal.IsRelevant(tired), "PrepareMeal: too tired to cook");
            Assert.IsFalse(ChefGoals.PrepareMeal.IsRelevant(hungryWithMeal), "PrepareMeal: meal already exists");

            Assert.IsTrue(ChefGoals.TakeRest.IsRelevant(tired), "TakeRest: energy below 40");
            Assert.IsFalse(ChefGoals.TakeRest.IsRelevant(fedAndRested), "TakeRest: nothing to rest from");
        }
    }
}
