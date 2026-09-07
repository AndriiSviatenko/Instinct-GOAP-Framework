using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{

    public sealed class ChefStateProvider : IWorldStateProvider
    {
        private readonly ChefBlackboard _board;

        public ChefStateProvider(ChefBlackboard board) => _board = board;

        public WorldState GetState() => WorldState.For<ChefFacts>()
            .Set(ChefFacts.ClientHunger, _board.ClientHunger)
            .Set(ChefFacts.ClientPresent, _board.ClientPresent)
            .Set(ChefFacts.HasIngredients, _board.HasIngredients)
            .Set(ChefFacts.MealReady, _board.MealReady)
            .Set(ChefFacts.DistanceToStove, _board.DistanceToStove)
            .Set(ChefFacts.DistanceToClient, _board.DistanceToClient)
            .Set(ChefFacts.DistanceToBreak, _board.DistanceToBreak)
            .Set(ChefFacts.Energy, _board.Energy)
            .Set(ChefFacts.ChefState, _board.State);
    }
}
