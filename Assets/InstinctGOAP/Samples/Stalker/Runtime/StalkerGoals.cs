using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Stalker
{

    public static class StalkerGoals
    {

        public static IGoal SurviveEmission() => GoalBuilder.Create(StalkerGoalKeys.SurviveEmission)
            .Satisfy(StalkerFacts.SafeFromEmission, true)
            .RelevantWhen(s => s.Get(StalkerFacts.EmissionActive) && !s.Get(StalkerFacts.SafeFromEmission))
            .Priority(1000f)
            .Heuristic(_ => 4f)
            .Build();

        public static IGoal Defend() => GoalBuilder.Create(StalkerGoalKeys.Defend)
            .Satisfy(s => s.Get(StalkerFacts.ThreatDealt) || s.Get(StalkerFacts.SafeFromThreat), "threat resolved")
            .RelevantWhen(s => s.Get(StalkerFacts.EnemyVisible) || s.Get(StalkerFacts.MutantVisible))
            .Priority(s => s.Get(StalkerFacts.EnemyVisible) ? 400f : 350f)
            .Heuristic(_ => 2f)
            .Build();

        public static IGoal SatisfyHunger() => GoalBuilder.Create(StalkerGoalKeys.SatisfyHunger)
            .Satisfy(StalkerFacts.Hunger, Compare.LessOrEqual, 30)
            .RelevantWhen(s => s.Get(StalkerFacts.Hunger) > 50)
            .Priority(s => s.Get(StalkerFacts.Hunger) * 3f)
            .Heuristic(_ => 2f)
            .Build();

        public static IGoal Rest() => GoalBuilder.Create(StalkerGoalKeys.Rest)
            .Satisfy(StalkerFacts.Energy, Compare.Greater, 90)
            .RelevantWhen(s => s.Get(StalkerFacts.Energy) < 25)
            .Priority(s => (100f - s.Get(StalkerFacts.Energy)) * 3f)
            .Heuristic(_ => 3f)
            .Build();

        public static IGoal Heal() => GoalBuilder.Create(StalkerGoalKeys.Heal)
            .Satisfy(StalkerFacts.Health, Compare.Greater, 90)
            .RelevantWhen(s => s.Get(StalkerFacts.Health) < 50 && s.Get(StalkerFacts.HasMedkit))
            .Priority(s => (100f - s.Get(StalkerFacts.Health)) * 5f)
            .Heuristic(_ => 1f)
            .Build();

        public static IGoal CollectArtifact() => GoalBuilder.Create(StalkerGoalKeys.CollectArtifact)
            .Satisfy(StalkerFacts.ArtifactCollected, true)
            .RelevantWhen(s => s.Get(StalkerFacts.AnomalyNearby)
                               && !s.Get(StalkerFacts.EnemyVisible)
                               && !s.Get(StalkerFacts.MutantVisible)
                               && !s.Get(StalkerFacts.EmissionActive)
                               && s.Get(StalkerFacts.Artifacts) < 2)
            .Priority(180f)
            .Heuristic(_ => 3f)
            .Build();

        public static IGoal TradeArtifacts() => GoalBuilder.Create(StalkerGoalKeys.TradeArtifacts)
            .Satisfy(StalkerFacts.Artifacts, Compare.LessOrEqual, 0)
            .RelevantWhen(s => s.Get(StalkerFacts.Artifacts) > 0
                               && !s.Get(StalkerFacts.EmissionActive)
                               && !s.Get(StalkerFacts.EnemyVisible)
                               && !s.Get(StalkerFacts.MutantVisible))
            .Priority(160f)
            .Heuristic(_ => 2f)
            .Build();

        public static IGoal Restock() => GoalBuilder.Create(StalkerGoalKeys.Restock)
            .Satisfy(s => s.Get(StalkerFacts.HasFood) && s.Get(StalkerFacts.HasMedkit), "supplies restocked")
            .RelevantWhen(s => s.Get(StalkerFacts.Money) >= 200
                               && (!s.Get(StalkerFacts.HasFood) || !s.Get(StalkerFacts.HasMedkit))
                               && !s.Get(StalkerFacts.EmissionActive)
                               && !s.Get(StalkerFacts.EnemyVisible)
                               && !s.Get(StalkerFacts.MutantVisible))
            .Priority(140f)
            .Heuristic(_ => 2f)
            .Build();

        public static IGoal RoamZone() => GoalBuilder.Create(StalkerGoalKeys.RoamZone)
            .Satisfy(StalkerFacts.PatrolPointsVisited, Compare.GreaterOrEqual, 4)
            .RelevantWhen(s => !s.Get(StalkerFacts.EmissionActive)
                               && !s.Get(StalkerFacts.EnemyVisible)
                               && !s.Get(StalkerFacts.MutantVisible)
                               && !s.Get(StalkerFacts.AnomalyNearby)
                               && s.Get(StalkerFacts.Hunger) <= 50
                               && s.Get(StalkerFacts.Energy) >= 25)
            .Priority(20f)
            .Heuristic(s => 1f * (4 - s.Get(StalkerFacts.PatrolPointsVisited)))
            .Build();

        public static IReadOnlyList<IGoal> All() => new[]
        {
            SurviveEmission(),
            Defend(),
            SatisfyHunger(),
            Rest(),
            Heal(),
            CollectArtifact(),
            TradeArtifacts(),
            Restock(),
            RoamZone(),
        };
    }
}
