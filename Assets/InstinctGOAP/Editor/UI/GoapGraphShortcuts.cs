using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Instinct.GOAP.EditorTools
{
    internal static class GoapGraphShortcuts
    {
        private static bool CanHandleGraphKeys(out GoapGraphWindow window)
        {
            window = EditorWindow.focusedWindow as GoapGraphWindow;
            if (window == null) return false;

            var focused = window.rootVisualElement?.focusController?.focusedElement;
            return focused is not TextField;
        }

        [Shortcut("GOAP/Graph: Save Layout", typeof(GoapGraphWindow), KeyCode.S, ShortcutModifiers.Alt)]
        private static void SaveLayout(ShortcutArguments _)
        {
            if (CanHandleGraphKeys(out var window)) window.SaveLayoutFromShortcut();
        }

        [Shortcut("GOAP/Graph: Reset Layout", typeof(GoapGraphWindow), KeyCode.R, ShortcutModifiers.Alt)]
        private static void ResetLayout(ShortcutArguments _)
        {
            if (CanHandleGraphKeys(out var window)) window.ResetLayoutFromShortcut();
        }
    }
}
