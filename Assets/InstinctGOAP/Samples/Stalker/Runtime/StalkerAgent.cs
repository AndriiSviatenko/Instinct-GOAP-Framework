using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Stalker
{
    public sealed class StalkerAgent : IGoapAgentView
    {
        private readonly GoapAgent<StalkerCommand> _agent;

        public StalkerBlackboard Board { get; }

        public StalkerAgent(StalkerBlackboard board)
        {
            Board = board;
            _agent = new GoapAgent<StalkerCommand>(
                new GoapPlanner(maxIterations: 400, maxDepth: 8),
                StalkerGoals.All(),
                StalkerActions.All(),
                new StalkerStateProvider(board),
                new StalkerExecutor(board),
                board,
                new PlanningContext(board))
            {
                Policy = new StalkerPolicy(),
                Fallback = _ => StalkerCommand.Idle,
            };
        }

        public IPlan CurrentPlan => _agent.CurrentPlan;
        public IGoal CurrentGoal => _agent.CurrentGoal;
        public IAction CurrentAction => _agent.CurrentAction;
        public int PlanStep => _agent.PlanStep;

        public IReadOnlyList<GoalEvaluation> GoalScores => _agent.GoalEvaluations;

        public StalkerCommand Tick() => _agent.Tick();
        public void NotifyActionComplete(bool success) => _agent.NotifyActionComplete(success);
        public void ForceReplan() => _agent.ForceReplan();

        public string PlanChain() => GoapExplain.Chain(_agent.CurrentPlan);
        public string ExplainDecision() => GoapExplain.Decision(_agent.GoalEvaluations, _agent.CurrentPlan);

        public static string ValidateDomain() =>
            new DomainBuilder()
                .AddActions(StalkerActions.All())
                .AddGoals(StalkerGoals.All())
                .DeclaredGoalsIn(typeof(StalkerGoalKeys))
                .DeclaredActionsIn(typeof(StalkerActionKeys))
                .Describe();
    }
}
