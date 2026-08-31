# Instinct GOAP — Documentation

Goal-Oriented Action Planning for Unity. Engine-free core, code-first domains, typed identity.

| # | Page | Read it when |
|---|------|--------------|
| 1 | [Installation](01-installation.md) | Putting the package into a project for the first time |
| 2 | [Quick Start](02-quickstart.md) | You want a working agent in ~15 minutes |
| 3 | [Core Concepts](03-concepts.md) | You want to understand facts, actions, goals, planning, goal selection |
| 4 | [Integration Guide](04-integration.md) | Wiring the framework into an existing game — sensors, movement, many agents |
| 5 | [API Reference](05-api-reference.md) | You need the exact signature of something |
| 6 | [Debugging](06-debugging.md) | The agent does something you did not expect |
| 7 | [Performance](07-performance.md) | Many agents, mobile, or a frame-time budget |
| 8 | [Troubleshooting](08-troubleshooting.md) | Symptom → cause lookup table |
| 9 | [Limits & FAQ](09-limits-and-faq.md) | Deciding whether this framework fits your game |

Other material:

- [`index.html`](index.html) — **the handbook**: everything on this list as one self-contained offline page. Also served on the web via GitHub Pages
- [`GUIDE-UA.md`](GUIDE-UA.md) — Ukrainian guide to the Command API, plus a short Flow API section
- [`../README.md`](../README.md) — the package readme: pitch, feature table, quick start
- [`../Samples/`](../Samples) — Guard, Chef and Stalker reference integrations
- [`../Samples/Farmer`](../Samples/Farmer) — the Flow API end to end, as a teaching agent

## Two APIs, one planner

The framework exposes the same planner through two layers. Pick one per agent; do not mix them in a single agent.

| | **Flow API** (recommended) | **Command API** (low-level) |
|---|---|---|
| You write | one domain file + action classes | facts, state provider, executor, command enum, host |
| Action behaviour | `async UniTask Run(ctx)` or `IStep<TCtx>` | your own code, driven by a command you translate |
| World ↔ facts | `Bind(fact, read, write)` | hand-written `IWorldStateProvider` |
| Entry point | `GoapBrain<TCtx>` | `GoapAgent<TCommand>` + `GoapAgentHost<TCommand>` |
| Dependencies | UniTask (for async actions) | core only, no UniTask |
| Best for | new agents, gameplay code | existing command/state-machine pipelines, DOTS-ish command buses, no-UniTask projects |

Everything below the entry point — planner, world state, goal selection, validation, the graph window — is shared.
