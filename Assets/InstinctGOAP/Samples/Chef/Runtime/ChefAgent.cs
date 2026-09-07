using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{
    public sealed class ChefAgent : IGoapAgentView
    {
        private readonly GoapAgent<ChefCommand> _agent;
        private readonly ChefBlackboard _board;

        public ChefAgent(ChefBlackboard board)
        {
            _board = board;
            _agent = new GoapAgent<ChefCommand>(
                new GoapPlanner(maxIterations: 200, maxDepth: 10),
                ChefGoals.All(),
                ChefActions.All(),
                new ChefStateProvider(board),
                new ChefExecutor(board),
                board,
                new PlanningContext(board))
            {
                Policy = new ChefPolicy(),
                Fallback = _ => ChefCommand.Idle,
            };
        }

        public IPlan CurrentPlan => _agent.CurrentPlan;
        public IGoal CurrentGoal => _agent.CurrentGoal;
        public IAction CurrentAction => _agent.CurrentAction;
        public ChefBlackboard Board => _board;

        public ChefCommand Tick() => _agent.Tick();
        public void NotifyActionComplete(bool success) => _agent.NotifyActionComplete(success);
        public void ForceReplan() => _agent.ForceReplan();

        public string PlanChain() => GoapExplain.Chain(_agent.CurrentPlan);
        public string ExplainDecision() => GoapExplain.Decision(_agent.GoalEvaluations, _agent.CurrentPlan);
    }
}
