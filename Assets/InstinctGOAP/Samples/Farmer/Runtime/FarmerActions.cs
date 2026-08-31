using Instinct.GOAP;
using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Farmer
{
    public static class FarmerActions
    {

        public static IAction WalkToField => ActionBuilder.Create(FarmerActionKeys.WalkToField)
            .Require(FarmerFacts.DistanceToField, Compare.Greater, 1f)
            .Effect(FarmerFacts.DistanceToField, 0f)
            .Cost(1f)
            .Build();

        public static IAction WalkToHome => ActionBuilder.Create(FarmerActionKeys.WalkToHome)
            .Require(FarmerFacts.DistanceToHome, Compare.Greater, 1f)
            .Effect(FarmerFacts.DistanceToHome, 0f)
            .Cost(1f)
            .Build();

        public static IAction Harvest => ActionBuilder.Create(FarmerActionKeys.Harvest)
            .Require(FarmerFacts.DistanceToField, Compare.LessOrEqual, 1f)
            .Require(FarmerFacts.Energy, Compare.GreaterOrEqual, 25)
            .Add(FarmerFacts.Energy, -25, min: 0, max: 100)
            .Add(FarmerFacts.CropsGrown, +1)
            .Cost(1f)
            .Build();

        public static IAction Rest => ActionBuilder.Create(FarmerActionKeys.Rest)
            .Require(FarmerFacts.DistanceToHome, Compare.LessOrEqual, 1f)
            .Effect(FarmerFacts.Energy, 100)
            .Cost(0.5f)
            .Build();

        public static IReadOnlyList<IAction> All() => new IAction[]
        {
            WalkToField,
            WalkToHome,
            Harvest,
            Rest,
        };
    }
}
