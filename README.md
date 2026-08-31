<div align="center">

# 🧠 Instinct GOAP

### Goal-Oriented Action Planning for Unity — and nothing else

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-MIT-green)](Assets/InstinctGOAP/LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-e8192c)](Assets/InstinctGOAP/package.json)
[![Core](https://img.shields.io/badge/Core-no%20UnityEngine-blue)]()
[![Docs](https://img.shields.io/badge/Docs-handbook-orange)](https://andriisviatenko.github.io/Instinct-GOAP-Framework/)

**[📖 Handbook](https://andriisviatenko.github.io/Instinct-GOAP-Framework/)** · **[🚀 Quick Start](Assets/InstinctGOAP/Documentation/02-quickstart.md)** · **[📚 API Reference](Assets/InstinctGOAP/Documentation/05-api-reference.md)** · **[🇺🇦 Гайд українською](Assets/InstinctGOAP/Documentation/GUIDE-UA.md)**

</div>

---

You describe **facts**, **actions** and **goals**. It decides what the agent does next, and tells you exactly why.

```csharp
// what the agent can do
public sealed class Harvest : GoapAction<FarmerContext>
{
    protected override void Setup()
    {
        Require(Facts.DistanceToField, Compare.LessOrEqual, 1f);
        Require(Facts.Energy, Compare.GreaterOrEqual, 25);
        Add(Facts.Energy, -25, min: 0, max: 100);
        Add(Facts.CropsGrown, +1);
        Cost(1f);
    }

    protected override async UniTask Run(FarmerContext c) => await Wait(0.5f);
}

// what it wants
d.Goal(GoalKeys.WorkTheField)
    .Satisfy(Facts.Energy, Compare.LessOrEqual, 15)
    .RelevantWhen(s => s.Get(Facts.Energy) >= 45)
    .Priority(40);

// the whole host
private void Update() => _brain.Tick();
```

Nowhere is it written “if tired, go home” — the agent finds `WalkToHome → Rest` on its own, and `GoapExplain` prints why it picked that over harvesting.

---

## What makes it different

| | |
|---|---|
| **Typed identity end to end** | `Fact<T>`, `ActionKey`, `GoalKey`. No `Name` string to compare — a renamed action is a compile error, not a branch that silently stops firing |
| **It explains itself** | Per-goal scoreboard with the reason each goal lost: pruned, unreachable, out of depth, out of iterations |
| **Static domain validation** | Duplicate keys, effect-less actions, unsatisfiable goals, and goals declared but never registered — caught before the playtest |
| **Engine-free core** | `noEngineReferences: true`. The planner compiles and unit-tests as a plain .NET library, outside Unity |
| **Two integration layers** | A high-level Flow API (bindings + async action classes), and a low-level command API for pipelines that already exist |
| **Measured, not claimed** | 0.35 ms and 34 KB per replan on a 57-fact / 18-action / 15-goal domain — the numbers and their method are in the docs |

---

## Install

**Copy the folder.** Drop [`Assets/InstinctGOAP`](Assets/InstinctGOAP) anywhere under your `Assets/`, add `Instinct.GOAP` to your gameplay asmdef, and you are done. Nothing inside depends on the folder path.

**Or as a UPM package.** The package manifest is [`Assets/InstinctGOAP/package.json`](Assets/InstinctGOAP/package.json):

```
Window → Package Manager → + → Add package from git URL
https://github.com/AndriiSviatenko/Instinct-GOAP-Framework.git?path=Assets/InstinctGOAP
```

The manifest installs **UniTask** for the async Unity layer and **TextMesh Pro** for sample labels. The core has no dependencies; a core-only setup is covered in [Installation](Assets/InstinctGOAP/Documentation/01-installation.md#core-only-installation).

---

## Documentation

| Page | |
|---|---|
| [Handbook (single page, offline)](Assets/InstinctGOAP/Documentation/index.html) | Everything below, as one HTML file that ships inside the package |
| [Installation](Assets/InstinctGOAP/Documentation/01-installation.md) | Requirements, asmdefs, core-only setup, UPM |
| [Quick Start](Assets/InstinctGOAP/Documentation/02-quickstart.md) | A working agent in 15 minutes |
| [Core Concepts](Assets/InstinctGOAP/Documentation/03-concepts.md) | Facts, actions, goals, the planner, goal selection |
| [Integration Guide](Assets/InstinctGOAP/Documentation/04-integration.md) | Wiring it into a game that already exists |
| [API Reference](Assets/InstinctGOAP/Documentation/05-api-reference.md) | Every public type |
| [Debugging](Assets/InstinctGOAP/Documentation/06-debugging.md) | Explain, validation, the graph window |
| [Performance](Assets/InstinctGOAP/Documentation/07-performance.md) | Measured numbers, budgets, the no-plan trap |
| [Troubleshooting](Assets/InstinctGOAP/Documentation/08-troubleshooting.md) | Symptom → cause |
| [Limits & FAQ](Assets/InstinctGOAP/Documentation/09-limits-and-faq.md) | Known limitations, and whether this fits your game |

---

## What is in this repository

This is the **development project**: a Unity project that contains the plugin, its samples, its tests and a teaching course. Only one folder ships.

```
Assets/InstinctGOAP/        the plugin — this is what you copy into your game
  Runtime/Core/             Instinct.GOAP           planner, agent, facts, keys, builders
  Runtime/Core/Flow/        the Flow API            domain builder, brain, bindings, steps
  Runtime/Unity/            Instinct.GOAP.Unity     async actions, movement, MonoBehaviour host
  Editor/                   Instinct.GOAP.Editor    the GOAP Graph window
  Samples/                  Guard · Chef · Stalker · Farmer (course) — each with a demo scene
  Tests/EditMode/           planner semantics, keys, enum facts, sample domains
  Documentation/            the docs set + the offline handbook
.github/workflows/pages.yml publishes Documentation/ to GitHub Pages — no second copy
Tools/CoreCheck/            dotnet build of the engine-free core — the no-UnityEngine proof
```

---

## Samples

| Sample | Shows |
|---|---|
| **Guard** | The full Command API integration: senses → blackboard → facts, actions, goals, executor, policy, host |
| **Chef** | A cooking domain: planner chains plus a live scene demo. The one to read first |
| **Stalker** | A survival agent: hunger, emissions, artifacts, combat, trade — a domain large enough to hurt |
| **Farmer** (course) | The Flow API end to end: bindings, async action classes, `GoapBrain`, one-line host |

Every sample ships as a playable scene — open one and press **Play**:

- [`Assets/InstinctGOAP/Samples/Guard/Guard_Patrol_Demo.unity`](Assets/InstinctGOAP/Samples/Guard/Guard_Patrol_Demo.unity) — a full perimeter micro-sim: an intruder skulks the ring, flees when spotted and breaks line of sight behind the walls → patrol → spot → chase → grab → custody → respawn, with ambient noises, yellow noise pings, radio backup and a live status label
- [`Assets/InstinctGOAP/Samples/Chef/Chef_Kitchen_Demo.unity`](Assets/InstinctGOAP/Samples/Chef/Chef_Kitchen_Demo.unity) — a guest walks in and waits hungry → cook → cross the room to serve → the guest leaves happy; the chef rests in the break corner — color-coded zones with labels and a live status label
- [`Assets/InstinctGOAP/Samples/Stalker/Stalker_ALife_Demo.unity`](Assets/InstinctGOAP/Samples/Stalker/Stalker_ALife_Demo.unity) — the A-Life survival demo: spots and fights threats, works the anomaly, sells artifacts, restocks, eats, sleeps, hides from emissions — a full autonomous loop with a live status label
- [`Assets/InstinctGOAP/Samples/Farmer/FarmerGoap.unity`](Assets/InstinctGOAP/Samples/Farmer/FarmerGoap.unity) — the course farmer in a living field: crops ripen on a timer (gold stalks), harvest knocks them down and consumes a ripe one, the farmer works until tired and naps at home — the Flow API end to end with a live status label

Each sample is its own assembly. Deleting any of them cannot break the framework.

---

## Editor tooling

`Window → Analysis → GOAP Graph` — actions → facts → goals, with the running plan highlighted while playing. Every domain in the project appears in the dropdown; implementing `IGoapGraphSource` takes about 15 lines. Node layouts are saved per domain.

---

## Requirements

- Unity 2022.3 LTS or newer (developed on Unity 6000.0), Mono and IL2CPP
- [UniTask](https://github.com/Cysharp/UniTask) 2.5+ — only for the async action layer

---

## License

MIT — free for personal and commercial use. See [LICENSE](Assets/InstinctGOAP/LICENSE).
