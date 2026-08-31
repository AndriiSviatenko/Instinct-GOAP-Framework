using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Farmer
{
    public sealed class FarmerFacts
    {
        private FarmerFacts() { }

        public static readonly Fact<int> Energy = Fact<int>.Declare();
        public static readonly Fact<int> CropsRipe = Fact<int>.Declare();
        public static readonly Fact<float> DistanceToHome = Fact<float>.Declare();
        public static readonly Fact<float> DistanceToField = Fact<float>.Declare();
        public static readonly Fact<int> CropsGrown = Fact<int>.Declare();
    }
}
