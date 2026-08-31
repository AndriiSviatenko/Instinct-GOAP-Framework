using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Instinct.GOAP.Unity
{
    public abstract class GoapAction<TCtx> where TCtx : class
    {
        private RuntimeActionBuilder<TCtx> _builder;

        protected CancellationToken Ct { get; private set; }

        protected virtual ActionKey Key => ActionKey.Of(GetType());

        protected abstract void Setup();

        protected abstract UniTask Run(TCtx ctx);


        protected void Require<T>(Fact<T> fact, T value) => Builder.Require(fact, value);
        protected void Require<T>(Fact<T> fact, Compare op, T value) => Builder.Require(fact, op, value);
        protected void Require(Func<IWorldState, bool> predicate, string description = null)
            => Builder.Require(predicate, description);

        protected void Effect<T>(Fact<T> fact, T value) => Builder.Effect(fact, value);
        protected void Add(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
            => Builder.Add(fact, delta, min, max);

        protected void Cost(float cost) => Builder.Cost(cost);
        protected void Cost(Func<IWorldState, TCtx, float> cost) => Builder.Cost(cost);

        protected void NoMirror() => Builder.NoMirror();

        private RuntimeActionBuilder<TCtx> Builder => _builder
            ?? throw new InvalidOperationException(
                $"{GetType().Name}: Require/Effect/Cost можна викликати лише з Setup().");


        protected static void Fail(string reason = null) => throw new GoapActionFailed(reason);

        protected UniTask Wait(float seconds) => GoapAwait.Seconds(seconds, Ct);
        protected UniTask NextFrame() => GoapAwait.NextFrame(Ct);
        protected UniTask Until(Func<bool> predicate) => GoapAwait.Until(predicate, Ct);
        protected UniTask While(Func<bool> predicate) => GoapAwait.While(predicate, Ct);

        protected UniTask MoveTo(IMoveContext ctx, Vector3 target) => GoapAwait.MoveTo(ctx, target, Ct);
        protected UniTask MoveTo(IMoveContext ctx, Func<Vector3> target) => GoapAwait.MoveTo(ctx, target, Ct);

        protected UniTask Timeout(float seconds, Func<bool> done) => GoapAwait.Timeout(seconds, done, Ct);


        internal void Register(GoapDomainBuilder<TCtx> domain)
        {
            _builder = domain.Action(Key);
            Setup();
            _builder.Run(new AsyncStep<TCtx>(Launch));
            _builder = null;
        }

        private UniTask Launch(TCtx ctx, CancellationToken token)
        {
            Ct = token;
            return Run(ctx);
        }
    }

    public static class GoapDomainAsyncExtensions
    {
        public static GoapDomainBuilder<TCtx> Use<TCtx>(this GoapDomainBuilder<TCtx> domain,
                                                        params GoapAction<TCtx>[] actions) where TCtx : class
        {
            for (int i = 0; i < actions.Length; i++) actions[i].Register(domain);
            return domain;
        }

        public static RuntimeActionBuilder<TCtx> RunAsync<TCtx>(this RuntimeActionBuilder<TCtx> action,
                                                                Func<TCtx, CancellationToken, UniTask> body)
            where TCtx : class
            => action.Run(new AsyncStep<TCtx>(body));
    }
}
