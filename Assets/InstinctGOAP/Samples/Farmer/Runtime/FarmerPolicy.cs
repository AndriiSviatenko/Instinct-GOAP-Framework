using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Farmer
{
    public class FarmerPolicy : IAgentPolicy
    {
        private const float Stickiness = 2f;

        public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
            => false;

        public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state) =>
            currentGoal != null && goal.Key == currentGoal.Key ? Stickiness : 0f;

        public void OnPlanCleared(IAgentContext context) { }
    }
}
