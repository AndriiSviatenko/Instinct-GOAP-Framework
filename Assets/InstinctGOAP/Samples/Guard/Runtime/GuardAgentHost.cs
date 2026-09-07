using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Instinct.GOAP.Samples.Guard
{
    [AddComponentMenu("GOAP/Samples/Guard Agent Host")]
    public sealed class GuardAgentHost : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform intruder;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        [Header("Senses")]
        [SerializeField] private float sightRange = 12f;
        [SerializeField] private float sightAngle = 110f;
        [SerializeField] private LayerMask sightBlockers = ~0;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float arriveDistance = 0.35f;

        [Header("Intruder world")]
        [SerializeField] private float roamRadius = 8f;
        [SerializeField] private float skulkSpeed = 1.1f;
        [SerializeField] private float fleeSpeed = 2.8f;
        [SerializeField] private float fleeRadius = 7f;
        [SerializeField] private float escapeDistance = 11f;
        [SerializeField] private float custodySeconds = 2.5f;
        [SerializeField] private float respawnDelay = 5f;
        [SerializeField] private float noiseInterval = 12f;

        [Header("Gear")]
        [SerializeField] private bool hasRadio = true;

        [Header("Debug")]
        [SerializeField] private bool logDecisions;
        [SerializeField] private bool showStatusLabel = true;

        private enum IntruderPhase
        {
            Skulking,
            Fleeing,
            Custody,
            Away,
        }

        private GuardAgent _agent;
        private GuardCommand _command;
        private float _lookTimer;
        private TextMeshPro _status;
        private string _statusText;
        private TextMeshPro _intruderLabel;
        private string _intruderLabelText;
        private IntruderPhase _phase;
        private Vector3 _roamTarget;
        private float _pauseAt;
        private float _lostSince = -1f;
        private Vector3 _lastKnownPosition;
        private Vector3 _fleePoint;
        private float _fleeRecalcAt;
        private float _fleeStartedAt;
        private float _custodyUntil;
        private float _respawnAt;
        private float _noiseClock;
        private Transform _noisePing;
        private float _pingUntil;

        public GuardAgent Agent => _agent;

        private void Awake()
        {
            var board = new GuardBlackboard
            {
                Self = transform,
                Intruder = intruder,
                Waypoints = waypoints,
                HasRadio = hasRadio,
            };

            _agent = new GuardAgent(board);

            var report = GuardAgent.ValidateDomain();
            if (!string.IsNullOrEmpty(report)) Debug.LogWarning($"[Guard] domain issues:\n{report}");

            if (showStatusLabel) CreateStatusLabel();
            if (intruder != null) CreateIntruderLabel();

            _noiseClock = Random.Range(noiseInterval * 0.4f, noiseInterval);
            PickRoamTarget();
        }

        private void Update()
        {
            if (_agent == null) return;

            Sense();
            SimulateIntruder();

            _command = _agent.Tick();
            if (logDecisions) Debug.Log($"[Guard] {_agent.CurrentGoal.NameOf()} :: {_agent.PlanChain()} :: {_command}");

            Act();
            UpdateStatusLabel();
            UpdateIntruderLabel();
        }

        private void LateUpdate()
        {
            var cam = Camera.main;

            if (_status != null)
                _status.transform.rotation = cam != null
                    ? cam.transform.rotation
                    : Quaternion.identity;

            if (_intruderLabel != null)
                _intruderLabel.transform.rotation = cam != null
                    ? cam.transform.rotation
                    : Quaternion.identity;
        }

        private void Sense()
        {
            var board = _agent.Board;
            board.CanSeeIntruder = _phase == IntruderPhase.Skulking || _phase == IntruderPhase.Fleeing
                ? CanSee(intruder)
                : false;
            if (board.CanSeeIntruder)
            {
                board.Alert = Alert.Hunting;
                board.ClearNoise();
            }
        }

        private bool CanSee(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;

            Vector3 toTarget = target.position - transform.position;
            if (toTarget.sqrMagnitude > sightRange * sightRange) return false;
            if (Vector3.Angle(transform.forward, toTarget) > sightAngle * 0.5f) return false;

            return !Physics.Linecast(transform.position + Vector3.up, target.position + Vector3.up,
                                     sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private void SimulateIntruder()
        {
            if (intruder == null) return;

            var board = _agent.Board;

            switch (_phase)
            {
                case IntruderPhase.Skulking:
                    if (board.IntruderCaught)
                    {
                        EnterCustody();
                        break;
                    }

                    bool guardClosingIn = board.DistanceToIntruder <= fleeRadius
                        && (_command.Kind == GuardCommandKind.Sprint || board.CanSeeIntruder);
                    if (guardClosingIn)
                    {
                        _phase = IntruderPhase.Fleeing;
                        _fleeStartedAt = Time.time;
                        _fleeRecalcAt = 0f;
                        _lostSince = -1f;
                        break;
                    }

                    if (Time.time < _pauseAt) break;
                    if (StepIntruder(_roamTarget, skulkSpeed)) PickRoamTarget();
                    break;

                case IntruderPhase.Fleeing:
                    if (board.IntruderCaught)
                    {
                        EnterCustody();
                        break;
                    }

                    if (Time.time >= _fleeRecalcAt)
                    {
                        Vector3 away = intruder.position - transform.position;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.01f) away = Vector3.right;
                        Quaternion jitter = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f);
                        _fleePoint = transform.position + jitter * away.normalized * (roamRadius + 2f);
                        _fleePoint = ClampToRing(_fleePoint);
                        _fleeRecalcAt = Time.time + 0.7f;
                    }

                    bool visible = board.CanSeeIntruder;
                    if (!visible)
                    {
                        if (_lostSince < 0f)
                        {
                            _lostSince = Time.time;
                            _lastKnownPosition = intruder.position;
                        }

                        float dist = board.DistanceToIntruder;
                        if (dist >= escapeDistance && Time.time - _lostSince >= 1.5f)
                        {
                            board.ReportNoise(_lastKnownPosition);
                            _agent.ForceReplan();
                            SpawnNoisePing(_lastKnownPosition);
                            PickRoamTarget();
                            _phase = IntruderPhase.Skulking;
                            break;
                        }
                    }
                    else
                    {
                        _lostSince = -1f;
                    }

                    float tiredFlee = Mathf.Max(1.2f, fleeSpeed - Mathf.Max(0f, Time.time - _fleeStartedAt - 4f) * 0.5f);
                    StepIntruder(_fleePoint, tiredFlee);
                    break;

                case IntruderPhase.Custody:
                    if (Time.time >= _custodyUntil)
                    {
                        intruder.gameObject.SetActive(false);
                        _phase = IntruderPhase.Away;
                        _respawnAt = Time.time + Mathf.Max(0.5f, respawnDelay);
                    }
                    break;

                case IntruderPhase.Away:
                    if (Time.time >= _respawnAt)
                    {
                        board.IntruderCaught = false;
                        board.BackupCalled = false;
                        board.Alert = Alert.Calm;
                        board.ClearNoise();
                        PickRoamTarget();
                        intruder.position = _roamTarget;
                        intruder.gameObject.SetActive(true);
                        _phase = IntruderPhase.Skulking;
                    }
                    break;
            }

            SimulateAmbientNoise();
            SimulateNoisePing();
        }

        private void EnterCustody()
        {
            _phase = IntruderPhase.Custody;
            _custodyUntil = Time.time + Mathf.Max(0.5f, custodySeconds);
        }

        private void SimulateAmbientNoise()
        {
            if (intruder == null) return;

            var board = _agent.Board;
            if (board.CanSeeIntruder || _phase == IntruderPhase.Fleeing)
            {
                _noiseClock = 0f;
                return;
            }

            _noiseClock += Time.deltaTime;
            if (_noiseClock < noiseInterval) return;

            _noiseClock = Random.Range(noiseInterval * 0.6f, noiseInterval * 1.4f);

            Vector3 pos = RandomRingPoint();
            if (Vector3.Distance(pos, transform.position) < 3f) return;

            _agent.Board.ReportNoise(pos);
            _agent.ForceReplan();
            SpawnNoisePing(pos);
        }

        private void SpawnNoisePing(Vector3 position)
        {
            if (_noisePing == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "NoisePing";
                var collider = go.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);

                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(1f, 0.9f, 0.3f, 1f);
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                }

                _noisePing = go.transform;
            }

            _noisePing.position = new Vector3(position.x, 0.4f, position.z);
            _noisePing.gameObject.SetActive(true);
            _pingUntil = Time.time + 1.5f;
        }

        private void SimulateNoisePing()
        {
            if (_noisePing == null || !_noisePing.gameObject.activeSelf) return;

            float left = _pingUntil - Time.time;
            if (left <= 0f)
            {
                _noisePing.gameObject.SetActive(false);
                return;
            }

            float t = 1f - Mathf.Clamp01(left / 1.5f);
            _noisePing.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, t);
        }

        private void PickRoamTarget()
        {
            _roamTarget = RandomRingPoint();
            _pauseAt = Time.time + Random.Range(0.5f, 1.8f);
        }

        private Vector3 RandomRingPoint()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(roamRadius * 0.45f, roamRadius);
            return new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);
        }

        private Vector3 ClampToRing(Vector3 point)
        {
            Vector3 flat = new Vector3(point.x, 0.5f, point.z);
            float length = flat.magnitude;
            float limit = roamRadius * 0.95f;

            if (length > limit && length > 0.001f) flat *= limit / length;
            return flat;
        }

        private bool StepIntruder(Vector3 target, float speed)
        {
            Vector3 flat = new Vector3(target.x, intruder.position.y, target.z);
            Vector3 delta = flat - intruder.position;

            if (delta.sqrMagnitude <= arriveDistance * arriveDistance) return true;

            Vector3 direction = delta.normalized;
            Vector3 movement = MoveSafely(intruder, direction, speed * Time.deltaTime);
            if (movement.sqrMagnitude <= 0.0001f) return false;

            intruder.forward = Vector3.Slerp(intruder.forward, movement.normalized, 8f * Time.deltaTime);
            return false;
        }

        private void Act()
        {
            switch (_command.Kind)
            {
                case GuardCommandKind.MoveTo:
                    if (StepToward(_command.Target, walkSpeed)) Complete(true);
                    break;

                case GuardCommandKind.Sprint:
                    if (StepToward(_command.Target, sprintSpeed)) Complete(true);
                    break;

                case GuardCommandKind.LookAround:
                    _lookTimer += Time.deltaTime;
                    transform.Rotate(0f, 90f * Time.deltaTime, 0f);
                    if (_lookTimer >= Mathf.Max(0.1f, _command.Duration))
                    {
                        _lookTimer = 0f;
                        Complete(true);
                    }
                    break;

                case GuardCommandKind.Interact:
                    Complete(true);
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

            Vector3 direction = delta.normalized;
            Vector3 movement = MoveSafely(transform, direction, speed * Time.deltaTime);
            if (movement.sqrMagnitude <= 0.0001f) return false;

            transform.forward = Vector3.Slerp(transform.forward, movement.normalized, 8f * Time.deltaTime);
            return false;
        }

        private Vector3 MoveSafely(Transform moving, Vector3 direction, float distance)
        {
            var controller = moving.GetComponent<CharacterController>();
            if (controller != null)
            {
                Vector3 before = moving.position;
                controller.Move(direction * distance);
                return moving.position - before;
            }

            Vector3 movement = ResolveMovement(moving, direction, distance);
            moving.position += movement;
            return movement;
        }

        private Vector3 ResolveMovement(Transform moving, Vector3 direction, float distance)
        {
            Collider collider = moving.GetComponent<Collider>();
            float radius = collider != null ? Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z) * 0.9f : 0.35f;
            RaycastHit[] hits = Physics.SphereCastAll(
                moving.position,
                Mathf.Max(0.05f, radius),
                direction,
                distance + 0.05f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.transform == moving || hit.transform == transform || hit.transform == intruder)
                    continue;

                Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                if (slide.sqrMagnitude <= 0.0001f) return Vector3.zero;

                Vector3 slideDirection = slide.normalized;
                RaycastHit[] slideHits = Physics.SphereCastAll(
                    moving.position,
                    Mathf.Max(0.05f, radius),
                    slideDirection,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                foreach (var slideHit in slideHits)
                {
                    if (slideHit.transform != moving && slideHit.transform != transform && slideHit.transform != intruder)
                        return Vector3.zero;
                }

                return slideDirection * distance;
            }

            return direction * distance;
        }

        private void Complete(bool success)
        {
            _lookTimer = 0f;
            _agent.NotifyActionComplete(success);
        }

        private void CreateStatusLabel()
        {
            var label = new GameObject("Status");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 3.3f, 0f);

            _status = label.AddComponent<TextMeshPro>();
            _status.font = TMP_Settings.defaultFontAsset;
            _status.fontSize = 30;
            _status.transform.localScale = Vector3.one * 0.075f;
            _status.alignment = TextAlignmentOptions.Center;
            _status.overflowMode = TextOverflowModes.Overflow;
            _status.rectTransform.sizeDelta = new Vector2(40f, 12f);
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

        private void CreateIntruderLabel()
        {
            var label = new GameObject("Mood");
            label.transform.SetParent(intruder, false);
            label.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            label.transform.localScale = Vector3.one * (0.07f / Mathf.Max(0.01f, intruder.localScale.x));
            _intruderLabel = label.AddComponent<TextMeshPro>();
            _intruderLabel.font = TMP_Settings.defaultFontAsset;
            _intruderLabel.fontSize = 28;
            _intruderLabel.alignment = TextAlignmentOptions.Center;
            _intruderLabel.overflowMode = TextOverflowModes.Overflow;
            _intruderLabel.rectTransform.sizeDelta = new Vector2(24f, 5f);
#if UNITY_6000_0_OR_NEWER
            _intruderLabel.textWrappingMode = TextWrappingModes.NoWrap;
#else
            _intruderLabel.enableWordWrapping = false;
#endif
            _intruderLabel.color = new Color(1f, 0.55f, 0.5f);
            _intruderLabel.fontStyle = FontStyles.Bold;
            _intruderLabel.outlineWidth = 0.12f;
            _intruderLabel.outlineColor = Color.black;
        }

        private void UpdateStatusLabel()
        {
            if (_status == null) return;

            var board = _agent.Board;
            var goal = _agent.CurrentGoal;
            var step = _agent.CurrentAction;
            string goalName = goal != null ? goal.NameOf() : "idle";
            string stepName = step != null ? step.NameOf() : "-";

            string text = $"goal: {goalName}\nstep:  {stepName}\nalert: {board.Alert}  round: {board.WaypointsVisited}/{GuardBlackboard.PatrolRoundPoints}\nintruder: {DescribeIntruder()}";
            if (board.BackupCalled) text += "\n>> backup called <<";
            if (text == _statusText) return;

            _statusText = text;
            _status.text = text;
        }

        private void UpdateIntruderLabel()
        {
            if (_intruderLabel == null) return;

            string text = _phase switch
            {
                IntruderPhase.Skulking => "skulking",
                IntruderPhase.Fleeing => "fleeing!",
                IntruderPhase.Custody => "in custody",
                _ => string.Empty,
            };

            if (text == _intruderLabelText) return;

            _intruderLabelText = text;
            _intruderLabel.text = text;
        }

        private string DescribeIntruder() => _phase switch
        {
            IntruderPhase.Skulking => "skulking around",
            IntruderPhase.Fleeing => "fleeing!",
            IntruderPhase.Custody => "in custody",
            _ => "away",
        };

        public void ReportNoise(Vector3 position)
        {
            _agent?.Board.ReportNoise(position);
            _agent?.ForceReplan();
            SpawnNoisePing(position);
        }

        [ContextMenu("Report random noise")]
        private void MenuReportNoise() => ReportNoise(RandomRingPoint());

        [ContextMenu("Explain last decision")]
        private void ExplainLastDecision() => Debug.Log(_agent?.ExplainDecision());

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, sightRange);

            if (waypoints == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireCube(waypoints[i].position, Vector3.one * 0.4f);

                var next = waypoints[(i + 1) % waypoints.Count];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
