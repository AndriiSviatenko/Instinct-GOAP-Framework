using System;
using System.Collections.Generic;

namespace Instinct.GOAP
{
    public sealed class GoapPlanner : IPlanner
    {
        private readonly int _maxIterations;
        private readonly int _maxDepth;
        private readonly Dictionary<WorldState, float> _bestByState = new();
        private readonly MinHeap _open = new();

        private readonly Stack<Node> _nodePool = new();
        private readonly List<Node> _live = new();

        public PlanFailure LastFailure { get; private set; }
        public int LastExpandedNodes { get; private set; }

        public GoapPlanner(int maxIterations = 200, int maxDepth = 6)
        {
            if (maxIterations <= 0) throw new ArgumentOutOfRangeException(nameof(maxIterations));
            if (maxDepth < 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));

            _maxIterations = maxIterations;
            _maxDepth = maxDepth;
        }

        public IPlan BuildPlan(IReadOnlyList<IAction> actions, IGoal goal, WorldState start, IPlanningContext context = null)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (goal == null) throw new ArgumentNullException(nameof(goal));
            if (start == null) throw new ArgumentNullException(nameof(start));

            LastExpandedNodes = 0;

            if (goal.IsSatisfiedBy(start))
            {
                LastFailure = PlanFailure.AlreadySatisfied;
                return null;
            }

            Reset();

            bool hitDepthLimit = false;
            _open.Push(Rent(start, 0f, Heuristic(goal, start, context), 0, null, null));

            int iterations = 0;
            while (_open.Count > 0)
            {
                if (++iterations > _maxIterations)
                {
                    LastFailure = PlanFailure.IterationLimit;
                    return Done(null);
                }

                var current = _open.Pop();
                if (_bestByState.TryGetValue(current.State, out float bestG) && current.G > bestG)
                    continue;

                if (goal.IsSatisfiedBy(current.State))
                {
                    LastFailure = PlanFailure.None;
                    return Done(Reconstruct(goal, current));
                }

                if (current.Depth >= _maxDepth)
                {
                    hitDepthLimit = true;
                    continue;
                }

                LastExpandedNodes++;

                for (int a = 0; a < actions.Count; a++)
                {
                    var action = actions[a];
                    if (!action.PreconditionsSatisfied(current.State)) continue;

                    float rawCost = action.Cost(current.State, context);

                    if (float.IsInfinity(rawCost) || float.IsNaN(rawCost)) continue;

                    float stepCost = Math.Max(0.01f, rawCost);
                    float g = current.G + stepCost;
                    var nextState = action.ApplyTo(current.State);

                    if (_bestByState.TryGetValue(nextState, out float prevG) && prevG <= g)
                        continue;

                    _bestByState[nextState.Freeze()] = g;

                    _open.Push(Rent(nextState, g, g + Heuristic(goal, nextState, context),
                                    current.Depth + 1, current, action));
                }
            }

            LastFailure = hitDepthLimit ? PlanFailure.DepthLimit : PlanFailure.Unreachable;
            return Done(null);
        }

        private static float Heuristic(IGoal goal, WorldState state, IPlanningContext context)
        {
            if (goal.IsSatisfiedBy(state)) return 0f;
            var h = goal.Heuristic(state, context);

            if (h.HasValue && !float.IsNaN(h.Value) && !float.IsInfinity(h.Value))
                return Math.Max(0f, h.Value);
            return 0.05f;
        }

        private static IPlan Reconstruct(IGoal goal, Node end)
        {
            var chain = new List<IAction>();
            for (var n = end; n?.Via != null; n = n.From) chain.Add(n.Via);
            chain.Reverse();
            return new Plan(goal, chain, end.G);
        }

        private void Reset()
        {
            _open.Clear();
            _bestByState.Clear();
            for (int i = 0; i < _live.Count; i++)
            {
                _live[i].Clear();
                _nodePool.Push(_live[i]);
            }
            _live.Clear();
        }

        private IPlan Done(IPlan plan)
        {
            return plan;
        }

        private Node Rent(WorldState state, float g, float f, int depth, Node from, IAction via)
        {
            var n = _nodePool.Count > 0 ? _nodePool.Pop() : new Node();
            n.State = state;
            n.G = g;
            n.F = f;
            n.Depth = depth;
            n.From = from;
            n.Via = via;
            _live.Add(n);
            return n;
        }

        private sealed class Node
        {
            public WorldState State;
            public float G, F;
            public int Depth;
            public Node From;
            public IAction Via;

            public void Clear()
            {
                State = null;
                From = null;
                Via = null;
            }
        }

        private sealed class MinHeap
        {
            private Node[] _items = new Node[32];
            public int Count { get; private set; }

            public void Clear()
            {
                Array.Clear(_items, 0, Count);
                Count = 0;
            }

            public void Push(Node n)
            {
                if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
                int i = Count++;
                _items[i] = n;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_items[parent].F <= _items[i].F) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public Node Pop()
            {
                var root = _items[0];
                int last = --Count;
                _items[0] = _items[last];
                _items[last] = null;

                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                    if (l < Count && _items[l].F < _items[smallest].F) smallest = l;
                    if (r < Count && _items[r].F < _items[smallest].F) smallest = r;
                    if (smallest == i) break;
                    (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                    i = smallest;
                }
                return root;
            }
        }
    }
}
