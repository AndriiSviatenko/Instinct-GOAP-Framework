using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Instinct.GOAP.EditorTools
{
    public sealed class GoapGraphWindow : EditorWindow
    {
        private const string StyleSheetName = "GoapGraph";
        private const string SourcePrefKey = "Instinct.GOAP.GraphWindow.Source";

        [MenuItem("Window/Analysis/GOAP Graph")]
        [MenuItem("GOAP/Graph Window")]
        public static void Open()
        {
            var w = GetWindow<GoapGraphWindow>(false, "GOAP Graph", true);
            w.titleContent = new GUIContent("GOAP Graph");
            w.minSize = new Vector2(900, 480);
            w.Show();
            w.Focus();
            w.Repaint();
        }

        private GoapGraphView _graph;
        private Label _status;
        private Label _counts;
        private VisualElement _inspector;
        private Label _inspectorTitle;
        private Label _inspectorBody;
        private ToolbarMenu _domainMenu;
        private IGoapGraphSource _source;

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("goap-window");
            rootVisualElement.EnableInClassList("goap-dark", EditorGUIUtility.isProSkin);
            rootVisualElement.EnableInClassList("goap-light", !EditorGUIUtility.isProSkin);

            var sheet = LoadStyleSheet();
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);

            _source = PickRememberedSource(GoapGraphSources.All);

            rootVisualElement.Add(BuildToolbar());

            var body = new VisualElement();
            body.AddToClassList("goap-body");

            _graph = new GoapGraphView();
            _graph.AddToClassList("goap-graph");
            _graph.SelectionChanged += ShowInInspector;
            body.Add(_graph);
            body.Add(BuildInspector());

            rootVisualElement.Add(body);
            rootVisualElement.Add(BuildStatusBar());

            Rebuild();
        }

        private static StyleSheet LoadStyleSheet()
        {
            foreach (var guid in AssetDatabase.FindAssets(StyleSheetName + " t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + StyleSheetName + ".uss", System.StringComparison.Ordinal))
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return null;
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("goap-toolbar");

            _domainMenu = new ToolbarMenu { text = _source?.DisplayName ?? "No domain" };
            _domainMenu.AddToClassList("goap-toolbar__domain");
            RebuildDomainMenu();
            toolbar.Add(_domainMenu);

            toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(() => _graph?.FrameAll()) { text = "Frame All" });

            var search = new ToolbarSearchField();
            search.AddToClassList("goap-toolbar__search");
            search.RegisterValueChangedCallback(evt => _graph?.SetSearch(evt.newValue));
            toolbar.Add(search);

            toolbar.Add(BuildViewMenu());
            toolbar.Add(BuildLayoutMenu());

            var spacer = new VisualElement();
            spacer.AddToClassList("goap-toolbar__spacer");
            toolbar.Add(spacer);

            _counts = new Label();
            _counts.AddToClassList("goap-toolbar__counts");
            toolbar.Add(_counts);

            return toolbar;
        }

        private void RebuildDomainMenu()
        {
            var sources = GoapGraphSources.All;
            _domainMenu.menu.MenuItems().Clear();

            foreach (var s in sources)
            {
                var captured = s;
                _domainMenu.menu.AppendAction(captured.DisplayName, _ =>
                {
                    _source = captured;
                    _domainMenu.text = captured.DisplayName;
                    EditorPrefs.SetString(SourcePrefKey, captured.DisplayName);
                    Rebuild();
                }, _ => _source == captured ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }

            if (sources.Count == 0)
                _domainMenu.menu.AppendAction("(no IGoapGraphSource found)", null, _ => DropdownMenuAction.Status.Disabled);
        }

        private ToolbarMenu BuildViewMenu()
        {
            var menu = new ToolbarMenu { text = "View" };

            void Toggle(string label, System.Func<bool> get, System.Action<bool> set, bool rebuild)
            {
                menu.menu.AppendAction(label, _ =>
                {
                    set(!get());
                    if (rebuild) Rebuild(); else ApplyLightSettings();
                }, _ => get() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }

            Toggle("Facts column", () => GoapGraphSettings.ShowFacts, v => GoapGraphSettings.ShowFacts = v, true);
            Toggle("Hide unused facts", () => GoapGraphSettings.HideOrphanFacts, v => GoapGraphSettings.HideOrphanFacts = v, true);
            menu.menu.AppendSeparator();
            Toggle("Edges: reads", () => GoapGraphSettings.ShowPreconditionEdges, v => GoapGraphSettings.ShowPreconditionEdges = v, true);
            Toggle("Edges: writes", () => GoapGraphSettings.ShowEffectEdges, v => GoapGraphSettings.ShowEffectEdges = v, true);
            Toggle("Edges: satisfies", () => GoapGraphSettings.ShowSatisfiesEdges, v => GoapGraphSettings.ShowSatisfiesEdges = v, true);
            menu.menu.AppendSeparator();
            Toggle("Live highlight", () => GoapGraphSettings.LiveHighlight, v => GoapGraphSettings.LiveHighlight = v, false);
            Toggle("Dim off-plan nodes", () => GoapGraphSettings.DimInactive, v => GoapGraphSettings.DimInactive = v, false);
            Toggle("Hover focus", () => GoapGraphSettings.HoverFocus, v => GoapGraphSettings.HoverFocus = v, false);
            menu.menu.AppendSeparator();
            Toggle("Mini-map", () => GoapGraphSettings.ShowMiniMap, v => GoapGraphSettings.ShowMiniMap = v, false);
            Toggle("Inspector", () => GoapGraphSettings.ShowInspector, v => GoapGraphSettings.ShowInspector = v, false);

            return menu;
        }

        private ToolbarMenu BuildLayoutMenu()
        {
            var menu = new ToolbarMenu { text = "Layout" };

            menu.menu.AppendAction("Save positions", _ => { _graph?.SaveLayout(); UpdateStatus(); });
            menu.menu.AppendAction("Restore on open",
                _ => { GoapGraphSettings.RestoreLayout = !GoapGraphSettings.RestoreLayout; },
                _ => GoapGraphSettings.RestoreLayout ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Reset to auto-arrange", _ => { _graph?.ResetLayout(_source); UpdateStatus(); });

            return menu;
        }

        private VisualElement BuildInspector()
        {
            _inspector = new VisualElement();
            _inspector.AddToClassList("goap-inspector");

            _inspectorTitle = new Label("Nothing selected");
            _inspectorTitle.AddToClassList("goap-inspector__title");
            _inspector.Add(_inspectorTitle);

            var scroll = new ScrollView();
            _inspectorBody = new Label("Click a node to see its preconditions, effects and traits.");
            _inspectorBody.AddToClassList("goap-inspector__body");
            _inspectorBody.selection.isSelectable = true;
            scroll.Add(_inspectorBody);
            _inspector.Add(scroll);

            return _inspector;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("goap-statusbar");
            _status = new Label();
            _status.AddToClassList("goap-statusbar__text");
            bar.Add(_status);
            return bar;
        }

        private static IGoapGraphSource PickRememberedSource(IReadOnlyList<IGoapGraphSource> sources)
        {
            if (sources.Count == 0) return null;
            var remembered = EditorPrefs.GetString(SourcePrefKey, null);
            return sources.FirstOrDefault(s => s.DisplayName == remembered) ?? sources[0];
        }

        private void OnEnable() => EditorApplication.update += Tick;

        private void OnDisable()
        {
            EditorApplication.update -= Tick;

            _graph?.SaveLayout();
        }

        private void Refresh()
        {
            GoapGraphSources.Invalidate();
            _source = PickRememberedSource(GoapGraphSources.All);
            RebuildDomainMenu();
            if (_domainMenu != null) _domainMenu.text = _source?.DisplayName ?? "No domain";
            Rebuild();
        }

        private void Rebuild()
        {
            _graph?.Rebuild(_source);
            ApplyLightSettings();
            ShowInInspector(null);
            UpdateCounts();
        }

        private void ApplyLightSettings()
        {
            _graph?.RefreshMiniMap();
            if (_inspector != null)
                _inspector.style.display = GoapGraphSettings.ShowInspector ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateCounts()
        {
            if (_counts == null) return;
            _counts.text = _source == null
                ? string.Empty
                : $"{_source.Actions.Count} actions   {_source.Goals.Count} goals";
        }

        private void ShowInInspector(GoapNode node)
        {
            if (_inspectorTitle == null) return;

            if (node == null)
            {
                _inspectorTitle.text = "Nothing selected";
                _inspectorBody.text = "Click a node to see its preconditions, effects and traits.";
                return;
            }

            _inspectorTitle.text = node.title;
            _inspectorBody.text = node.Details;
        }

        private void Tick()
        {
            if (_graph == null || _status == null) return;

            if (_source == null)
            {
                UpdateStatus("No IGoapGraphSource in this project - implement one to see a domain here.", live: false);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                UpdateStatus($"Not playing - static structure of {_source.DisplayName}", live: false);
                _graph.ClearLiveHighlight();
                return;
            }

            var agent = _source.FindLiveAgent();
            if (agent == null)
            {
                UpdateStatus($"Playing - no live {_source.DisplayName} agent in the scene", live: false);
                _graph.ClearLiveHighlight();
                return;
            }

            UpdateStatus($"{agent.CurrentGoal.NameOf()}   →   {GoapExplain.Chain(agent.CurrentPlan)}", live: true);
            _graph.ApplyLiveHighlight(agent);
        }

        internal void SaveLayoutFromShortcut()
        {
            _graph?.SaveLayout();
            UpdateStatus();
        }

        internal void ResetLayoutFromShortcut()
        {
            _graph?.ResetLayout(_source);
            UpdateStatus();
        }

        private void UpdateStatus(string text = null, bool live = false)
        {
            if (_status == null) return;
            if (text != null)
            {
                _status.text = text;
                _status.EnableInClassList("goap-statusbar__text--live", live);
                return;
            }

            _status.text = _graph != null && _graph.HasSavedLayout
                ? "Layout saved for this domain."
                : "Layout reset to auto-arrange.";
        }
    }
}
