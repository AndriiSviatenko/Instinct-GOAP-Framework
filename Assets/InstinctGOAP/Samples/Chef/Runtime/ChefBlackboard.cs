using Instinct.GOAP;
using UnityEngine;

namespace Instinct.GOAP.Samples.Chef
{

    public sealed class ChefBlackboard : IAgentContext
    {

        public Vector3 StovePosition;
        public Vector3 StoragePosition;
        public Vector3 BreakPosition;
        public Vector3 ClientPosition;
        public Vector3 SelfPosition;

        public int ClientHunger = 30;
        public int Energy = 100;
        public bool HasIngredients;
        public bool MealReady;
        public bool ClientPresent;
        public ChefState State = ChefState.Idle;

        public float DistanceToStove => Vector3.Distance(SelfPosition, StovePosition);

        public float DistanceToClient => Vector3.Distance(SelfPosition, ClientPosition);

        public float DistanceToBreak => Vector3.Distance(SelfPosition, BreakPosition);

        public void PickUpIngredients() { HasIngredients = true; Energy = Mathf.Max(0, Energy - 5); }
        public void FinishCooking() { MealReady = true; HasIngredients = false; Energy = Mathf.Max(0, Energy - 25); State = ChefState.Cooking; }
        public void ServeFood() { MealReady = false; Energy = Mathf.Max(0, Energy - 10); ClientHunger = 0; State = ChefState.Serving; }
        public void ArriveAtStove() { SelfPosition = StovePosition; Energy = Mathf.Max(0, Energy - 5); }
        public void ArriveAtClient() { SelfPosition = ClientPosition; Energy = Mathf.Max(0, Energy - 5); }
        public void ArriveAtBreak() { SelfPosition = BreakPosition; Energy = Mathf.Max(0, Energy - 5); }
        public void TakeBreak() { Energy = 100; State = ChefState.Idle; }
    }
}
