# Troubleshooting

[← Docs index](README.md)

Symptom → cause, in the order these actually happen to people.

---

### The agent stands still and does nothing

Run `GoapExplain.Decision(...)` first. Then:

| What the scoreboard says | Cause | Fix |
|---|---|---|
| every goal `--` (not relevant) | your `RelevantWhen` windows leave a gap at the current value | make the windows tile the state space (`>= 45` / `< 45`, not `>= 45` / `< 20`) |
| goals `!!` with `Unreachable` | no action chain reaches the goal from here | check that some action writes the fact the goal tests; check that a precondition is not waiting on a fact only the world writes |
| goals `!!` with `DepthLimit` | the honest chain is longer than `maxDepth` | raise `maxDepth`, or clamp the counter driving the length |
| goals `!!` with `IterationLimit` | budget exhausted | add or improve the goal's heuristic; raise `maxIterations` |
| a goal is `ok` but nothing happens | the plan is produced but your executor does not run the command | Command API: check `Translate` returns a command your host actually executes and that you call `NotifyActionComplete` |

Also confirm the game is stuttering — a planless agent replans every frame, which is both the symptom and a profiler signature. See [Performance](07-performance.md#the-no-plan-trap).

---

### "Fact 'x' (id N) does not belong to this state's schema"

The state was built for a different fact class than the fact you passed.

- Two `Facts` classes and you built the state with the wrong one → `WorldState.For<TheRightFacts>()`.
- **Facts declared in an array or list** (`static readonly Fact<bool>[] All = {...}`) — the schema is discovered by reflecting over *fields of type `Fact<T>`*, so array elements are invisible and the schema comes out empty. Declare each fact as its own static field; keep arrays only as lookup helpers.
- A fact declared in one class but referenced through a helper that belongs to another schema.

---

### The plan is always one action long

An effect is lying. `Effect(EverythingDone, true)` on an action that does one step of the work makes the planner believe one step is enough — GOAP degraded into a utility selector.

Make the effect describe the real result: `Add(ShelvesMapped, +1)` plus a goal `Satisfy(ShelvesMapped, Compare.GreaterOrEqual, 8)`.

---

### The plan is not the cheapest one

The heuristic overestimates. A* only returns optimal plans when the heuristic never exceeds the true remaining cost.

```csharp
[Test] public void HeuristicIsAdmissible()
{
    var plan = planner.BuildPlan(actions, goal, start);
    Assert.LessOrEqual(goal.Heuristic(start, null) ?? 0f, plan.TotalCost);
}
```

Also check for a `Cost` lambda that returns different values for the same state (randomness, `Time.time`) — the search assumes cost is a function of state.

---

### The agent flickers between two goals every frame

Their utilities are nearly equal. Add stickiness in the policy:

```csharp
public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
    => currentGoal != null && goal.Key == currentGoal.Key ? 2f : 0f;
```

Start at ~5 % of your typical priority spread. If it still flickers, some fact is oscillating in the snapshot (a distance hovering on a threshold) — add hysteresis in the sensor, not in the planner.

---

### The agent commits to a plan while the world burns

The opposite problem. Use the interrupt hook:

```csharp
public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
    => state.Get(Facts.PlayerVisible) && plan.Goal.Key != GoalKeys.Chase;
```

---

### An action's effects do not appear in the world (Flow API)

Effect mirroring writes only through **two-way** bindings, and only for facts the action actually declared as effects.

- `Bind(fact, read)` with no writer → read-only; nothing is written back. That is correct for derived values, wrong for state the action owns.
- `NoMirror()` was called on the action.
- The effect is a `DynamicEffect` (its `Subject` is `null`) — that makes the framework write **all** writable bindings, which may not be what you wanted either.
- The action ended in `Failure`; mirroring happens only on success.

---

### An async action never finishes / restarts constantly

- Reaching the end of `Run` is success. If your body is an infinite `while (true)` loop it will never report success — that is legitimate for a "keep doing this" action only if some precondition eventually invalidates the plan.
- `Fail("...")` is the only way to report failure; a returned `bool` or a thrown `Exception` is not (a non-`GoapActionFailed` exception is logged and treated as failure).
- If it restarts every frame, the *plan* is being rebuilt every frame — the action is fine, the goal selection is not. Check stickiness and the no-plan trap.
- After a replan the token is cancelled, so `await` calls simply stop. Do not `catch (OperationCanceledException)` inside your action body and swallow it.

---

### `InvalidOperationException: This WorldState is frozen`

You wrote to a state the planner holds. Effects must write to the `next` state passed to `Apply`, never to `pre`; anything else must `Clone()` first. Note this check is compiled only in the Editor, development builds and DEBUG — a release player will silently corrupt the search instead, which is why the check exists.

---

### Two actions behave as one / "duplicate action key"

`ActionKey.Named("Chase")` interns by string: two calls with the same string are the *same* key. Prefer `ActionKey.Declare()` in a keys class or `ActionKey.Of<T>()`.

`DomainBuilder.Validate()` reports this as an error — run it.

---

### A goal never fires even though it looks correct

Classic cause: the goal was written as a factory method but never added to the list passed to the agent. Catch it permanently:

```csharp
new DomainBuilder()
    .AddGoals(MyGoals.All())
    .DeclaredGoalsIn(typeof(MyGoalKeys))   // declared but never registered → error
    .Describe();
```

---

### Compile error: `The type or namespace name 'Cysharp' could not be found`

The async layer lives in `Instinct.GOAP.Unity.Async` and needs UniTask. Install it through the Package Manager (`https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`), then let Unity recompile.

If UniTask is already installed and the error persists, the `INSTINCT_UNITASK` define did not get raised. Check `Edit → Project Settings → Player → Scripting Define Symbols` for the active build target, and add `INSTINCT_UNITASK` by hand if it is missing.

A project that cannot use UniTask can drop the async layer entirely: `Instinct.GOAP` and `Instinct.GOAP.Unity` compile without it, and actions are composed from `IStep<TCtx>` instead. See the [core-only installation](01-installation.md#core-only-installation).

---

### The GOAP Graph window is empty

The window lists domains that implement `IGoapGraphSource` with a public parameterless constructor. Add one for your domain (about 15 lines). If it throws while constructing, a warning appears in the console and the source is skipped.

---

### Ten NPCs share one NPC's timers

You built the domain once, statically, and handed it to every agent. Actions hold per-run state (cancellation tokens, `Wait` elapsed time, sequence indices). Build one domain per agent in `Awake`.

---

### Values reset after entering play mode / after a domain reload

Facts, keys and schemas are static and are rebuilt on domain reload; that is fine. What is *not* safe is persisting key ids or `WorldState` in a save file — ids depend on static-initialisation order. Save your context, restore it, and let the agent replan.
