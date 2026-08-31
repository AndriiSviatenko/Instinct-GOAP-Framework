using NUnit.Framework;

namespace Instinct.GOAP.Tests
{
    public class KeyTests
    {
        private static class Keys
        {
            public static readonly GoalKey Alpha = GoalKey.Declare();
            public static readonly GoalKey Beta = GoalKey.Declare();
        }

        private sealed class Facts
        {
            public static readonly Fact<bool> Done = Fact<bool>.Declare();
        }

        private sealed class Walk : IAction
        {
            public ActionKey Key { get; } = ActionKey.Of<Walk>();
            public System.Collections.Generic.IReadOnlyList<ICondition> Preconditions { get; } = new ICondition[0];
            public System.Collections.Generic.IReadOnlyList<IEffect> Effects { get; } =
                new IEffect[] { new Effect<bool>(Facts.Done, true) };
            public float Cost(IWorldState s, IPlanningContext c) => 1f;
            public WorldState ApplyTo(WorldState s) => EffectExtensions.ApplyAll(Effects, s);
        }

        [Test]
        public void DeclaredKeysAreDistinctAndNamedAfterTheirField()
        {
            Assert.AreNotEqual(Keys.Alpha, Keys.Beta);
            Assert.AreEqual("Alpha", Keys.Alpha.DebugName);
            Assert.AreEqual("Alpha", Keys.Alpha.ToString());
        }

        [Test]
        public void DefaultKeyIsNoneAndCollidesWithNothing()
        {
            Assert.IsTrue(default(GoalKey).IsNone);
            Assert.IsTrue(default(ActionKey).IsNone);
            Assert.AreNotEqual(default(GoalKey), Keys.Alpha);
        }

        [Test]
        public void TypeKeysAreStableAcrossInstances()
        {
            Assert.AreEqual(new Walk().Key, new Walk().Key);
            Assert.AreEqual(ActionKey.Of<Walk>(), new Walk().Key);
            Assert.AreEqual("Walk", ActionKey.Of<Walk>().DebugName);
        }

        [Test]
        public void NamedKeysIntern()
        {
            Assert.AreEqual(GoalKey.Named("Patrol"), GoalKey.Named("Patrol"));
            Assert.AreNotEqual(GoalKey.Named("Patrol"), GoalKey.Named("patrol"));
            Assert.AreEqual(ActionKey.Named("Sit"), ActionKey.Named("Sit"));
        }

        [Test]
        public void BuilderGoalsWithTheSameNameShareOneIdentity()
        {
            var a = GoalBuilder.Create("Rest").Satisfy(Facts.Done, true).Build();
            var b = GoalBuilder.Create("Rest").Satisfy(Facts.Done, true).Build();
            Assert.AreEqual(a.Key, b.Key);
        }

        [Test]
        public void RegistryFindsEveryDeclaredKey()
        {
            var found = KeyRegistry.GoalKeysIn(typeof(Keys));
            Assert.AreEqual(2, found.Count);
            Assert.Contains(Keys.Alpha, (System.Collections.ICollection)found);
        }

        [Test]
        public void ValidateReportsADeclaredGoalThatNeverMadeItIntoTheDomain()
        {
            var domain = new DomainBuilder()
                .AddGoal(GoalBuilder.Create(Keys.Alpha).Satisfy(Facts.Done, true).Build())
                .AddAction(new Walk())
                .DeclaredGoalsIn(typeof(Keys));

            var issues = domain.Validate();
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(DomainBuilder.Issue.Level.Error, issues[0].Severity);
            StringAssert.Contains("Beta", issues[0].Subject);
        }

        [Test]
        public void ValidateReportsTwoGoalsSharingOneIdentity()
        {
            var domain = new DomainBuilder()
                .AddGoal(GoalBuilder.Create(Keys.Alpha).Satisfy(Facts.Done, true).Build())
                .AddGoal(GoalBuilder.Create(Keys.Alpha).Satisfy(Facts.Done, false).Build())
                .AddAction(new Walk());

            var issues = domain.Validate();
            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("duplicate goal key", issues[0].Message);
        }
    }
}
