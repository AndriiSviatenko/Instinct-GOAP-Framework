using System.Collections.Generic;
using UnityEngine;

namespace Instinct.GOAP.Samples.Stalker
{

    public sealed class StalkerBlackboard : IAgentContext
    {

        public Transform Self;
        public Vector3 CampfirePosition;
        public Vector3 TraderPosition;
        public Vector3 StashPosition;
        public Vector3 ShelterPosition;
        public Vector3 AnomalyPosition;
        public IReadOnlyList<Vector3> PatrolPoints = new Vector3[0];

        public int Health = 100;
        public int Hunger = 30;
        public int Energy = 100;

        public int Money = 150;
        public int Artifacts;
        public int PatrolPointsVisited;
        public bool HasWeapon = true;
        public bool HasFood;
        public bool HasMedkit = true;

        public bool EmissionActive;
        public bool EmissionSafe;

        public Vector3 ThreatPosition;
        public bool EnemyVisible;
        public bool MutantVisible;
        public float DistanceToThreat = 99f;
        public bool ThreatDealt;
        public bool SafeFromThreat;

        public bool AnomalyNearby;
        public bool AnomalyScanned;
        public bool ArtifactCollected;

        public StalkerLocation Location = StalkerLocation.Campfire;
        public StalkerActivity Activity = StalkerActivity.Idle;
        public int NextPatrolPoint;

        public Vector3 SelfPosition => Self != null ? Self.position : Vector3.zero;

        public float DistanceTo(Vector3 target)
            => Vector3.Distance(SelfPosition, target);

        public Vector3 PatrolTarget()
        {
            if (PatrolPoints == null || PatrolPoints.Count == 0) return ShelterPosition;
            return PatrolPoints[NextPatrolPoint % PatrolPoints.Count];
        }

        public void ArrivePatrolPoint()
        {
            if (PatrolPoints != null && PatrolPoints.Count > 0)
                NextPatrolPoint = (NextPatrolPoint + 1) % PatrolPoints.Count;
        }

        public void ArriveAt(StalkerLocation location) => Location = location;

        public void TakeFood()
        {
            HasFood = true;
            Activity = StalkerActivity.Moving;
        }

        public void Eat()
        {
            HasFood = false;
            Hunger = 20;
            Activity = StalkerActivity.Eating;
        }

        public void Sleep()
        {
            Energy = 100;
            Activity = StalkerActivity.Sleeping;
        }

        public void HealSelf()
        {
            HasMedkit = false;
            Health = 100;
            Activity = StalkerActivity.Healing;
        }

        public void ScanAnomalySite()
        {
            AnomalyScanned = true;
            Activity = StalkerActivity.Searching;
        }

        public void CollectArtifact()
        {
            ArtifactCollected = true;
            Artifacts++;
            AnomalyScanned = false;
            AnomalyNearby = false;
            Activity = StalkerActivity.Searching;
        }

        public void DealWithThreat()
        {
            ThreatDealt = true;
            EnemyVisible = false;
            MutantVisible = false;
            Activity = StalkerActivity.Fighting;
        }

        public void FleeThreat()
        {
            SafeFromThreat = true;
            EnemyVisible = false;
            MutantVisible = false;
            Activity = StalkerActivity.Fleeing;
        }

        public void SellArtifact()
        {
            Artifacts = Mathf.Max(0, Artifacts - 1);
            Money += 300;
            Activity = StalkerActivity.Trading;
        }

        public void BuySupplies()
        {
            Money = Mathf.Max(0, Money - 200);
            HasFood = true;
            HasMedkit = true;
            Activity = StalkerActivity.Trading;
        }

        public void WaitOutEmission()
        {
            EmissionSafe = true;
            Activity = StalkerActivity.Resting;
        }

        public void WanderDone()
        {
            PatrolPointsVisited++;
            if (PatrolPointsVisited >= 4) PatrolPointsVisited = 0;
            ArrivePatrolPoint();
            Activity = StalkerActivity.Moving;
        }
    }
}
