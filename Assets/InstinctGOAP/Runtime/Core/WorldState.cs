using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Instinct.GOAP
{
    public interface IWorldState
    {
        T Get<T>(Fact<T> fact);
        bool Has<T>(Fact<T> fact);
    }

    public sealed class WorldState : IWorldState, IWorldStateReader, IEquatable<WorldState>
    {
        private readonly FactValue[] _values;
        private readonly int[] _slotByFactId;
        private int _cachedHash;
        private bool _hashDirty = true;
        private bool _frozen;

        private WorldState(int[] slotByFactId, int count)
        {
            _slotByFactId = slotByFactId;
            _values = new FactValue[count];
        }

        private WorldState(WorldState src)
        {
            _slotByFactId = src._slotByFactId;
            _values = new FactValue[src._values.Length];
            Array.Copy(src._values, _values, src._values.Length);
            _cachedHash = src._cachedHash;
            _hashDirty = src._hashDirty;
        }

        public static WorldState For<TFacts>() where TFacts : class
            => For(FactSchema<TFacts>.Schema);

        public static WorldState For(Type factsType) => For(FactSchema.Of(factsType));

        private static WorldState For(FactSchema.Schema schema)
            => new WorldState(schema.SlotByFactId, schema.Count);

        public static WorldState Empty => new WorldState(Array.Empty<int>(), 0);

        public int SlotCount => _values.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SlotOf<T>(Fact<T> fact)
        {
            int slot = fact.Id < _slotByFactId.Length ? _slotByFactId[fact.Id] : -1;
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(fact),
                    $"Fact '{fact.Name}' (id {fact.Id}) does not belong to this state's schema. " +
                    "Mixing facts from two schemas is always a bug - build the state with WorldState.For<TheRightFacts>().");
            return slot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(Fact<T> fact)
        {
            var v = _values[SlotOf(fact)];
            switch (FactKindOf<T>.Kind)
            {
                case FactKind.Bool: return (T)(object)v.AsBool;
                case FactKind.Int: return (T)(object)v.AsInt;
                case FactKind.Float: return (T)(object)v.AsFloat;

                case FactKind.Enum: return (T)Enum.ToObject(typeof(T), v.AsInt);
                default: throw new InvalidOperationException($"Unsupported fact type {typeof(T)}.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IWorldStateReader.ReadBool<T>(Fact<T> fact) => _values[SlotOf(fact)].AsBool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float IWorldStateReader.ReadFloat<T>(Fact<T> fact) => _values[SlotOf(fact)].AsFloat;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WorldState Set<T>(Fact<T> fact, T value)
        {
            ThrowIfFrozen();
            FactValue fv = FactKindOf<T>.Kind switch
            {
                FactKind.Bool => FactValue.Of((bool)(object)value),
                FactKind.Int => FactValue.Of((int)(object)value),
                FactKind.Float => FactValue.Of((float)(object)value),
                FactKind.Enum => FactValue.Of(EnumSlot.ToInt(value)),
                _ => throw new InvalidOperationException($"Unsupported fact type {typeof(T)}.")
            };
            _values[SlotOf(fact)] = fv;
            _hashDirty = true;
            return this;
        }

        public FactValue Read(IFact fact)
        {
            int slot = fact.Id < _slotByFactId.Length ? _slotByFactId[fact.Id] : -1;
            return slot >= 0 ? _values[slot] : FactValue.None;
        }

        public bool Has<T>(Fact<T> fact)
        {
            int slot = fact.Id < _slotByFactId.Length ? _slotByFactId[fact.Id] : -1;
            return slot >= 0 && !_values[slot].IsNone;
        }

        public WorldState Clone() => new WorldState(this);

        public WorldState Freeze()
        {
            _frozen = true;
            return this;
        }

        public bool IsFrozen => _frozen;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("DEBUG")]
        private void ThrowIfFrozen()
        {
            if (_frozen)
                throw new InvalidOperationException(
                    "This WorldState is frozen (the planner is holding it as a search key). " +
                    "Clone it before writing - see IAction.ApplyTo.");
        }

        public bool Equals(WorldState other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            if (!ReferenceEquals(_slotByFactId, other._slotByFactId)) return false;
            for (int i = 0; i < _values.Length; i++)
                if (!_values[i].Equals(other._values[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is WorldState ws && Equals(ws);

        public override int GetHashCode()
        {
            if (!_hashDirty) return _cachedHash;
            var hc = new HashCode();
            for (int i = 0; i < _values.Length; i++)
            {
                if (_values[i].IsNone) continue;
                hc.Add(i);
                hc.Add(_values[i]);
            }
            _cachedHash = hc.ToHashCode();
            _hashDirty = false;
            return _cachedHash;
        }
    }
}
