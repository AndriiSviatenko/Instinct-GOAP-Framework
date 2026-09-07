using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Instinct.GOAP
{
    public interface IFact
    {
        int Id { get; }
        string Name { get; }
        Type ValueType { get; }
    }

    public enum FactKind : byte
    {
        Unsupported = 0,
        Bool,
        Int,
        Float,

        Enum,
    }

    public static class FactKindOf<T>
    {
        public static readonly FactKind Kind = Resolve();

        private static FactKind Resolve()
        {
            if (typeof(T) == typeof(bool)) return FactKind.Bool;
            if (typeof(T) == typeof(int)) return FactKind.Int;
            if (typeof(T) == typeof(float)) return FactKind.Float;

            if (typeof(T).IsEnum)
                return Enum.GetUnderlyingType(typeof(T)) == typeof(long)
                    || Enum.GetUnderlyingType(typeof(T)) == typeof(ulong)
                    ? FactKind.Unsupported
                    : FactKind.Enum;
            return FactKind.Unsupported;
        }
    }

    public readonly struct Fact<T> : IFact, IEquatable<Fact<T>>
    {
        public int Id { get; }
        public string Name { get; }
        public Type ValueType => typeof(T);

        Type IFact.ValueType => ValueType;

        private Fact(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static Fact<T> Declare([CallerMemberName] string name = "")
        {
            if (FactKindOf<T>.Kind == FactKind.Unsupported)
                throw new NotSupportedException(
                    $"Fact<{typeof(T).Name}> is not supported: a world state slot must be bool, int, float " +
                    "or an enum whose underlying type fits in 32 bits.");

            return new Fact<T>(FactIdProvider.Next(), name);
        }

        public bool Equals(Fact<T> other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Fact<T> f && Equals(f);
        public override int GetHashCode() => Id;
        public override string ToString() => $"{Name ?? $"f{Id}"} ({typeof(T).Name})";

        public static bool operator ==(Fact<T> a, Fact<T> b) => a.Id == b.Id;
        public static bool operator !=(Fact<T> a, Fact<T> b) => a.Id != b.Id;
    }

    public static class EnumSlot
    {
        public static int ToInt<T>(T value) => Convert.ToInt32(value);
    }

    internal static class FactIdProvider
    {
        private static int _nextId = -1;
        public static int Next() => Interlocked.Increment(ref _nextId);
    }

    public static class FactSchema
    {
        public sealed class Schema
        {
            public readonly IFact[] Facts;

            internal readonly int[] SlotByFactId;

            public int Count => Facts.Length;

            internal Schema(IFact[] facts, int[] slotByFactId)
            {
                Facts = facts;
                SlotByFactId = slotByFactId;
            }
        }

        private static readonly Dictionary<Type, Schema> _byType = new Dictionary<Type, Schema>();

        public static Schema Of(Type factsType)
        {
            if (factsType == null) throw new ArgumentNullException(nameof(factsType));

            lock (_byType)
            {
                if (_byType.TryGetValue(factsType, out var cached)) return cached;

                RuntimeHelpers.RunClassConstructor(factsType.TypeHandle);

                var declared = new List<IFact>();
                foreach (var field in factsType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var ft = field.FieldType;
                    if (!ft.IsGenericType || ft.GetGenericTypeDefinition() != typeof(Fact<>)) continue;
                    if (field.GetValue(null) is IFact fact) declared.Add(fact);
                }

                declared.Sort((a, b) => a.Id.CompareTo(b.Id));
                var facts = declared.ToArray();

                int maxId = -1;
                foreach (var f in facts) if (f.Id > maxId) maxId = f.Id;

                var slots = new int[maxId + 1];
                for (int i = 0; i < slots.Length; i++) slots[i] = -1;
                for (int slot = 0; slot < facts.Length; slot++) slots[facts[slot].Id] = slot;

                var schema = new Schema(facts, slots);
                _byType[factsType] = schema;
                return schema;
            }
        }
    }

    public static class FactSchema<TFacts> where TFacts : class
    {
        private static readonly FactSchema.Schema _schema = FactSchema.Of(typeof(TFacts));

        public static int Count => _schema.Count;

        public static IReadOnlyList<IFact> Facts => _schema.Facts;

        internal static FactSchema.Schema Schema => _schema;
    }
}
