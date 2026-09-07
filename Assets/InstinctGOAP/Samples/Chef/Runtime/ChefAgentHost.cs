using Instinct.GOAP;
using TMPro;
using UnityEngine;

namespace Instinct.GOAP.Samples.Chef
{

    [AddComponentMenu("GOAP/Samples/Chef Agent Host")]
    public sealed class ChefAgentHost : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform stove;
        [SerializeField] private Transform storage;
        [SerializeField] private Transform client;
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform breakSpot;

        [Header("Movement")]
        [SerializeField] private float speed = 3f;
        [SerializeField] private float arriveDistance = 0.5f;

        [Header("Timings")]
        [SerializeField] private float cookTime = 1.5f;
        [SerializeField] private float serveTime = 1f;
        [SerializeField] private float breakTime = 2f;

        [Header("World")]
        [Tooltip("Наскільки голоднішим стає клієнт за секунду — без цього демо закінчується після першої страви")]
        [SerializeField] private float hungerPerSecond = 6f;
        [SerializeField] private int clientStartHunger = 30;
        [SerializeField] private float clientSpeed = 2f;
        [SerializeField] private float clientRespawnDelay = 4f;

        [Header("Debug")]
        [SerializeField] private bool logDecisions;
        [SerializeField] private bool showStatusLabel = true;

        private enum ClientPhase
        {
            Arriving,
            Waiting,
            Leaving,
            Away,
        }

        private ChefBlackboard _board;
        private ChefAgent _agent;
        private ChefCommand _command;
        private float _timer;
        private float _hungerAccum;
        private TextMeshPro _status;
        private string _statusText;
        private TextMeshPro _clientLabel;
        private string _clientLabelText;
        private ClientPhase _phase;
        private Vector3 _waitPosition;
        private float _respawnAt;

        public ChefAgent Agent => _agent;

        private void Awake()
        {
            _board = new ChefBlackboard
            {
                StovePosition = stove != null ? stove.position : transform.position,
                StoragePosition = storage != null ? storage.position : transform.position,
                BreakPosition = breakSpot != null ? breakSpot.position : transform.position,
                SelfPosition = transform.position,
                ClientHunger = clientStartHunger,
            };

            _agent = new ChefAgent(_board);

            var report = ChefValidator.ValidateDomain();
            if (!string.IsNullOrEmpty(report)) Debug.LogWarning($"[Chef] domain issues:\n{report}");

            if (showStatusLabel) CreateStatusLabel();

            if (client != null)
            {
                _waitPosition = client.position;
                _board.ClientPosition = _waitPosition;
                CreateClientLabel();
                StartClientVisit();
            }
        }

        private void Update()
        {
            if (_agent == null) return;

            _board.SelfPosition = transform.position;
            if (client != null && client.gameObject.activeInHierarchy)
                _board.ClientPosition = client.position;

            _board.ClientPresent = _phase == ClientPhase.Waiting;

            SimulateClient();

            _command = _agent.Tick();
            if (logDecisions) Debug.Log($"[Chef] {_agent.CurrentGoal.NameOf()} :: {_agent.PlanChain()} :: {_command}");

            Act();
            UpdateStatusLabel();
            UpdateClientLabel();
        }

        private void LateUpdate()
        {
            var cam = Camera.main;

            if (_status != null)
                _status.transform.rotation = cam != null
                    ? cam.transform.rotation
                    : Quaternion.identity;

            if (_clientLabel != null)
                _clientLabel.transform.rotation = cam != null
                    ? cam.transform.rotation
                    : Quaternion.identity;
        }

        private void SimulateClient()
        {
            if (client == null) return;

            switch (_phase)
            {
                case ClientPhase.Arriving:
                    if (StepFlat(client, _waitPosition, clientSpeed, arriveDistance)) _phase = ClientPhase.Waiting;
                    break;

                case ClientPhase.Waiting:
                    GrowHunger();
                    if (_board.ClientHunger <= 0) _phase = ClientPhase.Leaving;
                    break;

                case ClientPhase.Leaving:
                    bool atDoor = entrance == null
                        || StepFlat(client, entrance.position, clientSpeed, arriveDistance);
                    if (atDoor)
                    {
                        client.gameObject.SetActive(false);
                        _phase = ClientPhase.Away;
                        _respawnAt = Time.time + Mathf.Max(0.5f, clientRespawnDelay);
                    }
                    break;

                case ClientPhase.Away:
                    if (Time.time >= _respawnAt) StartClientVisit();
                    break;
            }
        }

        private void StartClientVisit()
        {
            client.position = entrance != null ? entrance.position : _waitPosition + Vector3.right * 4f;
            client.gameObject.SetActive(true);
            _board.ClientPosition = client.position;
            _board.ClientHunger = clientStartHunger;
            _hungerAccum = 0f;
            _phase = ClientPhase.Arriving;
        }

        private static bool StepFlat(Transform target, Vector3 destination, float moveSpeed, float stopDistance)
        {
            Vector3 flat = new Vector3(destination.x, target.position.y, destination.z);
            Vector3 delta = flat - target.position;

            if (delta.sqrMagnitude <= stopDistance * stopDistance) return true;

            target.position += delta.normalized * (moveSpeed * Time.deltaTime);
            return false;
        }

        private void Act()
        {
            switch (_command.Kind)
            {
                case ChefCommandKind.MoveTo:
                    if (StepToward(_command.Target)) Complete(true);
                    break;

                case ChefCommandKind.Cook:
                    _timer += Time.deltaTime;
                    transform.Rotate(0f, 120f * Time.deltaTime, 0f);
                    if (_timer >= Mathf.Max(0.1f, cookTime)) Complete(true);
                    break;

                case ChefCommandKind.Serve:
                    _timer += Time.deltaTime;
                    if (_timer >= Mathf.Max(0.1f, serveTime)) Complete(true);
                    break;

                case ChefCommandKind.TakeBreak:
                    _timer += Time.deltaTime;
                    if (_timer >= Mathf.Max(0.1f, breakTime)) Complete(true);
                    break;

                default:
                    if (_agent.CurrentAction != null) Complete(true);
                    break;
            }
        }

        private bool StepToward(Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
            Vector3 delta = flat - transform.position;

            if (delta.sqrMagnitude <= arriveDistance * arriveDistance) return true;

            transform.position += delta.normalized * (speed * Time.deltaTime);
            transform.forward = Vector3.Slerp(transform.forward, delta.normalized, 8f * Time.deltaTime);
            return false;
        }

        private void GrowHunger()
        {
            if (hungerPerSecond <= 0f) return;

            _hungerAccum += hungerPerSecond * Time.deltaTime;
            int whole = (int)_hungerAccum;
            if (whole > 0)
            {
                _hungerAccum -= whole;
                _board.ClientHunger = Mathf.Min(100, _board.ClientHunger + whole);
            }
        }

        private void Complete(bool success)
        {
            _timer = 0f;
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

        private void CreateClientLabel()
        {
            var label = new GameObject("Mood");
            label.transform.SetParent(client, false);
            label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            label.transform.localScale = Vector3.one * (0.07f / Mathf.Max(0.01f, client.localScale.x));

            _clientLabel = label.AddComponent<TextMeshPro>();
            _clientLabel.font = TMP_Settings.defaultFontAsset;
            _clientLabel.fontSize = 28;
            _clientLabel.alignment = TextAlignmentOptions.Center;
            _clientLabel.overflowMode = TextOverflowModes.Overflow;
            _clientLabel.rectTransform.sizeDelta = new Vector2(24f, 5f);
#if UNITY_6000_0_OR_NEWER
            _clientLabel.textWrappingMode = TextWrappingModes.NoWrap;
#else
            _clientLabel.enableWordWrapping = false;
#endif
            _clientLabel.color = new Color(1f, 0.9f, 0.6f);
            _clientLabel.fontStyle = FontStyles.Bold;
            _clientLabel.outlineWidth = 0.12f;
            _clientLabel.outlineColor = Color.black;
        }

        private void UpdateStatusLabel()
        {
            if (_status == null) return;

            var goal = _agent.CurrentGoal;
            var step = _agent.CurrentAction;
            string goalName = goal != null ? goal.NameOf() : "idle";
            string stepName = step != null ? step.NameOf() : "-";
            string text = $"goal: {goalName}\nstep:  {stepName}\nhunger: {_board.ClientHunger}  energy: {_board.Energy}\nclient: {DescribePhase()}";
            if (_board.MealReady) text += "\n>> meal ready <<";
            if (text == _statusText) return;

            _statusText = text;
            _status.text = text;
        }

        private void UpdateClientLabel()
        {
            if (_clientLabel == null) return;

            string text = _phase switch
            {
                ClientPhase.Arriving => "coming in",
                ClientPhase.Waiting => $"hungry: {_board.ClientHunger}",
                ClientPhase.Leaving => "thank you!",
                _ => string.Empty,
            };

            if (text == _clientLabelText) return;

            _clientLabelText = text;
            _clientLabel.text = text;
        }

        private string DescribePhase() => _phase switch
        {
            ClientPhase.Arriving => "arriving",
            ClientPhase.Waiting => "waiting",
            ClientPhase.Leaving => "leaving happy",
            _ => "away",
        };

        [ContextMenu("Explain last decision")]
        private void ExplainLastDecision() => Debug.Log(_agent?.ExplainDecision());

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            if (stove != null) Gizmos.DrawWireCube(stove.position, Vector3.one * 0.7f);

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            if (storage != null) Gizmos.DrawWireCube(storage.position, Vector3.one * 0.7f);

            Gizmos.color = new Color(0.3f, 0.85f, 0.75f, 0.8f);
            if (breakSpot != null) Gizmos.DrawWireCube(breakSpot.position, Vector3.one * 0.7f);

            Gizmos.color = new Color(0.35f, 0.85f, 0.4f, 0.8f);
            if (entrance != null) Gizmos.DrawWireCube(entrance.position, Vector3.one * 0.7f);

            Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.8f);
            if (client != null) Gizmos.DrawWireSphere(client.position, 0.6f);
        }
    }
}
