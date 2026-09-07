using UnityEditor;

namespace Instinct.GOAP.EditorTools
{
    public static class GoapGraphSettings
    {
        private const string Prefix = "Instinct.GOAP.GraphWindow.";

        private static bool GetBool(string key, bool fallback) => EditorPrefs.GetBool(Prefix + key, fallback);
        private static void SetBool(string key, bool value) => EditorPrefs.SetBool(Prefix + key, value);
        private static float GetFloat(string key, float fallback) => EditorPrefs.GetFloat(Prefix + key, fallback);

        public static bool ShowFacts
        {
            get => GetBool(nameof(ShowFacts), true);
            set => SetBool(nameof(ShowFacts), value);
        }

        public static bool ShowPreconditionEdges
        {
            get => GetBool(nameof(ShowPreconditionEdges), true);
            set => SetBool(nameof(ShowPreconditionEdges), value);
        }

        public static bool ShowEffectEdges
        {
            get => GetBool(nameof(ShowEffectEdges), true);
            set => SetBool(nameof(ShowEffectEdges), value);
        }

        public static bool ShowSatisfiesEdges
        {
            get => GetBool(nameof(ShowSatisfiesEdges), true);
            set => SetBool(nameof(ShowSatisfiesEdges), value);
        }

        public static bool HideOrphanFacts
        {
            get => GetBool(nameof(HideOrphanFacts), false);
            set => SetBool(nameof(HideOrphanFacts), value);
        }

        public static bool LiveHighlight
        {
            get => GetBool(nameof(LiveHighlight), true);
            set => SetBool(nameof(LiveHighlight), value);
        }

        public static bool DimInactive
        {
            get => GetBool(nameof(DimInactive), true);
            set => SetBool(nameof(DimInactive), value);
        }

        public static bool HoverFocus
        {
            get => GetBool(nameof(HoverFocus), true);
            set => SetBool(nameof(HoverFocus), value);
        }

        public static bool ShowMiniMap
        {
            get => GetBool(nameof(ShowMiniMap), true);
            set => SetBool(nameof(ShowMiniMap), value);
        }

        public static bool ShowInspector
        {
            get => GetBool(nameof(ShowInspector), true);
            set => SetBool(nameof(ShowInspector), value);
        }

        public static bool RestoreLayout
        {
            get => GetBool(nameof(RestoreLayout), true);
            set => SetBool(nameof(RestoreLayout), value);
        }

        public static float ColumnGap => GetFloat(nameof(ColumnGap), 420f);
        public static float RowGap => GetFloat(nameof(RowGap), 132f);
    }
}
