using System.Collections.Generic;
using Instinct.GOAP;
using Instinct.GOAP.Unity;
using TMPro;
using UnityEngine;

namespace Instinct.GOAP.Samples.Farmer
{

    public sealed class FarmerHost : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform home;
        [SerializeField] private Transform field;

        [Header("Movement")]
        [SerializeField] private float speed = 3f;
        [SerializeField] private float arriveDistance = 0.5f;

        [Header("Crops")]
        [SerializeField] private int startRipeCrops = 6;
        [SerializeField] private float cropRegrowSeconds = 5f;
        [SerializeField] private float cropRegrowJitter = 4f;

        [Header("Debug")]
        [SerializeField] private bool logDecisions;
        [SerializeField] private bool showStatusLabel = true;

        private sealed class Crop
        {
            public Transform Root;
            public bool Ripe;
            public float ReadyAt;
        }

        private FarmerContext _ctx;
        private GoapBrain<FarmerContext> _brain;
        private ActionKey _loggedAction;
        private readonly List<Crop> _crops = new List<Crop>();
        private int _lastRipe;
        private float _fieldTop = 0.3f;
        private TextMeshPro _status;
        private string _statusText;

        public GoapBrain<FarmerContext> Brain => _brain;
        public FarmerContext Context => _ctx;

        private void Awake()
        {
            _ctx = new FarmerContext
            {
                Self = transform,
                Home = home,
                Field = field,
                Energy = 100,
                Speed = speed,
                ArriveDistance = arriveDistance,
            };

            _brain = new GoapBrain<FarmerContext>(
                FarmerBrain.Build(),
                _ctx,
                new GoapPlanner(maxIterations: 100, maxDepth: 6),
                new FarmerPolicy());

            var issues = _brain.Domain.Describe();
            if (!string.IsNullOrEmpty(issues)) Debug.LogWarning($"[farmer] domain issues:\n{issues}");

            CollectCrops();
            if (showStatusLabel) CreateStatusLabel();
        }

        private void Update()
        {
            if (_brain == null) return;

            _brain.Tick();
            SimulateCrops();

            if (logDecisions) LogWhenActionChanges();
            UpdateStatusLabel();
        }

        private void LateUpdate()
        {
            if (_status == null) return;

            var cam = Camera.main;
            _status.transform.rotation = cam != null
                ? cam.transform.rotation
                : Quaternion.identity;
        }

        private void CollectCrops()
        {
            if (field == null || field.parent == null) return;

            var parent = field.parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith("Crop")) continue;

                _crops.Add(new Crop
                {
                    Root = child,
                    Ripe = _crops.Count < startRipeCrops,
                    ReadyAt = _crops.Count < startRipeCrops
                        ? 0f
                        : Time.time + cropRegrowSeconds + Random.Range(0f, cropRegrowJitter),
                });
            }

            _ctx.CropsRipe = CountRipe();
            _lastRipe = _ctx.CropsRipe;

            if (field != null)
                _fieldTop = field.position.y + field.lossyScale.y * 0.5f;
        }

        private int CountRipe()
        {
            int ripe = 0;
            foreach (var crop in _crops)
                if (crop.Ripe) ripe++;
            return ripe;
        }

        private void SimulateCrops()
        {
            if (_crops.Count == 0)
            {
                _ctx.CropsRipe = Mathf.Max(_ctx.CropsRipe, 3);
                return;
            }

            if (_ctx.CropsRipe < _lastRipe)
            {
                int knocked = _lastRipe - _ctx.CropsRipe;
                foreach (var crop in _crops)
                {
                    if (knocked <= 0) break;
                    if (!crop.Ripe) continue;

                    crop.Ripe = false;
                    crop.ReadyAt = Time.time + cropRegrowSeconds + Random.Range(0f, cropRegrowJitter);
                    knocked--;
                }
            }

            foreach (var crop in _crops)
            {
                if (!crop.Ripe && crop.Root != null && Time.time >= crop.ReadyAt)
                {
                    crop.Ripe = true;
                    _ctx.CropsRipe++;
                }

                if (crop.Root == null) continue;

                float targetHeight = crop.Ripe ? 0.9f : 0.18f;
                var scale = crop.Root.localScale;
                scale.y = Mathf.Lerp(scale.y, targetHeight, Time.deltaTime * 2.5f);
                crop.Root.localScale = scale;

                var pos = crop.Root.position;
                crop.Root.position = new Vector3(pos.x, _fieldTop + scale.y * 0.5f, pos.z);

                var renderer = crop.Root.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    Color to = crop.Ripe ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.2f, 0.5f, 0.22f);
                    renderer.sharedMaterial.color = Color.Lerp(renderer.sharedMaterial.color, to, Time.deltaTime * 2.5f);
                    renderer.sharedMaterial.SetColor("_BaseColor", renderer.sharedMaterial.color);
                }
            }

            _lastRipe = _ctx.CropsRipe;
        }

        private void LogWhenActionChanges()
        {
            var key = _brain.CurrentAction?.Key ?? default;
            if (key == _loggedAction) return;

            _loggedAction = key;
            Debug.Log($"[farmer] goal={_brain.CurrentGoal.NameOf()} plan={_brain.PlanChain()} step={_brain.CurrentAction.NameOf()} | {_ctx}");
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

        private void UpdateStatusLabel()
        {
            if (_status == null) return;

            var goal = _brain.CurrentGoal;
            var step = _brain.CurrentAction;
            string goalName = goal != null
                ? goal.NameOf()
                : _ctx.Energy >= 45 ? "crops ripening" : "idle";
            string stepName = step != null ? step.NameOf() : "-";
            string text = $"goal: {goalName}\nstep:  {stepName}\nenergy: {_ctx.Energy}  crops: {_ctx.CropsRipe} ripe / {_ctx.CropsGrown} in";
            if (text == _statusText) return;

            _statusText = text;
            _status.text = text;
        }

        [ContextMenu("Explain last decision")]
        private void ExplainLastDecision() => Debug.Log(_brain?.ExplainDecision());

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            if (home != null) Gizmos.DrawWireCube(home.position, Vector3.one * 0.6f);
            if (field != null) Gizmos.DrawWireCube(field.position, Vector3.one * 0.6f);
        }
    }
}
