using UnityEngine;

namespace Instinct.GOAP.Samples.Chef
{
    public enum ChefCommandKind
    {
        Idle = 0,
        MoveTo,
        Cook,
        Serve,
        TakeBreak,
    }

    public readonly struct ChefCommand
    {
        public readonly ChefCommandKind Kind;
        public readonly Vector3 Target;
        public readonly ActionKey Source;

        public ChefCommand(ChefCommandKind kind, Vector3 target, ActionKey source = default)
        {
            Kind = kind;
            Target = target;
            Source = source;
        }

        public ChefCommand From(ActionKey source) => new ChefCommand(Kind, Target, source);

        public static ChefCommand Idle => new ChefCommand(ChefCommandKind.Idle, Vector3.zero);
        public static ChefCommand MoveTo(Vector3 target) => new ChefCommand(ChefCommandKind.MoveTo, target);
        public static ChefCommand Cook() => new ChefCommand(ChefCommandKind.Cook, Vector3.zero);
        public static ChefCommand Serve() => new ChefCommand(ChefCommandKind.Serve, Vector3.zero);
        public static ChefCommand TakeBreak() => new ChefCommand(ChefCommandKind.TakeBreak, Vector3.zero);

        public override string ToString() =>
            Kind == ChefCommandKind.Idle ? "Idle" : $"{Kind} -> {Target} ({Source})";
    }
}
