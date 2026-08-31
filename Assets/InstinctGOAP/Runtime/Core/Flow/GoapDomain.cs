using System;
using System.Collections.Generic;

namespace Instinct.GOAP
{
    public sealed class GoapDomain<TCtx> where TCtx : class
    {
        private readonly List<IAction> _actionsAsPlanner;

        internal GoapDomain(Type factsType,
                            IReadOnlyList<IRuntimeAction<TCtx>> actions,
                            IReadOnlyList<IGoal> goals,
                            IReadOnlyList<IFactBinding<TCtx>> bindings)
        {
            FactsType = factsType;
            Actions = actions;
            Goals = goals;
            Bindings = bindings;

            _actionsAsPlanner = new List<IAction>(actions.Count);
            for (int i = 0; i < actions.Count; i++) _actionsAsPlanner.Add(actions[i]);
        }

        public Type FactsType { get; }
        public IReadOnlyList<IRuntimeAction<TCtx>> Actions { get; }
        public IReadOnlyList<IGoal> Goals { get; }
        public IReadOnlyList<IFactBinding<TCtx>> Bindings { get; }

        public IReadOnlyList<IAction> PlannerActions => _actionsAsPlanner;

        public IGoal Goal(GoalKey key)
        {
            for (int i = 0; i < Goals.Count; i++)
                if (Goals[i].Key == key) return Goals[i];
            return null;
        }

        public IRuntimeAction<TCtx> Action(ActionKey key)
        {
            for (int i = 0; i < Actions.Count; i++)
                if (Actions[i].Key == key) return Actions[i];
            return null;
        }

        public string Describe() => new DomainBuilder()
            .AddActions(_actionsAsPlanner)
            .AddGoals(Goals)
            .Describe();
    }

    public sealed class GoapDomainBuilder<TCtx> where TCtx : class
    {
        private readonly Type _factsType;
        private readonly List<RuntimeActionBuilder<TCtx>> _actions = new();
        private readonly List<GoalBuilder> _goals = new();
        private readonly List<IFactBinding<TCtx>> _bindings = new();

        private GoapDomainBuilder(Type factsType) => _factsType = factsType;

        public static GoapDomainBuilder<TCtx> For<TFacts>() where TFacts : class
            => new GoapDomainBuilder<TCtx>(typeof(TFacts));

        public static GoapDomainBuilder<TCtx> For(Type factsType)
            => new GoapDomainBuilder<TCtx>(factsType);

        public GoapDomainBuilder<TCtx> Bind<T>(Fact<T> fact, Func<TCtx, T> read)
            => Bind(fact, read, null);

        public GoapDomainBuilder<TCtx> Bind<T>(Fact<T> fact, Func<TCtx, T> read, Action<TCtx, T> write)
        {
            _bindings.Add(new FactBinding<TCtx, T>(fact, read, write));
            return this;
        }

        public RuntimeActionBuilder<TCtx> Action(ActionKey key)
        {
            var builder = new RuntimeActionBuilder<TCtx>(ActionBuilder.Create(key));
            _actions.Add(builder);
            return builder;
        }

        public RuntimeActionBuilder<TCtx> Action(string name)
        {
            var builder = new RuntimeActionBuilder<TCtx>(ActionBuilder.Create(ActionKey.Named(name)));
            _actions.Add(builder);
            return builder;
        }

        public GoalBuilder Goal(GoalKey key)
        {
            var builder = GoalBuilder.Create(key);
            _goals.Add(builder);
            return builder;
        }

        public GoalBuilder Goal(string name)
        {
            var builder = GoalBuilder.Create(GoalKey.Named(name));
            _goals.Add(builder);
            return builder;
        }

        public GoapDomain<TCtx> Build()
        {
            var actions = new List<IRuntimeAction<TCtx>>(_actions.Count);
            foreach (var a in _actions) actions.Add(a.Build());

            var goals = new List<IGoal>(_goals.Count);
            foreach (var g in _goals) goals.Add(g.Build());

            return new GoapDomain<TCtx>(_factsType, actions, goals, _bindings);
        }
    }

    public sealed class RuntimeActionBuilder<TCtx> where TCtx : class
    {
        private readonly ActionBuilder _spec;
        private IStep<TCtx> _step;
        private Action<TCtx, bool> _onDone;
        private bool _mirrors = true;

        internal RuntimeActionBuilder(ActionBuilder spec) => _spec = spec;


        public RuntimeActionBuilder<TCtx> Require<T>(Fact<T> fact, T value)
        {
            _spec.Require(fact, value);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Require<T>(Fact<T> fact, Compare op, T value)
        {
            _spec.Require(fact, op, value);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Require(Func<IWorldState, bool> predicate, string description = null)
        {
            _spec.Require(predicate, description);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Effect<T>(Fact<T> fact, T value)
        {
            _spec.Effect(fact, value);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Add(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
        {
            _spec.Add(fact, delta, min, max);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Copy<T>(Fact<T> target, Fact<T> source)
        {
            _spec.Copy(target, source);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Cost(float cost)
        {
            _spec.Cost(cost);
            return this;
        }

        public RuntimeActionBuilder<TCtx> Cost(Func<IWorldState, TCtx, float> cost)
        {
            _spec.Cost((state, planning) => cost(state, planning?.GetExtra<TCtx>()));
            return this;
        }


        public RuntimeActionBuilder<TCtx> Run(IStep<TCtx> step)
        {
            _step = step;
            return this;
        }

        public RuntimeActionBuilder<TCtx> Run(Func<TCtx, ActionStatus> tick) => Run(Steps.Run(tick));

        public RuntimeActionBuilder<TCtx> Instant(Action<TCtx> body = null) => Run(Steps.Instant(body));

        public RuntimeActionBuilder<TCtx> OnDone(Action<TCtx, bool> onDone)
        {
            _onDone = onDone;
            return this;
        }

        public RuntimeActionBuilder<TCtx> NoMirror()
        {
            _mirrors = false;
            return this;
        }

        internal IRuntimeAction<TCtx> Build()
            => new RuntimeAction<TCtx>(_spec.Build(), _step, _onDone, _mirrors);
    }
}
