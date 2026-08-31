using Instinct.GOAP;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{
    public readonly struct FarmerCommand
    {
        public readonly FarmerAction What;
        public readonly Vector3 Destination;
        public readonly ActionKey Source;

        public FarmerCommand(FarmerAction what, Vector3 destination, ActionKey source)
        {
            What = what;
            Destination = destination;
            Source = source;
        }

        public static FarmerCommand None => new FarmerCommand(FarmerAction.None, default, default);

        public bool IsMove => What == FarmerAction.MoveToField || What == FarmerAction.MoveToHome;

        public override string ToString() => $"{What} ({Source})";
    }
}
