using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{
    public static class ChefActionKeys
    {
        public static readonly ActionKey GetIngredients = ActionKey.Declare();
        public static readonly ActionKey WalkToStove = ActionKey.Declare();
        public static readonly ActionKey WalkToClient = ActionKey.Declare();
        public static readonly ActionKey WalkToBreak = ActionKey.Declare();
        public static readonly ActionKey CookMeal = ActionKey.Declare();
        public static readonly ActionKey ServeMeal = ActionKey.Declare();
        public static readonly ActionKey TakeBreak = ActionKey.Declare();
    }

    public static class ChefGoalKeys
    {
        public static readonly GoalKey ServeClient = GoalKey.Declare();
        public static readonly GoalKey PrepareMeal = GoalKey.Declare();
        public static readonly GoalKey TakeRest = GoalKey.Declare();
    }
}
