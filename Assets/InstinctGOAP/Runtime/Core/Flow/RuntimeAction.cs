using System;
using System.Collections.Generic;

namespace Instinct.GOAP
{
    public enum ActionStatus : byte
    {
        Running = 0,
        Success,
        Failure,
    }

    public interface ITickContext
    {
        float DeltaTime { get; }
    }

    public interface IStep<in TCtx>
    {
        void OnEnter(TCtx ctx);
        ActionStatus Tick(TCtx ctx);
        void OnExit(TCtx ctx, bool success);
    }

    public interface IRuntimeAction<in TCtx> : IAction
    {
        bool MirrorsEffects { get; }

        void OnEnter(TCtx ctx);
        ActionStatus Tick(TCtx ctx);
        void OnExit(TCtx ctx, bool success);
    }

    public sealed class RuntimeAction<TCtx> : IRuntimeAction<TCtx>
    {
        private readonly IAction _spec;
        private readonly IStep<TCtx> _step;
        private readonly Action<TCtx, bool> _onDone;

        public RuntimeAction(IAction spec, IStep<TCtx> step, Action<TCtx, bool> onDone, bool mirrorsEffects)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _step = step;
            _onDone = onDone;
            MirrorsEffects = mirrorsEffects;
        }

        public bool MirrorsEffects { get; }

        public ActionKey Key => _spec.Key;
        public IReadOnlyList<ICondition> Preconditions => _spec.Preconditions;
        public IReadOnlyList<IEffect> Effects => _spec.Effects;
        public float Cost(IWorldState state, IPlanningContext context) => _spec.Cost(state, context);
        public WorldState ApplyTo(WorldState state) => _spec.ApplyTo(state);

        public void OnEnter(TCtx ctx) => _step?.OnEnter(ctx);

        public ActionStatus Tick(TCtx ctx) => _step == null ? ActionStatus.Success : _step.Tick(ctx);

        public void OnExit(TCtx ctx, bool success)
        {
            _step?.OnExit(ctx, success);
            _onDone?.Invoke(ctx, success);
        }

        public override string ToString() => Key.ToString();
    }

    public static class Steps
    {
        public static IStep<TCtx> Instant<TCtx>(Action<TCtx> body = null) => new InstantStep<TCtx>(body);

        public static IStep<TCtx> Run<TCtx>(Func<TCtx, ActionStatus> tick) => new FuncStep<TCtx>(tick);

        public static IStep<TCtx> Wait<TCtx>(float seconds) where TCtx : ITickContext
            => new WaitStep<TCtx>(_ => seconds);

        public static IStep<TCtx> Wait<TCtx>(Func<TCtx, float> seconds) where TCtx : ITickContext
            => new WaitStep<TCtx>(seconds);

        public static IStep<TCtx> Sequence<TCtx>(params IStep<TCtx>[] steps) => new SequenceStep<TCtx>(steps);

        private sealed class InstantStep<TCtx> : IStep<TCtx>
        {
            private readonly Action<TCtx> _body;
            public InstantStep(Action<TCtx> body) => _body = body;
            public void OnEnter(TCtx ctx) { }
            public ActionStatus Tick(TCtx ctx) { _body?.Invoke(ctx); return ActionStatus.Success; }
            public void OnExit(TCtx ctx, bool success) { }
        }

        private sealed class FuncStep<TCtx> : IStep<TCtx>
        {
            private readonly Func<TCtx, ActionStatus> _tick;
            public FuncStep(Func<TCtx, ActionStatus> tick) => _tick = tick;
            public void OnEnter(TCtx ctx) { }
            public ActionStatus Tick(TCtx ctx) => _tick == null ? ActionStatus.Success : _tick(ctx);
            public void OnExit(TCtx ctx, bool success) { }
        }

        private sealed class WaitStep<TCtx> : IStep<TCtx> where TCtx : ITickContext
        {
            private readonly Func<TCtx, float> _seconds;
            private float _elapsed;

            public WaitStep(Func<TCtx, float> seconds) => _seconds = seconds;

            public void OnEnter(TCtx ctx) => _elapsed = 0f;

            public ActionStatus Tick(TCtx ctx)
            {
                _elapsed += ctx.DeltaTime;
                return _elapsed >= _seconds(ctx) ? ActionStatus.Success : ActionStatus.Running;
            }

            public void OnExit(TCtx ctx, bool success) => _elapsed = 0f;
        }

        private sealed class SequenceStep<TCtx> : IStep<TCtx>
        {
            private readonly IStep<TCtx>[] _steps;
            private int _index;

            public SequenceStep(IStep<TCtx>[] steps) => _steps = steps ?? Array.Empty<IStep<TCtx>>();

            public void OnEnter(TCtx ctx)
            {
                _index = 0;
                if (_steps.Length > 0) _steps[0].OnEnter(ctx);
            }

            public ActionStatus Tick(TCtx ctx)
            {
                while (_index < _steps.Length)
                {
                    var status = _steps[_index].Tick(ctx);
                    if (status == ActionStatus.Running) return ActionStatus.Running;

                    _steps[_index].OnExit(ctx, status == ActionStatus.Success);
                    if (status == ActionStatus.Failure)
                    {
                        _index = _steps.Length;
                        return ActionStatus.Failure;
                    }

                    _index++;
                    if (_index < _steps.Length) _steps[_index].OnEnter(ctx);
                }
                return ActionStatus.Success;
            }

            public void OnExit(TCtx ctx, bool success)
            {
                if (_index < _steps.Length) _steps[_index].OnExit(ctx, success);
                _index = 0;
            }
        }
    }
}
