using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Instinct.GOAP.EditorTools
{
    public sealed class GoapGraphView : GraphView
    {
        private static readonly Color PreconditionEdge = new Color(0.35f, 0.65f, 0.95f);
        private static readonly Color EffectEdge = new Color(0.95f, 0.55f, 0.25f);
        private static readonly Color SatisfiesEdge = new Color(0.55f, 0.90f, 0.45f);

        private readonly Dictionary<GoalKey, GoapGoalNode> _goalNodes = new Dictionary<GoalKey, GoapGoalNode>();
        private readonly Dictionary<ActionKey, GoapActionNode> _actionNodes = new Dictionary<ActionKey, GoapActionNode>();
        private readonly Dictionary<int, GoapFactNode> _factNodes = new Dictionary<int, GoapFactNode>();
        private readonly List<GoapNode> _allNodes = new List<GoapNode>();

        private readonly Dictionary<GoapNode, HashSet<GoapNode>> _neighbours = new Dictionary<GoapNode, HashSet<GoapNode>>();

        private MiniMap _miniMap;
        private string _domain = "default";
        private string _search = string.Empty;
        private GoapNode _hovered;
        private readonly HashSet<GoapNode> _planNodes = new HashSet<GoapNode>();
        private bool _hasLiveHighlight;

        public event Action<GoapNode> SelectionChanged;

        public GoapGraphView()
        {
            Insert(0, new GridBackground());
            SetupZoom(0.15f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            viewTransformChanged += _ => ApplyZoomLod();
            RegisterCallback<GeometryChangedEvent>(_ => ApplyZoomLod());
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) => new List<Port>();

        public void Rebuild(IGoapGraphSource source)
        {
            SaveLayout();

            DeleteElements(graphElements.ToList());
            _goalNodes.Clear();
            _actionNodes.Clear();
            _factNodes.Clear();
            _allNodes.Clear();
            _neighbours.Clear();
            _planNodes.Clear();
            _hovered = null;
            _hasLiveHighlight = false;

            if (source == null) { RefreshMiniMap(); return; }
            _domain = source.DisplayName ?? "default";

            BuildActions(source);
            BuildGoals(source);
            PruneOrphanFacts();
            LayoutNodes();
            RefreshMiniMap();
            RefreshEmphasis();

            schedule.Execute(() => FrameAll()).ExecuteLater(16);
        }

        private void BuildActions(IGoapGraphSource source)
        {
            foreach (var action in source.Actions)
            {
                var node = new GoapActionNode(action);
                Mount(node);
                _actionNodes[action.Key] = node;

                foreach (var cond in action.Preconditions)
                {
                    if (cond.Subject == null) continue;
                    var fact = FactFor(cond.Subject);
                    if (fact == null) continue;
                    fact.CountLink();
                    Link(node, fact);
                    if (GoapGraphSettings.ShowPreconditionEdges)
                        Connect(node.PreconditionPort, fact.InPort, PreconditionEdge, "goap-edge--reads");
                }

                foreach (var effect in action.Effects)
                {
                    if (effect.Subject == null) continue;
                    var fact = FactFor(effect.Subject);
                    if (fact == null) continue;
                    fact.CountLink();
                    Link(node, fact);
                    if (GoapGraphSettings.ShowEffectEdges)
                        Connect(node.EffectPort, fact.InPort, EffectEdge, "goap-edge--writes");
                }
            }
        }

        private void BuildGoals(IGoapGraphSource source)
        {
            foreach (var goal in source.Goals)
            {
                var node = new GoapGoalNode(goal, source.BadgesFor(goal));
                Mount(node);
                _goalNodes[goal.Key] = node;

                if (goal is not IInspectableGoal inspectable) continue;
                foreach (var cond in inspectable.Conditions)
                {
                    if (cond.Subject == null) continue;
                    var fact = FactFor(cond.Subject);
                    if (fact == null) continue;
                    fact.CountLink();
                    Link(node, fact);
                    if (GoapGraphSettings.ShowSatisfiesEdges)
                        Connect(fact.OutPort, node.InPort, SatisfiesEdge, "goap-edge--satisfies");
                }
            }
        }

        private GoapFactNode FactFor(IFact fact)
        {
            if (!GoapGraphSettings.ShowFacts) return null;
            if (_factNodes.TryGetValue(fact.Id, out var existing)) return existing;

            var node = new GoapFactNode(fact);
            Mount(node);
            _factNodes[fact.Id] = node;
            return node;
        }

        private void PruneOrphanFacts()
        {
            if (!GoapGraphSettings.HideOrphanFacts) return;

            foreach (var fact in _factNodes.Values.Where(f => f.LinkCount == 0).ToList())
            {
                _factNodes.Remove(fact.Fact.Id);
                _allNodes.Remove(fact);
                _neighbours.Remove(fact);
                RemoveElement(fact);
            }
        }

        private void Mount(GoapNode node)
        {
            AddElement(node);
            _allNodes.Add(node);
            _neighbours[node] = new HashSet<GoapNode>();

            node.RegisterCallback<MouseEnterEvent>(_ => { _hovered = node; RefreshEmphasis(); });
            node.RegisterCallback<MouseLeaveEvent>(_ => { if (_hovered == node) { _hovered = null; RefreshEmphasis(); } });
            node.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) SelectionChanged?.Invoke(node);
            });
        }

        private void Link(GoapNode a, GoapNode b)
        {
            _neighbours[a].Add(b);
            _neighbours[b].Add(a);
        }

        private void Connect(Port from, Port to, Color tint, string ussClass)
        {
            var edge = from.ConnectTo(to);
            edge.edgeControl.inputColor = tint;
            edge.edgeControl.outputColor = tint;
            edge.AddToClassList("goap-edge");
            edge.AddToClassList(ussClass);
            AddElement(edge);
        }

        private void LayoutNodes()
        {
            var saved = GoapGraphSettings.RestoreLayout
                ? GoapLayoutStore.Load(_domain)
                : new Dictionary<string, Vector2>();

            float gapX = GoapGraphSettings.ColumnGap;
            float gapY = GoapGraphSettings.RowGap;
            float actionY = 20f, factY = 20f, goalY = 20f;

            foreach (var node in _allNodes)
            {
                if (saved.TryGetValue(node.LayoutKey, out var stored))
                {
                    node.SetPosition(new Rect(stored.x, stored.y, 220, 0));
                    continue;
                }

                switch (node)
                {
                    case GoapActionNode:
                        node.SetPosition(new Rect(40f, actionY, 220, 0));
                        actionY += gapY;
                        break;
                    case GoapFactNode:
                        node.SetPosition(new Rect(40f + gapX, factY, 180, 0));
                        factY += gapY * 0.5f;
                        break;
                    case GoapGoalNode:
                        node.SetPosition(new Rect(40f + gapX * 2f, goalY, 240, 0));
                        goalY += gapY;
                        break;
                }
            }
        }

        public void SaveLayout()
        {
            if (_allNodes.Count == 0) return;
            var positions = new Dictionary<string, Vector2>(_allNodes.Count);
            foreach (var node in _allNodes)
            {
                var rect = node.GetPosition();
                if (float.IsNaN(rect.x) || float.IsNaN(rect.y)) continue;
                positions[node.LayoutKey] = new Vector2(rect.x, rect.y);
            }
            GoapLayoutStore.Save(_domain, positions);
        }

        public bool HasSavedLayout => GoapLayoutStore.Has(_domain);

        public void ResetLayout(IGoapGraphSource source)
        {
            GoapLayoutStore.Delete(_domain);
            bool restore = GoapGraphSettings.RestoreLayout;
            GoapGraphSettings.RestoreLayout = false;
            try { Rebuild(source); }
            finally { GoapGraphSettings.RestoreLayout = restore; }
        }

        public void SetSearch(string term)
        {
            _search = term ?? string.Empty;
            RefreshEmphasis();
        }

        public void ClearLiveHighlight()
        {
            if (!_hasLiveHighlight) return;
            _hasLiveHighlight = false;
            _planNodes.Clear();
            foreach (var node in _allNodes) node.SetActive(false);
            RefreshEmphasis();
        }

        public void ApplyLiveHighlight(IGoapAgentView agent)
        {
            if (agent == null || !GoapGraphSettings.LiveHighlight) { ClearLiveHighlight(); return; }

            _hasLiveHighlight = true;
            _planNodes.Clear();

            var currentGoal = agent.CurrentGoal;
            foreach (var kv in _goalNodes)
            {
                bool active = currentGoal != null && kv.Key == currentGoal.Key;
                kv.Value.SetActive(active);
                if (active) _planNodes.Add(kv.Value);
            }

            var plan = agent.CurrentPlan;
            foreach (var kv in _actionNodes)
            {
                int step = -1;
                if (plan != null)
                    for (int i = 0; i < plan.Actions.Count; i++)
                        if (plan.Actions[i].Key == kv.Key) { step = i; break; }

                kv.Value.SetActive(step >= 0, step);
                if (step >= 0) _planNodes.Add(kv.Value);
            }

            RefreshEmphasis();
        }

        private void RefreshEmphasis()
        {
            bool searching = !string.IsNullOrEmpty(_search);
            bool hovering = GoapGraphSettings.HoverFocus && _hovered != null;
            bool livePlan = _hasLiveHighlight && GoapGraphSettings.DimInactive && _planNodes.Count > 0;

            HashSet<GoapNode> hoverSet = null;
            if (hovering)
            {
                hoverSet = new HashSet<GoapNode> { _hovered };
                foreach (var n in _neighbours[_hovered]) hoverSet.Add(n);
            }

            foreach (var node in _allNodes)
            {
                bool matches = searching && node.title.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
                node.SetHighlighted(matches);

                bool dim = false;
                if (searching && !matches) dim = true;
                if (hovering && !hoverSet.Contains(node)) dim = true;
                if (livePlan && !_planNodes.Contains(node) && node is not GoapFactNode) dim = true;

                node.SetDimmed(dim);
            }
        }

        private void ApplyZoomLod()
        {
            float scale = viewTransform.scale.x;
            EnableInClassList("goap-zoom-mid", scale < 0.65f && scale >= 0.4f);
            EnableInClassList("goap-zoom-far", scale < 0.4f);
        }

        public void RefreshMiniMap()
        {
            if (GoapGraphSettings.ShowMiniMap)
            {
                if (_miniMap != null) return;
                _miniMap = new MiniMap { anchored = true };
                _miniMap.AddToClassList("goap-minimap");
                _miniMap.SetPosition(new Rect(12, 12, 210, 150));
                Add(_miniMap);
            }
            else if (_miniMap != null)
            {
                Remove(_miniMap);
                _miniMap = null;
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Frame All", _ => FrameAll());
            evt.menu.AppendAction("Frame Selection", _ => FrameSelection(),
                _ => selection.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Save Layout", _ => SaveLayout());
        }
    }
}
