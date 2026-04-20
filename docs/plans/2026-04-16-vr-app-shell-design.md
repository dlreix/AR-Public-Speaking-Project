# VR App Shell Design

**Date:** 2026-04-16
**Project:** Unity-based VR Public Speaking Training
**Purpose:** Persist the approved product/frontend brief and the agreed minimal-risk integration design.

## Source Brief Summary

The requested work is a product-grade VR application shell that unifies the current project into one coherent user flow:

App Launch -> Main Hub / Lobby -> Practice Mode -> Environment Selection -> Session Setup -> Ready -> Session Launch -> In-Session HUD -> Session End -> Results Summary -> Dashboard Entry -> Retry / Return

Key constraints from the brief:

- Treat this as the full product frontend, not a demo menu.
- Do not rewrite or replace working backend/business systems unless necessary.
- Build a modular presentation, flow, and integration layer on top of the current project.
- Adapt to the existing scene and script structure instead of forcing a clean-slate rewrite.
- Prioritize safe wrappers/adapters around current scoring, tracking, and session systems.

## Repository Findings

Current repository state:

- Existing environment scenes:
  - `Assets/Scenes/Scene_Classroom.unity`
  - `Assets/Scenes/Scene_ConferenceHall.unity`
  - `Assets/Scenes/Scene_MeetingRoom.unity`
- No clear product-level hub/bootstrap/results scene currently exists.
- Existing runtime/session logic is centered around:
  - `MainController`
  - `EyeTrackingSystem`
  - `GazeScoringSystem`
  - `PerformanceScoringEngine`
  - `CircleEventSystem`
  - `PlayerController`
- Legacy/debug-style view switching paths also exist:
  - `ViewModeSwitcher`
  - `ModeSpawnSwitcher`
  - `EyeContactAdapter`
  - `eye_track.prefab`

Main risks found:

- Scene-to-scene wiring is inconsistent.
- Some prefab paths contain incomplete inspector references.
- Current HUD/session control is monolithic inside `MainController`.
- There is no central runtime state or scene-launch orchestration layer.
- VR UI interaction infrastructure for a product shell is not yet established.

## Recommended Architecture

### Layers

1. Presentation Layer
   - World-space VR UI panels
   - Environment cards
   - Setup controls
   - Ready summary
   - Minimal in-session HUD
   - Results summary

2. Flow / Navigation Layer
   - Panel switching
   - Back-stack and routing
   - Session launch entry
   - Results routing

3. Integration Layer
   - Adapters that connect UI actions to the existing project scripts
   - Runtime state handoff into environment scenes
   - Existing system discovery and safe configuration

4. Existing Core Systems Layer
   - Existing tracking, scoring, session, player, and scene logic
   - Preserved as the active business logic path

### Scene Strategy

Recommended structure:

- `MainHubScene`
  - New VR lobby and application shell
- Existing environment scenes
  - Keep and integrate
- Optional `ResultsScene`
  - Dedicated summary/dashboard entry scene if needed later

This design keeps current environment scenes intact and adds a clean frontend shell above them.

## Manager Breakdown

- `AppRuntimeState`
  - Persistent runtime state container
  - Holds current config, active runtime state, and last session result
- `AppFlowManager`
  - Main hub routing and product-level navigation
- `UIStateController`
  - Manages active/inactive panels and back navigation
- `EnvironmentSelectionController`
  - Environment card selection and preview state
- `SessionConfigController`
  - Reads/writes setup UI into `SessionConfig`
- `SessionLaunchController`
  - Validates config and launches selected environment
- `TransitionManager`
  - Fade overlay and motion-safe scene transitions
- `EnvironmentSceneInstaller`
  - Finds current scene systems and applies runtime config
- `ExistingSceneFlowAdapter`
  - Bridges shell flow into `MainController`
- `TrackingAdapter`
  - Applies tracking-related configuration to current scene systems
- `ScoringAdapter`
  - Collects session summary data from current scoring systems
- `PlayerRigAdapter`
  - Applies spawn point and player positioning
- `ResultsFlowController`
  - Retry, change environment, dashboard entry, and return-to-hub paths

## Panel Hierarchy

Main hub panel structure:

- `HomePanel`
- `PracticeModePanel`
- `EnvironmentSelectionPanel`
- `SessionSetupPanel`
- `ReadyPanel`
- `ProgressPanel`
- `SettingsPanel`
- `TransitionOverlay`

Later additions:

- `InSessionHUD`
- `ResultsSummaryPanel`

## Data Flow

1. UI updates `SessionConfig`
2. `SessionLaunchController` writes config into `AppRuntimeState`
3. Transition begins
4. Selected environment scene loads
5. `EnvironmentSceneInstaller` locates current scene systems
6. Installer applies config via adapters
7. `ExistingSceneFlowAdapter` starts the session using the existing runtime path
8. Session ends through current scene logic
9. `ScoringAdapter` builds `SessionResultSummary`
10. `ResultsFlowController` routes to retry, hub, or dashboard entry

## Integration Rules

Preserve existing logic:

- `EyeTrackingSystem`
- `GazeScoringSystem`
- `PerformanceScoringEngine`
- `CircleEventSystem`
- `PlayerController`
- Core scene-specific generators and content

Adapt instead of replace:

- `MainController`
  - Add shell-friendly public wrappers/events instead of rewriting it

Treat as legacy/debug paths unless explicitly elevated later:

- `ViewModeSwitcher`
- `ModeSpawnSwitcher`
- `EyeContactAdapter`
- partially wired legacy prefabs

## Phase Priority

### Phase 1

- Main hub shell
- Home panel
- Practice mode flow
- Environment selection flow
- Session setup flow
- Ready flow
- Central session config/runtime state

### Phase 2

- Session launch pipeline
- Transition manager
- Environment scene installer
- Existing scene integration adapters

### Phase 3

- In-session HUD
- Session end capture
- Results summary routing
- Dashboard entry points

### Phase 4

- Settings/progress polish
- Cleanup
- Better visual packaging

## First Stable Version Scope

The first stable implementation in this repository should deliver:

- The new `AppShell` code architecture
- Persistent runtime state and config models
- Main hub flow manager and panel state management
- Environment selection and session setup controllers
- Launch and transition pipeline
- Scene installer/adapters for the current environment scenes
- Safe `MainController` shell integration points

## Inspector and Unity Setup Expectations

Because this repo does not yet contain the final product hub scene/prefabs, the first stable version should favor:

- clean C# architecture
- inspector-driven references
- explicit setup instructions
- minimal modifications to existing environment scenes

This keeps the integration low-risk while preparing the project for full scene/prefab assembly in Unity.

## Setup Reference

Concrete Unity scene assembly notes now live in:

- `docs/plans/2026-04-17-vr-app-shell-scene-setup.md`

That document covers:

- recommended `MainHubScene` hierarchy
- panel placement and component wiring
- environment scene installer setup
- HUD placement
- build settings shortcuts and editor menu usage
