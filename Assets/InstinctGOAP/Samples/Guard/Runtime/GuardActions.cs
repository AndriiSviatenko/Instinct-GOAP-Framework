using System;
using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Samples.Guard
{
    public interface IGuardAction : IAction
    {
        GuardCommand Translate(IWorldState state, GuardBlackboard board);
        void OnCompleted(GuardBlackboard board, bool success);
    }

    public abstract class GuardActionBase : IGuardAction
    {
        public ActionKey Key { get; }

        private readonly List<ICondition> _preconditions = new List<ICondition>();
        private readonly List<IEffect> _effects = new List<IEffect>();
        private Func<IWorldState, IPlanningContext, float> _cost = (_, _) => 1f;

        protected GuardActionBase()
        {
            Key = ActionKey.Of(GetType());
            Configure();
        }

        protected abstract void Configure();

        public IReadOnlyList<ICondition> Preconditions => _preconditions;
        public IReadOnlyList<IEffect> Effects => _effects;

        public float Cost(IWorldState state, IPlanningContext context) => _cost(state, context);
        public WorldState ApplyTo(WorldState state) => EffectExtensions.ApplyAll(_effects, state);

        public virtual GuardCommand Translate(IWorldState state, GuardBlackboard board) => GuardCommand.Idle;
        public virtual void OnCompleted(GuardBlackboard board, bool success) { }

        protected void Require<T>(Fact<T> fact, T value) => _preconditions.Add(new Condition<T>(fact, value));
        protected void Require<T>(Fact<T> fact, Compare op, T value) => _preconditions.Add(new Condition<T>(fact, op, value));

        protected void Effect<T>(Fact<T> fact, T value) => _effects.Add(new Effect<T>(fact, value));
        protected void Add(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
            => _effects.Add(new AddEffect(fact, delta, min, max));

        protected void Cost(float cost) => _cost = (_, _) => cost;
        protected void Cost(Func<IWorldState, IPlanningContext, float> cost) => _cost = cost;

        protected static GuardBlackboard Board(IPlanningContext context) => context?.GetExtra<GuardBlackboard>();
    }

    public sealed class ChaseIntruder : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.IntruderVisible, true);
            Require(GuardFacts.DistanceToIntruder, Compare.Greater, 1.6f);
            Effect(GuardFacts.DistanceToIntruder, 1.5f);
            Effect(GuardFacts.AlertLevel, Alert.Hunting);
            Cost((state, _) => 1f + state.Get(GuardFacts.DistanceToIntruder) * 0.25f);
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
            => GuardCommand.Sprint(board.IntruderPosition);
    }

    public sealed class GrabIntruder : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.IntruderVisible, true);
            Require(GuardFacts.DistanceToIntruder, Compare.LessOrEqual, 1.6f);
            Effect(GuardFacts.IntruderCaught, true);
            Cost(0.5f);
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
            => GuardCommand.Interact(board.IntruderPosition);

        public override void OnCompleted(GuardBlackboard board, bool success)
        {
            if (success) board.IntruderCaught = true;
        }
    }

    public sealed class RadioForBackup : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.HasRadio, true);
            Require(GuardFacts.BackupCalled, false);
            Require(GuardFacts.AlertLevel, Compare.GreaterOrEqual, Alert.Suspicious);
            Effect(GuardFacts.BackupCalled, true);
            Cost(1.5f);
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
            => GuardCommand.LookAround(0.8f);

        public override void OnCompleted(GuardBlackboard board, bool success)
        {
            if (success) board.BackupCalled = true;
        }
    }

    public sealed class WalkToNoise : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.HeardNoise, true);
            Require(GuardFacts.NoiseChecked, false);
            Require(GuardFacts.AtNoise, false);
            Effect(GuardFacts.AtNoise, true);
            Effect(GuardFacts.AtPost, false);
            Effect(GuardFacts.AlertLevel, Alert.Suspicious);
            Cost((_, context) =>
            {
                var board = Board(context);
                if (board == null) return 3f;
                return 1f + Vector3.Distance(board.SelfPosition, board.LastNoisePosition) * 0.2f;
            });
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
            => GuardCommand.MoveTo(board.LastNoisePosition);
    }

    public sealed class SweepArea : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.HeardNoise, true);
            Require(GuardFacts.AtNoise, true);
            Effect(GuardFacts.NoiseChecked, true);
            Cost(2f);
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
            => GuardCommand.LookAround(board.SweepSeconds);

        public override void OnCompleted(GuardBlackboard board, bool success)
        {
            if (success) board.ClearNoise();
        }
    }

    public sealed class WalkToWaypoint : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.IntruderVisible, false);
            Add(GuardFacts.WaypointsVisited, 1, max: 64);
            Effect(GuardFacts.AtPost, true);
            Cost(2f);
        }

        public override GuardCommand Translate(IWorldState state, GuardBlackboard board)
        {
            var waypoint = board.CurrentWaypoint;
            return waypoint != null ? GuardCommand.MoveTo(waypoint.position) : GuardCommand.Idle;
        }

        public override void OnCompleted(GuardBlackboard board, bool success)
        {
            if (success) board.AdvanceWaypoint();
        }
    }

    public sealed class CalmDown : GuardActionBase
    {
        protected override void Configure()
        {
            Require(GuardFacts.IntruderVisible, false);
            Require(GuardFacts.NoiseChecked, true);
            Require(GuardFacts.AlertLevel, Compare.Greater, Alert.Calm);
            Effect(GuardFacts.AlertLevel, Alert.Calm);
            Cost(0.2f);
        }

        public override void OnCompleted(GuardBlackboard board, bool success)
        {
            if (success) board.Alert = Alert.Calm;
        }
    }

    public static class GuardActions
    {
        public static IReadOnlyList<IAction> All() => new IAction[]
        {
            new ChaseIntruder(),
            new GrabIntruder(),
            new RadioForBackup(),
            new WalkToNoise(),
            new SweepArea(),
            new WalkToWaypoint(),
            new CalmDown(),
        };
    }
}
