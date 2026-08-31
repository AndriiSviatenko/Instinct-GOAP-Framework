using System.Collections.Generic;

namespace Instinct.GOAP
{
    public interface IAction
    {
        ActionKey Key { get; }

        IReadOnlyList<ICondition> Preconditions { get; }
        IReadOnlyList<IEffect> Effects { get; }
        float Cost(IWorldState state, IPlanningContext context);

        WorldState ApplyTo(WorldState state);
    }

    public interface IGoal
    {
        GoalKey Key { get; }

        bool IsSatisfiedBy(IWorldState state);
        float Priority(IWorldState state);
        bool IsRelevant(IWorldState state);

        float? Heuristic(IWorldState state, IPlanningContext context);
    }

    public interface IInspectableGoal
    {
        IReadOnlyList<ICondition> Conditions { get; }
    }

    public interface IPlan
    {
        IGoal Goal { get; }
        IReadOnlyList<IAction> Actions { get; }
        float TotalCost { get; }
    }

    public enum PlanFailure
    {
        None = 0,

        AlreadySatisfied,

        Unreachable,

        IterationLimit,

        DepthLimit,
    }
}
