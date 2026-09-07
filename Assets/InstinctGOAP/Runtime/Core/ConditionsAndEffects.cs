using System;

namespace Instinct.GOAP
{
    public enum Compare
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
    }

    public interface ICondition
    {
        string Description { get; }
        bool Test(IWorldState state);

        IFact Subject { get; }
    }

    public sealed class Condition<T> : ICondition
    {
        public readonly Fact<T> Fact;
        public readonly T Expected;
        public readonly Compare Op;

        private readonly bool _expectedBool;
        private readonly float _expectedNumeric;

        public Condition(Fact<T> fact, T expected) : this(fact, Compare.Equal, expected) { }

        public Condition(Fact<T> fact, Compare op, T expected)
        {
            if (typeof(T) == typeof(bool) && op != Compare.Equal && op != Compare.NotEqual)
                throw new ArgumentException($"Compare.{op} makes no sense for a bool fact ({fact.Name}).", nameof(op));

            Fact = fact;
            Op = op;
            Expected = expected;

            switch (FactKindOf<T>.Kind)
            {
                case FactKind.Bool: _expectedBool = (bool)(object)expected; break;
                case FactKind.Int: _expectedNumeric = (int)(object)expected; break;
                case FactKind.Float: _expectedNumeric = (float)(object)expected; break;
                case FactKind.Enum: _expectedNumeric = EnumSlot.ToInt(expected); break;
                default:
                    throw new NotSupportedException($"Condition on unsupported fact type {typeof(T)}.");
            }
        }

        public IFact Subject => Fact;

        public string Description => $"{Fact.Name} {Symbol(Op)} {Expected}";

        public bool Test(IWorldState state)
        {
            var reader = state as IWorldStateReader;

            if (FactKindOf<T>.Kind == FactKind.Bool)
            {
                bool actual = reader != null ? reader.ReadBool(Fact) : (bool)(object)state.Get(Fact);
                return Op == Compare.NotEqual ? actual != _expectedBool : actual == _expectedBool;
            }

            float a = reader != null ? reader.ReadFloat(Fact) : ReadNumericBoxed(state);
            float b = _expectedNumeric;
            return Op switch
            {
                Compare.Equal => a == b,
                Compare.NotEqual => a != b,
                Compare.Greater => a > b,
                Compare.GreaterOrEqual => a >= b,
                Compare.Less => a < b,
                Compare.LessOrEqual => a <= b,
                _ => false,
            };
        }

        private float ReadNumericBoxed(IWorldState state) => FactKindOf<T>.Kind switch
        {
            FactKind.Int => (int)(object)state.Get(Fact),
            FactKind.Float => (float)(object)state.Get(Fact),
            FactKind.Enum => EnumSlot.ToInt(state.Get(Fact)),
            _ => 0f,
        };

        internal static string Symbol(Compare op) => op switch
        {
            Compare.Equal => "==",
            Compare.NotEqual => "!=",
            Compare.Greater => ">",
            Compare.GreaterOrEqual => ">=",
            Compare.Less => "<",
            Compare.LessOrEqual => "<=",
            _ => "?",
        };
    }

    public sealed class PredicateCondition : ICondition
    {
        private readonly Func<IWorldState, bool> _predicate;
        public string Description { get; }

        public PredicateCondition(Func<IWorldState, bool> predicate, string description = null)
        {
            _predicate = predicate;
            Description = description ?? "custom";
        }

        public IFact Subject => null;

        public bool Test(IWorldState state) => _predicate(state);
    }

    public interface IEffect
    {
        string Description { get; }

        IFact Subject { get; }

        bool IsConstant { get; }

        void Apply(IWorldState pre, WorldState next);
    }

    public sealed class Effect<T> : IEffect
    {
        public readonly Fact<T> Fact;
        public readonly T Value;

        public Effect(Fact<T> fact, T value)
        {
            Fact = fact;
            Value = value;
        }

        public IFact Subject => Fact;
        public bool IsConstant => true;
        public string Description => $"{Fact.Name} := {Value}";

        public void Apply(IWorldState pre, WorldState next) => next.Set(Fact, Value);
    }

    public sealed class CopyEffect<T> : IEffect
    {
        public readonly Fact<T> Target;
        public readonly Fact<T> Source;

        public CopyEffect(Fact<T> target, Fact<T> source)
        {
            Target = target;
            Source = source;
        }

        public IFact Subject => Target;
        public bool IsConstant => false;
        public string Description => $"{Target.Name} := {Source.Name}";

        public void Apply(IWorldState pre, WorldState next) => next.Set(Target, pre.Get(Source));
    }

    public sealed class AddEffect : IEffect
    {
        public readonly Fact<int> Fact;
        public readonly int Delta;
        public readonly int Min;
        public readonly int Max;

        public AddEffect(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
        {
            Fact = fact;
            Delta = delta;
            Min = min;
            Max = max;
        }

        public IFact Subject => Fact;
        public bool IsConstant => false;
        public string Description => $"{Fact.Name} {(Delta >= 0 ? "+" : "-")}= {Math.Abs(Delta)}";

        public void Apply(IWorldState pre, WorldState next)
        {
            int v = pre.Get(Fact) + Delta;
            if (v < Min) v = Min;
            if (v > Max) v = Max;
            next.Set(Fact, v);
        }
    }

    public sealed class ComputedEffect<T> : IEffect
    {
        public readonly Fact<T> Fact;
        private readonly Func<IWorldState, T> _value;

        public ComputedEffect(Fact<T> fact, Func<IWorldState, T> value, string description = null)
        {
            Fact = fact;
            _value = value;
            Description = description ?? $"{fact.Name} := f(state)";
        }

        public IFact Subject => Fact;
        public bool IsConstant => false;
        public string Description { get; }

        public void Apply(IWorldState pre, WorldState next) => next.Set(Fact, _value(pre));
    }

    public sealed class DynamicEffect : IEffect
    {
        private readonly Action<IWorldState, WorldState> _apply;

        public DynamicEffect(Action<IWorldState, WorldState> apply, string description = null)
        {
            _apply = apply;
            Description = description ?? "dynamic";
        }

        public IFact Subject => null;
        public bool IsConstant => false;
        public string Description { get; }

        public void Apply(IWorldState pre, WorldState next) => _apply(pre, next);
    }

    internal interface IWorldStateReader
    {
        bool ReadBool<T>(Fact<T> fact);
        float ReadFloat<T>(Fact<T> fact);
    }
}
