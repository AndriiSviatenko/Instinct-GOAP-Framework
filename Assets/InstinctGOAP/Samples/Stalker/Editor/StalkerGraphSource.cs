using System.Collections.Generic;
using Instinct.GOAP.EditorTools;
using UnityEngine;

namespace Instinct.GOAP.Samples.Stalker.EditorTools
{
    public sealed class StalkerGraphSource : GoapGraphSource
    {
        public override string DisplayName => "Stalker A-Life (sample)";

        public override IReadOnlyList<IAction> Actions => StalkerActions.All();
        public override IReadOnlyList<IGoal> Goals => StalkerGoals.All();

        public override IGoapAgentView FindLiveAgent()
            => Object.FindFirstObjectByType<StalkerAgentHost>()?.Agent;

        public override IEnumerable<string> BadgesFor(IGoal goal)
        {
            if (goal.Key == StalkerGoalKeys.SurviveEmission) yield return "emergency=1000";
            if (goal.Key == StalkerGoalKeys.Defend) yield return "emergency";
            if (goal.Key == StalkerGoalKeys.RoamZone) yield return "background";
            if (goal.Key == StalkerGoalKeys.TradeArtifacts) yield return "loot->cash";
            if (goal.Key == StalkerGoalKeys.Restock) yield return "supplies";
        }
    }
}
