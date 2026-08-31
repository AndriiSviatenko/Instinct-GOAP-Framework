using Instinct.GOAP;
using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Chef
{

    public static class ChefGoals
    {

        public static IGoal ServeClient => GoalBuilder.Create(ChefGoalKeys.ServeClient)
            .Satisfy(ChefFacts.ClientHunger, Compare.LessOrEqual, 0)
            .RelevantWhen(s => s.Get(ChefFacts.ClientPresent)
                            && s.Get(ChefFacts.ClientHunger) > 0
                            && s.Get(ChefFacts.MealReady))
            .Priority(100f)
            .Heuristic(s => s.Get(ChefFacts.DistanceToClient) > 1.5f ? 2f : 1f)
            .Build();

        public static IGoal PrepareMeal => GoalBuilder.Create(ChefGoalKeys.PrepareMeal)
            .Satisfy(ChefFacts.MealReady, true)
            .RelevantWhen(s => s.Get(ChefFacts.ClientPresent)
                            && s.Get(ChefFacts.ClientHunger) > 0
                            && !s.Get(ChefFacts.MealReady)
                            && s.Get(ChefFacts.Energy) >= 35)
            .Priority(80f)
            .Heuristic(s => s.Get(ChefFacts.HasIngredients) ? 1f : 2f)
            .Build();

        public static IGoal TakeRest => GoalBuilder.Create(ChefGoalKeys.TakeRest)
            .Satisfy(ChefFacts.Energy, Compare.GreaterOrEqual, 100)
            .RelevantWhen(s => s.Get(ChefFacts.Energy) < 40)
            .Priority(60f)
            .Heuristic(s => (100 - s.Get(ChefFacts.Energy)) / 100f)
            .Build();

        public static IReadOnlyList<IGoal> All() => new IGoal[]
        {
            ServeClient,
            PrepareMeal,
            TakeRest,
        };
    }
}
