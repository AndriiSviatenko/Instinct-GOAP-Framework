using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{
    public sealed class FarmerBlackboard
    {
        public Transform Self;
        public Transform Home;
        public Transform Field;

        public int Energy = 100;
        public int CropsGrown;
    }
}
