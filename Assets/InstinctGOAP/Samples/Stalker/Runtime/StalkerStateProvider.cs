namespace Instinct.GOAP.Samples.Stalker
{

    public sealed class StalkerStateProvider : IWorldStateProvider
    {
        private readonly StalkerBlackboard _board;

        public StalkerStateProvider(StalkerBlackboard board) => _board = board;

        public WorldState GetState() => WorldState.For<StalkerFacts>()
            .Set(StalkerFacts.AtLocation, _board.Location)
            .Set(StalkerFacts.Activity, _board.Activity)
            .Set(StalkerFacts.Health, _board.Health)
            .Set(StalkerFacts.Hunger, _board.Hunger)
            .Set(StalkerFacts.Energy, _board.Energy)
            .Set(StalkerFacts.Money, _board.Money)
            .Set(StalkerFacts.Artifacts, _board.Artifacts)
            .Set(StalkerFacts.PatrolPointsVisited, _board.PatrolPointsVisited)
            .Set(StalkerFacts.HasWeapon, _board.HasWeapon)
            .Set(StalkerFacts.HasFood, _board.HasFood)
            .Set(StalkerFacts.HasMedkit, _board.HasMedkit)
            .Set(StalkerFacts.EmissionActive, _board.EmissionActive)
            .Set(StalkerFacts.SafeFromEmission, _board.EmissionSafe)
            .Set(StalkerFacts.EnemyVisible, _board.EnemyVisible)
            .Set(StalkerFacts.MutantVisible, _board.MutantVisible)
            .Set(StalkerFacts.DistanceToThreat, _board.DistanceToThreat)
            .Set(StalkerFacts.ThreatDealt, _board.ThreatDealt)
            .Set(StalkerFacts.SafeFromThreat, _board.SafeFromThreat)
            .Set(StalkerFacts.AnomalyNearby, _board.AnomalyNearby)
            .Set(StalkerFacts.AnomalyScanned, _board.AnomalyScanned)
            .Set(StalkerFacts.ArtifactCollected, _board.ArtifactCollected);
    }
}
