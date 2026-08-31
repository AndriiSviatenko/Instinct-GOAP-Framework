using NUnit.Framework;

namespace Instinct.GOAP.Tests
{
    public class FlowTests
    {
        private sealed class ProbeStep : IStep<object>
        {
            private readonly ActionStatus _status;

            public ProbeStep(ActionStatus status) => _status = status;

            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public void OnEnter(object ctx) => EnterCount++;
            public ActionStatus Tick(object ctx) => _status;
            public void OnExit(object ctx, bool success) => ExitCount++;
        }

        [Test]
        public void FailingSequenceStepExitsExactlyOnce()
        {
            var step = new ProbeStep(ActionStatus.Failure);
            var neverEntered = new ProbeStep(ActionStatus.Success);
            var sequence = Steps.Sequence(step, neverEntered);
            var context = new object();

            sequence.OnEnter(context);
            var status = sequence.Tick(context);
            sequence.OnExit(context, false);

            Assert.AreEqual(ActionStatus.Failure, status);
            Assert.AreEqual(1, step.EnterCount);
            Assert.AreEqual(1, step.ExitCount);
            Assert.AreEqual(0, neverEntered.EnterCount);
            Assert.AreEqual(0, neverEntered.ExitCount);
        }

        [Test]
        public void SuccessfulSequenceEntersAndExitsEachStepOnce()
        {
            var first = new ProbeStep(ActionStatus.Success);
            var second = new ProbeStep(ActionStatus.Success);
            var sequence = Steps.Sequence(first, second);
            var context = new object();

            sequence.OnEnter(context);
            var status = sequence.Tick(context);
            sequence.OnExit(context, true);

            Assert.AreEqual(ActionStatus.Success, status);
            Assert.AreEqual(1, first.EnterCount);
            Assert.AreEqual(1, first.ExitCount);
            Assert.AreEqual(1, second.EnterCount);
            Assert.AreEqual(1, second.ExitCount);
        }
    }
}
