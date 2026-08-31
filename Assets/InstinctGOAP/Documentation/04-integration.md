# Integration Guide

[← Docs index](README.md)

How to put this into a game that already exists, without rewriting it.

---

## 1. Decide which API you are integrating

| Your situation | Use |
|---|---|
| New AI, gameplay code you own, UniTask available | **Flow API** — `GoapDomainBuilder` + `GoapBrain` |
| An existing command bus / animation state machine / netcode layer that already executes "orders" | **Command API** — `GoapAgent<TCommand>` |
| A simulation with its own tick (no MonoBehaviour, headless tests, server) | **Command API**, core assembly only — it has no `UnityEngine` reference at all |

Both can coexist in one project; just not inside one agent.

---

## 2. Where each piece of your game goes

```
your world                     framework
──────────                     ─────────
Transforms, NavMesh, HP,   →   Context (your class)      never touched by the planner
inventory, senses, memory

what the planner may reason →  Fact<T> declarations       one dense slot each
about (a small subset!)

sensing                    →   Bind(fact, read)      or   IWorldStateProvider.GetState()
acting                     →   GoapAction<TCtx>.Run  or   IActionExecutor.Translate → your command
writing results back       →   Bind(fact, read, write) or your own OnCompleted
interrupts / commitment    →   IAgentPolicy
per-frame drive            →   GoapBrain.Tick()      or   GoapAgentHost<TCommand>
```

The single most common integration mistake is **promoting the whole blackboard to facts**. Facts are the planner's search space: every extra fact widens the state, slows the closed-set comparison and makes plans harder to reason about. If no action changes it and no goal tests it, it is not a fact — it stays in your context and is read directly by a `Cost` lambda or by the action body.

---

## 3. Sensing: world → facts

### Flow API

```csharp
d.Bind(Facts.HasWeapon, c => c.Inventory.HasWeapon, (c, v) => c.Inventory.HasWeapon = v);  // two-way
d.Bind(Facts.DistanceToPlayer, c => c.DistanceTo(c.Player));                                // read-only
```

A read-only binding is right for derived values (distances, counts computed from the scene) — those change because the world moved, not because an effect said so.

### Command API

```csharp
public sealed class GuardStateProvider : IWorldStateProvider
{
    private readonly GuardBlackboard _board;
    public GuardStateProvider(GuardBlackboard board) => _board = board;

    public WorldState GetState() => WorldState.For<GuardFacts>()
        .Set(GuardFacts.AlertLevel,      _board.Alert)
        .Set(GuardFacts.IntruderVisible, _board.CanSeeIntruder)
        .Set(GuardFacts.DistanceToIntruder, _board.DistanceToIntruder);
}
```

`GetState()` runs **every tick**, so keep it to field reads and cached values. Raycasts, `FindObjectsOfType`, `Physics.OverlapSphere` and pathfinding belong in a sensor component that runs on its own slower schedule and writes into the blackboard.

> Facts must be **individual static fields** of the declaring class. A `Fact<bool>[]` array field is invisible to the schema — the state will be built with zero slots and the first `Get` throws "does not belong to this state's schema". Generate the fields (or hand-write them); keep arrays only as convenience lookups.

---

## 4. Acting

### Flow API — async action classes

```csharp
public sealed class OpenDoor : GoapAction<AiContext>
{
    protected override void Setup()
    {
        Require(Facts.AtDoor, true);
        Require(Facts.DoorOpen, false);
        Effect(Facts.DoorOpen, true);
        Cost(1f);
    }

    protected override async UniTask Run(AiContext c)
    {
        if (c.Door == null) Fail("no door");
        c.Animator.SetTrigger("Open");
        await Timeout(2f, () => c.Door.IsOpen);   // fail if the animation never finishes
    }
}
```

### Flow API — step-composed action (no UniTask)

```csharp
d.Action(Keys.Deliver)
    .Require(Facts.HasParcel, true)
    .Effect(Facts.ParcelDelivered, true)
    .Cost(1f)
    .Run(Steps.Sequence(
        UnitySteps.MoveTo<AiContext>(c => c.Target.position),
        Steps.Wait<AiContext>(0.4f),
        Steps.Instant<AiContext>(c => c.DropParcel())));
```

### Command API — translate to your own command

```csharp
public sealed class GuardExecutor : IActionExecutor<GuardCommand>
{
    public GuardCommand Translate(IWorldState state, IAction action, IAgentContext ctx)
        => action is IGuardAction g ? g.Translate(state, _board).From(action.Key) : GuardCommand.Idle;

    public void OnSelected(IWorldState state, IAction action, IAgentContext ctx) { }
    public void OnCompleted(IAction action, IAgentContext ctx, bool success)
        => (action as IGuardAction)?.OnCompleted(_board, success);
}
```

Carry the `ActionKey` on the command (`.From(action.Key)`) so downstream code can ask *which* action issued it without string comparisons.

`OnSelected` fires **every tick**, not once per action. If you need entry semantics, compare with the previous action yourself:

```csharp
public void OnSelected(IWorldState state, IAction action, IAgentContext ctx)
{
    if (action.Key == _lastKey) return;
    _lastKey = action.Key;
    // real "on enter" work here
}
```

---

## 5. Movement

Implement `IMoveContext` **once per project**:

```csharp
public sealed class AiContext : IMoveContext, ITickContext
{
    public NavMeshAgent Nav;

    public Vector3 Position => Nav.transform.position;
    public float DeltaTime => Time.deltaTime;

    public bool MoveTowards(Vector3 target)
    {
        if (Nav.destination != target) Nav.SetDestination(target);
        return !Nav.pathPending && Nav.remainingDistance <= Nav.stoppingDistance;
    }
}
```

Then every action just says `await MoveTo(c, () => c.Target.position)`. Nothing in the framework knows about NavMesh, root motion or your character controller.

---

## 6. Driving the agent

```csharp
private void Update() => _brain.Tick();                      // Flow API
```

```csharp
public sealed class GuardHost : GoapAgentHost<GuardCommand>   // Command API
{
    protected override IGoapAgent<GuardCommand> Agent => _agent;
    protected override void ExecuteCommand(GuardCommand cmd) => _motor.Run(cmd);
    // call NotifyActionComplete(success) when the motor finishes
}
```

`GoapAgentHost` calls `ExecuteCommand` only when the command actually changes (`EqualityComparer<TCommand>.Default`), so make `TCommand` a value type with meaningful equality — a struct or an enum, not a class.

### Ticking on a budget

`Tick()` per frame per agent is fine for a handful of agents. Beyond that you must stagger, because **the framework has no built-in replan interval** — it replans whenever the current plan stops being valid, including every single frame while no plan exists at all.

There is no separate "think" and "act" entry point, so staggering means calling `Tick()` less often for that agent:

```csharp
private void Awake()  => _phase = Random.value * ThinkInterval;   // spread agents across frames
private void Update()
{
    if (Time.time - _phase < _nextThink) return;
    _nextThink = Time.time - _phase + ThinkInterval;              // 0.05–0.2 s
    _brain.Tick();
}
```

Because `Tick()` also advances the current action, a long interval makes movement and timers coarser. Practical compromise: full rate for the few agents near the player, a staggered interval for the rest, and make sure at least one goal is always relevant so the expensive no-plan path never runs — see [Performance](07-performance.md#the-no-plan-trap).

---

## 7. Many agents

- **One domain instance per agent.** Actions hold per-run state (cancellation tokens, `Wait` timers, sequence indices). Sharing a domain mixes them. Cheap: `FarmerBrain.Build()` in `Awake`.
- **One planner per agent** (or per group ticking in sequence). `GoapPlanner` holds a heap, a dictionary and a node pool as mutable fields; it is single-threaded by design and **not** safe to share across agents that tick concurrently. Sharing one across agents that tick sequentially on the main thread does work and saves the pooled memory.
- **Facts are global identities.** All agents of the same archetype share one `Facts` class and therefore one schema, which is exactly what you want; two different archetypes get two classes and two schemas, and mixing them throws.

---

## 8. Validation in CI

```csharp
[Test] public void DomainIsClean()
{
    var brain = MyBrain.Build();
    Assert.IsNull(brain.Describe(), brain.Describe());
}

[Test] public void HeuristicNeverOverestimates()
{
    var plan = new GoapPlanner(500, 10).BuildPlan(actions, goal, start);
    Assert.LessOrEqual(goal.Heuristic(start, null) ?? 0f, plan.TotalCost);
}

[Test] public void EveryStateHasARelevantGoal()
{
    for (int energy = 0; energy <= 100; energy++)
        Assert.IsTrue(goals.Any(g => g.IsRelevant(StateWith(energy))), $"gap at {energy}");
}
```

Those three tests catch the majority of GOAP bugs before they become "the NPC just stands there" bug reports. The framework's own `Tests/EditMode` folder shows the pattern.

---

## 9. Saving and loading

The agent holds no persistent state worth serialising: a plan is rebuilt from the world in one tick. Save your **context** (energy, inventory, memory), restore it, and let the next `Tick()` replan. Do not serialise `WorldState`, `IPlan` or keys — key ids are assignment-order dependent and are not stable across builds.
