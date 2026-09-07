using UnityEngine;

namespace Instinct.GOAP.Samples.Stalker
{
    public enum StalkerCommandKind
    {
        Idle = 0,
        MoveTo,
        Attack,
        Interact,
        Search,
        Wait,
    }

    public readonly struct StalkerCommand
    {
        public readonly StalkerCommandKind Kind;
        public readonly Vector3 Target;
        public readonly float Duration;
        public readonly ActionKey Source;

        public StalkerCommand(StalkerCommandKind kind, Vector3 target, float duration, ActionKey source = default)
        {
            Kind = kind;
            Target = target;
            Duration = duration;
            Source = source;
        }

        public StalkerCommand From(ActionKey source) => new StalkerCommand(Kind, Target, Duration, source);

        public static StalkerCommand Idle => new StalkerCommand(StalkerCommandKind.Idle, Vector3.zero, 0f);
        public static StalkerCommand MoveTo(Vector3 target) => new StalkerCommand(StalkerCommandKind.MoveTo, target, 0f);
        public static StalkerCommand Attack(Vector3 target) => new StalkerCommand(StalkerCommandKind.Attack, target, 0f);
        public static StalkerCommand Interact(Vector3 target) => new StalkerCommand(StalkerCommandKind.Interact, target, 0f);
        public static StalkerCommand Search(Vector3 target, float seconds) => new StalkerCommand(StalkerCommandKind.Search, target, seconds);
        public static StalkerCommand Wait(float seconds) => new StalkerCommand(StalkerCommandKind.Wait, Vector3.zero, seconds);

        public override string ToString() =>
            Kind == StalkerCommandKind.Idle ? "Idle" : $"{Kind} -> {Target} ({Source})";
    }
}
