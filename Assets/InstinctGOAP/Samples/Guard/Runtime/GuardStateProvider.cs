namespace Instinct.GOAP.Samples.Guard
{
    public sealed class GuardStateProvider : IWorldStateProvider
    {
        private readonly GuardBlackboard _board;

        public GuardStateProvider(GuardBlackboard board) => _board = board;

        public WorldState GetState() => WorldState.For<GuardFacts>()
            .Set(GuardFacts.AlertLevel, _board.Alert)
            .Set(GuardFacts.IntruderVisible, _board.CanSeeIntruder)
            .Set(GuardFacts.IntruderCaught, _board.IntruderCaught)
            .Set(GuardFacts.HeardNoise, _board.NoisePending)
            .Set(GuardFacts.NoiseChecked, _board.NoiseInvestigated)
            .Set(GuardFacts.AtNoise, _board.AtNoise)
            .Set(GuardFacts.AtPost, !_board.NoisePending)
            .Set(GuardFacts.HasRadio, _board.HasRadio)
            .Set(GuardFacts.BackupCalled, _board.BackupCalled)
            .Set(GuardFacts.WaypointsVisited, _board.WaypointsVisited)
            .Set(GuardFacts.DistanceToIntruder, _board.DistanceToIntruder);
    }
}
