using System;

namespace Instinct.GOAP
{
    public interface IPlanningContext
    {
        object Extra { get; }
        T GetExtra<T>();
    }

    public sealed class PlanningContext : IPlanningContext
    {
        public object Extra { get; }

        public PlanningContext(object extra = null) => Extra = extra;

        public T GetExtra<T>() => Extra is T t ? t : default;
    }
}
