# Limits & FAQ

[← Docs index](README.md)

Read this before adopting, not after.

## What this framework is

A forward-searching GOAP planner with utility-based goal selection, a typed fact system, and two integration layers. About 2 500 lines of engine-free core plus a small Unity layer, an editor graph window and three sample agents. MIT.

It has been driving two agents in a real game (a large stalker/hunter NPC with 57 facts, 18 actions and 15 goals, plus a scripted player bot) through the Command API.

## What it deliberately is not

| Not supported | Why, and what to do instead |
|---|---|
| Multithreading / jobs / Burst | A plan costs microseconds; a shared mutable planner is not worth the class of bug threading brings. One planner per concurrently-ticking agent |
| Backward chaining (regression planning) | Forward search only. Domains with a huge action set and a narrow goal may plan faster backwards — this framework will not do that for you |
| HTN, behaviour trees, utility-only AI | Different tools. GOAP composes plans; a BT executes authored ones |
| ScriptableObject / drag-and-drop authoring | Domains are code. The graph window visualises, it does not author |
| Runtime domain editing | Domains are built once per agent and treated as immutable |
| Hierarchical or partial-order plans | Plans are flat action sequences |
| Serialising plans or keys | Key ids depend on static-initialisation order. Save your context instead |

## Known limitations (as of this version)

1. **No replan throttle.** The agent replans whenever the plan is invalid, including *every frame* while no plan exists at all. Stagger ticks yourself and always keep one satisfiable floor goal ([Performance](07-performance.md#the-no-plan-trap)).
2. **Allocation per expansion.** Each expansion clones a `WorldState`; each tick allocates a fresh snapshot. Measured: ~34 KB per replan and ~750 B per tick on a 57-fact domain. Fine for dozens of agents, not for hundreds on mobile.
3. **Facts must be individual static fields.** A `Fact<T>[]` field is invisible to schema discovery, and the failure mode is a confusing runtime exception on the first `Get`.
4. **`Fact<long>`-backed enums are rejected**, as are `string`, `Vector3` and reference types. That is a deliberate design constraint of the dense state, not an oversight — model positions as distances or discrete cells.
5. **The async action layer requires UniTask.** `Instinct.GOAP.Unity.Async` is guarded by the `INSTINCT_UNITASK` define and is simply skipped when UniTask is absent, as is the Farmer sample. The planner and `Instinct.GOAP.Unity` still compile; actions are then composed from `IStep<TCtx>`.
7. **Predicates and `DynamicEffect` are invisible to tooling.** They draw no graph edges and validation cannot reason about them.
8. **`IActionExecutor.OnSelected` fires every tick**, not once per action. Track entry yourself in a hand-written executor.
9. **A failing step inside `Steps.Sequence` receives `OnExit` twice** (once when the sequence sees the failure, once when the action exits). Keep `OnExit` idempotent.
10. **The frozen-state guard is Editor/development-only.** In a release player, mutating a state the planner holds silently corrupts the search.

## FAQ

**Which API should I start with?**
Flow API, unless you already have a command/order pipeline you want the AI to feed. The Command API is not deprecated — it is the layer the Flow API is built on, and it is the one with shipping-game mileage.

**Can I mix both in one project?**
Yes, per agent. Not inside a single agent.

**Does the core really compile without Unity?**
Yes — `Instinct.GOAP` sets `noEngineReferences: true`, and it builds as a plain netstandard2.1 library. That is what makes the planner testable in a normal .NET test project and reusable on a headless server.

**How many agents can I run?**
Time is not the constraint; garbage is. Budget ~750 B per agent per tick plus ~35 KB per replan on a mid-sized domain, then decide your tick rate. Dozens of agents at a staggered 5–10 Hz is comfortable; hundreds at 60 Hz is not what this is built for.

**Can I plan on a background thread?**
Not safely with a shared planner. You can give each worker its own `GoapPlanner` and its own state snapshot, but nothing in the framework helps you do that, and the Flow API's execution layer assumes the main thread.

**How do I model positions?**
Not as `Vector3`. Either a `float` distance fact (`DistanceToTarget`), or a discrete `enum`/`int` cell id. Real navigation stays in your context, behind `IMoveContext`.

**How do I do "do X three times"?**
`Add(fact, +1, min, max)` on the action plus `Satisfy(fact, Compare.GreaterOrEqual, 3)` on the goal, and make sure `maxDepth` covers the resulting chain length.

**How do I stop the agent from changing its mind constantly?**
`IAgentPolicy.UtilityBias` for stickiness, `ShouldAbandonPlan` for the interrupts you *do* want.

**Is it production-ready?**
The planner, the fact system, the validation and the Command API have carried a real game. The Flow API and the async action layer are newer and have less mileage — they are well-designed and unit-tested, but if you are shipping soon, prefer the layer with the miles on it, and read [Known limitations](#known-limitations-as-of-this-version) first.

**Licence?**
MIT, including commercial use. See [`LICENSE`](../LICENSE).
