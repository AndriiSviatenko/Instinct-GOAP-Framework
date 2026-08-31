# Core Concepts

[← Docs index](README.md)

Everything the framework does fits in one sentence: **each frame it takes a snapshot of your world, scores every relevant goal by `Priority − PlanCost`, and runs the first action of the winning plan.**

The rest of this page is what each of those words means.

---

## 1. Facts

A fact is a typed handle to one slot of the world state.

```csharp
public static readonly Fact<bool>  IntruderVisible = Fact<bool>.Declare();
public static readonly Fact<int>   Ammo            = Fact<int>.Declare();
public static readonly Fact<float> Distance        = Fact<float>.Declare();
public static readonly Fact<Alert> AlertLevel      = Fact<Alert>.Declare();  // any enum
```

- Supported: `bool`, `int`, `float`, and enums with a 32-bit-or-smaller underlying type. `long`/`ulong`-backed enums throw at declaration time.
- Facts are grouped by their **declaring class**, which becomes the schema: `WorldState.For<GuardFacts>()` allocates exactly as many slots as that class declares.
- The declaring class must be a `sealed class` (not `static`) if you want `WorldState.For<T>()`; a `static class` still works via `WorldState.For(typeof(MyFacts))`.
- Ids are global and assigned in static-initialisation order, so a state built for one schema **rejects** a fact from another one with an explicit exception. Mixing schemas is always a bug, and it fails loudly.

**What belongs in a fact:** anything an action changes or a goal tests.
**What does not:** transforms, references, inventory objects, timers. Those live in your context; the planner does not need them.

### Enums read like the state machine they replace

```csharp
public enum Alert { Calm = 0, Suspicious = 1, Hunting = 2 }

.Require(GuardFacts.AlertLevel, Compare.GreaterOrEqual, Alert.Suspicious)
.Effect(GuardFacts.AlertLevel, Alert.Hunting)
```

Stored as the underlying integer, so an ordered comparison is a real comparison.

---

## 2. WorldState

A dense `FactValue[]`, one entry per fact in the schema. Mutable, cloneable, value-comparable, cached hash.

```csharp
var s = WorldState.For<GuardFacts>()
    .Set(GuardFacts.IntruderVisible, true)
    .Set(GuardFacts.Ammo, 6);

bool visible = s.Get(GuardFacts.IntruderVisible);
```

Two invariants that matter:

- **Never mutate a state the planner holds.** `IAction.ApplyTo` clones first. States that enter the search's closed set are `Freeze()`d and throw on write in the Editor, development builds and DEBUG. In a release player that check compiles away, so the discipline must hold on its own.
- **A state is a snapshot, not a channel.** Writing to a state after planning changes nothing in your game — the world is written through bindings (Flow API) or through your executor (Command API).

---

## 3. Actions

An action is a promise: *given these preconditions, this will be true afterwards, and it costs this much right now.*

```csharp
var chase = ActionBuilder.Create(GuardActionKeys.Chase)
    .Require(GuardFacts.IntruderVisible, true)
    .Require(GuardFacts.Distance, Compare.Greater, 1.6f)
    .Effect(GuardFacts.Distance, 1.5f)
    .Effect(GuardFacts.AlertLevel, Alert.Hunting)
    .Cost((s, ctx) => 1f + s.Get(GuardFacts.Distance) * 0.25f)
    .Build();
```

### Preconditions

| Form | Meaning |
|---|---|
| `Require(fact, value)` | equality |
| `Require(fact, Compare.Greater, value)` | ordered comparison (`Equal`, `NotEqual`, `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`) |
| `Require(state => ...)` | arbitrary predicate — **opaque to tooling**, use sparingly |

`Compare.Greater` on a `bool` fact throws at construction: it never means anything.

### Effects

| Form | Applied as |
|---|---|
| `Effect(fact, value)` | `fact := value` |
| `Add(fact, delta, min, max)` | `fact := clamp(pre + delta)` — `int` only |
| `Copy(target, source)` | `target := pre.source` |
| `Computed(fact, s => ...)` | `fact := f(pre)` |
| `DynamicEffect((pre, next) => ...)` | anything; invisible to validation and to the graph window |

**Every effect resolves against the pre-action state.** `Add(Wood, +1)` and `Copy(Best, Wood)` in the same action both read the state as it was before the action, so declaration order can never change the outcome.

### Cost

`Cost(float)` or `Cost((state, context) => float)`. The cost is evaluated **at every expansion during search**, on the hypothetical state at that point in the plan — not once per frame. Keep it cheap and pure.

Costs are clamped to `>= 0.01` (a zero-cost action would let A* loop forever). `NaN` and `Infinity` make the planner skip the action entirely — that is the supported way to say "not available right now" without touching preconditions.

### Identity

```csharp
ActionKey.Declare()          // in a *ActionKeys class; name from the field
ActionKey.Of<ChaseAction>()  // the class IS the identity
ActionKey.Named("Chase")     // interned by string — for data-driven domains
```

`Named` interning means two calls with the same string return the same key. That is convenient and it is also the one place a typo can silently produce a second identity, so prefer `Declare` / `Of<T>`.

---

## 4. Goals

```csharp
var catchIntruder = GoalBuilder.Create(GuardGoalKeys.CatchIntruder)
    .Satisfy(GuardFacts.IntruderCaught, true)
    .RelevantWhen(s => s.Get(GuardFacts.IntruderVisible))
    .Priority(100f)
    .Heuristic(s => 1f)
    .Build();
```

| Piece | Role |
|---|---|
| `Satisfy(...)` | the condition(s) that end the search. Multiple `Satisfy` calls are ANDed |
| `RelevantWhen(...)` | gate. An irrelevant goal is not planned at all — this is your cheapest optimisation |
| `Priority(...)` | the value of achieving it; constant or a function of state |
| `Heuristic(...)` | estimated remaining cost from a state. Optional but strongly recommended |

### The heuristic contract

A* returns an optimal plan **only if the heuristic never overestimates** the true remaining cost. Overestimate and you silently get worse plans; underestimate and you only get a slower search. When no heuristic is given the planner substitutes `0.05`, which is admissible but nearly uninformed — the search degenerates to Dijkstra and burns the iteration budget on any domain with more than a handful of actions.

Assert it in a test: plan the goal, then check `heuristic(start) <= plan.TotalCost`.

---

## 5. The planner

`GoapPlanner : IPlanner` — forward A* from the current state towards the goal condition.

```csharp
new GoapPlanner(maxIterations: 200, maxDepth: 6)
```

- **open list**: binary min-heap on `f = g + h`
- **best-known map**: `Dictionary<WorldState, float>` — a state reached more cheaply than before is re-expanded, otherwise skipped
- **node pool**: search nodes are reused between runs; the `WorldState` clone per expansion is not pooled
- **`maxDepth`**: hard cap on plan length. Over it the planner returns *no plan* — never a truncated one
- **`maxIterations`**: cap on pops from the open list

Failure is reported, not guessed:

| `PlanFailure` | Meaning |
|---|---|
| `None` | planned |
| `AlreadySatisfied` | the goal was already true; `BuildPlan` returns `null` |
| `Unreachable` | the search exhausted the space — no chain of actions reaches the goal |
| `DepthLimit` | every branch hit `maxDepth`. The honest chain is longer than the planner may look |
| `IterationLimit` | ran out of iterations. Usually a weak heuristic, not a real dead end |

Choosing the budget: `maxDepth` must cover your longest *honest* plan (count it by hand), and `maxIterations` should be several times the branching factor times the depth. See [Performance](07-performance.md) for measured numbers.

---

## 6. Goal selection (utility over planning)

Each replan, the agent:

1. drops every goal whose `RelevantWhen` is false;
2. computes each survivor's upper bound `Priority + PolicyBias` and sorts descending;
3. plans them in that order, scoring each as `utility = Priority − PlanCost` (`+ bias` for the winner comparison);
4. **stops early** as soon as the next candidate's upper bound cannot beat the best score found — those goals are reported as `skipped`, not planned.

That pruning is exact: a skipped goal could not have won, because cost is never negative. On a domain with many goals it usually removes most A* runs.

The winner's plan becomes the active plan.

### Policy

```csharp
public interface IAgentPolicy
{
    bool  ShouldAbandonPlan(IPlan plan, int step, WorldState state);
    float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state);
    void  OnPlanCleared(IAgentContext context);
}
```

- `UtilityBias` is where **stickiness** lives: give the current goal `+2` and the agent stops oscillating between two near-equal goals. Give a chase goal `+6` and it commits.
- `ShouldAbandonPlan` is the **interrupt** hook: return `true` to drop a still-legal plan (an intruder appeared, the building caught fire).

---

## 7. Execution

The agent replans when — and only when — one of these is true:

- there is no active plan;
- the plan finished;
- `Policy.ShouldAbandonPlan` says so;
- the active goal stopped being relevant;
- the current step's preconditions no longer hold.

Otherwise it keeps running the current step. Note that "no plan" is the *cheapest to detect and the most expensive to handle*: an agent whose goals are all unreachable re-plans everything every single frame. See [Performance](07-performance.md#the-no-plan-trap).

### Flow API lifecycle

```
GoapBrain.Tick()
  └── GoapAgent.Tick()                   snapshot → replan if needed → current action
        ├── executor.OnSelected(...)     new action? OnExit(previous, false) + OnEnter(new)
        └── returns the action
  └── action.Tick(ctx)  →  Running | Success | Failure
        └── on Success/Failure: agent.NotifyActionComplete(...)
              ├── action.OnExit(ctx, success)
              ├── on success: effects mirrored into the world through write-bindings
              └── on failure: plan cleared → replan next frame
```

**Effect mirroring** is what removes the classic GOAP duplication ("`Effect(Energy, -25)` here and `board.Energy -= 25` there"). After a successful action the framework applies that action's own effects to the world through the two-way bindings, touching only the facts the action actually declared and only bindings that have a writer. Turn it off per action with `NoMirror()` when the world applies the change itself.

### Command API lifecycle

`GoapAgent<TCommand>.Tick()` returns a `TCommand` produced by your `IActionExecutor<TCommand>.Translate(...)`. You run it; when it finishes, you call `NotifyActionComplete(success)`. `GoapAgentHost<TCommand>` is a MonoBehaviour that does this and only calls `ExecuteCommand` when the command actually changes.

Note that `OnSelected` is invoked **every tick**, not once per action — if you need "on entry" semantics in a hand-written executor, compare against the previously selected action yourself.

---

## 8. Validation

```csharp
var report = new DomainBuilder()
    .AddActions(GuardActions.All())
    .AddGoals(GuardGoals.All())
    .DeclaredGoalsIn(typeof(GuardGoalKeys))
    .DeclaredActionsIn(typeof(GuardActionKeys))
    .Describe();                  // null when clean

// Flow API: the same check, already wired
var report = domain.Describe();
```

It catches: duplicate action/goal keys, keys declared but never registered, actions with no effects, goals with no conditions, and goals no action can ever satisfy. Run it in `Awake` behind a `Debug.LogWarning`, and again in an EditMode test so it fails CI rather than a playtest.

---

## 9. Design rules that keep GOAP honest

1. **An effect must describe the real result.** `AllShelvesMapped := true` on an action that visits one shelf makes every plan one action long — a planner degraded into a utility selector.
2. **A heuristic must never overestimate**, or plans stop being optimal. Test it.
3. **`maxDepth` must cover the longest honest chain**, or you get *no plan* instead of a shorter one.
4. **Never re-derive identity from text.** If gameplay asks "which action issued this?", compare `ActionKey`s.
5. **Do not mutate a state the planner holds.** `ApplyTo` works on a clone.
6. **Relevance windows must tile the state space.** A value where no goal is relevant is an agent standing still.
