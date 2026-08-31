using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{
    public enum ChefState : byte
    {
        None = 0,
        Idle = 1,
        Cooking = 2,
        Serving = 3,
    }

    public sealed class ChefFacts
    {
        private ChefFacts() { }

        public static readonly Fact<int> ClientHunger = Fact<int>.Declare();
        public static readonly Fact<bool> ClientPresent = Fact<bool>.Declare();
        public static readonly Fact<bool> HasIngredients = Fact<bool>.Declare();
        public static readonly Fact<bool> MealReady = Fact<bool>.Declare();
        public static readonly Fact<float> DistanceToStove = Fact<float>.Declare();
        public static readonly Fact<float> DistanceToClient = Fact<float>.Declare();
        public static readonly Fact<float> DistanceToBreak = Fact<float>.Declare();
        public static readonly Fact<int> Energy = Fact<int>.Declare();
        public static readonly Fact<ChefState> ChefState = Fact<ChefState>.Declare();
    }
}
