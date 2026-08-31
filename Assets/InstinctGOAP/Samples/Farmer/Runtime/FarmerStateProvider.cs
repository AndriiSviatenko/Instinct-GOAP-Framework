using Instinct.GOAP;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{
    public sealed class FarmerStateProvider : IWorldStateProvider
    {
        private readonly FarmerBlackboard _b;

        public FarmerStateProvider(FarmerBlackboard b)
        {
            _b = b;
        }

        public WorldState GetState()
        {
            return WorldState.For<FarmerFacts>()
                .Set(FarmerFacts.Energy, _b.Energy)
                .Set(FarmerFacts.CropsGrown, _b.CropsGrown)
                .Set(FarmerFacts.DistanceToHome, Distance(_b.Self, _b.Home))
                .Set(FarmerFacts.DistanceToField, Distance(_b.Self, _b.Field));
        }

        private static float Distance(Transform a, Transform b)
            => a != null && b != null ? Vector3.Distance(a.position, b.position) : 999f;
    }
}
