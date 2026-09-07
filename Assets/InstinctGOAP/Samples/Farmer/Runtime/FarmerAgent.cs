using System.Collections.Generic;
using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Farmer
{
    public sealed class FarmerAgent : IGoapAgentView
    {
        private readonly GoapAgent<FarmerCommand> _agent;
        private readonly FarmerBlackboard _board;

        public FarmerBlackboard Blackboard => _board;
        public IGoal CurrentGoal => _agent.CurrentGoal;
        public IAction CurrentAction => _agent.CurrentAction;
        public IPlan CurrentPlan => _agent.CurrentPlan;
        public IReadOnlyList<GoalEvaluation> GoalScores => _agent.GoalEvaluations;

        public FarmerAgent(FarmerBlackboard b)
        {
            _board = b;
            _agent = new GoapAgent<FarmerCommand>(
                new GoapPlanner(100, 6),
                FarmerGoals.All(),
                FarmerActions.All(),
                new FarmerStateProvider(b),
                new FarmerExecutor(b))
            {
                Fallback = _ => FarmerCommand.None,
                Policy = new FarmerPolicy(),
            };

        }

        public FarmerCommand Tick() => _agent.Tick();

        public void NotifyActionComplete(bool success) => _agent.NotifyActionComplete(success);

        public void ForceReplan() => _agent.ForceReplan();

        public string PlanChain() => _agent.CurrentPlan == null
            ? "(none)"
            : GoapExplain.Chain(_agent.CurrentPlan);

        public string ExplainDecision() => GoapExplain.Decision(_agent.GoalEvaluations, _agent.CurrentPlan);
    }
}
