using System.Collections.Generic;
using Instinct.GOAP;
using Instinct.GOAP.Samples.Farmer;
using Instinct.GOAP.EditorTools;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer.EditorTools
{
    public sealed class FarmerGraphSource : GoapGraphSource
    {

        private readonly GoapDomain<FarmerContext> _domain = FarmerBrain.Build();

        public override string DisplayName => "Farmer (course)";

        public override IReadOnlyList<IAction> Actions => _domain.PlannerActions;
        public override IReadOnlyList<IGoal> Goals => _domain.Goals;

        public override IGoapAgentView FindLiveAgent()
            => Object.FindFirstObjectByType<FarmerHost>()?.Brain;

        public override IEnumerable<string> BadgesFor(IGoal goal)
        {
            if (goal.Key == FarmerGoalKeys.WorkTheField) yield return "tire-to-15";
            if (goal.Key == FarmerGoalKeys.Recover) yield return "recharge";
        }
    }
}
