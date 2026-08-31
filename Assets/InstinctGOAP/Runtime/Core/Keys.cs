using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Instinct.GOAP
{
    public interface IGoapKey
    {
        int Id { get; }
        string DebugName { get; }
    }

    public readonly struct GoalKey : IGoapKey, IEquatable<GoalKey>
    {
        public int Id { get; }
        public string DebugName { get; }

        private GoalKey(int id, string name)
        {
            Id = id;
            DebugName = name;
        }

        public bool IsNone => Id == 0;

        public static GoalKey Declare([CallerMemberName] string name = "")
            => new GoalKey(KeyIdProvider.NextGoal(), name);

        public static GoalKey Named(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            lock (KeyIdProvider.GoalsByName)
            {
                if (KeyIdProvider.GoalsByName.TryGetValue(name, out var existing)) return existing;
                var key = new GoalKey(KeyIdProvider.NextGoal(), name);
                KeyIdProvider.GoalsByName[name] = key;
                return key;
            }
        }

        public bool Equals(GoalKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GoalKey k && Id == k.Id;
        public override int GetHashCode() => Id;
        public override string ToString() => DebugName ?? (Id == 0 ? "-" : $"goal{Id}");

        public static bool operator ==(GoalKey a, GoalKey b) => a.Id == b.Id;
        public static bool operator !=(GoalKey a, GoalKey b) => a.Id != b.Id;
    }

    public readonly struct ActionKey : IGoapKey, IEquatable<ActionKey>
    {
        public int Id { get; }
        public string DebugName { get; }

        private ActionKey(int id, string name)
        {
            Id = id;
            DebugName = name;
        }

        public bool IsNone => Id == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActionKey Of<T>() where T : IAction => TypeKey<T>.Value;

        public static ActionKey Of(Type type)
        {
            if (type == null) return default;
            lock (KeyIdProvider.ActionsByType)
            {
                if (KeyIdProvider.ActionsByType.TryGetValue(type, out var existing)) return existing;
                var key = Named(type.Name);
                KeyIdProvider.ActionsByType[type] = key;
                return key;
            }
        }

        public static ActionKey Declare([CallerMemberName] string name = "")
            => new ActionKey(KeyIdProvider.NextAction(), name);

        public static ActionKey Named(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            lock (KeyIdProvider.ActionsByName)
            {
                if (KeyIdProvider.ActionsByName.TryGetValue(name, out var existing)) return existing;
                var key = new ActionKey(KeyIdProvider.NextAction(), name);
                KeyIdProvider.ActionsByName[name] = key;
                return key;
            }
        }

        public bool Equals(ActionKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ActionKey k && Id == k.Id;
        public override int GetHashCode() => Id;
        public override string ToString() => DebugName ?? (Id == 0 ? "-" : $"action{Id}");

        public static bool operator ==(ActionKey a, ActionKey b) => a.Id == b.Id;
        public static bool operator !=(ActionKey a, ActionKey b) => a.Id != b.Id;

        private static class TypeKey<T> where T : IAction
        {
            public static readonly ActionKey Value = Of(typeof(T));
        }
    }

    internal static class KeyIdProvider
    {
        private static int _nextGoal;
        private static int _nextAction;

        internal static readonly Dictionary<string, GoalKey> GoalsByName = new Dictionary<string, GoalKey>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, ActionKey> ActionsByName = new Dictionary<string, ActionKey>(StringComparer.Ordinal);
        internal static readonly Dictionary<Type, ActionKey> ActionsByType = new Dictionary<Type, ActionKey>();

        internal static int NextGoal() => Interlocked.Increment(ref _nextGoal);
        internal static int NextAction() => Interlocked.Increment(ref _nextAction);
    }

    public static class KeyRegistry
    {
        private static readonly Dictionary<Type, GoalKey[]> _goals = new Dictionary<Type, GoalKey[]>();
        private static readonly Dictionary<Type, ActionKey[]> _actions = new Dictionary<Type, ActionKey[]>();

        public static IReadOnlyList<GoalKey> GoalKeysIn(Type keysType)
        {
            lock (_goals)
            {
                if (_goals.TryGetValue(keysType, out var cached)) return cached;
                var found = Collect<GoalKey>(keysType);
                _goals[keysType] = found;
                return found;
            }
        }

        public static IReadOnlyList<ActionKey> ActionKeysIn(Type keysType)
        {
            lock (_actions)
            {
                if (_actions.TryGetValue(keysType, out var cached)) return cached;
                var found = Collect<ActionKey>(keysType);
                _actions[keysType] = found;
                return found;
            }
        }

        private static T[] Collect<T>(Type keysType)
        {
            if (keysType == null) throw new ArgumentNullException(nameof(keysType));

            RuntimeHelpers.RunClassConstructor(keysType.TypeHandle);

            var list = new List<T>();
            foreach (var field in keysType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType == typeof(T) && field.GetValue(null) is T key)
                    list.Add(key);

            return list.ToArray();
        }
    }

    public static class GoapNames
    {
        public const string None = "-";

        public static string NameOf(this IGoal goal) => goal == null ? None : goal.Key.ToString();
        public static string NameOf(this IAction action) => action == null ? None : action.Key.ToString();
    }
}
