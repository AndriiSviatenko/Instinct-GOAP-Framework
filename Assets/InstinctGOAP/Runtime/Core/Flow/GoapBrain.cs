using System.Collections.Generic;

namespace Instinct.GOAP
{
    public sealed class RuntimeExecutor<TCtx> : IActionExecutor<IRuntimeAction<TCtx>> where TCtx : class
    {
        private readonly TCtx _ctx;
        private readonly IReadOnlyList<IFactBinding<TCtx>> _bindings;
        private readonly IWorldStateProvider _stateProvider;

        private IRuntimeAction<TCtx> _current;

        public RuntimeExecutor(TCtx ctx, IReadOnlyList<IFactBinding<TCtx>> bindings, IWorldStateProvider stateProvider)
        {
            _ctx = ctx;
            _bindings = bindings;
            _stateProvider = stateProvider;
        }

        public IRuntimeAction<TCtx> Translate(IWorldState state, IAction action, IAgentContext context)
            => action as IRuntimeAction<TCtx>;

        public void OnSelected(IWorldState state, IAction action, IAgentContext context)
        {
            var next = action as IRuntimeAction<TCtx>;
            if (ReferenceEquals(next, _current)) return;

            _current?.OnExit(_ctx, false);

            _current = next;
            _current?.OnEnter(_ctx);
        }

        public void OnCompleted(IAction action, IAgentContext context, bool success)
        {
            var finished = action as IRuntimeAction<TCtx>;
            if (finished == null) return;

            finished.OnExit(_ctx, success);
            _current = null;

            if (success && finished.MirrorsEffects) MirrorEffects(finished);
        }

        private void MirrorEffects(IRuntimeAction<TCtx> action)
        {
            if (_bindings == null || _bindings.Count == 0) return;

            var before = _stateProvider.GetState();
            var after = action.ApplyTo(before);

            bool writeAll = false;
            var touched = new HashSet<int>();
            var effects = action.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var subject = effects[i].Subject;
                if (subject == null) { writeAll = true; break; }
                touched.Add(subject.Id);
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (!binding.CanWrite) continue;
                if (!writeAll && !touched.Contains(binding.Fact.Id)) continue;
                binding.Write(_ctx, after);
            }
        }
    }

    public sealed class GoapBrain<TCtx> : IGoapAgentView where TCtx : class
    {
        private readonly GoapAgent<IRuntimeAction<TCtx>> _agent;
        private readonly TCtx _ctx;

        public GoapBrain(GoapDomain<TCtx> domain, TCtx ctx, IPlanner planner = null, IAgentPolicy policy = null)
        {
            _ctx = ctx;
            Domain = domain;

            var stateProvider = new BoundStateProvider<TCtx>(domain.FactsType, domain.Bindings, ctx);
            var executor = new RuntimeExecutor<TCtx>(ctx, domain.Bindings, stateProvider);

            _agent = new GoapAgent<IRuntimeAction<TCtx>>(
                planner ?? new GoapPlanner(),
                domain.Goals,
                domain.PlannerActions,
                stateProvider,
                executor,
                ctx as IAgentContext,
                new PlanningContext(ctx))
            {
                Policy = policy,
            };
        }

        public GoapDomain<TCtx> Domain { get; }

        public IPlan CurrentPlan => _agent.CurrentPlan;
        public IGoal CurrentGoal => _agent.CurrentGoal;
        public IAction CurrentAction => _agent.CurrentAction;
        public int PlanStep => _agent.PlanStep;
        public IReadOnlyList<GoalEvaluation> GoalScores => _agent.GoalEvaluations;

        public IAgentPolicy Policy
        {
            get => _agent.Policy;
            set => _agent.Policy = value;
        }

        public ActionStatus Tick()
        {
            var action = _agent.Tick();
            if (action == null) return ActionStatus.Failure;

            var status = action.Tick(_ctx);
            if (status != ActionStatus.Running)
                _agent.NotifyActionComplete(status == ActionStatus.Success);

            return status;
        }

        public void ForceReplan() => _agent.ForceReplan();

        public string PlanChain() => _agent.CurrentPlan == null ? "(none)" : GoapExplain.Chain(_agent.CurrentPlan);

        public string ExplainDecision() => GoapExplain.Decision(_agent.GoalEvaluations, _agent.CurrentPlan);
    }
}
