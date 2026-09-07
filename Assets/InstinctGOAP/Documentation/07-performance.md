# Performance

[← Docs index](README.md)

## Measured numbers

Measured against the shipped core (`Instinct.GOAP`) compiled as a plain .NET library, .NET 8 / x64 Release, on a desktop CPU. Unity's Mono JIT and IL2CPP are typically **1.5–3× slower** and their GC is far less forgiving, so treat these as the optimistic end.

**Domain A — small** (10 facts, 10 actions, 1 goal, plan length 8):

| Budget | Time per plan | Allocated per plan |
|---|---|---|
| `maxIterations 200, maxDepth 6` (fails: DepthLimit) | 0.16 ms | 19 KB |
| `maxIterations 2000, maxDepth 12` (8-action plan) | 0.50 ms | 54 KB |

**Domain B — the size of a real shipped agent** (57 facts, 18 actions, 15 goals, `400 / 8`):

| Situation | Time | Allocated | Frequency |
|---|---|---|---|
| Full replan (7 of 15 goals actually planned, rest pruned) | **0.35 ms** | **34 KB** | only when the plan becomes invalid |
| Steady tick, plan still valid | **6 µs** | **752 B** | every tick |
| **No plan exists** (all goals relevant but unreachable) | **0.85 ms** | **89 KB** | **every single tick** |

Two conclusions worth acting on:

1. Planning itself is cheap enough to do per-agent, per-decision. A dozen agents replanning a few times a second is not a problem.
2. **Allocation, not time, is the limit.** Every expansion clones a `WorldState`, and every tick allocates a fresh snapshot. That is 34 KB per replan and ~750 B per agent per tick — on mobile IL2CPP, 50 agents at 60 fps is ≈ 2 MB/s of garbage from snapshots alone, before any replanning.

## Where the allocations come from

| Source | Per | Fix available to you |
|---|---|---|
| `WorldState.Clone()` in `IAction.ApplyTo` | every expansion | fewer facts (the clone is proportional to schema size), fewer actions, tighter `maxDepth`, a good heuristic |
| `IWorldStateProvider.GetState()` / `BoundStateProvider` | every tick | tick the agent less often; keep the schema small |
| `RuntimeExecutor` effect mirroring (`GetState` + `ApplyTo` + a `HashSet`) | every successful action | `NoMirror()` on actions whose results the world already applies |
| `Plan` + `List<IAction>` | every successful plan | nothing; it is small |
| Search nodes, heap, closed set | pooled and reused | nothing needed |

The framework has no allocation-free mode and no object pooling for world states. If you need one, the honest lever is **schema size**: halve the number of facts and you halve the clone traffic.

## The no-plan trap

The agent replans whenever there is no active plan — and if no goal can produce a plan, there is never an active plan, so **it replans every tick, forever**, at the most expensive setting the domain has (every relevant goal planned to exhaustion of its iteration budget).

This is the single worst performance failure mode in the framework, and it looks like a gameplay bug ("the NPC is standing still and the game is stuttering"). Guard against it:

- **Always have a floor goal** — an always-relevant, always-satisfiable `Idle`/`Patrol` goal with a low priority. One trivially satisfiable goal turns the 0.85 ms path back into the 0.35 ms path.
- **Gate goals with `RelevantWhen`.** An irrelevant goal costs nothing; a relevant unreachable one costs a full A* run.
- **Watch `LastPlannedGoals`** — if it equals your goal count every tick, pruning is not working and probably nothing is planning.
- **Set `Fallback`** on `GoapAgent<TCommand>` so a planless agent at least does something visible.

## Choosing budgets

```csharp
new GoapPlanner(maxIterations: 400, maxDepth: 8)
```

- `maxDepth` — count your longest **honest** chain by hand, add 2. Over the limit you get *no plan*, not a shorter one, so being stingy here is what produces mysterious idling.
- `maxIterations` — start at `~50 × maxDepth`, then look at `planner.LastExpandedNodes` in the worst case you can construct and leave 2–3× headroom. Hitting the limit is reported as `IterationLimit` and almost always means the heuristic is weak.

Worst-case cost grows with (applicable actions)^depth, so depth is the dangerous dial, not iterations.

## Making planning cheaper

1. **Give every goal a heuristic.** Without one the planner substitutes `0.05` and the search degenerates towards Dijkstra — the single biggest speed factor in a non-trivial domain.
2. **Keep the fact schema minimal.** Facts are the state vector: they size every clone, every hash and every equality check. Anything no action writes and no goal tests belongs in your context, not in a fact.
3. **Gate hard with `RelevantWhen`.** It is checked before any planning happens.
4. **Prefer typed `Require`/`Effect` over predicates and `DynamicEffect`.** Typed conditions are cheap struct comparisons through a non-boxing reader; a predicate is a delegate call per expansion, and it also blinds validation and the graph window.
5. **Keep `Cost` lambdas trivial.** They run at every expansion, not once per frame. No `Vector3.Distance` over a list, no `GetComponent`, no allocation.
6. **Split archetypes.** One domain with 40 actions serving four NPC types is four times more expensive than four domains with 10.
7. **Stagger ticks** across agents with a per-agent phase offset ([Integration §6](04-integration.md#ticking-on-a-budget)).

## Threading

`GoapPlanner` is deliberately single-threaded: it holds a heap, a dictionary and a node pool as mutable fields. One planner instance per concurrently-ticking agent; sharing one across agents that tick sequentially on the main thread is fine and saves memory. There is no job-system or Burst path, and the core's use of `Dictionary`, `List` and delegates makes one impractical without a rewrite.

Because a plan costs microseconds, this is usually the right trade — but if your design needs hundreds of agents replanning simultaneously, this framework is not built for it.

## Profiling checklist

- `GoapPlanner.LastExpandedNodes` — how hard the last search worked.
- `GoapAgent.LastPlannedGoals` — how many goals survived pruning.
- Unity Profiler, GC Alloc column, on the frame the agent replans; compare against a frame where it does not.
- If the two are indistinguishable, the agent is replanning every frame — see [the no-plan trap](#the-no-plan-trap).
