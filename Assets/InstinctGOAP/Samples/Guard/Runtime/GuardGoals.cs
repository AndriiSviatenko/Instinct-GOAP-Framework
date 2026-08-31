using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Guard
{
    public static class GuardGoals
    {
        public static IGoal CatchIntruder() => GoalBuilder.Create(GuardGoalKeys.CatchIntruder)
            .Satisfy(GuardFacts.IntruderCaught, true)
            .RelevantWhen(s => s.Get(GuardFacts.IntruderVisible))
            .Priority(100f)
            .Heuristic(s => s.Get(GuardFacts.DistanceToIntruder) > 1.6f ? 1f : 0.5f)
            .Build();

        public static IGoal CallBackup() => GoalBuilder.Create(GuardGoalKeys.CallBackup)
            .Satisfy(GuardFacts.BackupCalled, true)
            .RelevantWhen(s => s.Get(GuardFacts.HasRadio)
                               && !s.Get(GuardFacts.BackupCalled)
                               && s.Get(GuardFacts.AlertLevel) >= Alert.Suspicious)
            .Priority(s => s.Get(GuardFacts.AlertLevel) == Alert.Hunting ? 80f : 30f)
            .Heuristic(_ => 1.5f)
            .Build();

        public static IGoal InvestigateNoise() => GoalBuilder.Create(GuardGoalKeys.InvestigateNoise)
            .Satisfy(GuardFacts.NoiseChecked, true)
            .RelevantWhen(s => s.Get(GuardFacts.HeardNoise) && !s.Get(GuardFacts.IntruderVisible))
            .Priority(50f)
            .Heuristic(_ => 2f)
            .Build();

        public static IGoal Patrol() => GoalBuilder.Create(GuardGoalKeys.Patrol)
            .Satisfy(GuardFacts.WaypointsVisited, Compare.GreaterOrEqual, 3)
            .RelevantWhen(s => !s.Get(GuardFacts.IntruderVisible) && !s.Get(GuardFacts.HeardNoise))
            .Priority(10f)
            .Heuristic(s => 2f * (3 - s.Get(GuardFacts.WaypointsVisited)))
            .Build();

        public static IReadOnlyList<IGoal> All() => new[]
        {
            CatchIntruder(),
            CallBackup(),
            InvestigateNoise(),
            Patrol(),
        };
    }
}
