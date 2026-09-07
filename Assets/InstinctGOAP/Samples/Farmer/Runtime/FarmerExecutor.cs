using Instinct.GOAP;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{
    public sealed class FarmerExecutor : IActionExecutor<FarmerCommand>
    {
        private const float Arrive = 1f;
        private readonly FarmerBlackboard _b;

        public FarmerExecutor(FarmerBlackboard b)
        {
            _b = b;
        }

        public FarmerCommand Translate(IWorldState state, IAction a, IAgentContext context)
        {
            if (a.Key == FarmerActionKeys.WalkToField)
                return new FarmerCommand(FarmerAction.MoveToField, _b.Field.position, a.Key);

            if (a.Key == FarmerActionKeys.WalkToHome)
                return new FarmerCommand(FarmerAction.MoveToHome, _b.Home.position, a.Key);

            if (a.Key == FarmerActionKeys.Harvest)
                return new FarmerCommand(FarmerAction.Harvest, default, a.Key);

            if (a.Key == FarmerActionKeys.Rest)
                return new FarmerCommand(FarmerAction.Rest, default, a.Key);

            return FarmerCommand.None;
        }

        public void OnSelected(IWorldState state, IAction action, IAgentContext context)
        {
        }

        public void OnCompleted(IAction action, IAgentContext context, bool success)
        {
            if (!success)
                return;

            if (action.Key == FarmerActionKeys.Harvest)
            {
                _b.Energy = System.Math.Max(0, _b.Energy - 25);
                _b.CropsGrown += 1;
            }
            else if (action.Key == FarmerActionKeys.Rest)
            {
                _b.Energy = 100;
            }
        }
    }
}
