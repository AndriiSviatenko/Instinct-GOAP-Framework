using NUnit.Framework;

namespace Instinct.GOAP.Tests
{
    public class PlannerTests
    {
        private sealed class Facts
        {
            public static readonly Fact<bool> HasKey = Fact<bool>.Declare();
            public static readonly Fact<bool> DoorOpen = Fact<bool>.Declare();
            public static readonly Fact<bool> Escaped = Fact<bool>.Declare();
        }

        private static IGoal EscapeGoal => GoalBuilder.Create()
            .Satisfy(Facts.Escaped, true)
            .Priority(10f)
            .Build();

        private static IAction PickKey => ActionBuilder.Create()
            .Require(Facts.HasKey, false)
            .Effect(Facts.HasKey, true)
            .Cost(1f)
            .Build();

        private static IAction OpenDoor => ActionBuilder.Create()
            .Require(Facts.HasKey, true)
            .Require(Facts.DoorOpen, false)
            .Effect(Facts.DoorOpen, true)
            .Cost(1f)
            .Build();

        private static IAction Escape => ActionBuilder.Create()
            .Require(Facts.DoorOpen, true)
            .Require(Facts.Escaped, false)
            .Effect(Facts.Escaped, true)
            .Cost(1f)
            .Build();

        [Test]
        public void PlansKeyDoorEscapeSequence()
        {
            var planner = new GoapPlanner();
            var actions = new[] { PickKey, OpenDoor, Escape };
            var start = WorldState.For<Facts>();
            var plan = planner.BuildPlan(actions, EscapeGoal, start);

            Assert.IsNotNull(plan);
            Assert.AreEqual(3, plan.Actions.Count);
            Assert.AreEqual("PickKey", plan.Actions[0].NameOf());
            Assert.AreEqual("OpenDoor", plan.Actions[1].NameOf());
            Assert.AreEqual("Escape", plan.Actions[2].NameOf());
        }

        [Test]
        public void ReturnsNullWhenGoalAlreadySatisfied()
        {
            var planner = new GoapPlanner();
            var actions = new[] { PickKey, OpenDoor, Escape };
            var start = WorldState.For<Facts>().Set(Facts.Escaped, true);
            var plan = planner.BuildPlan(actions, EscapeGoal, start);

            Assert.IsNull(plan);
        }

        [Test]
        public void ChoosesCheaperPath()
        {
            var planner = new GoapPlanner();

            var expensive = ActionBuilder.Create()
                .Name("expensive")
                .Require(Facts.HasKey, false)
                .Effect(Facts.HasKey, true)
                .Cost(100f)
                .Build();

            var cheap = ActionBuilder.Create()
                .Name("cheap")
                .Require(Facts.HasKey, false)
                .Effect(Facts.HasKey, true)
                .Cost(1f)
                .Build();

            var open = ActionBuilder.Create()
                .Name("open")
                .Require(Facts.HasKey, true)
                .Effect(Facts.DoorOpen, true)
                .Cost(1f)
                .Build();

            var goal = GoalBuilder.Create()
                .Name("OpenDoorGoal")
                .Satisfy(Facts.DoorOpen, true)
                .Priority(10f)
                .Build();

            var start = WorldState.For<Facts>();
            var plan = planner.BuildPlan(new[] { expensive, cheap, open }, goal, start);

            Assert.IsNotNull(plan);
            Assert.AreEqual(2, plan.Actions.Count);
            Assert.AreEqual("cheap", plan.Actions[0].NameOf());
        }
    }
}
