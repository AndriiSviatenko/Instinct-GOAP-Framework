namespace Instinct.GOAP.Samples.Guard
{
    public static class GuardGoalKeys
    {
        public static readonly GoalKey CatchIntruder = GoalKey.Declare();
        public static readonly GoalKey CallBackup = GoalKey.Declare();
        public static readonly GoalKey InvestigateNoise = GoalKey.Declare();
        public static readonly GoalKey Patrol = GoalKey.Declare();
    }

    public static class GuardActionKeys
    {
        public static readonly ActionKey ChaseIntruder = ActionKey.Of<ChaseIntruder>();
        public static readonly ActionKey GrabIntruder = ActionKey.Of<GrabIntruder>();
        public static readonly ActionKey RadioForBackup = ActionKey.Of<RadioForBackup>();
        public static readonly ActionKey WalkToNoise = ActionKey.Of<WalkToNoise>();
        public static readonly ActionKey SweepArea = ActionKey.Of<SweepArea>();
        public static readonly ActionKey WalkToWaypoint = ActionKey.Of<WalkToWaypoint>();
        public static readonly ActionKey CalmDown = ActionKey.Of<CalmDown>();
    }
}
