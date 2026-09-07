# Debugging

[← Docs index](README.md)

GOAP fails differently from a state machine: nothing is "stuck in a state", the agent simply chose something you did not expect, or chose nothing. Three tools answer three different questions.

---

## 1. "Why this goal?" — `GoapExplain.Decision`

```csharp
Debug.Log(GoapExplain.Decision(agent.GoalEvaluations, agent.CurrentPlan));
// Flow API:
Debug.Log(brain.ExplainDecision());
```

```
chosen: CatchIntruder  cost=2.75  plan=ChaseIntruder -> GrabIntruder
  ok   CatchIntruder      100 - 2.8 = 97.2  (len 2)
  ..   Patrol             skipped, best case 10 could not beat the winner
  !!   CallBackup         no plan (Unreachable)
  --   InvestigateNoise   not relevant
```

| Marker | Meaning | Usual cause when it surprises you |
|---|---|---|
| `ok` | planned and scored | — |
| `..` | pruned — its best possible score could not beat the winner | priority too low, or the winner's plan is unrealistically cheap |
| `!!` | relevant, but no plan | see the failure reason below |
| `--` | `RelevantWhen` returned false | the relevance window has a gap |

Wire it to a `[ContextMenu]` on your host; it costs nothing until you click it.

## 2. "Why no plan?" — `PlanFailure`

| Failure | What it actually means | First thing to check |
|---|---|---|
| `Unreachable` | the search space was exhausted | a precondition depends on a fact only the world can write, or an action's effects are dishonest |
| `DepthLimit` | every branch hit `maxDepth` | count the honest chain by hand; raise `maxDepth`, or clamp the counter driving the length (`Add(..., max:)`) |
| `IterationLimit` | budget spent before the goal was reached | weak or missing heuristic, far more often than a real dead end |
| `AlreadySatisfied` | the goal is already true | usually a goal that should have been gated by `RelevantWhen` |

`GoapExplain.Failure(goal, planner.LastFailure)` prints that as a sentence.

## 3. "Why is this action never used?" — `GoapExplain.Applicability`

```csharp
Debug.Log(GoapExplain.Applicability(actions, stateProvider.GetState()));
```

```
  OK   ChaseIntruder
  --   GrabIntruder   blocked by: DistanceToIntruder <= 1.5
  --   CallBackup     blocked by: HasRadio == true, BackupCalled == false
```

This is the state *right now*, not inside the search — so it explains "why did the plan not start", not "why is step 4 missing".

## 4. "What does the agent think the world is?" — `GoapExplain.State`

```csharp
Debug.Log(GoapExplain.State<GuardFacts>(stateProvider.GetState()));
// AlertLevel=Hunting  IntruderVisible=true  Ammo=3  DistanceToIntruder=4.2
```

Nine times out of ten, an inexplicable decision is a sensing bug, not a planning bug: the state the planner saw was not the world you were looking at.

## 5. The GOAP Graph window

`Window → Analysis → GOAP Graph`

- **Domain dropdown** — every `IGoapGraphSource` in the project. About 15 lines to add one (see [API Reference](05-api-reference.md#igoapgraphsource--goapgraphsource)).
- **Columns** — actions → facts → goals, with edges for reads and writes.
- **Live highlight** — while playing, the running plan is highlighted along the graph.
- **Inspector** — click a node for its full preconditions, effects and traits.
- **Search** highlights matches and fades the rest; hovering a node fades everything it is not wired to.
- **View menu** — toggle the facts column, each edge type, live highlight, dimming, mini-map, inspector.
- **Layout** — drag nodes; positions persist per domain in `UserSettings/InstinctGOAP/`. `Alt+S` saves, `Alt+R` resets.

What the graph cannot show: `PredicateCondition` and `DynamicEffect` have no `Subject`, so they draw no edge. If a node looks disconnected but works, that is why — and it is a good argument for using typed conditions wherever you can.

## 6. Validation as a permanent guard

```csharp
var issues = domain.Describe();                       // or new DomainBuilder()...Describe()
if (!string.IsNullOrEmpty(issues)) Debug.LogWarning($"[ai] {issues}");
```

`Describe()` returns `null` when the domain is clean, so this line is free in a shipping build. Also put it in an EditMode test — the check that catches the classic "a goal exists as a factory method but was never added to the library, so it can never fire" bug is `DeclaredGoalsIn(typeof(MyGoalKeys))`.

## 7. Logging plan changes without spamming

```csharp
private void LogWhenActionChanges()
{
    var key = _brain.CurrentAction?.Key ?? default;
    if (key == _loggedAction) return;
    _loggedAction = key;
    Debug.Log($"goal={_brain.CurrentGoal.NameOf()} plan={_brain.PlanChain()} step={key}");
}
```

`GoapAgentHost<TCommand>` does the equivalent for the Command API behind its `_logPlan` toggle.

## 8. Reproducing a decision in a test

The planner is deterministic and engine-free, so any misbehaviour can be pinned in an EditMode test:

```csharp
[Test] public void ChasesInsteadOfPatrollingWhenIntruderVisible()
{
    var state = WorldState.For<GuardFacts>()
        .Set(GuardFacts.IntruderVisible, true)
        .Set(GuardFacts.DistanceToIntruder, 8f);

    var plan = new GoapPlanner(200, 6).BuildPlan(GuardActions.All(), CatchIntruder, state);

    Assert.AreEqual(GuardActionKeys.ChaseIntruder, plan.Actions[0].Key);
}
```

This is the fastest debugging loop the framework offers: no play mode, no scene, milliseconds per run.
