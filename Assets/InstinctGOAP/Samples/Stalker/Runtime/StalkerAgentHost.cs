using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Instinct.GOAP.Samples.Stalker
{

    [AddComponentMenu("GOAP/Samples/Stalker Agent Host")]
    public sealed class StalkerAgentHost : MonoBehaviour
    {
        [Header("Zone points")]
        [SerializeField] private Transform campfire;
        [SerializeField] private Transform trader;
        [SerializeField] private Transform stash;
        [SerializeField] private Transform shelter;
        [SerializeField] private Transform anomaly;
        [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

        [Header("Starting gear")]
        [SerializeField] private bool hasWeapon = true;
        [SerializeField] private bool hasMedkit = true;
        [SerializeField] private int startMoney = 150;
        [SerializeField] private int startHunger = 30;
        [SerializeField] private int startEnergy = 100;

        [Header("Survival tuning")]
        [SerializeField] private float hungerPerSecond = 3f;
        [SerializeField] private float energyDrainPerSecond = 2.5f;
        [SerializeField] private float emissionInterval = 45f;
        [SerializeField] private float emissionDuration = 8f;

        [Header("Movement / action")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float arriveDistance = 0.5f;
        [SerializeField] private float attackTime = 0.6f;
        [SerializeField] private float interactTime = 1f;

        [Header("Threats & sensing")]
        [SerializeField] private Transform enemy;
        [SerializeField] private Transform mutant;
        [SerializeField] private float sightRadius = 6f;
        [SerializeField] private float anomalyProximity = 6.5f;
        [SerializeField] private float threatDamagePerSecond = 12f;
        [SerializeField] private float threatRespawnSeconds = 15f;
        [SerializeField] private float anomalyRefillSeconds = 25f;

        [Header("Debug")]
        [SerializeField] private bool logDecisions;
        [SerializeField] private bool showStatusLabel = true;

        private StalkerAgent _agent;
        private StalkerCommand _command;
        private float _actionTimer;
        private float _emissionClock;
        private float _hungerAccum;
        private float _energyAccum;
        private float _healthAccum;
        private bool _warnedNullPositions;
        private bool _wasNearAnomaly;
        private bool _artifactTaken;
        private float _anomalyReadyAt;
        private Transform _threat;
        private float _enemyRespawnAt = -1f;
        private float _mutantRespawnAt = -1f;
        private TextMeshPro _status;
        private string _statusText;

        public StalkerAgent Agent => _agent;
        public StalkerBlackboard Board => _agent?.Board;

        private void Awake()
        {
            var board = new StalkerBlackboard
            {
                Self = transform,
                CampfirePosition = Position(campfire, "Campfire"),
                TraderPosition = Position(trader, "Trader"),
                StashPosition = Position(stash, "Stash"),
                ShelterPosition = Position(shelter, "Shelter"),
                AnomalyPosition = Position(anomaly, "Anomaly"),
                PatrolPoints = ToVectors(patrolPoints),
                HasWeapon = hasWeapon,
                HasMedkit = hasMedkit,
                Money = startMoney,
                Hunger = startHunger,
                Energy = startEnergy,
            };

            _agent = new StalkerAgent(board);

            var report = StalkerAgent.ValidateDomain();
            if (!string.IsNullOrEmpty(report)) Debug.LogWarning($"[Stalker] domain issues:\n{report}");

            if (showStatusLabel) CreateStatusLabel();
        }

        private void Update()
        {
            if (_agent == null) return;

            Sense();

            _command = _agent.Tick();
            if (logDecisions)
                Debug.Log($"[Stalker] {_agent.CurrentGoal.NameOf()} :: {_agent.PlanChain()} :: {_command}");

            Act();
            UpdateStatusLabel();
        }

        private void LateUpdate()
        {
            if (_status == null) return;

            _status.transform.position = transform.position + Vector3.up * 3.4f;

            var cam = Camera.main;
            _status.transform.rotation = cam != null
                ? cam.transform.rotation
                : Quaternion.identity;
        }

        private void Sense()
        {
            var b = Board;

            _hungerAccum += hungerPerSecond * Time.deltaTime;
            while (_hungerAccum >= 1f) { _hungerAccum -= 1f; b.Hunger = Mathf.Clamp(b.Hunger + 1, 0, 100); }

            if (b.Activity != StalkerActivity.Sleeping)
            {
                _energyAccum += energyDrainPerSecond * Time.deltaTime;
                while (_energyAccum >= 1f) { _energyAccum -= 1f; b.Energy = Mathf.Clamp(b.Energy - 1, 0, 100); }
            }

            _emissionClock += Time.deltaTime;
            if (!b.EmissionActive && _emissionClock >= emissionInterval)
            {
                b.EmissionActive = true;
                b.EmissionSafe = false;
                _emissionClock = 0f;
                _agent.ForceReplan();
            }
            else if (b.EmissionActive && _emissionClock >= emissionDuration)
            {
                b.EmissionActive = false;
                _emissionClock = 0f;
                _agent.ForceReplan();
            }

            b.DistanceToThreat = b.EnemyVisible || b.MutantVisible
                ? Vector3.Distance(transform.position, b.ThreatPosition)
                : 99f;

            Perceive();
        }

        private void Perceive()
        {
            var b = Board;

            DespawnDealtThreat();

            float enemyDistance = ThreatDistance(enemy);
            float mutantDistance = ThreatDistance(mutant);
            bool enemyInSight = enemyDistance <= sightRadius;
            bool mutantInSight = mutantDistance <= sightRadius;

            if (enemyInSight && !b.EnemyVisible && !b.MutantVisible && !b.ThreatDealt)
            {
                _threat = enemy;
                ReportEnemy(enemy.position);
            }
            else if (mutantInSight && !b.EnemyVisible && !b.MutantVisible && !b.ThreatDealt)
            {
                _threat = mutant;
                ReportMutant(mutant.position);
            }

            ApplyThreatDamage(b, enemyDistance, mutantDistance);

            TrackAnomalyRefill(b);

            bool nearAnomaly = b.DistanceTo(b.AnomalyPosition) <= anomalyProximity;
            if (nearAnomaly && !_wasNearAnomaly && !b.AnomalyNearby && Time.time >= _anomalyReadyAt)
                ReportAnomaly(b.AnomalyPosition);
            _wasNearAnomaly = nearAnomaly;
        }

        private void TrackAnomalyRefill(StalkerBlackboard b)
        {
            if (b.ArtifactCollected && !_artifactTaken)
            {
                _artifactTaken = true;
                _anomalyReadyAt = Time.time + anomalyRefillSeconds;
            }
            else if (!b.ArtifactCollected)
            {
                _artifactTaken = false;
            }
        }

        private float ThreatDistance(Transform threat)
            => threat != null && threat.gameObject.activeInHierarchy
                ? Vector3.Distance(transform.position, threat.position)
                : 99f;

        private void DespawnDealtThreat()
        {
            if (_threat != null && _threat.gameObject.activeInHierarchy
                && Board.ThreatDealt && !Board.EnemyVisible && !Board.MutantVisible)
            {
                bool wasEnemy = _threat == enemy;
                _threat.gameObject.SetActive(false);
                _threat = null;
                if (wasEnemy) _enemyRespawnAt = Time.time + threatRespawnSeconds;
                else _mutantRespawnAt = Time.time + threatRespawnSeconds;
                Board.ThreatDealt = false;
                Board.SafeFromThreat = false;
            }

            if (_enemyRespawnAt > 0f && Time.time >= _enemyRespawnAt)
            {
                _enemyRespawnAt = -1f;
                if (enemy != null) enemy.gameObject.SetActive(true);
            }

            if (_mutantRespawnAt > 0f && Time.time >= _mutantRespawnAt)
            {
                _mutantRespawnAt = -1f;
                if (mutant != null) mutant.gameObject.SetActive(true);
            }
        }

        private void ApplyThreatDamage(StalkerBlackboard b, float enemyDistance, float mutantDistance)
        {
            bool inReach = (b.EnemyVisible && enemyDistance <= 4f) || (b.MutantVisible && mutantDistance <= 4f);
            if (!inReach) return;

            _healthAccum += threatDamagePerSecond * Time.deltaTime;
            while (_healthAccum >= 1f)
            {
                _healthAccum -= 1f;
                b.Health = Mathf.Max(1, b.Health - 1);
            }
        }

        private void Act()
        {
            switch (_command.Kind)
            {
                case StalkerCommandKind.MoveTo:
                    if (StepToward(_command.Target, moveSpeed)) Complete(true);
                    break;

                case StalkerCommandKind.Attack:
                    _actionTimer += Time.deltaTime;
                    if (_actionTimer >= attackTime) Complete(true);
                    break;

                case StalkerCommandKind.Interact:
                    _actionTimer += Time.deltaTime;
                    if (_actionTimer >= interactTime) Complete(true);
                    break;

                case StalkerCommandKind.Search:
                    _actionTimer += Time.deltaTime;
                    if (_actionTimer >= Mathf.Max(0.1f, _command.Duration)) Complete(true);
                    break;

                case StalkerCommandKind.Wait:
                    _actionTimer += Time.deltaTime;
                    if (_actionTimer >= Mathf.Max(0.1f, _command.Duration)) Complete(true);
                    break;

                default:
                    if (_agent.CurrentAction != null) Complete(true);
                    break;
            }
        }

        private bool StepToward(Vector3 target, float speed)
        {
            Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
            Vector3 delta = flat - transform.position;

            if (delta.sqrMagnitude <= arriveDistance * arriveDistance) return true;

            transform.position += delta.normalized * (speed * Time.deltaTime);
            transform.forward = Vector3.Slerp(transform.forward, delta.normalized, 8f * Time.deltaTime);
            return false;
        }

        private void Complete(bool success)
        {
            _actionTimer = 0f;
            _agent.NotifyActionComplete(success);
        }

        private void CreateStatusLabel()
        {
            var label = new GameObject("Status");
            label.transform.position = transform.position + Vector3.up * 3.4f;

            _status = label.AddComponent<TextMeshPro>();
            _status.font = TMP_Settings.defaultFontAsset;
            _status.fontSize = 30;
            _status.transform.localScale = Vector3.one * 0.075f;
            _status.alignment = TextAlignmentOptions.Center;
            _status.overflowMode = TextOverflowModes.Overflow;
            _status.rectTransform.sizeDelta = new Vector2(44f, 14f);
#if UNITY_6000_0_OR_NEWER
            _status.textWrappingMode = TextWrappingModes.NoWrap;
#else
            _status.enableWordWrapping = false;
#endif
            _status.color = Color.white;
            _status.fontStyle = FontStyles.Bold;
            _status.outlineWidth = 0.12f;
            _status.outlineColor = Color.black;
        }

        private void UpdateStatusLabel()
        {
            if (_status == null) return;

            var b = Board;
            var goal = _agent.CurrentGoal;
            var step = _agent.CurrentAction;
            string goalName = goal != null ? goal.NameOf() : b.EmissionActive ? "ride out emission" : "idle";
            string stepName = step != null ? step.NameOf() : b.EmissionActive ? "wait" : "-";
            string text = $"goal: {goalName}\nstep:  {stepName}\nhunger: {b.Hunger}  energy: {b.Energy}  hp: {b.Health}";
            if (b.Artifacts > 0 || b.Money != startMoney) text += $"\nart: {b.Artifacts}  money: {b.Money}";
            if (b.EmissionActive) text += "\n>> EMISSION <<";
            if (text == _statusText) return;

            _statusText = text;
            _status.text = text;
        }

        public void TriggerEmission()
        {
            var b = Board;
            if (b == null) return;
            b.EmissionActive = true;
            b.EmissionSafe = false;
            _emissionClock = 0f;
            _agent.ForceReplan();
        }

        public void ReportEnemy(Vector3 position)
        {
            var b = Board;
            if (b == null) return;
            b.EnemyVisible = true;
            b.MutantVisible = false;
            b.ThreatPosition = position;
            b.ThreatDealt = false;
            b.SafeFromThreat = false;
            _agent.ForceReplan();
        }

        public void ReportMutant(Vector3 position)
        {
            var b = Board;
            if (b == null) return;
            b.MutantVisible = true;
            b.EnemyVisible = false;
            b.ThreatPosition = position;
            b.ThreatDealt = false;
            b.SafeFromThreat = false;
            _agent.ForceReplan();
        }

        public void ClearThreat()
        {
            var b = Board;
            if (b == null) return;
            b.EnemyVisible = false;
            b.MutantVisible = false;
            _agent.ForceReplan();
        }

        public void ReportAnomaly(Vector3 position)
        {
            var b = Board;
            if (b == null) return;
            b.AnomalyNearby = true;
            b.AnomalyScanned = false;
            b.ArtifactCollected = false;
            b.AnomalyPosition = position;
            _agent.ForceReplan();
        }

        [ContextMenu("Trigger emission")]
        private void MenuTriggerEmission() => TriggerEmission();

        [ContextMenu("Report enemy nearby")]
        private void MenuReportEnemy() => ReportEnemy(transform.position + transform.forward * 6f + Vector3.right * 2f);

        [ContextMenu("Report mutant nearby")]
        private void MenuReportMutant() => ReportMutant(transform.position + transform.forward * 6f - Vector3.right * 2f);

        [ContextMenu("Report anomaly nearby")]
        private void MenuReportAnomaly() => ReportAnomaly(transform.position + Vector3.forward * 8f);

        [ContextMenu("Explain last decision")]
        private void ExplainLastDecision() => Debug.Log(_agent?.ExplainDecision());

        private Vector3 Position(Transform t, string label)
        {
            if (t != null) return t.position;
            if (!_warnedNullPositions)
            {
                Debug.LogWarning($"[Stalker] '{label}' point is not assigned - using self position.", this);
                _warnedNullPositions = true;
            }
            return transform.position;
        }

        private static List<Vector3> ToVectors(List<Transform> transforms)
        {
            var list = new List<Vector3>(transforms.Count);
            foreach (var t in transforms)
                if (t != null) list.Add(t.position);
            return list;
        }

        private void OnDrawGizmosSelected()
        {
            var points = new (Transform t, Color c, string label)[]
            {
                (campfire, new Color(1f, 0.6f, 0.2f), "Campfire"),
                (trader, new Color(0.2f, 1f, 0.4f), "Trader"),
                (stash, new Color(0.8f, 0.7f, 0.2f), "Stash"),
                (shelter, new Color(0.3f, 0.7f, 1f), "Shelter"),
                (anomaly, new Color(1f, 0.2f, 1f), "Anomaly"),
            };

            foreach (var (t, c, label) in points)
            {
                if (t == null) continue;
                Gizmos.color = c;
                Gizmos.DrawWireSphere(t.position, 0.6f);
            }

            if (patrolPoints == null) return;
            Gizmos.color = new Color(0.6f, 0.9f, 0.3f, 0.7f);
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (patrolPoints[i] == null) continue;
                Gizmos.DrawWireCube(patrolPoints[i].position, Vector3.one * 0.4f);
            }

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            if (enemy != null) Gizmos.DrawWireSphere(enemy.position, 0.6f);

            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.9f);
            if (mutant != null) Gizmos.DrawWireSphere(mutant.position, 0.6f);
        }
    }
}
