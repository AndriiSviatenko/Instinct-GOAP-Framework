namespace Instinct.GOAP.Samples.Guard
{
    public sealed class GuardExecutor : IActionExecutor<GuardCommand>
    {
        private readonly GuardBlackboard _board;

        public GuardExecutor(GuardBlackboard board) => _board = board;

        public GuardCommand Translate(IWorldState state, IAction action, IAgentContext context)
        {
            if (action is IGuardAction guardAction)
                return guardAction.Translate(state, _board).From(action.Key);

            return GuardCommand.Idle;
        }

        public void OnSelected(IWorldState state, IAction action, IAgentContext context) { }

        public void OnCompleted(IAction action, IAgentContext context, bool success)
        {
            if (action is IGuardAction guardAction)
                guardAction.OnCompleted(_board, success);
        }
    }

    public sealed class GuardPolicy : IAgentPolicy
    {
        public const float ChaseStickiness = 6f;
        public const float DefaultStickiness = 2f;

        public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
        {
            if (!state.Get(GuardFacts.IntruderVisible)) return false;
            return plan.Goal.Key != GuardGoalKeys.CatchIntruder;
        }

        public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
        {
            if (currentGoal == null || goal.Key != currentGoal.Key) return 0f;
            return goal.Key == GuardGoalKeys.CatchIntruder ? ChaseStickiness : DefaultStickiness;
        }

        public void OnPlanCleared(IAgentContext context) { }
    }
}
