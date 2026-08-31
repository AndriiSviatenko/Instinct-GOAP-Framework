using System;
using System.Collections.Generic;

namespace Instinct.GOAP
{
    public interface IGoapAgentView
    {
        IPlan CurrentPlan { get; }
        IAction CurrentAction { get; }
        IGoal CurrentGoal { get; }
    }

    public interface IGoapAgent<out TCommand> : IGoapAgentView
    {
        TCommand Tick();
        void NotifyActionComplete(bool success);
        void ForceReplan();
    }

    public readonly struct GoalEvaluation
    {
        public readonly GoalKey Goal;
        public readonly bool Relevant;
        public readonly float Priority;
        public readonly float Cost;
        public readonly float Utility;
        public readonly int PlanLength;

        public readonly PlanFailure Failure;

        public readonly bool Skipped;

        public GoalEvaluation(GoalKey goal, bool relevant, float priority, float cost, float utility,
                              int planLength, PlanFailure failure = PlanFailure.None, bool skipped = false)
        {
            Goal = goal; Relevant = relevant; Priority = priority; Cost = cost; Utility = utility;
            PlanLength = planLength; Failure = failure; Skipped = skipped;
        }
    }

    public interface IAgentPolicy
    {
        bool ShouldAbandonPlan(IPlan plan, int step, WorldState state);

        float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state);

        void OnPlanCleared(IAgentContext context);
    }

    public sealed class GoapAgent<TCommand> : IGoapAgent<TCommand>
    {
        private readonly IPlanner _planner;
        private readonly IReadOnlyList<IGoal> _goals;
        private readonly IReadOnlyList<IAction> _actions;
        private readonly IWorldStateProvider _stateProvider;
        private readonly IActionExecutor<TCommand> _executor;
        private readonly IAgentContext _context;
        private readonly IPlanningContext _planningContext;

        private readonly List<GoalEvaluation> _evaluations = new();
        private readonly List<Candidate> _candidates = new();

        private static readonly Comparison<Candidate> ByUpperBoundDesc = (a, b) =>
        {
            int byBound = b.UpperBound.CompareTo(a.UpperBound);
            return byBound != 0 ? byBound : a.Index.CompareTo(b.Index);
        };

        private IPlan _activePlan;
        private int _planStep;
        private bool _selectionNotified;

        public IPlan CurrentPlan => _activePlan;
        public IGoal CurrentGoal => _activePlan?.Goal;
        public IAction CurrentAction => _activePlan != null && _planStep < _activePlan.Actions.Count ? _activePlan.Actions[_planStep] : null;
        public int PlanStep => _planStep;

        public IReadOnlyList<GoalEvaluation> GoalEvaluations => _evaluations;

        public int LastPlannedGoals { get; private set; }

        public IAgentPolicy Policy { get; set; }

        public Func<WorldState, TCommand> Fallback { get; set; }

        public GoapAgent(IPlanner planner, IReadOnlyList<IGoal> goals, IReadOnlyList<IAction> actions,
                         IWorldStateProvider stateProvider, IActionExecutor<TCommand> executor,
                         IAgentContext context = null, IPlanningContext planningContext = null)
        {
            _planner = planner;
            _goals = goals;
            _actions = actions;
            _stateProvider = stateProvider;
            _executor = executor;
            _context = context;
            _planningContext = planningContext ?? new PlanningContext();
        }

        public TCommand Tick()
        {
            var state = _stateProvider.GetState();
            if (ShouldReplan(state))
            {
                var previousGoal = _activePlan?.Goal;
                if (_activePlan != null)
                    ClearPlan(interruptCurrentAction: true);

                _activePlan = SelectBestPlan(state, previousGoal);
                _planStep = 0;
            }

            var action = CurrentAction;
            if (action == null)
                return Fallback != null ? Fallback(state) : default;

            if (!_selectionNotified)
            {
                _executor.OnSelected(state, action, _context);
                _selectionNotified = true;
            }

            return _executor.Translate(state, action, _context);
        }

        public void NotifyActionComplete(bool success)
        {
            var action = CurrentAction;
            if (action == null) return;

            _executor.OnCompleted(action, _context, success);
            _selectionNotified = false;

            if (!success)
            {
                ClearPlan();
                return;
            }

            _planStep++;
            if (_activePlan != null && _planStep >= _activePlan.Actions.Count)
                ClearPlan();
        }

        public void ForceReplan() => ClearPlan(interruptCurrentAction: true);

        private void ClearPlan(bool interruptCurrentAction = false)
        {
            var action = CurrentAction;
            if (interruptCurrentAction && _selectionNotified && action != null)
                _executor.OnCompleted(action, _context, false);

            _selectionNotified = false;
            _activePlan = null;
            _planStep = 0;
            Policy?.OnPlanCleared(_context);
        }

        private bool ShouldReplan(WorldState state)
        {
            if (_activePlan == null) return true;
            if (_planStep >= _activePlan.Actions.Count) return true;
            if (Policy != null && Policy.ShouldAbandonPlan(_activePlan, _planStep, state)) return true;
            if (!_activePlan.Goal.IsRelevant(state)) return true;
            if (!_activePlan.Actions[_planStep].PreconditionsSatisfied(state)) return true;
            return false;
        }

        private readonly struct Candidate
        {
            public readonly IGoal Goal;
            public readonly float Priority;
            public readonly float Bias;
            public readonly float UpperBound;
            public readonly int Index;

            public Candidate(IGoal goal, float priority, float bias, int index)
            {
                Goal = goal;
                Priority = priority;
                Bias = bias;
                UpperBound = priority + bias;
                Index = index;
            }
        }

        private IPlan SelectBestPlan(WorldState state, IGoal currentGoal)
        {
            _evaluations.Clear();
            _candidates.Clear();
            LastPlannedGoals = 0;

            for (int i = 0; i < _goals.Count; i++)
            {
                var goal = _goals[i];
                if (!goal.IsRelevant(state))
                {
                    _evaluations.Add(new GoalEvaluation(goal.Key, false, 0f, float.PositiveInfinity, float.NegativeInfinity, 0));
                    continue;
                }
                float bias = Policy?.UtilityBias(goal, currentGoal, state) ?? 0f;
                _candidates.Add(new Candidate(goal, goal.Priority(state), bias, i));
            }

            _candidates.Sort(ByUpperBoundDesc);

            IPlan best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _candidates.Count; i++)
            {
                var c = _candidates[i];

                if (best != null && c.UpperBound <= bestScore)
                {
                    for (int j = i; j < _candidates.Count; j++)
                    {
                        var rest = _candidates[j];
                        _evaluations.Add(new GoalEvaluation(rest.Goal.Key, true, rest.Priority,
                            float.PositiveInfinity, float.NegativeInfinity, 0, PlanFailure.None, skipped: true));
                    }
                    break;
                }

                LastPlannedGoals++;
                var plan = _planner.BuildPlan(_actions, c.Goal, state, _planningContext);
                if (plan == null || float.IsInfinity(plan.TotalCost))
                {
                    var why = plan == null ? _planner.LastFailure : PlanFailure.Unreachable;
                    _evaluations.Add(new GoalEvaluation(c.Goal.Key, true, c.Priority,
                        float.PositiveInfinity, float.NegativeInfinity, 0, why));
                    continue;
                }

                float utility = c.Priority - plan.TotalCost;
                _evaluations.Add(new GoalEvaluation(c.Goal.Key, true, c.Priority, plan.TotalCost,
                    utility, plan.Actions.Count));

                float score = utility + c.Bias;
                if (best == null || score > bestScore)
                {
                    bestScore = score;
                    best = plan;
                }
            }

            return best;
        }
    }

    public static class ActionExtensions
    {
        public static bool PreconditionsSatisfied(this IAction action, IWorldState state)
        {
            var conditions = action.Preconditions;
            for (int i = 0; i < conditions.Count; i++)
                if (!conditions[i].Test(state)) return false;
            return true;
        }
    }
}
