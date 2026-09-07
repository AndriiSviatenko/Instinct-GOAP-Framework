using System;
using System.Collections.Generic;

namespace Instinct.GOAP
{
    public interface IFactBinding<in TCtx>
    {
        IFact Fact { get; }
        bool CanWrite { get; }

        void Read(TCtx ctx, WorldState state);
        void Write(TCtx ctx, IWorldState state);
    }

    public sealed class FactBinding<TCtx, T> : IFactBinding<TCtx>
    {
        private readonly Fact<T> _fact;
        private readonly Func<TCtx, T> _read;
        private readonly Action<TCtx, T> _write;

        public FactBinding(Fact<T> fact, Func<TCtx, T> read, Action<TCtx, T> write)
        {
            _fact = fact;
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _write = write;
        }

        public IFact Fact => _fact;
        public bool CanWrite => _write != null;

        public void Read(TCtx ctx, WorldState state) => state.Set(_fact, _read(ctx));

        public void Write(TCtx ctx, IWorldState state) => _write?.Invoke(ctx, state.Get(_fact));
    }

    public sealed class BoundStateProvider<TCtx> : IWorldStateProvider
    {
        private readonly Type _factsType;
        private readonly IReadOnlyList<IFactBinding<TCtx>> _bindings;
        private readonly TCtx _ctx;

        public BoundStateProvider(Type factsType, IReadOnlyList<IFactBinding<TCtx>> bindings, TCtx ctx)
        {
            _factsType = factsType ?? throw new ArgumentNullException(nameof(factsType));
            _bindings = bindings ?? Array.Empty<IFactBinding<TCtx>>();
            _ctx = ctx;
        }

        public WorldState GetState()
        {
            var state = WorldState.For(_factsType);
            for (int i = 0; i < _bindings.Count; i++) _bindings[i].Read(_ctx, state);
            return state;
        }
    }
}
