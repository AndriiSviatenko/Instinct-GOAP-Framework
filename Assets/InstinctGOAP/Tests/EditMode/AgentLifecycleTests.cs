using System.Collections.Generic;
using NUnit.Framework;

namespace Instinct.GOAP.Tests
{
    public class AgentLifecycleTests
    {
        private sealed class Facts
        {
            public static readonly Fact<bool> Done = Fact<bool>.Declare();
        }

        private sealed class Provider : IWorldStateProvider
        {
            public WorldState GetState() => WorldState.For<Facts>();
        }

        private sealed class Executor : IActionExecutor<IAction>
        {
            public int SelectedCount { get; private set; }
            public int TranslateCount { get; private set; }
            public int InterruptedCount { get; private set; }

            public IAction Translate(IWorldState state, IAction action, IAgentContext context)
            {
                TranslateCount++;
                return action;
            }

            public void OnSelected(IWorldState state, IAction action, IAgentContext context)
                => SelectedCount++;

            public void OnCompleted(IAction action, IAgentContext context, bool success)
            {
                if (!success) InterruptedCount++;
            }
        }

        private static IAction Action() => ActionBuilder.Create("Complete")
            .Effect(Facts.Done, true)
            .Cost(1f)
            .Build();

        private static IGoal Goal() => GoalBuilder.Create("Done")
            .Satisfy(Facts.Done, true)
            .Priority(10f)
            .Build();

        private static GoapAgent<IAction> CreateAgent(Executor executor)
            => new GoapAgent<IAction>(
                new GoapPlanner(),
                new List<IGoal> { Goal() },
                new List<IAction> { Action() },
                new Provider(),
                executor);

        [Test]
        public void OnSelectedRunsOncePerActionEntry()
        {
            var executor = new Executor();
            var agent = CreateAgent(executor);

            agent.Tick();
            agent.Tick();

            Assert.AreEqual(1, executor.SelectedCount);
            Assert.AreEqual(2, executor.TranslateCount);
        }

        [Test]
        public void ForceReplanInterruptsAndRestartsTheCurrentAction()
        {
            var executor = new Executor();
            var agent = CreateAgent(executor);

            agent.Tick();
            agent.ForceReplan();
            agent.Tick();

            Assert.AreEqual(1, executor.InterruptedCount);
            Assert.AreEqual(2, executor.SelectedCount);
        }
    }
}
