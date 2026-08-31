<div align="center">

# 🧠 Instinct
### GOAP Framework for Unity — Goal-Oriented Action Planning, and nothing else

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Engine-free core](https://img.shields.io/badge/Core-no%20UnityEngine-blue)]()
[![Scripts](https://img.shields.io/badge/Scripts-70-blue)]()

**[📖 Documentation](Documentation/README.md)** · **[🚀 Quick Start](Documentation/02-quickstart.md)** · **[📚 API Reference](Documentation/05-api-reference.md)** · **[🇺🇦 Гайд українською](Documentation/GUIDE-UA.md)** · **[🎮 Samples](Samples)**

</div>

---

## What is this?

A small, allocation-conscious GOAP planner for Unity. You describe **facts**, **actions** and **goals**; it decides what the agent does next and tells you *why*. No behaviour-tree editor, no ScriptableObject drag-and-drop — code-first domain, engine-free core.

Identity is typed end to end — there is no `Name` string to compare, so a renamed action is a compile error instead of a branch that silently stops firing.

---

## Features

| System | What it does |
|--------|-------------|
| 🎯 **Forward A\*** | Binary-heap open list, dictionary closed set, pooled search nodes — each expansion still clones a `WorldState` |
| 🏷️ **Typed identity** | `ActionKey` / `GoalKey` handles, int comparison, no string names anywhere |
| 📊 **Typed facts** | `Fact<bool>`, `Fact<int>`, `Fact<float>` and **any enum**, packed into a dense per-domain slot array |
| ⚖️ **Utility over planning** | Every relevant goal is planned; winner is best `Priority − Cost`, with exact pruning that skips most A\* runs |
| 🔗 **Honest effects** | `Effect`, `Copy`, `Add`, `Computed`, `DynamicEffect` — all resolved against the pre-action state, so declaration order cannot matter |
| 🔍 **Typed conditions** | `Require(fact, Compare.Greater, 0)` stays visible to tooling, unlike a lambda |
| 🧭 **Policy hooks** | When to abandon a still-legal plan, and how sticky the running goal is |
| 🩺 **Domain validation** | Duplicate keys, effect-less actions, unsatisfiable goals, and goals declared but never registered |
| 💬 **Explain** | Per-goal score, plan length, and *why* a goal produced no plan — unreachable vs out-of-depth vs out-of-iterations |
| 🪟 **Graph window** | Domain-agnostic editor graph: actions → facts → goals, live plan highlight, search, inspector, saved layout |
| 🎮 **Working sample** | A full guard NPC: senses, blackboard, actions, goals, executor, host MonoBehaviour |

---

## Quick Start

```csharp
// 1. Facts — one dense slot each. bool, int, float or any enum.
public sealed class GuardFacts
{
    public static readonly Fact<Alert> AlertLevel         = Fact<Alert>.Declare();
    public static readonly Fact<bool>  IntruderVisible    = Fact<bool>.Declare();
    public static readonly Fact<bool>  IntruderCaught     = Fact<bool>.Declare();
    public static readonly Fact<float> DistanceToIntruder = Fact<float>.Declare();
}

// 2. Identity — declared once, compared as ints. No strings anywhere.
public static class GuardGoalKeys
{
    public static readonly GoalKey CatchIntruder = GoalKey.Declare();
}

public static class GuardActionKeys
{
    // An action built by the builder declares its key here...
    public static readonly ActionKey ChaseIntruder = ActionKey.Declare();

    // ...and an action that is its own class needs no declaration at all:
    // the class IS the identity.
    public static readonly ActionKey GrabIntruder = ActionKey.Of<GrabIntruder>();
}

// 3. Actions — preconditions, effects, and what they cost RIGHT NOW.
var chase = ActionBuilder.Create(GuardActionKeys.ChaseIntruder)
    .Require(GuardFacts.IntruderVisible, true)
    .Require(GuardFacts.DistanceToIntruder, Compare.Greater, 1.6f)
    .Effect(GuardFacts.DistanceToIntruder, 1.5f)
    .Effect(GuardFacts.AlertLevel, Alert.Hunting)
    .Cost((s, ctx) => 1f + s.Get(GuardFacts.DistanceToIntruder) * 0.25f)
    .Build();

// 4. Goals — a condition, a priority, and (please) a heuristic.
var catchIntruder = GoalBuilder.Create(GuardGoalKeys.CatchIntruder)
    .Satisfy(GuardFacts.IntruderCaught, true)
    .RelevantWhen(s => s.Get(GuardFacts.IntruderVisible))
    .Priority(100f)
    .Heuristic(_ => 1f)
    .Build();

// 5. The loop is already written.
var agent = new GoapAgent<GuardCommand>(
    new GoapPlanner(maxIterations: 200, maxDepth: 6),
    GuardGoals.All(), GuardActions.All(),
    new GuardStateProvider(board),   // IWorldStateProvider: world → WorldState
    new GuardExecutor(board));       // IActionExecutor<T>: IAction → your command

GuardCommand command = agent.Tick();
```

---

## Two APIs, one planner

The snippet above is the **Command API**: you write a state provider, an executor and a command type, and the agent hands you a command each tick. It has no dependencies and it is the layer with shipping-game mileage.

The **Flow API** puts the prediction and the behaviour of an action in one place, and binds facts to your world in both directions — no state provider, no executor, no command enum:

```csharp
var d = GoapDomainBuilder<FarmerContext>.For<FarmerFacts>();

// world -> facts, and the action's own effects -> world after it succeeds
d.Bind(FarmerFacts.Energy, c => c.Energy, (c, v) => c.Energy = v);
d.Bind(FarmerFacts.DistanceToField, c => c.DistanceTo(c.Field));   // derived: read-only

d.Use(new WalkToField(), new Harvest(), new Rest());

d.Goal(FarmerGoalKeys.WorkTheField)
    .Satisfy(FarmerFacts.Energy, Compare.LessOrEqual, 15)
    .RelevantWhen(s => s.Get(FarmerFacts.Energy) >= 45)
    .Priority(40)
    .Heuristic(s => (s.Get(FarmerFacts.Energy) - 15) / 60f);

// an action: what it promises the planner, and what it does in the game
public sealed class Harvest : GoapAction<FarmerContext>
{
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

// the whole host
private void Update() => _brain.Tick();
```

Behaviour is plain `async` code with `if`/`while`/`await`; a replan cancels the token, so every `await` stops where it stood. Requires UniTask. Full walkthrough: **[Quick Start](Documentation/02-quickstart.md)**.

---

## Architecture

```
YOUR GAME
  StateProvider (world → facts)  ·  Executor (action → command)  ·  Policy
          │
AGENT LAYER
  GoapAgent<TCommand>
      ├── picks the goal        utility = Priority − PlanCost, plus policy bias
      ├── holds the plan        abandons it when the policy or a precondition says so
      └── GoalEvaluation[]      what every goal scored, and why it lost
          │
PLANNER
  GoapPlanner : IPlanner        forward A*, pooled nodes and states
          │
DOMAIN
  Fact<T>  ·  ActionKey / GoalKey  ·  Condition<T>  ·  Effect<T>  ·  WorldState
```

---

## File Structure

```
InstinctGOAP/
├── package.json        UPM manifest — the version lives here
├── LICENSE             MIT
├── Runtime/Core/       planner, agent loop, facts, keys, builders  (no UnityEngine)
│   └── Flow/           the Flow API: domain builder, brain, bindings, steps
├── Runtime/Unity/      async actions, movement, GoapAgentHost<TCommand>
├── Editor/             domain-agnostic graph window (Core / UI / Windows / Styles)
├── Samples/            Chef · Farmer · Guard · Stalker reference integrations
├── Documentation/      the docs set below, plus index.html — the offline handbook
└── Tests/EditMode/     planner semantics, keys, enum facts
```

---

## Installing into another project

Copy the plugin folder anywhere under `Assets/`. Nothing inside it depends on where it
lives — the graph window finds its stylesheet by asset search rather than by a hard-coded
path — so the folder can be named whatever suits the host project.

```
Assets/InstinctGOAP/          <- recommended name, but yours to choose
  Runtime/Core/               Instinct.GOAP              (noEngineReferences)
  Runtime/Unity/              Instinct.GOAP.Unity
  Editor/                     Instinct.GOAP.Editor
  Samples/Guard/              Instinct.GOAP.Samples.Guard
```

Add `Instinct.GOAP` to your gameplay asmdef and you are done. `Samples/` and `Tests/` are
separate assemblies — deleting either cannot break the framework.

---

## Requirements

- Unity 2022.3+ (developed on Unity 6000.0)
- `Instinct.GOAP` (the core) — **no dependencies**, and `noEngineReferences: true`: it compiles outside Unity, which is what lets it be unit-tested in a plain .NET test project
- `Instinct.GOAP.Unity` — requires [UniTask](https://github.com/Cysharp/UniTask) 2.5+ for the async action layer. A dependency-free setup is available as a [core-only installation](Documentation/01-installation.md#core-only-installation)
- Sample labels use TextMesh Pro; the package manifest installs it automatically

---

## Usage patterns

### Enum facts read like the state machine they replace

```csharp
public enum Alert { Calm = 0, Suspicious = 1, Hunting = 2 }

.Require(GuardFacts.AlertLevel, Compare.GreaterOrEqual, Alert.Suspicious)
.Effect(GuardFacts.AlertLevel, Alert.Hunting)
```

Stored as the underlying integer, so an ordered comparison is a real comparison, not a trick.

### Never ask "which action is this?" with a string

```csharp
// The class IS the identity — nothing to declare, nothing to misspell.
if (command.Source == ActionKey.Of<ChaseIntruder>()) { ... }

// Better still: put the answer on the declaration and stop asking.
public float Stickiness { get; set; } = 6f;
```

### Find the dead goal before the playtest does

```csharp
var report = new DomainBuilder()
    .AddActions(GuardActions.All())
    .AddGoals(GuardGoals.All())
    .DeclaredGoalsIn(typeof(GuardGoalKeys))    // declared but never registered → error
    .DeclaredActionsIn(typeof(GuardActionKeys))
    .Describe();                               // null when the domain is clean
```

### Ask why, instead of guessing

```csharp
Debug.Log(GoapExplain.Decision(agent.GoalEvaluations, agent.CurrentPlan));

// chosen: CatchIntruder  cost=2.75  plan=ChaseIntruder -> GrabIntruder
//   ok   CatchIntruder      100 - 2.8 = 97.2  (len 2)
//   ..   Patrol             skipped, best case 10 could not beat the winner
//   !!   CallBackup         no plan (Unreachable)
//   --   InvestigateNoise   not relevant
```

---

## Editor Tools

### GOAP Graph
`Window → Analysis → GOAP Graph`

Actions → facts → goals, with the running plan highlighted while playing.

- **Every domain in the project** appears in the dropdown — implement `IGoapGraphSource` (about 15 lines) and it shows up
- **Inspector panel** — click a node for its full preconditions, effects and traits
- **Search** highlights matches and fades the rest; **hover** fades everything a node is not wired to
- **View menu** toggles the facts column, each edge type, live highlight, dimming, mini-map, inspector
- **Layout is yours** — drag nodes, positions persist per domain in `UserSettings/`, `Alt+S` saves, `Alt+R` resets

---

## Rules that keep it working

1. **An effect must describe the real result.** `AllItemsMapped := true` on an action that visits one shelf makes every plan one action long — a GOAP engine reduced to a utility selector.
2. **A heuristic must never overestimate**, or A\* stops returning optimal plans. Assert it against a real plan's cost in a test.
3. **`maxDepth` must cover the longest honest chain.** Over it the planner returns *no plan*, not a shorter one.
4. **Never re-derive identity from text.** If gameplay code asks "which action issued this?", give the command a key or an enum.
5. **Do not mutate a state the planner is holding.** `ApplyTo` works on a clone; states in the closed set are frozen and throw in the editor if written to.

---

## Documentation

| Page | |
|---|---|
| **[Handbook (single page)](Documentation/index.html)** | **everything below as one offline HTML page** |
| [Installation](Documentation/01-installation.md) | requirements, asmdefs, core-only setup, UPM |
| [Quick Start](Documentation/02-quickstart.md) | a working agent in 15 minutes |
| [Core Concepts](Documentation/03-concepts.md) | facts, actions, goals, planner, goal selection |
| [Integration Guide](Documentation/04-integration.md) | wiring it into an existing game |
| [API Reference](Documentation/05-api-reference.md) | every public type |
| [Debugging](Documentation/06-debugging.md) | explain, validation, the graph window |
| [Performance](Documentation/07-performance.md) | measured numbers and budgets |
| [Troubleshooting](Documentation/08-troubleshooting.md) | symptom → cause |
| [Limits & FAQ](Documentation/09-limits-and-faq.md) | known limitations, is it right for you |

---

## Non-goals

No multithreading or job system (a plan costs microseconds; a shared mutable planner is not worth the class of bug threading brings). No backward chaining. No HTN, no behaviour trees, no ScriptableObject authoring layer.

---

## License

MIT — free for personal and commercial use. See [LICENSE](LICENSE).

---

<div align="center">
Made for Unity game developers · <a href="Documentation/index.html">Full docs →</a>
</div>
