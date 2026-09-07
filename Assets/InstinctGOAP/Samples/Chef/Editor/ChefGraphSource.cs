using System.Collections.Generic;
using Instinct.GOAP;
using Instinct.GOAP.EditorTools;
using UnityEngine;

namespace Instinct.GOAP.Samples.Chef.EditorTools
{
    public sealed class ChefGraphSource : GoapGraphSource
    {
        public override string DisplayName => "Chef (sample)";

        public override IReadOnlyList<IAction> Actions => ChefActions.All();
        public override IReadOnlyList<IGoal> Goals => ChefGoals.All();

        public override IGoapAgentView FindLiveAgent()
            => Object.FindFirstObjectByType<ChefAgentHost>()?.Agent;

        public override IEnumerable<string> BadgesFor(IGoal goal)
        {
            if (goal.Key == ChefGoalKeys.ServeClient) yield return "prio=100";
            if (goal.Key == ChefGoalKeys.PrepareMeal) yield return "prio=80";
            if (goal.Key == ChefGoalKeys.TakeRest) yield return "recharge";
        }
    }
}
