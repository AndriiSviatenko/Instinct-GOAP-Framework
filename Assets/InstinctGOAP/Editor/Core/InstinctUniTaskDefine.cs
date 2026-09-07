using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Instinct.GOAP.EditorTools
{
    /// <summary>
    /// Raises the INSTINCT_UNITASK scripting define for UniTask installs that asmdef
    /// versionDefines cannot see.
    ///
    /// A Package Manager install is already covered: every assembly that needs UniTask carries a
    /// versionDefine on com.cysharp.unitask, and this class then does nothing. The gap is the
    /// .unitypackage install that drops UniTask straight into Assets/ — there is no package, so no
    /// versionDefine ever fires, and the define has to come from Player Settings instead.
    ///
    /// The define is only ever added when UniTask is present without a package, and only ever
    /// removed once UniTask is gone entirely.
    /// </summary>
    [InitializeOnLoad]
    public static class InstinctUniTaskDefine
    {
        private const string Define = "INSTINCT_UNITASK";
        private const string UniTaskAssembly = "UniTask";
        private const int MaxRetries = 100;

#if INSTINCT_UNITASK
        private const bool FromPackage = true;
#else
        private const bool FromPackage = false;
#endif

        private static int _retries;

        static InstinctUniTaskDefine() => EditorApplication.delayCall += Sync;

        private static void Sync()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                // Bounded: a batchmode run can quit before the editor ever settles, and an
                // unbounded re-queue would spin for as long as it lives.
                if (_retries++ < MaxRetries) EditorApplication.delayCall += Sync;
                return;
            }

            NamedBuildTarget target;
            try
            {
                target = NamedBuildTarget.FromBuildTargetGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup);
            }
            catch (ArgumentException)
            {
                return;
            }

            var symbols = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            bool present = IsUniTaskPresent();
            bool defined = symbols.Contains(Define);

            if (present && !FromPackage && !defined) symbols.Add(Define);
            else if (!present && defined) symbols.Remove(Define);
            else return;

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            AssetDatabase.SaveAssets();
        }

        private static bool IsUniTaskPresent()
            => AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, UniTaskAssembly, StringComparison.Ordinal));
    }
}
