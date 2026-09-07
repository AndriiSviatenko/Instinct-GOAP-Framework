using Cysharp.Threading.Tasks;
using Instinct.GOAP;
using Instinct.GOAP.Unity;

namespace Instinct.GOAP.Samples.Farmer
{

    public static class FarmerBrain
    {

        public static GoapDomain<FarmerContext> Build()
        {
            var d = GoapDomainBuilder<FarmerContext>.For<FarmerFacts>();

            d.Bind(FarmerFacts.Energy, c => c.Energy, (c, v) => c.Energy = v);
            d.Bind(FarmerFacts.CropsGrown, c => c.CropsGrown, (c, v) => c.CropsGrown = v);
            d.Bind(FarmerFacts.CropsRipe, c => c.CropsRipe, (c, v) => c.CropsRipe = v);

            d.Bind(FarmerFacts.DistanceToHome, c => c.DistanceTo(c.Home));
            d.Bind(FarmerFacts.DistanceToField, c => c.DistanceTo(c.Field));

            d.Use(new WalkToField(), new WalkToHome(), new Harvest(), new Rest());

            d.Goal(FarmerGoalKeys.WorkTheField)
                .Satisfy(FarmerFacts.Energy, Compare.LessOrEqual, 15)
                .RelevantWhen(s => s.Get(FarmerFacts.Energy) >= 45)
                .Priority(40)
                .Heuristic(s => (s.Get(FarmerFacts.Energy) - 15) / 60f);

            d.Goal(FarmerGoalKeys.Recover)
                .Satisfy(FarmerFacts.Energy, Compare.GreaterOrEqual, 100)
                .RelevantWhen(s => s.Get(FarmerFacts.Energy) < 45)
                .Priority(80)
                .Heuristic(s => (100 - s.Get(FarmerFacts.Energy)) / 100f);

            return d.Build();
        }
    }

    public sealed class WalkToField : GoapAction<FarmerContext>
    {
        protected override ActionKey Key => FarmerActionKeys.WalkToField;

        protected override void Setup()
        {
            Require(FarmerFacts.DistanceToField, Compare.Greater, 1f);
            Effect(FarmerFacts.DistanceToField, 0f);
            Cost(1f);
        }

        protected override async UniTask Run(FarmerContext c)
        {
            if (c.Field == null) Fail("field is not assigned");
            await MoveTo(c, () => c.Field.position);
        }
    }

    public sealed class WalkToHome : GoapAction<FarmerContext>
    {
        protected override ActionKey Key => FarmerActionKeys.WalkToHome;

        protected override void Setup()
        {
            Require(FarmerFacts.DistanceToHome, Compare.Greater, 1f);
            Effect(FarmerFacts.DistanceToHome, 0f);
            Cost(1f);
        }

        protected override async UniTask Run(FarmerContext c)
        {
            if (c.Home == null) Fail("home is not assigned");
            await MoveTo(c, () => c.Home.position);
        }
    }

    public sealed class Harvest : GoapAction<FarmerContext>
    {
        protected override ActionKey Key => FarmerActionKeys.Harvest;

        protected override void Setup()
        {
            Require(FarmerFacts.DistanceToField, Compare.LessOrEqual, 1f);
            Require(FarmerFacts.CropsRipe, Compare.GreaterOrEqual, 1);
            Require(FarmerFacts.Energy, Compare.GreaterOrEqual, 25);
            Add(FarmerFacts.Energy, -25, min: 0, max: 100);
            Add(FarmerFacts.CropsRipe, -1, min: 0);
            Add(FarmerFacts.CropsGrown, +1);
            Cost(1f);
        }

        protected override async UniTask Run(FarmerContext c)
        {
            await Wait(0.5f);
        }
    }

    public sealed class Rest : GoapAction<FarmerContext>
    {
        protected override ActionKey Key => FarmerActionKeys.Rest;

        protected override void Setup()
        {
            Require(FarmerFacts.DistanceToHome, Compare.LessOrEqual, 1f);
            Effect(FarmerFacts.Energy, 100);
            Cost(0.5f);
        }

        protected override async UniTask Run(FarmerContext c)
        {
            await Wait(1f);
        }
    }
}
