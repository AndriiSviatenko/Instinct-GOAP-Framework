namespace Instinct.GOAP.Samples.Guard
{
    public enum Alert
    {
        Calm = 0,
        Suspicious = 1,
        Hunting = 2,
    }

    public sealed class GuardFacts
    {
        private GuardFacts() { }

        public static readonly Fact<Alert> AlertLevel = Fact<Alert>.Declare();

        public static readonly Fact<bool> IntruderVisible = Fact<bool>.Declare();
        public static readonly Fact<bool> IntruderCaught = Fact<bool>.Declare();
        public static readonly Fact<bool> HeardNoise = Fact<bool>.Declare();
        public static readonly Fact<bool> NoiseChecked = Fact<bool>.Declare();
        public static readonly Fact<bool> AtNoise = Fact<bool>.Declare();
        public static readonly Fact<bool> AtPost = Fact<bool>.Declare();
        public static readonly Fact<bool> HasRadio = Fact<bool>.Declare();
        public static readonly Fact<bool> BackupCalled = Fact<bool>.Declare();

        public static readonly Fact<int> WaypointsVisited = Fact<int>.Declare();
        public static readonly Fact<float> DistanceToIntruder = Fact<float>.Declare();
    }
}
