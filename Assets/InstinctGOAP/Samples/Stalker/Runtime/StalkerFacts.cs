using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Stalker
{

    public enum StalkerLocation
    {
        Field = 0,
        Campfire = 1,
        Trader = 2,
        Stash = 3,
        Shelter = 4,
        Anomaly = 5,
    }

    public enum StalkerActivity
    {
        Idle = 0,
        Moving = 1,
        Resting = 2,
        Sleeping = 3,
        Eating = 4,
        Searching = 5,
        Fighting = 6,
        Fleeing = 7,
        Trading = 8,
        Healing = 9,
    }

    public sealed class StalkerFacts
    {
        private StalkerFacts() { }

        public static readonly Fact<StalkerLocation> AtLocation = Fact<StalkerLocation>.Declare();
        public static readonly Fact<StalkerActivity> Activity = Fact<StalkerActivity>.Declare();

        public static readonly Fact<int> Health = Fact<int>.Declare();
        public static readonly Fact<int> Hunger = Fact<int>.Declare();
        public static readonly Fact<int> Energy = Fact<int>.Declare();

        public static readonly Fact<int> Money = Fact<int>.Declare();
        public static readonly Fact<int> Artifacts = Fact<int>.Declare();
        public static readonly Fact<int> PatrolPointsVisited = Fact<int>.Declare();
        public static readonly Fact<bool> HasWeapon = Fact<bool>.Declare();
        public static readonly Fact<bool> HasFood = Fact<bool>.Declare();
        public static readonly Fact<bool> HasMedkit = Fact<bool>.Declare();

        public static readonly Fact<bool> EmissionActive = Fact<bool>.Declare();
        public static readonly Fact<bool> SafeFromEmission = Fact<bool>.Declare();

        public static readonly Fact<bool> EnemyVisible = Fact<bool>.Declare();
        public static readonly Fact<bool> MutantVisible = Fact<bool>.Declare();
        public static readonly Fact<float> DistanceToThreat = Fact<float>.Declare();
        public static readonly Fact<bool> ThreatDealt = Fact<bool>.Declare();
        public static readonly Fact<bool> SafeFromThreat = Fact<bool>.Declare();

        public static readonly Fact<bool> AnomalyNearby = Fact<bool>.Declare();
        public static readonly Fact<bool> AnomalyScanned = Fact<bool>.Declare();
        public static readonly Fact<bool> ArtifactCollected = Fact<bool>.Declare();
    }
}
