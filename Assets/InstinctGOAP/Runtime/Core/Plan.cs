using System.Collections.Generic;

namespace Instinct.GOAP
{
    public sealed class Plan : IPlan
    {
        public IGoal Goal { get; }
        public IReadOnlyList<IAction> Actions { get; }
        public float TotalCost { get; }

        public Plan(IGoal goal, IReadOnlyList<IAction> actions, float totalCost)
        {
            Goal = goal;
            Actions = actions;
            TotalCost = totalCost;
        }

        public override string ToString() => $"{Goal.NameOf()} [{Actions.Count} actions] cost={TotalCost:F2}";
    }
}
