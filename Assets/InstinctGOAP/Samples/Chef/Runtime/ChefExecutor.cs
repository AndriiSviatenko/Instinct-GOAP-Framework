using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{
    public sealed class ChefExecutor : IActionExecutor<ChefCommand>
    {
        private readonly ChefBlackboard _board;

        public ChefExecutor(ChefBlackboard board) => _board = board;

        public ChefCommand Translate(IWorldState state, IAction action, IAgentContext context)
        {
            var key = action.Key;

            if (key == ChefActionKeys.GetIngredients)
                return ChefCommand.MoveTo(_board.StoragePosition).From(key);

            if (key == ChefActionKeys.WalkToStove)
                return ChefCommand.MoveTo(_board.StovePosition).From(key);

            if (key == ChefActionKeys.WalkToClient)
                return ChefCommand.MoveTo(_board.ClientPosition).From(key);

            if (key == ChefActionKeys.WalkToBreak)
                return ChefCommand.MoveTo(_board.BreakPosition).From(key);

            if (key == ChefActionKeys.CookMeal)
                return ChefCommand.Cook().From(key);

            if (key == ChefActionKeys.ServeMeal)
                return ChefCommand.Serve().From(key);

            if (key == ChefActionKeys.TakeBreak)
                return ChefCommand.TakeBreak().From(key);

            return ChefCommand.Idle;
        }

        public void OnSelected(IWorldState state, IAction action, IAgentContext context) { }

        public void OnCompleted(IAction action, IAgentContext context, bool success)
        {
            if (!success) return;

            var key = action.Key;
            if (key == ChefActionKeys.GetIngredients) _board.PickUpIngredients();
            else if (key == ChefActionKeys.WalkToStove) _board.ArriveAtStove();
            else if (key == ChefActionKeys.WalkToClient) _board.ArriveAtClient();
            else if (key == ChefActionKeys.WalkToBreak) _board.ArriveAtBreak();
            else if (key == ChefActionKeys.CookMeal) _board.FinishCooking();
            else if (key == ChefActionKeys.ServeMeal) _board.ServeFood();
            else if (key == ChefActionKeys.TakeBreak) _board.TakeBreak();
        }
    }

    public sealed class ChefPolicy : IAgentPolicy
    {
        public const float ServeStickiness = 6f;
        public const float DefaultStickiness = 2f;

        public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
        {
            return plan.Goal.Key == ChefGoalKeys.TakeRest
                && state.Get(ChefFacts.ClientHunger) > 0
                && state.Get(ChefFacts.ClientPresent)
                && state.Get(ChefFacts.Energy) >= 35;
        }

        public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
        {
            if (currentGoal == null || goal.Key != currentGoal.Key) return 0f;
            return goal.Key == ChefGoalKeys.ServeClient ? ServeStickiness : DefaultStickiness;
        }

        public void OnPlanCleared(IAgentContext context) { }
    }
}
