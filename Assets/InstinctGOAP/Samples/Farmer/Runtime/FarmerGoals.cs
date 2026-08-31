using Instinct.GOAP;
using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Farmer
{
    public static class FarmerGoals
    {

        public static readonly IGoal WorkTheField = GoalBuilder.Create(FarmerGoalKeys.WorkTheField)
            .Satisfy(FarmerFacts.Energy, Compare.LessOrEqual, 15)
            .RelevantWhen(s => s.Get(FarmerFacts.Energy) >= 45)
            .Priority(40)
            .Heuristic(s => (s.Get(FarmerFacts.Energy) - 15) / 60f)
            .Build();

        public static readonly IGoal Recover = GoalBuilder.Create(FarmerGoalKeys.Recover)
            .Satisfy(FarmerFacts.Energy, Compare.GreaterOrEqual, 100)
            .RelevantWhen(s => s.Get(FarmerFacts.Energy) <= 20)
            .Priority(80)
            .Heuristic(s => (100 - s.Get(FarmerFacts.Energy)) / 100f)
            .Build();

        public static IReadOnlyList<IGoal> All() => new IGoal[]
        {
            WorkTheField,
            Recover,
        };
    }
}
