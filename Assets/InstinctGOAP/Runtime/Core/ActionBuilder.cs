using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Instinct.GOAP
{
    public sealed class ActionBuilder
    {
        private ActionKey _key;
        private readonly List<ICondition> _preconditions = new();
        private readonly List<IEffect> _effects = new();
        private Func<IWorldState, IPlanningContext, float> _cost = (_, _) => 1f;

        public ActionBuilder Key(ActionKey key)
        {
            _key = key;
            return this;
        }

        public ActionBuilder Name(string name) => Key(ActionKey.Named(name));

        public static ActionBuilder For<T>() where T : IAction => new ActionBuilder().Key(ActionKey.Of<T>());

        public static ActionBuilder Create(ActionKey key) => new ActionBuilder().Key(key);

        public static ActionBuilder Create([CallerMemberName] string callerName = "")
            => new ActionBuilder().Name(callerName);

        public ActionBuilder Require<T>(Fact<T> fact, T value)
        {
            _preconditions.Add(new Condition<T>(fact, value));
            return this;
        }

        public ActionBuilder Require<T>(Fact<T> fact, Compare op, T value)
        {
            _preconditions.Add(new Condition<T>(fact, op, value));
            return this;
        }

        public ActionBuilder Require(Func<IWorldState, bool> predicate, string description = null)
        {
            _preconditions.Add(new PredicateCondition(predicate, description));
            return this;
        }

        public ActionBuilder Effect<T>(Fact<T> fact, T value)
        {
            _effects.Add(new Effect<T>(fact, value));
            return this;
        }

        public ActionBuilder Copy<T>(Fact<T> target, Fact<T> source)
        {
            _effects.Add(new CopyEffect<T>(target, source));
            return this;
        }

        public ActionBuilder Add(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
        {
            _effects.Add(new AddEffect(fact, delta, min, max));
            return this;
        }

        public ActionBuilder Computed<T>(Fact<T> fact, Func<IWorldState, T> value, string description = null)
        {
            _effects.Add(new ComputedEffect<T>(fact, value, description));
            return this;
        }

        public ActionBuilder DynamicEffect(Action<IWorldState, WorldState> apply, string description = null)
        {
            _effects.Add(new Instinct.GOAP.DynamicEffect(apply, description));
            return this;
        }

        public ActionBuilder Cost(float cost)
        {
            _cost = (_, _) => cost;
            return this;
        }

        public ActionBuilder Cost(Func<IWorldState, IPlanningContext, float> cost)
        {
            _cost = cost;
            return this;
        }

        public IAction Build() => new BuiltAction(_key, _preconditions, _effects, _cost);

        private sealed class BuiltAction : IAction
        {
            public ActionKey Key { get; }
            public IReadOnlyList<ICondition> Preconditions { get; }
            public IReadOnlyList<IEffect> Effects { get; }
            private readonly Func<IWorldState, IPlanningContext, float> _cost;

            public BuiltAction(ActionKey key, IReadOnlyList<ICondition> preconditions, IReadOnlyList<IEffect> effects,
                               Func<IWorldState, IPlanningContext, float> cost)
            {
                Key = key;
                Preconditions = preconditions;
                Effects = effects;
                _cost = cost;
            }

            public float Cost(IWorldState state, IPlanningContext context) => _cost(state, context);

            public WorldState ApplyTo(WorldState state) => EffectExtensions.ApplyAll(Effects, state);

            public override string ToString() => Key.ToString();
        }
    }

    public static class EffectExtensions
    {
        public static WorldState ApplyAll(IReadOnlyList<IEffect> effects, WorldState state)
        {
            var next = state.Clone();
            for (int i = 0; i < effects.Count; i++) effects[i].Apply(state, next);
            return next;
        }
    }
}
