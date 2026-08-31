# Installation

[← Docs index](README.md)

## Requirements

| | |
|---|---|
| Unity | 2022.3 LTS or newer |
| Scripting backend | Mono or IL2CPP |
| Core dependency | none |
| Package dependencies | UniTask 2.5.9 and TextMesh Pro 3.0.6 |

The package manifest installs UniTask and TextMesh Pro automatically. The engine-free `Instinct.GOAP` core does not reference either package. UniTask powers the async Unity layer, while TextMesh Pro is used by the sample labels.

## Install from a Git repository

Open `Window → Package Manager`, select `+`, choose `Add package from git URL`, and enter:

```
https://github.com/AndriiSviatenko/Instinct-GOAP-Framework.git?path=Assets/InstinctGOAP
```


## Install by copying

Copy the `InstinctGOAP` folder anywhere under `Assets/`:

```
Assets/InstinctGOAP/
├── Runtime/Core/     Instinct.GOAP
├── Runtime/Unity/    Instinct.GOAP.Unity
├── Editor/           Instinct.GOAP.Editor
├── Samples/          Chef · Farmer · Guard · Stalker
├── Tests/EditMode/   Instinct.GOAP.Tests
└── Documentation/   documentation and offline handbook
```

The code does not rely on a fixed installation path.

## Reference the assemblies

Add these references to your gameplay asmdef:

- `Instinct.GOAP` for the planner, state, goals, actions, policies, validation, explanations, and Flow domain types.
- `Instinct.GOAP.Unity` for async actions, Unity movement steps, or `GoapAgentHost<TCommand>`.

Projects that use `Assembly-CSharp` can access both auto-referenced assemblies without extra setup.

## Verify the installation

Open `Window → Analysis → GOAP Graph`. Then open any scene under `Samples`, enter Play Mode, and confirm that the status label updates.

The automated suite is available under `Window → General → Test Runner → EditMode`.

## Core-only installation

For a project that needs only the engine-free planner, copy `Runtime/Core` and its asmdef. Do not copy `Runtime/Unity`, `Samples`, or their asmdefs. The core then has no UnityEngine, UniTask, or TextMesh Pro dependency.

## Uninstalling

Remove the package through Package Manager or delete the copied folder. Editor graph layouts in `UserSettings/InstinctGOAP` and view toggles in `EditorPrefs` may remain; they do not affect the project.
