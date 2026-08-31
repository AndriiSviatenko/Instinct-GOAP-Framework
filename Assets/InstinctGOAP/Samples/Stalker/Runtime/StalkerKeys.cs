using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Stalker
{
    public static class StalkerGoalKeys
    {
        public static readonly GoalKey SurviveEmission = GoalKey.Declare();
        public static readonly GoalKey Defend = GoalKey.Declare();
        public static readonly GoalKey SatisfyHunger = GoalKey.Declare();
        public static readonly GoalKey Rest = GoalKey.Declare();
        public static readonly GoalKey Heal = GoalKey.Declare();
        public static readonly GoalKey CollectArtifact = GoalKey.Declare();
        public static readonly GoalKey TradeArtifacts = GoalKey.Declare();
        public static readonly GoalKey Restock = GoalKey.Declare();
        public static readonly GoalKey RoamZone = GoalKey.Declare();
    }

    public static class StalkerActionKeys
    {
        public static readonly ActionKey GoToShelter = ActionKey.Of<GoToShelter>();
        public static readonly ActionKey WaitOutEmission = ActionKey.Of<WaitOutEmission>();

        public static readonly ActionKey ChaseThreat = ActionKey.Of<ChaseThreat>();
        public static readonly ActionKey AttackThreat = ActionKey.Of<AttackThreat>();
        public static readonly ActionKey FleeThreat = ActionKey.Of<FleeThreat>();

        public static readonly ActionKey GoToStash = ActionKey.Of<GoToStash>();
        public static readonly ActionKey TakeFood = ActionKey.Of<TakeFood>();
        public static readonly ActionKey EatFood = ActionKey.Of<EatFood>();

        public static readonly ActionKey GoToCampfire = ActionKey.Of<GoToCampfire>();
        public static readonly ActionKey SleepAtCampfire = ActionKey.Of<SleepAtCampfire>();

        public static readonly ActionKey UseMedkit = ActionKey.Of<UseMedkit>();

        public static readonly ActionKey GoToAnomaly = ActionKey.Of<GoToAnomaly>();
        public static readonly ActionKey ScanAnomaly = ActionKey.Of<ScanAnomaly>();
        public static readonly ActionKey ExtractArtifact = ActionKey.Of<ExtractArtifact>();

        public static readonly ActionKey GoToTrader = ActionKey.Of<GoToTrader>();
        public static readonly ActionKey SellArtifacts = ActionKey.Of<SellArtifacts>();
        public static readonly ActionKey BuySupplies = ActionKey.Of<BuySupplies>();

        public static readonly ActionKey GoToField = ActionKey.Of<GoToField>();
        public static readonly ActionKey Roam = ActionKey.Of<Roam>();
    }
}
