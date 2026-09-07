namespace Instinct.GOAP.Samples.Stalker
{

    public sealed class StalkerExecutor : IActionExecutor<StalkerCommand>
    {
        private readonly StalkerBlackboard _board;

        public StalkerExecutor(StalkerBlackboard board) => _board = board;

        public StalkerCommand Translate(IWorldState state, IAction action, IAgentContext context)
        {
            if (action is IStalkerAction stalkerAction)
                return stalkerAction.Translate(state, _board).From(action.Key);

            return StalkerCommand.Idle;
        }

        public void OnSelected(IWorldState state, IAction action, IAgentContext context) { }

        public void OnCompleted(IAction action, IAgentContext context, bool success)
        {
            if (action is IStalkerAction stalkerAction)
                stalkerAction.OnCompleted(_board, success);
        }
    }

    public sealed class StalkerPolicy : IAgentPolicy
    {
        public const float EmergencyStickiness = 10f;
        public const float NeedStickiness = 6f;
        public const float BackgroundStickiness = 2f;

        public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
        {
            var key = plan.Goal.Key;

            if (state.Get(StalkerFacts.EmissionActive) && key != StalkerGoalKeys.SurviveEmission)
                return true;

            if ((state.Get(StalkerFacts.EnemyVisible) || state.Get(StalkerFacts.MutantVisible))
                && key != StalkerGoalKeys.Defend)
                return true;

            return false;
        }

        public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
        {
            if (currentGoal == null || goal.Key != currentGoal.Key) return 0f;

            var key = goal.Key;
            if (key == StalkerGoalKeys.SurviveEmission || key == StalkerGoalKeys.Defend)
                return EmergencyStickiness;
            if (key == StalkerGoalKeys.SatisfyHunger || key == StalkerGoalKeys.Rest || key == StalkerGoalKeys.Heal)
                return NeedStickiness;
            return BackgroundStickiness;
        }

        public void OnPlanCleared(IAgentContext context) { }
    }
}
