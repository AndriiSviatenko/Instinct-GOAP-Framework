using System;
using UnityEngine;

namespace Instinct.GOAP.Unity
{
    public interface IMoveContext
    {
        Vector3 Position { get; }

        bool MoveTowards(Vector3 target);
    }

    public static class UnitySteps
    {
        public static IStep<TCtx> MoveTo<TCtx>(Func<TCtx, Vector3> target) where TCtx : IMoveContext
            => Steps.Run<TCtx>(ctx => ctx.MoveTowards(target(ctx)) ? ActionStatus.Success : ActionStatus.Running);

        public static IStep<TCtx> MoveTo<TCtx>(Func<TCtx, Transform> target) where TCtx : IMoveContext
            => Steps.Run<TCtx>(ctx =>
            {
                var t = target(ctx);
                if (t == null) return ActionStatus.Failure;
                return ctx.MoveTowards(t.position) ? ActionStatus.Success : ActionStatus.Running;
            });
    }
}
