using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Instinct.GOAP.EditorTools
{
    public interface IGoapGraphSource
    {
        string DisplayName { get; }

        IReadOnlyList<IAction> Actions { get; }
        IReadOnlyList<IGoal> Goals { get; }

        IGoapAgentView FindLiveAgent();

        IEnumerable<string> BadgesFor(IGoal goal);
    }

    public abstract class GoapGraphSource : IGoapGraphSource
    {
        public virtual string DisplayName => GetType().Name.Replace("GoapGraphSource", "");
        public abstract IReadOnlyList<IAction> Actions { get; }
        public abstract IReadOnlyList<IGoal> Goals { get; }
        public virtual IGoapAgentView FindLiveAgent() => null;
        public virtual IEnumerable<string> BadgesFor(IGoal goal) => Array.Empty<string>();
    }

    public static class GoapGraphSources
    {
        private static IGoapGraphSource[] _cached;

        public static IReadOnlyList<IGoapGraphSource> All => _cached ??= Discover();

        public static void Invalidate() => _cached = null;

        private static IGoapGraphSource[] Discover()
        {
            var found = new List<IGoapGraphSource>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch (Exception) { continue; }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IGoapGraphSource).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    try { found.Add((IGoapGraphSource)Activator.CreateInstance(type)); }
                    catch (Exception e) { UnityEngine.Debug.LogWarning($"[GOAP] graph source {type.Name} failed to load: {e.Message}"); }
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return found.ToArray();
        }
    }
}
