using System;
using System.Collections.Generic;
using System.Text;

namespace Instinct.GOAP
{
    public sealed class DomainBuilder
    {
        private readonly List<IAction> _actions = new();
        private readonly List<IGoal> _goals = new();
        private Type _goalKeysType;
        private Type _actionKeysType;

        public DomainBuilder AddAction(IAction action)
        {
            _actions.Add(action);
            return this;
        }

        public DomainBuilder AddActions(IEnumerable<IAction> actions)
        {
            foreach (var a in actions) _actions.Add(a);
            return this;
        }

        public DomainBuilder AddGoal(IGoal goal)
        {
            _goals.Add(goal);
            return this;
        }

        public DomainBuilder AddGoals(IEnumerable<IGoal> goals)
        {
            foreach (var g in goals) _goals.Add(g);
            return this;
        }

        public DomainBuilder DeclaredGoalsIn(Type goalKeysType)
        {
            _goalKeysType = goalKeysType;
            return this;
        }

        public DomainBuilder DeclaredActionsIn(Type actionKeysType)
        {
            _actionKeysType = actionKeysType;
            return this;
        }

        public IReadOnlyList<IAction> Actions => _actions;
        public IReadOnlyList<IGoal> Goals => _goals;

        public sealed class Issue
        {
            public enum Level { Warning, Error }

            public readonly Level Severity;
            public readonly string Subject;
            public readonly string Message;

            public Issue(Level severity, string subject, string message)
            {
                Severity = severity;
                Subject = subject;
                Message = message;
            }

            public override string ToString() => $"[{Severity}] {Subject}: {Message}";
        }

        public IReadOnlyList<Issue> Validate()
        {
            var issues = new List<Issue>();

            var seenActions = new HashSet<ActionKey>();
            foreach (var a in _actions)
                if (!seenActions.Add(a.Key))
                    issues.Add(new Issue(Issue.Level.Error, a.NameOf(),
                        "duplicate action key - two actions share one identity, so plan chains and " +
                        "any 'which action issued this?' check become ambiguous"));

            var seenGoals = new HashSet<GoalKey>();
            foreach (var g in _goals)
                if (!seenGoals.Add(g.Key))
                    issues.Add(new Issue(Issue.Level.Error, g.NameOf(), "duplicate goal key"));

            if (_goalKeysType != null)
                foreach (var key in KeyRegistry.GoalKeysIn(_goalKeysType))
                    if (!seenGoals.Contains(key))
                        issues.Add(new Issue(Issue.Level.Error, key.ToString(),
                            $"declared in {_goalKeysType.Name} but not in this domain - it can never be picked"));

            if (_actionKeysType != null)
                foreach (var key in KeyRegistry.ActionKeysIn(_actionKeysType))
                    if (!seenActions.Contains(key))
                        issues.Add(new Issue(Issue.Level.Error, key.ToString(),
                            $"declared in {_actionKeysType.Name} but not in this domain - nothing can ever run it"));

            var written = new HashSet<int>();
            bool anyOpaqueEffect = false;
            foreach (var a in _actions)
            {
                foreach (var e in a.Effects)
                {
                    if (e.Subject == null) { anyOpaqueEffect = true; continue; }
                    written.Add(e.Subject.Id);
                }
            }

            foreach (var a in _actions)
            {
                if (a.Effects.Count == 0)
                    issues.Add(new Issue(Issue.Level.Error, a.NameOf(),
                        "no effects - it can never advance a plan and only costs the planner an expansion"));
            }

            foreach (var g in _goals)
            {
                if (g is not IInspectableGoal ig) continue;

                if (ig.Conditions.Count == 0)
                {
                    issues.Add(new Issue(Issue.Level.Warning, g.NameOf(),
                        "no satisfy conditions - this goal is always satisfied and can never be planned for"));
                    continue;
                }

                bool anyTyped = false, anyWritable = false;
                foreach (var c in ig.Conditions)
                {
                    if (c.Subject == null) continue;
                    anyTyped = true;
                    if (written.Contains(c.Subject.Id)) anyWritable = true;
                }

                if (anyTyped && !anyWritable && !anyOpaqueEffect)
                    issues.Add(new Issue(Issue.Level.Warning, g.NameOf(),
                        "no action writes any fact this goal is satisfied by - it can only ever be " +
                        "satisfied by the world changing on its own"));
            }

            return issues;
        }

        public string Describe()
        {
            var issues = Validate();
            if (issues.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append(_actions.Count).Append(" actions, ").Append(_goals.Count).Append(" goals, ")
              .Append(issues.Count).AppendLine(" issue(s):");
            foreach (var i in issues) sb.Append("  ").AppendLine(i.ToString());
            return sb.ToString();
        }
    }
}
