using System;
using System.Runtime.CompilerServices;

namespace Instinct.GOAP
{
    public readonly struct FactValue : IEquatable<FactValue>
    {
        public enum Kind : byte { None = 0, Bool = 1, Int = 2, Float = 3 }

        public readonly Kind Type;
        private readonly int _int;
        private readonly float _float;

        private FactValue(Kind type, int i, float f)
        {
            Type = type;
            _int = i;
            _float = f;
        }

        public static readonly FactValue None = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FactValue Of(bool v) => new FactValue(Kind.Bool, v ? 1 : 0, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FactValue Of(int v) => new FactValue(Kind.Int, v, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FactValue Of(float v) => new FactValue(Kind.Float, 0, v);

        public bool IsNone => Type == Kind.None;
        public bool AsBool => _int != 0;
        public int AsInt => Type == Kind.Float ? (int)_float : _int;
        public float AsFloat => Type == Kind.Float ? _float : _int;

        public bool IsDefaultLike() => Type switch
        {
            Kind.Bool => _int == 0,
            Kind.Int => _int == 0,
            Kind.Float => _float == 0,
            _ => true,
        };

        public bool Equals(FactValue other) =>
            Type == other.Type && _int == other._int && _float.Equals(other._float);

        public override bool Equals(object obj) => obj is FactValue v && Equals(v);

        public override int GetHashCode() =>
            Type == Kind.Float
                ? HashCode.Combine(Type, _float)
                : HashCode.Combine(Type, _int);

        public static implicit operator FactValue(bool v) => Of(v);
        public static implicit operator FactValue(int v) => Of(v);
        public static implicit operator FactValue(float v) => Of(v);

        public override string ToString() => Type switch
        {
            Kind.Bool => AsBool ? "true" : "false",
            Kind.Int => _int.ToString(),
            Kind.Float => _float.ToString("0.###"),
            _ => "none",
        };
    }
}
