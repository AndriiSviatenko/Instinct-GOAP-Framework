using System.Collections.Generic;

namespace Instinct.GOAP.Samples.Guard
{
    public sealed class GuardAgent : IGoapAgentView
    {
        private readonly GoapAgent<GuardCommand> _agent;
        private readonly GuardBlackboard _board;

        public GuardBlackboard Board => _board;

        public IPlan CurrentPlan => _agent.CurrentPlan;
        public IGoal CurrentGoal => _agent.CurrentGoal;
        public IAction CurrentAction => _agent.CurrentAction;
        public int PlanStep => _agent.PlanStep;

        public IReadOnlyList<GoalEvaluation> GoalScores => _agent.GoalEvaluations;

        public GuardCommand LastCommand { get; private set; }

        public GuardAgent(GuardBlackboard board)
        {
            _board = board;
            _agent = new GoapAgent<GuardCommand>(
                new GoapPlanner(maxIterations: 200, maxDepth: 6),
                GuardGoals.All(),
                GuardActions.All(),
                new GuardStateProvider(board),
                new GuardExecutor(board),
                board,
                new PlanningContext(board))
            {
                Policy = new GuardPolicy(),
                Fallback = _ => GuardCommand.Idle,
            };
        }

        public GuardCommand Tick()
        {
            LastCommand = _agent.Tick();
            return LastCommand;
        }

        public void NotifyActionComplete(bool success) => _agent.NotifyActionComplete(success);
        public void ForceReplan() => _agent.ForceReplan();

        public string PlanChain() => GoapExplain.Chain(_agent.CurrentPlan);
        public string ExplainDecision() => GoapExplain.Decision(_agent.GoalEvaluations, _agent.CurrentPlan);

        public static string ValidateDomain() =>
            new DomainBuilder()
                .AddActions(GuardActions.All())
                .AddGoals(GuardGoals.All())
                .DeclaredGoalsIn(typeof(GuardGoalKeys))
                .DeclaredActionsIn(typeof(GuardActionKeys))
                .Describe();
    }
}
