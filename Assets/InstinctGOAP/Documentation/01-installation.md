# Installation

[← Docs index](README.md)

## Requirements

| | |
|---|---|
| Unity | 2022.3 LTS or newer |
| Scripting backend | Mono or IL2CPP |
| Core dependency | none |
| Optional dependencies | UniTask 2.5+ (async action layer) and TextMesh Pro (sample labels) |

Only the async layer needs UniTask, and only the samples need TextMesh Pro. The engine-free `Instinct.GOAP` core and the `Instinct.GOAP.Unity` layer (`GoapAgentHost<TCommand>`, `UnitySteps`, `IMoveContext`) reference neither.

`Instinct.GOAP.Unity.Async` — `GoapAction<TCtx>`, `AsyncStep<TCtx>`, `GoapAwait`, `RunAsync` — is guarded by the `INSTINCT_UNITASK` scripting define. Without UniTask the assembly is skipped instead of failing to compile, and the same applies to the Farmer sample. Installing UniTask raises the define automatically: through `versionDefines` for a Package Manager install, and through an editor hook for a `.unitypackage` install that drops UniTask straight into `Assets/`.

## Install from a Git repository

Open `Window → Package Manager`, select `+`, choose `Add package from git URL`, and enter:

```
https://github.com/AndriiSviatenko/Instinct-GOAP-Framework.git?path=Assets/InstinctGOAP
```


## Install from the Asset Store

Import the package through `Window → Package Manager → My Assets`. UniTask is not bundled — install it separately if you want the async action layer and the Farmer sample:

```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

Everything else compiles and runs without it.

## Install by copying

Copy the `InstinctGOAP` folder anywhere under `Assets/`:

```
Assets/InstinctGOAP/
├── Runtime/Core/     Instinct.GOAP
├── Runtime/Unity/    Instinct.GOAP.Unity
├── Runtime/Unity/Async/  Instinct.GOAP.Unity.Async  (needs UniTask)
├── Editor/           Instinct.GOAP.Editor
├── Samples/          Chef · Farmer · Guard · Stalker
├── Tests/EditMode/   Instinct.GOAP.Tests
└── Documentation/   documentation and offline handbook
```

The code does not rely on a fixed installation path.

## Reference the assemblies

Add these references to your gameplay asmdef:

- `Instinct.GOAP` for the planner, state, goals, actions, policies, validation, explanations, and Flow domain types.
- `Instinct.GOAP.Unity` for Unity movement steps, `IMoveContext`, or `GoapAgentHost<TCommand>`.
- `Instinct.GOAP.Unity.Async` for `GoapAction<TCtx>` and the other UniTask-based async types. An asmdef that references it needs the same `INSTINCT_UNITASK` define constraint, otherwise it will not compile in a project without UniTask.

Projects that use `Assembly-CSharp` can access both auto-referenced assemblies without extra setup.

## Verify the installation

Open `Window → Analysis → GOAP Graph`. Then open any scene under `Samples`, enter Play Mode, and confirm that the status label updates.

The automated suite is available under `Window → General → Test Runner → EditMode`.

## Core-only installation

For a project that needs only the engine-free planner, copy `Runtime/Core` and its asmdef. Do not copy `Runtime/Unity`, `Samples`, or their asmdefs. The core then has no UnityEngine, UniTask, or TextMesh Pro dependency.

## Uninstalling

Remove the package through Package Manager or delete the copied folder. Editor graph layouts in `UserSettings/InstinctGOAP` and view toggles in `EditorPrefs` may remain; they do not affect the project.
