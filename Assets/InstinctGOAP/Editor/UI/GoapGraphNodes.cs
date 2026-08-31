using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Instinct.GOAP.EditorTools
{
    public abstract class GoapNode : Node
    {
        public string LayoutKey { get; }

        public abstract string Details { get; }

        private readonly string _baseTitle;
        private readonly Label _badge;

        private Rect _storedPosition;
        private bool _hasStoredPosition;

        protected GoapNode(string layoutKey, string nodeTitle, string subtitle, string modifierClass, Texture icon)
        {
            LayoutKey = layoutKey;
            _baseTitle = nodeTitle;
            title = nodeTitle;

            AddToClassList("goap-node");
            AddToClassList(modifierClass);

            if (titleButtonContainer != null)
                titleButtonContainer.style.display = DisplayStyle.None;

            if (icon != null)
            {
                var image = new Image { name = "goap-node-icon", image = icon, scaleMode = ScaleMode.ScaleToFit };
                titleContainer.Insert(0, image);
            }

            _badge = new Label(string.Empty) { name = "goap-node-badge" };
            _badge.style.display = DisplayStyle.None;
            titleContainer.Add(_badge);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = new Label(subtitle) { name = "goap-node-subtitle", pickingMode = PickingMode.Ignore };
                topContainer.Insert(1, sub);
            }
        }

        protected void SetBadge(string text)
        {
            _badge.text = text ?? string.Empty;
            _badge.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected Port AddPort(Direction direction, string portName)
        {
            var port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(bool));
            port.portName = portName;
            port.AddToClassList(direction == Direction.Input ? "goap-port--in" : "goap-port--out");
            (direction == Direction.Input ? inputContainer : outputContainer).Add(port);
            return port;
        }

        protected void Finish()
        {
            RefreshExpandedState();
            RefreshPorts();
        }

        public void SetActive(bool active, int step = -1)
        {
            EnableInClassList("goap-node--active", active);
            title = active && step >= 0 ? $"{step + 1}. {_baseTitle}" : _baseTitle;
        }

        public void SetHighlighted(bool on) => EnableInClassList("goap-node--hl", on);
        public void SetDimmed(bool on) => EnableInClassList("goap-node--dim", on);

        public override void SetPosition(Rect newPos)
        {
            _storedPosition = newPos;
            _hasStoredPosition = true;
            base.SetPosition(newPos);
        }

        public override Rect GetPosition()
        {
            var live = base.GetPosition();
            if (!_hasStoredPosition) return live;

            float w = !float.IsNaN(live.width) && live.width > 1f ? live.width : 200f;
            float h = !float.IsNaN(live.height) && live.height > 1f ? live.height : 60f;
            bool livePositionResolved = !float.IsNaN(live.x) && (live.x != 0f || live.y != 0f);
            return livePositionResolved
                ? new Rect(live.x, live.y, w, h)
                : new Rect(_storedPosition.x, _storedPosition.y, w, h);
        }
    }

    public sealed class GoapFactNode : GoapNode
    {
        public Port InPort { get; }
        public Port OutPort { get; }
        public IFact Fact { get; }

        public int LinkCount { get; private set; }

        public GoapFactNode(IFact fact)
            : base("fact:" + fact.Id,
                   string.IsNullOrEmpty(fact.Name) ? $"fact#{fact.Id}" : fact.Name,
                   fact.ValueType?.Name ?? "?",
                   "goap-node--fact",
                   EditorGUIUtility.IconContent("d_Preset.Context").image)
        {
            Fact = fact;
            InPort = AddPort(Direction.Input, string.Empty);
            OutPort = AddPort(Direction.Output, string.Empty);
            tooltip = $"Fact: {fact.Name}\nType: {fact.ValueType?.Name}";
            Finish();
        }

        public void CountLink()
        {
            LinkCount++;
            SetBadge(LinkCount.ToString());
        }

        public override string Details =>
            $"FACT  {Fact.Name}\n\nType: {Fact.ValueType?.Name}\nRead/written by: {LinkCount} node(s)";
    }

    public sealed class GoapActionNode : GoapNode
    {
        public Port PreconditionPort { get; }
        public Port EffectPort { get; }
        public IAction Action { get; }

        public GoapActionNode(IAction action)
            : base("action:" + action.NameOf(),
                   action.NameOf(),
                   $"{action.Preconditions.Count} req · {action.Effects.Count} eff",
                   "goap-node--action",
                   EditorGUIUtility.IconContent("d_PlayButton").image)
        {
            Action = action;
            PreconditionPort = AddPort(Direction.Output, "reads");
            EffectPort = AddPort(Direction.Output, "writes");
            tooltip = Details;
            Finish();
        }

        public override string Details
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("ACTION  ").AppendLine(Action.NameOf());
                sb.AppendLine();

                sb.AppendLine($"Preconditions ({Action.Preconditions.Count})");
                if (Action.Preconditions.Count == 0) sb.AppendLine("   (always applicable)");
                foreach (var c in Action.Preconditions)
                    sb.Append("   • ").AppendLine(c.Description);

                sb.AppendLine();
                sb.AppendLine($"Effects ({Action.Effects.Count})");
                if (Action.Effects.Count == 0)
                    sb.AppendLine("   (none - this action can never advance a plan)");
                foreach (var e in Action.Effects)
                    sb.Append("   • ").Append(e.Description).AppendLine(e.IsConstant ? "" : "   [derived]");

                return sb.ToString();
            }
        }
    }

    public sealed class GoapGoalNode : GoapNode
    {
        public Port InPort { get; }
        public IGoal Goal { get; }

        private readonly IReadOnlyList<string> _badges;

        public GoapGoalNode(IGoal goal, IEnumerable<string> badges)
            : base("goal:" + goal.NameOf(),
                   goal.NameOf(),
                   BuildSubtitle(badges, out var kept),
                   "goap-node--goal",
                   EditorGUIUtility.IconContent("d_Favorite").image)
        {
            Goal = goal;
            _badges = kept;
            InPort = AddPort(Direction.Input, "satisfied by");
            tooltip = Details;
            Finish();
        }

        private static string BuildSubtitle(IEnumerable<string> badges, out IReadOnlyList<string> kept)
        {
            var list = badges?.Where(b => !string.IsNullOrEmpty(b)).ToList() ?? new List<string>();
            kept = list;
            return list.Count > 0 ? string.Join(" · ", list) : "—";
        }

        public override string Details
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("GOAL  ").AppendLine(Goal.NameOf());
                sb.AppendLine();

                if (_badges.Count > 0)
                {
                    sb.AppendLine("Traits");
                    foreach (var b in _badges) sb.Append("   • ").AppendLine(b);
                    sb.AppendLine();
                }

                if (Goal is IInspectableGoal inspectable)
                {
                    sb.AppendLine($"Satisfied when ({inspectable.Conditions.Count})");
                    if (inspectable.Conditions.Count == 0)
                        sb.AppendLine("   (nothing - this goal is always satisfied)");
                    foreach (var c in inspectable.Conditions)
                        sb.Append("   • ").AppendLine(c.Description);
                }
                else
                {
                    sb.AppendLine("Satisfy conditions are opaque (goal does not implement IInspectableGoal).");
                }

                return sb.ToString();
            }
        }
    }
}
