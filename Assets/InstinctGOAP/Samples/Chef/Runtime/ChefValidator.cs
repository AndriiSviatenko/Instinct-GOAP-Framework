using Instinct.GOAP;

namespace Instinct.GOAP.Samples.Chef
{

    public static class ChefValidator
    {

        public static string ValidateDomain() =>
            new DomainBuilder()
                .AddActions(ChefActions.All())
                .AddGoals(ChefGoals.All())
                .DeclaredActionsIn(typeof(ChefActionKeys))
                .DeclaredGoalsIn(typeof(ChefGoalKeys))
                .Describe();
    }
}
