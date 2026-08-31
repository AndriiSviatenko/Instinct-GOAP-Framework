using System;
using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Samples.Stalker
{
    public interface IStalkerAction : IAction
    {
        StalkerCommand Translate(IWorldState state, StalkerBlackboard board);
        void OnCompleted(StalkerBlackboard board, bool success);
    }

    public abstract class StalkerActionBase : IStalkerAction
    {
        public ActionKey Key { get; }

        private readonly List<ICondition> _preconditions = new List<ICondition>();
        private readonly List<IEffect> _effects = new List<IEffect>();
        private Func<IWorldState, IPlanningContext, float> _cost = (_, _) => 1f;

        protected StalkerActionBase()
        {
            Key = ActionKey.Of(GetType());
            Configure();
        }

        protected abstract void Configure();

        public IReadOnlyList<ICondition> Preconditions => _preconditions;
        public IReadOnlyList<IEffect> Effects => _effects;

        public float Cost(IWorldState state, IPlanningContext context) => _cost(state, context);
        public WorldState ApplyTo(WorldState state) => EffectExtensions.ApplyAll(_effects, state);

        public virtual StalkerCommand Translate(IWorldState state, StalkerBlackboard board) => StalkerCommand.Idle;
        public virtual void OnCompleted(StalkerBlackboard board, bool success) { }

        protected void Require<T>(Fact<T> fact, T value) => _preconditions.Add(new Condition<T>(fact, value));
        protected void Require<T>(Fact<T> fact, Compare op, T value) => _preconditions.Add(new Condition<T>(fact, op, value));
        protected void Require(Func<IWorldState, bool> predicate, string description = null)
            => _preconditions.Add(new PredicateCondition(predicate, description));

        protected void Effect<T>(Fact<T> fact, T value) => _effects.Add(new Effect<T>(fact, value));
        protected void Add(Fact<int> fact, int delta, int min = int.MinValue, int max = int.MaxValue)
            => _effects.Add(new AddEffect(fact, delta, min, max));

        protected void Cost(float cost) => _cost = (_, _) => cost;
        protected void Cost(Func<IWorldState, IPlanningContext, float> cost) => _cost = cost;

        protected static StalkerBlackboard Board(IPlanningContext context) => context?.GetExtra<StalkerBlackboard>();
    }

    public sealed class GoToShelter : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, true);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Shelter);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Shelter);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost((state, context) =>
            {
                var board = Board(context);
                return board == null ? 3f : 1f + board.DistanceTo(board.ShelterPosition) * 0.15f;
            });
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.ShelterPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Shelter);
        }
    }

    public sealed class WaitOutEmission : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, true);
            Require(StalkerFacts.AtLocation, StalkerLocation.Shelter);
            Require(StalkerFacts.SafeFromEmission, false);
            Effect(StalkerFacts.SafeFromEmission, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Resting);
            Cost(3f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Wait(5f);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.WaitOutEmission();
        }
    }

    public sealed class ChaseThreat : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(s => s.Get(StalkerFacts.EnemyVisible) || s.Get(StalkerFacts.MutantVisible), "threat visible");
            Require(StalkerFacts.DistanceToThreat, Compare.Greater, 4f);
            Effect(StalkerFacts.DistanceToThreat, 3f);
            Effect(StalkerFacts.Activity, StalkerActivity.Fighting);
            Cost((state, _) => 1f + state.Get(StalkerFacts.DistanceToThreat) * 0.2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.ThreatPosition);
    }

    public sealed class AttackThreat : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.HasWeapon, true);
            Require(StalkerFacts.DistanceToThreat, Compare.LessOrEqual, 4f);
            Require(s => s.Get(StalkerFacts.EnemyVisible) || s.Get(StalkerFacts.MutantVisible), "threat visible");
            Effect(StalkerFacts.ThreatDealt, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Fighting);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Attack(board.ThreatPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.DealWithThreat();
        }
    }

    public sealed class FleeThreat : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.HasWeapon, false);
            Require(s => s.Get(StalkerFacts.EnemyVisible) || s.Get(StalkerFacts.MutantVisible), "threat visible");
            Effect(StalkerFacts.SafeFromThreat, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Fleeing);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.ShelterPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.FleeThreat();
        }
    }

    public sealed class GoToStash : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, false);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Stash);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Stash);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost((state, context) =>
            {
                var board = Board(context);
                return board == null ? 3f : 1f + board.DistanceTo(board.StashPosition) * 0.15f;
            });
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.StashPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Stash);
        }
    }

    public sealed class TakeFood : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Stash);
            Require(StalkerFacts.HasFood, false);
            Effect(StalkerFacts.HasFood, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost(1f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.StashPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.TakeFood();
        }
    }

    public sealed class EatFood : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.HasFood, true);
            Require(StalkerFacts.Hunger, Compare.Greater, 30);
            Effect(StalkerFacts.Hunger, 20);
            Effect(StalkerFacts.HasFood, false);
            Effect(StalkerFacts.Activity, StalkerActivity.Eating);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.SelfPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.Eat();
        }
    }

    public sealed class GoToCampfire : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, false);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Campfire);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Campfire);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost((state, context) =>
            {
                var board = Board(context);
                return board == null ? 2f : 1f + board.DistanceTo(board.CampfirePosition) * 0.15f;
            });
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.CampfirePosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Campfire);
        }
    }

    public sealed class SleepAtCampfire : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Campfire);
            Require(StalkerFacts.Energy, Compare.Less, 100);
            Effect(StalkerFacts.Energy, 100);
            Effect(StalkerFacts.Activity, StalkerActivity.Sleeping);
            Cost(3f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Wait(6f);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.Sleep();
        }
    }

    public sealed class UseMedkit : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.HasMedkit, true);
            Require(StalkerFacts.Health, Compare.Less, 100);
            Effect(StalkerFacts.Health, 100);
            Effect(StalkerFacts.HasMedkit, false);
            Effect(StalkerFacts.Activity, StalkerActivity.Healing);
            Cost(1f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.SelfPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.HealSelf();
        }
    }

    public sealed class GoToAnomaly : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, false);
            Require(StalkerFacts.AnomalyNearby, true);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Anomaly);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Anomaly);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost((state, context) =>
            {
                var board = Board(context);
                return board == null ? 3f : 1f + board.DistanceTo(board.AnomalyPosition) * 0.15f;
            });
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.AnomalyPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Anomaly);
        }
    }

    public sealed class ScanAnomaly : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Anomaly);
            Require(StalkerFacts.AnomalyScanned, false);
            Effect(StalkerFacts.AnomalyScanned, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Searching);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Search(board.AnomalyPosition, 3f);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ScanAnomalySite();
        }
    }

    public sealed class ExtractArtifact : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Anomaly);
            Require(StalkerFacts.AnomalyScanned, true);
            Require(StalkerFacts.ArtifactCollected, false);
            Effect(StalkerFacts.ArtifactCollected, true);
            Add(StalkerFacts.Artifacts, 1);
            Effect(StalkerFacts.Activity, StalkerActivity.Searching);
            Cost(3f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.AnomalyPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.CollectArtifact();
        }
    }

    public sealed class GoToTrader : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, false);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Trader);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Trader);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost((state, context) =>
            {
                var board = Board(context);
                return board == null ? 3f : 1f + board.DistanceTo(board.TraderPosition) * 0.15f;
            });
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.TraderPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Trader);
        }
    }

    public sealed class SellArtifacts : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Trader);
            Require(StalkerFacts.Artifacts, Compare.Greater, 0);
            Add(StalkerFacts.Artifacts, -1, min: 0);
            Add(StalkerFacts.Money, 300);
            Effect(StalkerFacts.Activity, StalkerActivity.Trading);
            Cost(1f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.TraderPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.SellArtifact();
        }
    }

    public sealed class BuySupplies : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Trader);
            Require(StalkerFacts.Money, Compare.GreaterOrEqual, 200);
            Require(s => !s.Get(StalkerFacts.HasFood) || !s.Get(StalkerFacts.HasMedkit), "missing supplies");
            Add(StalkerFacts.Money, -200, min: 0);
            Effect(StalkerFacts.HasFood, true);
            Effect(StalkerFacts.HasMedkit, true);
            Effect(StalkerFacts.Activity, StalkerActivity.Trading);
            Cost(1f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.Interact(board.TraderPosition);

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.BuySupplies();
        }
    }

    public sealed class GoToField : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.EmissionActive, false);
            Require(StalkerFacts.AtLocation, Compare.NotEqual, StalkerLocation.Field);
            Effect(StalkerFacts.AtLocation, StalkerLocation.Field);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.PatrolTarget());

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.ArriveAt(StalkerLocation.Field);
        }
    }

    public sealed class Roam : StalkerActionBase
    {
        protected override void Configure()
        {
            Require(StalkerFacts.AtLocation, StalkerLocation.Field);
            Require(StalkerFacts.EnemyVisible, false);
            Require(StalkerFacts.MutantVisible, false);
            Require(StalkerFacts.AnomalyNearby, false);
            Add(StalkerFacts.PatrolPointsVisited, 1);
            Effect(StalkerFacts.Activity, StalkerActivity.Moving);
            Cost(2f);
        }

        public override StalkerCommand Translate(IWorldState state, StalkerBlackboard board)
            => StalkerCommand.MoveTo(board.PatrolTarget());

        public override void OnCompleted(StalkerBlackboard board, bool success)
        {
            if (success) board.WanderDone();
        }
    }

    public static class StalkerActions
    {
        public static IReadOnlyList<IAction> All() => new IAction[]
        {
            new GoToShelter(),
            new WaitOutEmission(),
            new ChaseThreat(),
            new AttackThreat(),
            new FleeThreat(),
            new GoToStash(),
            new TakeFood(),
            new EatFood(),
            new GoToCampfire(),
            new SleepAtCampfire(),
            new UseMedkit(),
            new GoToAnomaly(),
            new ScanAnomaly(),
            new ExtractArtifact(),
            new GoToTrader(),
            new SellArtifacts(),
            new BuySupplies(),
            new GoToField(),
            new Roam(),
        };
    }
}
