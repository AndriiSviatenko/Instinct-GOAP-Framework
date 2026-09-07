using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Instinct.GOAP.Unity
{
    public sealed class GoapActionFailed : Exception
    {
        public GoapActionFailed(string reason = null) : base(reason ?? "action failed") { }
    }

    public sealed class AsyncStep<TCtx> : IStep<TCtx>
    {
        private readonly Func<TCtx, CancellationToken, UniTask> _body;

        private CancellationTokenSource _cts;
        private ActionStatus _status = ActionStatus.Running;
        private int _generation;

        public AsyncStep(Func<TCtx, CancellationToken, UniTask> body)
            => _body = body ?? throw new ArgumentNullException(nameof(body));

        public void OnEnter(TCtx ctx)
        {
            Stop();
            _status = ActionStatus.Running;
            _cts = new CancellationTokenSource();
            Launch(ctx, _cts.Token, ++_generation).Forget();
        }

        public ActionStatus Tick(TCtx ctx) => _status;

        public void OnExit(TCtx ctx, bool success) => Stop();

        private async UniTaskVoid Launch(TCtx ctx, CancellationToken token, int generation)
        {
            ActionStatus result;
            try
            {
                await _body(ctx, token);
                result = ActionStatus.Success;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (GoapActionFailed)
            {
                result = ActionStatus.Failure;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = ActionStatus.Failure;
            }

            if (generation == _generation) _status = result;
        }

        private void Stop()
        {
            _generation++;
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    public static class GoapAwait
    {
        public static UniTask NextFrame(CancellationToken ct)
            => UniTask.Yield(PlayerLoopTiming.Update, ct);

        public static UniTask Seconds(float seconds, CancellationToken ct)
            => UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);

        public static UniTask Until(Func<bool> predicate, CancellationToken ct)
            => UniTask.WaitUntil(predicate, cancellationToken: ct);

        public static UniTask While(Func<bool> predicate, CancellationToken ct)
            => UniTask.WaitWhile(predicate, cancellationToken: ct);

        public static async UniTask MoveTo<TCtx>(TCtx ctx, Func<Vector3> target, CancellationToken ct)
            where TCtx : IMoveContext
        {
            while (!ctx.MoveTowards(target()))
                await NextFrame(ct);
        }

        public static async UniTask MoveTo<TCtx>(TCtx ctx, Vector3 target, CancellationToken ct)
            where TCtx : IMoveContext
        {
            while (!ctx.MoveTowards(target))
                await NextFrame(ct);
        }

        public static async UniTask Timeout(float seconds, Func<bool> done, CancellationToken ct)
        {
            float deadline = Time.time + seconds;
            while (!done())
            {
                if (Time.time >= deadline) throw new GoapActionFailed($"timed out after {seconds}s");
                await NextFrame(ct);
            }
        }
    }
}
