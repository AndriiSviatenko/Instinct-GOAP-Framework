using System.Collections.Generic;
using System.Text;

namespace Instinct.GOAP
{
    public static class GoapExplain
    {
        public static List<ICondition> BlockedBy(IAction action, IWorldState state)
        {
            var blocked = new List<ICondition>();
            foreach (var c in action.Preconditions)
                if (!c.Test(state)) blocked.Add(c);
            return blocked;
        }

        public static string Applicability(IReadOnlyList<IAction> actions, IWorldState state)
        {
            var sb = new StringBuilder();
            foreach (var a in actions)
            {
                var blocked = BlockedBy(a, state);
                sb.Append(blocked.Count == 0 ? "  OK   " : "  --   ").Append(a.NameOf());
                if (blocked.Count > 0)
                {
                    sb.Append("   blocked by: ");
                    for (int i = 0; i < blocked.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(blocked[i].Description);
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Failure(IGoal goal, PlanFailure failure) => failure switch
        {
            PlanFailure.None => $"{goal.NameOf()}: planned.",
            PlanFailure.AlreadySatisfied => $"{goal.NameOf()}: already true - nothing to plan.",
            PlanFailure.Unreachable =>
                $"{goal.NameOf()}: no chain of actions reaches it from here. Either a precondition is " +
                "waiting on the world (a fact only the snapshot writes), or an action lies about its effects.",
            PlanFailure.IterationLimit =>
                $"{goal.NameOf()}: ran out of planner iterations. Usually a weak heuristic, not a real dead end.",
            PlanFailure.DepthLimit =>
                $"{goal.NameOf()}: every branch hit maxDepth. The honest chain is longer than the planner may look " +
                "- raise maxDepth, or clamp whatever counter is driving the length.",
            _ => $"{goal.NameOf()}: {failure}",
        };

        public static string Decision(IReadOnlyList<GoalEvaluation> evaluations, IPlan chosen)
        {
            var sb = new StringBuilder();
            sb.Append("chosen: ").AppendLine(chosen == null
                ? "<none - falling back>"
                : $"{chosen.Goal.NameOf()}  cost={chosen.TotalCost:0.##}  plan={Chain(chosen)}");

            foreach (var e in evaluations)
            {
                if (!e.Relevant) { sb.Append("  --   ").Append(e.Goal.ToString()).AppendLine("   not relevant"); }
                else if (e.Skipped)
                    sb.Append("  ..   ").Append(e.Goal.ToString())
                      .Append("   skipped, best case ").Append(e.Priority.ToString("0.#"))
                      .AppendLine(" could not beat the winner");
                else if (e.PlanLength == 0)
                    sb.Append("  !!   ").Append(e.Goal.ToString()).Append("   no plan (").Append(e.Failure).AppendLine(")");
                else
                    sb.Append("  ok   ").Append(e.Goal.ToString())
                      .Append("   ").Append(e.Priority.ToString("0.#"))
                      .Append(" - ").Append(e.Cost.ToString("0.#"))
                      .Append(" = ").Append(e.Utility.ToString("0.#"))
                      .Append("  (len ").Append(e.PlanLength).AppendLine(")");
            }
            return sb.ToString();
        }

        public static string Chain(IPlan plan)
        {
            if (plan == null || plan.Actions.Count == 0) return "-";
            var names = new string[plan.Actions.Count];
            for (int i = 0; i < plan.Actions.Count; i++) names[i] = plan.Actions[i].NameOf();
            return string.Join(" -> ", names);
        }

        public static string State<TFacts>(WorldState state) where TFacts : class
        {
            var sb = new StringBuilder();
            foreach (var fact in FactSchema<TFacts>.Facts)
            {
                sb.Append(fact.Name).Append('=');
                var raw = state.Read(fact);

                if (fact.ValueType.IsEnum) sb.Append(System.Enum.ToObject(fact.ValueType, raw.AsInt));
                else sb.Append(raw);
                sb.Append("  ");
            }
            return sb.ToString();
        }
    }
}
