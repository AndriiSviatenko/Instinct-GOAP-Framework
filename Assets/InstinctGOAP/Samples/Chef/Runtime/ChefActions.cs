using Instinct.GOAP;
using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Chef
{
    public static class ChefActions
    {
        public static IAction GetIngredients =>
            ActionBuilder.Create(ChefActionKeys.GetIngredients)
                .Require(ChefFacts.HasIngredients, false)
                .Effect(ChefFacts.HasIngredients, true)
                .Add(ChefFacts.Energy, -5, min: 0)
                .Cost(1f)
                .Build();

        public static IAction WalkToStove =>
            ActionBuilder.Create(ChefActionKeys.WalkToStove)
                .Require(ChefFacts.DistanceToStove, Compare.Greater, 1.5f)
                .Effect(ChefFacts.DistanceToStove, 1.0f)
                .Add(ChefFacts.Energy, -5, min: 0)
                .Cost((state, context) => 1f + state.Get(ChefFacts.DistanceToStove) * 0.3f)
                .Build();

        public static IAction WalkToClient =>
            ActionBuilder.Create(ChefActionKeys.WalkToClient)
                .Require(ChefFacts.ClientPresent, true)
                .Require(ChefFacts.DistanceToClient, Compare.Greater, 1.5f)
                .Effect(ChefFacts.DistanceToClient, 1.0f)
                .Add(ChefFacts.Energy, -5, min: 0)
                .Cost((state, context) => 1f + state.Get(ChefFacts.DistanceToClient) * 0.3f)
                .Build();

        public static IAction WalkToBreak =>
            ActionBuilder.Create(ChefActionKeys.WalkToBreak)
                .Require(ChefFacts.DistanceToBreak, Compare.Greater, 1.5f)
                .Effect(ChefFacts.DistanceToBreak, 1.0f)
                .Add(ChefFacts.Energy, -5, min: 0)
                .Cost((state, context) => 1f + state.Get(ChefFacts.DistanceToBreak) * 0.3f)
                .Build();

        public static IAction CookMeal =>
            ActionBuilder.Create(ChefActionKeys.CookMeal)
                .Require(ChefFacts.HasIngredients, true)
                .Require(ChefFacts.MealReady, false)
                .Require(ChefFacts.DistanceToStove, Compare.LessOrEqual, 1.5f)
                .Require(ChefFacts.Energy, Compare.GreaterOrEqual, 25)
                .Effect(ChefFacts.MealReady, true)
                .Effect(ChefFacts.HasIngredients, false)
                .Add(ChefFacts.Energy, -25, min: 0)
                .Effect(ChefFacts.ChefState, ChefState.Cooking)
                .Cost(2f)
                .Build();

        public static IAction ServeMeal =>
            ActionBuilder.Create(ChefActionKeys.ServeMeal)
                .Require(ChefFacts.MealReady, true)
                .Require(ChefFacts.ClientPresent, true)
                .Require(ChefFacts.DistanceToClient, Compare.LessOrEqual, 1.5f)
                .Require(ChefFacts.Energy, Compare.GreaterOrEqual, 10)
                .Effect(ChefFacts.MealReady, false)
                .Add(ChefFacts.Energy, -10, min: 0)
                .Effect(ChefFacts.ChefState, ChefState.Serving)
                .Add(ChefFacts.ClientHunger, -100, min: 0)
                .Cost(0.5f)
                .Build();

        public static IAction TakeBreak =>
            ActionBuilder.Create(ChefActionKeys.TakeBreak)
                .Require(ChefFacts.DistanceToBreak, Compare.LessOrEqual, 1.5f)
                .Require(ChefFacts.Energy, Compare.Less, 100)
                .Effect(ChefFacts.Energy, 100)
                .Effect(ChefFacts.ChefState, ChefState.Idle)
                .Cost(1f)
                .Build();

        public static IReadOnlyList<IAction> All() => new IAction[]
        {
            GetIngredients,
            WalkToStove,
            WalkToClient,
            WalkToBreak,
            CookMeal,
            ServeMeal,
            TakeBreak,
        };
    }
}
