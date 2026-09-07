# Quick Start — a farmer in 15 minutes

[← Docs index](README.md)

We build an agent that walks between a field and a house, harvests until it runs out of energy, then goes home to sleep. It plans this itself: nowhere do we write "if tired, go home".

The finished version of this agent lives in [`Assets/InstinctGOAP/Samples/Farmer`](../Samples/Farmer). Open [`FarmerGoap.unity`](../Samples/Farmer/FarmerGoap.unity) and press **Play** to watch it run before reading further.

## Step 1 — the context: what the agent actually is

The **context** is your object. The framework never owns your game state; it reads it and writes back through you.

```csharp
using Instinct.GOAP;
using Instinct.GOAP.Unity;
using UnityEngine;

public sealed class FarmerContext : IMoveContext, ITickContext
{
    public Transform Self, Home, Field;

    public int Energy = 100;
    public int CropsGrown;

    public float Speed = 3f;
    public float ArriveDistance = 0.5f;

    public Vector3 Position => Self.position;
    public float DeltaTime => Time.deltaTime;

    public float DistanceTo(Transform t)
        => Self != null && t != null ? Vector3.Distance(Self.position, t.position) : 999f;

    // The one movement implementation in the project. Actions only say "go there".
    public bool MoveTowards(Vector3 target)
    {
        var flat  = new Vector3(target.x, Self.position.y, target.z);
        var delta = flat - Self.position;
        if (delta.sqrMagnitude <= ArriveDistance * ArriveDistance) return true;

        Self.position += delta.normalized * (Speed * Time.deltaTime);
        return false;
    }
}
```

`IMoveContext` unlocks `MoveTo(...)` inside actions; `ITickContext` unlocks `Steps.Wait(...)`. Both are optional.

## Step 2 — the facts: what the planner may reason about

A fact is one dense slot in the world state. Keep it to what actions change or goals test — facts are not a blackboard.

```csharp
using Instinct.GOAP;

public sealed class FarmerFacts
{
    private FarmerFacts() { }                 // sealed class, never instantiated

    public static readonly Fact<int>   Energy          = Fact<int>.Declare();
    public static readonly Fact<float> DistanceToHome  = Fact<float>.Declare();
    public static readonly Fact<float> DistanceToField = Fact<float>.Declare();
    public static readonly Fact<int>   CropsGrown      = Fact<int>.Declare();
}
```

Supported types: `bool`, `int`, `float`, and **any enum** whose underlying type fits in 32 bits. `Declare()` picks up the field name automatically.

## Step 3 — identity: keys instead of strings

```csharp
public static class FarmerActionKeys
{
    public static readonly ActionKey Harvest     = ActionKey.Declare();
    public static readonly ActionKey Rest        = ActionKey.Declare();
    public static readonly ActionKey WalkToField = ActionKey.Declare();
    public static readonly ActionKey WalkToHome  = ActionKey.Declare();
}

public static class FarmerGoalKeys
{
    public static readonly GoalKey WorkTheField = GoalKey.Declare();
    public static readonly GoalKey Recover      = GoalKey.Declare();
}
```

Keys compare as ints. Renaming one is a compile error, not a silently dead branch.

## Step 4 — actions: prediction and behaviour in one class

An action has two halves that must agree: what it **promises** the planner (`Setup`) and what it **does** in the game (`Run`).

```csharp
using Cysharp.Threading.Tasks;
using Instinct.GOAP;
using Instinct.GOAP.Unity;

public sealed class WalkToField : GoapAction<FarmerContext>
{
    protected override ActionKey Key => FarmerActionKeys.WalkToField;

    protected override void Setup()
    {
        Require(FarmerFacts.DistanceToField, Compare.Greater, 1f);
        Effect(FarmerFacts.DistanceToField, 0f);
        Cost(1f);
    }

    protected override async UniTask Run(FarmerContext c)
    {
        if (c.Field == null) Fail("field is not assigned");
        await MoveTo(c, () => c.Field.position);   // target re-read every frame
    }
}

public sealed class Harvest : GoapAction<FarmerContext>
{
    protected override ActionKey Key => FarmerActionKeys.Harvest;

    protected override void Setup()
    {
        Require(FarmerFacts.DistanceToField, Compare.LessOrEqual, 1f);
        Require(FarmerFacts.Energy, Compare.GreaterOrEqual, 25);
        Add(FarmerFacts.Energy, -25, min: 0, max: 100);
        Add(FarmerFacts.CropsGrown, +1);
        Cost(1f);
    }

    protected override async UniTask Run(FarmerContext c) => await Wait(0.5f);
}

public sealed class Rest : GoapAction<FarmerContext>
{
    protected override ActionKey Key => FarmerActionKeys.Rest;

    protected override void Setup()
    {
        Require(FarmerFacts.DistanceToHome, Compare.LessOrEqual, 1f);
        Effect(FarmerFacts.Energy, 100);
        Cost(0.5f);
    }

    protected override async UniTask Run(FarmerContext c) => await Wait(1f);
}
```

Rules inside `Run`:

- reaching the end of the method = **success**;
- `Fail("reason")` = **failure** — the agent drops the plan and replans;
- a replan cancels the token, so every `await` simply stops where it stood — no manual cleanup;
- `Wait`, `NextFrame`, `Until`, `While`, `MoveTo`, `Timeout` are available as protected members.

## Step 5 — the domain: bind facts, register actions, declare goals

```csharp
public static class FarmerBrain
{
    public static GoapDomain<FarmerContext> Build()
    {
        var d = GoapDomainBuilder<FarmerContext>.For<FarmerFacts>();

        // Two-way: world → state before planning, action effects → world after success.
        d.Bind(FarmerFacts.Energy,     c => c.Energy,     (c, v) => c.Energy = v);
        d.Bind(FarmerFacts.CropsGrown, c => c.CropsGrown, (c, v) => c.CropsGrown = v);

        // Derived values are read-only: movement changes them, not an effect.
        d.Bind(FarmerFacts.DistanceToHome,  c => c.DistanceTo(c.Home));
        d.Bind(FarmerFacts.DistanceToField, c => c.DistanceTo(c.Field));

        d.Use(new WalkToField(), new WalkToHome(), new Harvest(), new Rest());

        d.Goal(FarmerGoalKeys.WorkTheField)
            .Satisfy(FarmerFacts.Energy, Compare.LessOrEqual, 15)
            .RelevantWhen(s => s.Get(FarmerFacts.Energy) >= 45)
            .Priority(40)
            .Heuristic(s => (s.Get(FarmerFacts.Energy) - 15) / 60f);

        d.Goal(FarmerGoalKeys.Recover)
            .Satisfy(FarmerFacts.Energy, Compare.GreaterOrEqual, 100)
            .RelevantWhen(s => s.Get(FarmerFacts.Energy) < 45)
            .Priority(80)
            .Heuristic(s => (100 - s.Get(FarmerFacts.Energy)) / 100f);

        return d.Build();
    }
}
```

Two things worth internalising now:

1. **Relevance windows must not leave a gap.** `>= 45` works, `< 45` rests — every energy value belongs to some goal. Leave a hole and the agent freezes with no plan at exactly that value.
2. **Build the domain per agent, not statically.** Actions hold per-run state (cancellation tokens, step timers). One shared domain across ten NPCs mixes their timers.

## Step 6 — the host: one call per frame

```csharp
using Instinct.GOAP;
using UnityEngine;

public sealed class FarmerHost : MonoBehaviour
{
    [SerializeField] private Transform home, field;

    private FarmerContext _ctx;
    private GoapBrain<FarmerContext> _brain;

    private void Awake()
    {
        _ctx = new FarmerContext { Self = transform, Home = home, Field = field };

        _brain = new GoapBrain<FarmerContext>(
            FarmerBrain.Build(),
            _ctx,
            new GoapPlanner(maxIterations: 100, maxDepth: 6),
            new FarmerPolicy());

        var issues = _brain.Domain.Describe();          // null when the domain is clean
        if (!string.IsNullOrEmpty(issues)) Debug.LogWarning(issues);
    }

    private void Update() => _brain.Tick();

    [ContextMenu("Explain last decision")]
    private void Explain() => Debug.Log(_brain.ExplainDecision());
}
```

`FarmerPolicy` adds stickiness so the agent does not flip between goals every frame:

```csharp
public sealed class FarmerPolicy : IAgentPolicy
{
    public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state) => false;

    public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
        => currentGoal != null && goal.Key == currentGoal.Key ? 2f : 0f;

    public void OnPlanCleared(IAgentContext context) { }
}
```

## Step 7 — watch it think

Press Play, then:

- `Window → Analysis → GOAP Graph` — actions → facts → goals, with the live plan highlighted;
- right-click the component → **Explain last decision**:

```
chosen: WorkTheField  cost=2  plan=WalkToField -> Harvest
  ok   WorkTheField    40 - 2 = 38  (len 2)
  --   Recover         not relevant
```

## Where to go next

- [Core Concepts](03-concepts.md) — what the planner actually does with those numbers
- [Integration Guide](04-integration.md) — sensors, many agents, the low-level command API
- [Debugging](06-debugging.md) — when the plan is not what you expected
