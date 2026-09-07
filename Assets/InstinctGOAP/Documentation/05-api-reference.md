# API Reference

[← Docs index](README.md)

Namespaces: `Instinct.GOAP` (core, engine-free) · `Instinct.GOAP.Unity` (MonoBehaviour, async, movement) · `Instinct.GOAP.EditorTools` (graph window).

---

## Facts and state

### `Fact<T>` — `Instinct.GOAP`

| Member | Description |
|---|---|
| `static Fact<T> Declare([CallerMemberName] string name = "")` | Declares a fact. `T` must be `bool`, `int`, `float` or an enum ≤32 bits, otherwise `NotSupportedException` |
| `int Id` | Global, assignment-ordered id |
| `string Name` | Field name captured at declaration |
| `Type ValueType` | `typeof(T)` |

Facts must be **static fields of the declaring class** — that class is the schema. Array or collection fields are not discovered.

### `WorldState` — `Instinct.GOAP`

| Member | Description |
|---|---|
| `static WorldState For<TFacts>()` / `For(Type)` | Allocates a state sized to that schema |
| `static WorldState Empty` | Zero-slot state |
| `T Get<T>(Fact<T>)` | Reads a slot. Throws if the fact belongs to another schema |
| `WorldState Set<T>(Fact<T>, T)` | Writes; returns `this` for chaining. Throws if frozen (Editor/dev/DEBUG only) |
| `bool Has<T>(Fact<T>)` | Slot exists **and** was written |
| `FactValue Read(IFact)` | Untyped read, for tooling |
| `WorldState Clone()` | Deep copy of the slot array |
| `WorldState Freeze()` / `bool IsFrozen` | Marks read-only; the planner freezes closed-set states |
| `int SlotCount` | Number of slots |
| `Equals` / `GetHashCode` | Value semantics over all slots, hash cached until the next `Set` |

### `FactValue` — `Instinct.GOAP`

Tagged 12-byte union (`Kind` = `None/Bool/Int/Float`) with `AsBool`, `AsInt`, `AsFloat`, `IsNone`, `IsDefaultLike()`. Enums are stored as `Int`.

### `FactSchema` / `FactSchema<TFacts>`

`FactSchema.Of(Type)` and `FactSchema<TFacts>.Facts` expose the ordered `IFact[]` of a schema — used by `GoapExplain.State<TFacts>` and the graph window.

---

## Identity

### `ActionKey` / `GoalKey` — `Instinct.GOAP`

| Member | Description |
|---|---|
| `static ActionKey Declare([CallerMemberName])` | New unique key, named after the field |
| `static ActionKey Of<T>() where T : IAction` | The class *is* the identity; cached per type |
| `static ActionKey Of(Type)` | Same, non-generic |
| `static ActionKey Named(string)` | **Interned** by string — same string returns the same key |
| `int Id`, `string DebugName`, `bool IsNone` | `Id == 0` means "none" |
| `==`, `!=`, `Equals`, `GetHashCode` | Int comparison |

`GoalKey` is identical minus `Of<T>`.

### `KeyRegistry`

`GoalKeysIn(Type)` / `ActionKeysIn(Type)` — every key declared in a keys class, via reflection with caching. Used by `DomainBuilder` to detect declared-but-never-registered keys.

---

## Conditions and effects

### `Compare`
`Equal`, `NotEqual`, `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`. Ordered operators on a `bool` fact throw at construction.

### `ICondition`
`bool Test(IWorldState)`, `string Description`, `IFact Subject` (`null` for predicates).

Implementations: `Condition<T>` (typed, visible to tooling) and `PredicateCondition` (opaque lambda).

### `IEffect`
`void Apply(IWorldState pre, WorldState next)`, `string Description`, `IFact Subject`, `bool IsConstant`.

| Implementation | Semantics |
|---|---|
| `Effect<T>` | `next[fact] = value` |
| `AddEffect` | `next[fact] = clamp(pre[fact] + delta, min, max)` — `int` only |
| `CopyEffect<T>` | `next[target] = pre[source]` |
| `ComputedEffect<T>` | `next[fact] = f(pre)` |
| `DynamicEffect` | arbitrary `(pre, next)` — `Subject` is `null`, so validation and the graph window cannot see it |

All effects read `pre`, so declaration order never matters.

---

## Building a domain (low-level)

### `ActionBuilder`

```csharp
ActionBuilder.Create(ActionKey)      ActionBuilder.Create()            // name from caller member
ActionBuilder.For<T>() where T : IAction
```

| Method | |
|---|---|
| `Require(fact, value)` / `Require(fact, op, value)` / `Require(predicate, desc)` | preconditions |
| `Effect(fact, value)` · `Add(fact, delta, min, max)` · `Copy(target, source)` · `Computed(fact, f, desc)` · `DynamicEffect(apply, desc)` | effects |
| `Cost(float)` / `Cost(Func<IWorldState, IPlanningContext, float>)` | cost; clamped to ≥ 0.01, `NaN`/`∞` makes the planner skip the action |
| `Build()` | → `IAction` |

### `GoalBuilder`

```csharp
GoalBuilder.Create(GoalKey)          GoalBuilder.Create()              // name from caller member
```

| Method | |
|---|---|
| `Satisfy(fact, value)` / `Satisfy(fact, op, value)` / `Satisfy(predicate, desc)` | goal condition(s), ANDed |
| `RelevantWhen(Func<IWorldState,bool>)` | gate; default `true` |
| `Priority(float)` / `Priority(Func<IWorldState,float>)` | default `1` |
| `Heuristic(Func<IWorldState,float?>)` / `(state, ctx) => float?` | default `null` → planner substitutes `0.05` |
| `Build()` | → `IGoal` (also `IInspectableGoal`) |

### `DomainBuilder`

`AddAction(s)`, `AddGoal(s)`, `DeclaredGoalsIn(Type)`, `DeclaredActionsIn(Type)`, `Validate() → IReadOnlyList<Issue>`, `Describe() → string?` (`null` when clean).

Checks: duplicate action/goal keys · keys declared but never registered · actions with no effects · goals with no conditions · goals no effect can ever satisfy.

---

## Planner

### `GoapPlanner : IPlanner`

```csharp
new GoapPlanner(int maxIterations = 200, int maxDepth = 6)

IPlan BuildPlan(IReadOnlyList<IAction> actions, IGoal goal, WorldState start, IPlanningContext ctx = null)
PlanFailure LastFailure { get; }
int LastExpandedNodes { get; }
```

Returns `null` on failure — inspect `LastFailure`. Not thread-safe; one instance per concurrently-ticking agent.

### `IPlan` / `Plan`
`IGoal Goal`, `IReadOnlyList<IAction> Actions`, `float TotalCost`.

### `PlanFailure`
`None`, `AlreadySatisfied`, `Unreachable`, `IterationLimit`, `DepthLimit`.

### `IPlanningContext` / `PlanningContext`
`object Extra`, `T GetExtra<T>()`. The Flow API passes your context here, which is what makes `Cost((state, ctx) => ...)` work.

---

## Agent (Command API)

### `GoapAgent<TCommand> : IGoapAgent<TCommand>`

```csharp
new GoapAgent<TCommand>(IPlanner planner,
                        IReadOnlyList<IGoal> goals,
                        IReadOnlyList<IAction> actions,
                        IWorldStateProvider stateProvider,
                        IActionExecutor<TCommand> executor,
                        IAgentContext context = null,
                        IPlanningContext planningContext = null)
```

| Member | Description |
|---|---|
| `TCommand Tick()` | Snapshot → replan if needed → `OnSelected` → `Translate` |
| `void NotifyActionComplete(bool success)` | Advances the plan, or clears it on failure |
| `void ForceReplan()` | Clears the plan |
| `IPlan CurrentPlan` · `IGoal CurrentGoal` · `IAction CurrentAction` · `int PlanStep` | |
| `IReadOnlyList<GoalEvaluation> GoalEvaluations` | Last decision, per goal |
| `int LastPlannedGoals` | How many goals actually ran A* (the rest were pruned) |
| `IAgentPolicy Policy { get; set; }` | |
| `Func<WorldState, TCommand> Fallback { get; set; }` | Returned when there is no plan |

### Supporting interfaces

```csharp
interface IWorldStateProvider          { WorldState GetState(); }
interface IActionExecutor<out TCommand>
{
    TCommand Translate(IWorldState state, IAction action, IAgentContext ctx);
    void OnSelected(IWorldState state, IAction action, IAgentContext ctx);   // every tick
    void OnCompleted(IAction action, IAgentContext ctx, bool success);
}
interface IAgentPolicy
{
    bool  ShouldAbandonPlan(IPlan plan, int step, WorldState state);
    float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state);
    void  OnPlanCleared(IAgentContext ctx);
}
interface IAgentContext { }            // marker for your own context object
```

### `GoalEvaluation` (readonly struct)
`Goal`, `Relevant`, `Priority`, `Cost`, `Utility`, `PlanLength`, `Failure`, `Skipped`.

---

## Flow API

### `GoapDomainBuilder<TCtx> where TCtx : class`

| Member | |
|---|---|
| `static GoapDomainBuilder<TCtx> For<TFacts>()` / `For(Type)` | picks the fact schema |
| `Bind<T>(Fact<T>, Func<TCtx,T> read)` | read-only binding |
| `Bind<T>(Fact<T>, Func<TCtx,T> read, Action<TCtx,T> write)` | two-way; enables effect mirroring |
| `RuntimeActionBuilder<TCtx> Action(ActionKey)` / `Action(string)` | |
| `GoalBuilder Goal(GoalKey)` / `Goal(string)` | |
| `GoapDomain<TCtx> Build()` | |

### `RuntimeActionBuilder<TCtx>`

Planner side: `Require`, `Effect`, `Add`, `Copy`, `Cost(float)`, `Cost(Func<IWorldState,TCtx,float>)`.
Game side: `Run(IStep<TCtx>)`, `Run(Func<TCtx,ActionStatus>)`, `Instant(Action<TCtx>)`, `RunAsync(Func<TCtx,CancellationToken,UniTask>)` (Unity assembly), `OnDone(Action<TCtx,bool>)`, `NoMirror()`.

### `GoapDomain<TCtx>`
`FactsType`, `Actions`, `Goals`, `Bindings`, `PlannerActions`, `Goal(GoalKey)`, `Action(ActionKey)`, `Describe()`.

Build **one per agent** — actions carry per-run state.

### `GoapBrain<TCtx> : IGoapAgentView`

```csharp
new GoapBrain<TCtx>(GoapDomain<TCtx> domain, TCtx ctx,
                    IPlanner planner = null, IAgentPolicy policy = null)
```

| Member | |
|---|---|
| `ActionStatus Tick()` | replan if needed, tick the current action, report completion |
| `IPlan CurrentPlan` · `IGoal CurrentGoal` · `IAction CurrentAction` · `int PlanStep` | |
| `IReadOnlyList<GoalEvaluation> GoalScores` | |
| `IAgentPolicy Policy { get; set; }` · `void ForceReplan()` | |
| `string PlanChain()` · `string ExplainDecision()` | debug strings |

### Steps

```csharp
interface IStep<in TCtx> { void OnEnter(TCtx); ActionStatus Tick(TCtx); void OnExit(TCtx, bool success); }
enum ActionStatus : byte { Running, Success, Failure }
interface ITickContext { float DeltaTime { get; } }
```

| Factory | |
|---|---|
| `Steps.Instant<TCtx>(Action<TCtx> body = null)` | run once, succeed |
| `Steps.Run<TCtx>(Func<TCtx,ActionStatus>)` | arbitrary per-tick logic |
| `Steps.Wait<TCtx>(seconds)` / `(Func<TCtx,float>)` | needs `ITickContext` |
| `Steps.Sequence<TCtx>(params IStep<TCtx>[])` | one GOAP action, several steps |
| `UnitySteps.MoveTo<TCtx>(Func<TCtx,Vector3>)` / `(Func<TCtx,Transform>)` | needs `IMoveContext` |

---

## Unity layer — `Instinct.GOAP.Unity`

### `GoapAction<TCtx> where TCtx : class` (requires UniTask)

| Member | |
|---|---|
| `protected abstract void Setup()` | `Require` / `Effect` / `Add` / `Cost` / `NoMirror` — valid only inside `Setup` |
| `protected abstract UniTask Run(TCtx ctx)` | behaviour; returning = success |
| `protected virtual ActionKey Key` | defaults to `ActionKey.Of(GetType())` |
| `protected static void Fail(string reason = null)` | throws `GoapActionFailed` → the plan is dropped |
| `protected CancellationToken Ct` | cancelled when the plan is abandoned |
| `Wait`, `NextFrame`, `Until`, `While`, `MoveTo`, `Timeout` | awaitables bound to `Ct` |

Register with `domain.Use(new ActionA(), new ActionB(), ...)`.

### `GoapAwait`
`NextFrame`, `Seconds`, `Until`, `While`, `MoveTo`, `Timeout` — the same set as free functions taking a `CancellationToken`.

### `AsyncStep<TCtx> : IStep<TCtx>`
Wraps `Func<TCtx, CancellationToken, UniTask>`. Starts the body on `OnEnter` (UniTask player loop), reports `Running` until it ends, converts `GoapActionFailed` and unhandled exceptions to `Failure`, and ignores results from a cancelled generation.

### `IMoveContext`
`Vector3 Position` · `bool MoveTowards(Vector3 target)` — returns `true` on arrival.

### `GoapAgentHost<TCommand> : MonoBehaviour`
Abstract host for the Command API: `Agent` and `ExecuteCommand(TCommand)` to override, `NotifyActionComplete(bool)` to call, `_logPlan` toggle in the inspector. Executes only when the command changes.

---

## Debug helpers — `GoapExplain`

| Method | Output |
|---|---|
| `Decision(IReadOnlyList<GoalEvaluation>, IPlan)` | the full goal scoreboard with reasons |
| `Chain(IPlan)` | `A -> B -> C` |
| `BlockedBy(IAction, IWorldState)` | the unmet preconditions |
| `Applicability(actions, state)` | every action, with what blocks it |
| `Failure(IGoal, PlanFailure)` | a sentence explaining the failure and what usually causes it |
| `State<TFacts>(WorldState)` | `Energy=40 DistanceToHome=3.2 …` (enums by member name) |

---

## Editor — `Instinct.GOAP.EditorTools`

### `IGoapGraphSource` / `GoapGraphSource`

```csharp
public sealed class GuardGraphSource : GoapGraphSource
{
    public override IReadOnlyList<IAction> Actions => GuardActions.All();
    public override IReadOnlyList<IGoal>   Goals   => GuardGoals.All();
    public override IGoapAgentView FindLiveAgent() => Object.FindObjectOfType<GuardHost>()?.Agent;
    public override IEnumerable<string> BadgesFor(IGoal goal) => new[] { "priority 100" };
}
```

Any parameterless implementation anywhere in the project is discovered automatically and appears in the window's domain dropdown. `Window → Analysis → GOAP Graph`.
