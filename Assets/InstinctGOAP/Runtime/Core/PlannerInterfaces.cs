using System.Collections.Generic;

namespace Instinct.GOAP
{
    public interface IPlanner
    {
        IPlan BuildPlan(IReadOnlyList<IAction> actions, IGoal goal, WorldState start, IPlanningContext context = null);

        PlanFailure LastFailure { get; }

        int LastExpandedNodes { get; }
    }
}
