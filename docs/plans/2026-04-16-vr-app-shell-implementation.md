# VR App Shell Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a modular VR application shell that orchestrates the existing public speaking scenes and systems without replacing their working business logic.

**Architecture:** Add a persistent runtime state layer, a main-hub flow layer, and scene integration adapters. Preserve the current environment scenes and existing tracking/scoring/session scripts, exposing only the minimum shell-facing hooks needed for orchestration.

**Tech Stack:** Unity, C#, Unity UI, TextMeshPro, existing XR/XRI project setup

---

### Task 1: Create persistent data models

**Files:**
- Create: `Assets/AppShell/Runtime/Data/SessionEnums.cs`
- Create: `Assets/AppShell/Runtime/Data/AppEnvironmentDefinition.cs`
- Create: `Assets/AppShell/Runtime/Data/SessionConfig.cs`
- Create: `Assets/AppShell/Runtime/Data/SessionRuntimeState.cs`
- Create: `Assets/AppShell/Runtime/Data/SessionResultSummary.cs`

**Steps:**
- Define enums for practice mode, difficulty, audience preset, feedback level, and panel ids.
- Define serializable environment definition data used by the hub.
- Define serializable session config, runtime state, and result summary containers.
- Add cloning/reset helpers where needed.

### Task 2: Create persistent runtime root

**Files:**
- Create: `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`

**Steps:**
- Add a `DontDestroyOnLoad` runtime state singleton.
- Store current session config, runtime state, selected environment, and last result summary.
- Expose safe mutation helpers and change events.

### Task 3: Build panel shell primitives

**Files:**
- Create: `Assets/AppShell/Runtime/UI/AppPanelView.cs`
- Create: `Assets/AppShell/Runtime/UI/UIStateController.cs`
- Create: `Assets/AppShell/Runtime/UI/HomePanelPresenter.cs`
- Create: `Assets/AppShell/Runtime/UI/PracticeModePanelPresenter.cs`
- Create: `Assets/AppShell/Runtime/UI/EnvironmentCardView.cs`
- Create: `Assets/AppShell/Runtime/UI/EnvironmentSelectionController.cs`
- Create: `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- Create: `Assets/AppShell/Runtime/UI/ReadyPanelPresenter.cs`

**Steps:**
- Create reusable panel visibility primitives.
- Add panel registration and switching.
- Add home panel entry methods.
- Add practice mode availability handling.
- Add environment card binding and selection state.
- Add session setup UI-to-config writing.
- Add ready summary generation.

### Task 4: Build app flow and transition pipeline

**Files:**
- Create: `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- Create: `Assets/AppShell/Runtime/Flow/TransitionManager.cs`
- Create: `Assets/AppShell/Runtime/Flow/SessionLaunchController.cs`

**Steps:**
- Add main hub routing logic.
- Add fade overlay transition support.
- Validate config and launch the selected environment scene.
- Persist launch state before scene load.

### Task 5: Build environment scene integration layer

**Files:**
- Create: `Assets/AppShell/Runtime/Integration/PlayerRigAdapter.cs`
- Create: `Assets/AppShell/Runtime/Integration/TrackingAdapter.cs`
- Create: `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`
- Create: `Assets/AppShell/Runtime/Integration/ExistingSceneFlowAdapter.cs`
- Create: `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`

**Steps:**
- Find player rig and spawn point safely.
- Apply tracking/scoring toggles to scene systems.
- Capture summary data from current scoring systems.
- Bridge shell launch into `MainController`.
- Auto-stop sessions based on configured duration.

### Task 6: Build results routing layer

**Files:**
- Create: `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`
- Create: `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs`
- Create: `Assets/AppShell/Runtime/Results/ResultsFlowController.cs`

**Steps:**
- Add a lightweight dashboard integration wrapper.
- Add a presenter that renders `SessionResultSummary`.
- Add retry, change environment, dashboard, and hub routing methods.

### Task 7: Add shell-friendly hooks to existing runtime script

**Files:**
- Modify: `Assets/Scripts/eye_track/MainController.cs`

**Steps:**
- Add public session status properties.
- Add public wrappers for shell-driven start/stop/review actions.
- Add session lifecycle events for adapters.
- Preserve current keyboard/VR behavior.

### Task 8: Verify compilation surface and document Unity wiring

**Files:**
- Modify: `docs/plans/2026-04-16-vr-app-shell-design.md`

**Steps:**
- Run a local compile/build verification pass if available.
- Record any required inspector wiring notes.
- Keep the setup steps explicit and minimal.
