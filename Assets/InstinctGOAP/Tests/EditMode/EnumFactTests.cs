using NUnit.Framework;

namespace Instinct.GOAP.Tests
{
    public class EnumFactTests
    {
        private enum Alert { Calm = 0, Suspicious = 1, Hunting = 2 }

        private enum Stance : byte { Standing = 0, Crouched = 1, Prone = 2 }

        private sealed class Facts
        {
            public static readonly Fact<Alert> Alert = Fact<Alert>.Declare();
            public static readonly Fact<Alert> RememberedAlert = Fact<Alert>.Declare();
            public static readonly Fact<Stance> Stance = Fact<Stance>.Declare();
            public static readonly Fact<bool> Handled = Fact<bool>.Declare();
        }

        private static WorldState State() => WorldState.For<Facts>();

        [Test]
        public void RoundTripsThroughASlot()
        {
            var s = State().Set(Facts.Alert, Alert.Hunting);
            Assert.AreEqual(Alert.Hunting, s.Get(Facts.Alert));
        }

        [Test]
        public void RoundTripsAByteBackedEnum()
        {
            var s = State().Set(Facts.Stance, Stance.Prone);
            Assert.AreEqual(Stance.Prone, s.Get(Facts.Stance));
        }

        [Test]
        public void UnwrittenEnumFactReadsAsItsZeroMember()
        {
            Assert.AreEqual(Alert.Calm, State().Get(Facts.Alert));
        }

        [Test]
        public void EqualityAndOrderingBothWork()
        {
            var s = State().Set(Facts.Alert, Alert.Suspicious);

            Assert.IsTrue(new Condition<Alert>(Facts.Alert, Alert.Suspicious).Test(s));
            Assert.IsFalse(new Condition<Alert>(Facts.Alert, Alert.Hunting).Test(s));
            Assert.IsTrue(new Condition<Alert>(Facts.Alert, Compare.GreaterOrEqual, Alert.Suspicious).Test(s));
            Assert.IsTrue(new Condition<Alert>(Facts.Alert, Compare.Less, Alert.Hunting).Test(s));
            Assert.IsFalse(new Condition<Alert>(Facts.Alert, Compare.Greater, Alert.Hunting).Test(s));
        }

        [Test]
        public void PlansOverAnEnumPrecondition()
        {
            var raise = ActionBuilder.Create("Raise")
                .Require(Facts.Alert, Alert.Calm)
                .Effect(Facts.Alert, Alert.Hunting)
                .Cost(1f)
                .Build();

            var handle = ActionBuilder.Create("Handle")
                .Require(Facts.Alert, Compare.GreaterOrEqual, Alert.Hunting)
                .Effect(Facts.Handled, true)
                .Cost(1f)
                .Build();

            var goal = GoalBuilder.Create("Handled").Satisfy(Facts.Handled, true).Priority(10f).Build();

            var plan = new GoapPlanner().BuildPlan(new[] { raise, handle }, goal, State());

            Assert.IsNotNull(plan);
            Assert.AreEqual(2, plan.Actions.Count);
            Assert.AreEqual("Raise", plan.Actions[0].NameOf());
        }

        [Test]
        public void CopyEffectMovesAnEnumBetweenFacts()
        {
            var pre = State().Set(Facts.Alert, Alert.Hunting);
            var next = pre.Clone();

            new CopyEffect<Alert>(Facts.RememberedAlert, Facts.Alert).Apply(pre, next);

            Assert.AreEqual(Alert.Hunting, next.Get(Facts.RememberedAlert));
            Assert.AreEqual(Alert.Calm, pre.Get(Facts.RememberedAlert));
        }

        [Test]
        public void StateDumpPrintsTheMemberNameNotTheNumber()
        {
            var dump = GoapExplain.State<Facts>(State().Set(Facts.Alert, Alert.Hunting));
            StringAssert.Contains("Alert=Hunting", dump);
        }

        [Test]
        public void LongBackedEnumIsRejectedLoudly()
        {
            Assert.AreEqual(FactKind.Unsupported, FactKindOf<TooWide>.Kind);
            Assert.Throws<System.NotSupportedException>(() => Fact<TooWide>.Declare("x"));
        }

        private enum TooWide : long { A = 0 }
    }
}
