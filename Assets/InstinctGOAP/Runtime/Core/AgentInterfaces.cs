namespace Instinct.GOAP
{
    public interface IAgentContext { }

    public interface IWorldStateProvider
    {
        WorldState GetState();
    }

    public interface IActionExecutor<out TCommand>
    {
        TCommand Translate(IWorldState state, IAction action, IAgentContext context);
        void OnSelected(IWorldState state, IAction action, IAgentContext context);
        void OnCompleted(IAction action, IAgentContext context, bool success);
    }
}
