using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Instinct.GOAP
{
    public sealed class GoalBuilder
    {
        private GoalKey _key;
        private readonly List<ICondition> _conditions = new();
        private Func<IWorldState, bool> _relevant = _ => true;
        private Func<IWorldState, float> _priority = _ => 1f;
        private Func<IWorldState, IPlanningContext, float?> _heuristic = (_, _) => null;

        public GoalBuilder Key(GoalKey key)
        {
            _key = key;
            return this;
        }

        public GoalBuilder Name(string name) => Key(GoalKey.Named(name));

        public static GoalBuilder Create(GoalKey key) => new GoalBuilder().Key(key);

        public static GoalBuilder Create([CallerMemberName] string callerName = "")
            => new GoalBuilder().Name(callerName);

        public GoalBuilder Satisfy<T>(Fact<T> fact, T value)
        {
            _conditions.Add(new Condition<T>(fact, value));
            return this;
        }

        public GoalBuilder Satisfy<T>(Fact<T> fact, Compare op, T value)
        {
            _conditions.Add(new Condition<T>(fact, op, value));
            return this;
        }

        public GoalBuilder Satisfy(Func<IWorldState, bool> predicate, string description = null)
        {
            _conditions.Add(new PredicateCondition(predicate, description));
            return this;
        }

        public GoalBuilder RelevantWhen(Func<IWorldState, bool> relevant)
        {
            _relevant = relevant;
            return this;
        }

        public GoalBuilder Priority(float priority)
        {
            _priority = _ => priority;
            return this;
        }

        public GoalBuilder Priority(Func<IWorldState, float> priority)
        {
            _priority = priority;
            return this;
        }

        public GoalBuilder Heuristic(Func<IWorldState, float?> heuristic)
        {
            _heuristic = (s, _) => heuristic(s);
            return this;
        }

        public GoalBuilder Heuristic(Func<IWorldState, IPlanningContext, float?> heuristic)
        {
            _heuristic = heuristic;
            return this;
        }

        public IGoal Build() => new BuiltGoal(_key, _conditions, _relevant, _priority, _heuristic);

        private sealed class BuiltGoal : IGoal, IInspectableGoal
        {
            public GoalKey Key { get; }
            private readonly IReadOnlyList<ICondition> _conditions;
            private readonly Func<IWorldState, bool> _relevant;
            private readonly Func<IWorldState, float> _priority;
            private readonly Func<IWorldState, IPlanningContext, float?> _heuristic;

            public IReadOnlyList<ICondition> Conditions => _conditions;

            public BuiltGoal(GoalKey key, IReadOnlyList<ICondition> conditions, Func<IWorldState, bool> relevant,
                             Func<IWorldState, float> priority, Func<IWorldState, IPlanningContext, float?> heuristic)
            {
                Key = key;
                _conditions = conditions;
                _relevant = relevant;
                _priority = priority;
                _heuristic = heuristic;
            }

            public bool IsSatisfiedBy(IWorldState state)
            {
                for (int i = 0; i < _conditions.Count; i++)
                    if (!_conditions[i].Test(state)) return false;
                return true;
            }

            public bool IsRelevant(IWorldState state) => _relevant(state);
            public float Priority(IWorldState state) => _priority(state);
            public float? Heuristic(IWorldState state, IPlanningContext context) => _heuristic(state, context);

            public override string ToString() => Key.ToString();
        }
    }
}
