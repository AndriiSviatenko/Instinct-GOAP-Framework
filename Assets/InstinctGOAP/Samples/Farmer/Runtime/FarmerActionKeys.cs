using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Farmer
{
    public static class FarmerActionKeys
    {
        public static readonly ActionKey Harvest = ActionKey.Declare();
        public static readonly ActionKey Rest = ActionKey.Declare();
        public static readonly ActionKey WalkToField = ActionKey.Declare();
        public static readonly ActionKey WalkToHome = ActionKey.Declare();
    }
}
