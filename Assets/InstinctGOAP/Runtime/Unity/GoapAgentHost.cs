using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Unity
{
    public abstract class GoapAgentHost<TCommand> : MonoBehaviour
    {
        [SerializeField] private bool _logPlan;

        protected abstract IGoapAgent<TCommand> Agent { get; }
        protected abstract void ExecuteCommand(TCommand command);

        private TCommand _lastCommand;

        protected virtual void Update()
        {
            var cmd = Agent.Tick();
            if (EqualityComparer<TCommand>.Default.Equals(cmd, _lastCommand))
                return;

            _lastCommand = cmd;

            if (_logPlan)
                Debug.Log($"[GOAP] goal={Agent.CurrentGoal.NameOf()} plan={PlanString()} command={cmd}");

            ExecuteCommand(cmd);
        }

        public void NotifyActionComplete(bool success)
        {
            Agent.NotifyActionComplete(success);
        }

        private string PlanString() => GoapExplain.Chain(Agent.CurrentPlan);
    }
}
