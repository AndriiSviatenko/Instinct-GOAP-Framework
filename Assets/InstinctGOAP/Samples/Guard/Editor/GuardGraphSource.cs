using System.Collections.Generic;
using Instinct.GOAP.EditorTools;
using UnityEngine;

namespace Instinct.GOAP.Samples.Guard.EditorTools
{
    public sealed class GuardGraphSource : GoapGraphSource
    {
        public override string DisplayName => "Guard (sample)";

        public override IReadOnlyList<IAction> Actions => GuardActions.All();
        public override IReadOnlyList<IGoal> Goals => GuardGoals.All();

        public override IGoapAgentView FindLiveAgent()
            => Object.FindFirstObjectByType<GuardAgentHost>()?.Agent;

        public override IEnumerable<string> BadgesFor(IGoal goal)
        {
            if (goal.Key == GuardGoalKeys.CatchIntruder) yield return "sticky=6";
            if (goal.Key == GuardGoalKeys.Patrol) yield return "background";
            if (goal.Key == GuardGoalKeys.CallBackup) yield return "one-shot";
        }
    }
}
