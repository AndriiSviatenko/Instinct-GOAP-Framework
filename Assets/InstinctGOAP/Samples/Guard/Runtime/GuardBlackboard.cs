using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Samples.Guard
{
    public sealed class GuardBlackboard : IAgentContext
    {
        public Transform Self;
        public Transform Intruder;

        public bool CanSeeIntruder;
        public bool HasRadio = true;
        public bool BackupCalled;
        public bool IntruderCaught;

        public Vector3 LastNoisePosition;
        public bool NoisePending;
        public bool NoiseInvestigated;

        public Alert Alert = Alert.Calm;

        public IReadOnlyList<Transform> Waypoints = new Transform[0];
        public int WaypointIndex;
        public int WaypointsVisited;

        public float NoiseArriveDistance = 1.2f;
        public float CatchDistance = 1.6f;
        public float SweepSeconds = 3f;

        public const int PatrolRoundPoints = 3;

        public Vector3 SelfPosition => Self != null ? Self.position : Vector3.zero;
        public Vector3 IntruderPosition => Intruder != null ? Intruder.position : SelfPosition;

        public float DistanceToIntruder =>
            Self != null && Intruder != null ? Vector3.Distance(Self.position, Intruder.position) : 999f;

        public bool AtNoise =>
            NoisePending && Self != null
            && Vector3.Distance(Self.position, LastNoisePosition) <= NoiseArriveDistance;

        public Transform CurrentWaypoint =>
            Waypoints != null && Waypoints.Count > 0 ? Waypoints[WaypointIndex % Waypoints.Count] : null;

        public void AdvanceWaypoint()
        {
            if (Waypoints == null || Waypoints.Count == 0) return;
            WaypointIndex = (WaypointIndex + 1) % Waypoints.Count;
            WaypointsVisited++;
            if (WaypointsVisited >= PatrolRoundPoints) WaypointsVisited = 0;
        }

        public void ReportNoise(Vector3 position)
        {
            LastNoisePosition = position;
            NoisePending = true;
            NoiseInvestigated = false;
            if (Alert == Alert.Calm) Alert = Alert.Suspicious;
        }

        public void ClearNoise()
        {
            NoisePending = false;
            NoiseInvestigated = true;
        }
    }
}
