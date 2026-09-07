using UnityEngine;

namespace Instinct.GOAP.Samples.Guard
{
    public enum GuardCommandKind
    {
        Idle = 0,
        MoveTo,
        Sprint,
        LookAround,
        Interact,
    }

    public readonly struct GuardCommand
    {
        public readonly GuardCommandKind Kind;
        public readonly Vector3 Target;
        public readonly float Duration;
        public readonly ActionKey Source;

        public GuardCommand(GuardCommandKind kind, Vector3 target, float duration, ActionKey source = default)
        {
            Kind = kind;
            Target = target;
            Duration = duration;
            Source = source;
        }

        public GuardCommand From(ActionKey source) => new GuardCommand(Kind, Target, Duration, source);

        public static GuardCommand Idle => new GuardCommand(GuardCommandKind.Idle, Vector3.zero, 0f);
        public static GuardCommand MoveTo(Vector3 target) => new GuardCommand(GuardCommandKind.MoveTo, target, 0f);
        public static GuardCommand Sprint(Vector3 target) => new GuardCommand(GuardCommandKind.Sprint, target, 0f);
        public static GuardCommand LookAround(float seconds) => new GuardCommand(GuardCommandKind.LookAround, Vector3.zero, seconds);
        public static GuardCommand Interact(Vector3 target) => new GuardCommand(GuardCommandKind.Interact, target, 0f);

        public override string ToString() =>
            Kind == GuardCommandKind.Idle ? "Idle" : $"{Kind} -> {Target} ({Source})";
    }
}
