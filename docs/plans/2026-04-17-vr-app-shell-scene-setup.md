# VR App Shell Scene Setup

**Date:** 2026-04-17
**Purpose:** Concrete Unity wiring steps for the first stable AppShell pass.

## What Is Already In Code

The repository now contains:

- persistent app runtime bootstrap via `AppRuntimeState` and `AppRuntimeBootstrap`
- hub flow managers and panel presenters under `Assets/AppShell/Runtime`
- environment-scene installers/adapters for existing speaking scenes
- results routing and summary presenters
- a minimal `InSessionHudPresenter`
- editor shortcuts under `Tools/VR Public Speaking/App Shell`

This means you do **not** need to create a separate persistent-managers scene just to get started.

## Fastest Setup Path

You can now generate the first scaffold directly from Unity:

`Tools -> VR Public Speaking -> App Shell -> Build Or Refresh Full App Shell`

That command will:

- create `Assets/AppShell/Config/DefaultEnvironmentCatalog.asset`
- create `Assets/Scenes/MainHubScene.unity`
- create `Assets/Scenes/ResultsScene.unity`
- add AppShell bindings plus `InSessionHUD` into each existing environment scene
- update build settings with the hub, results, and environment scenes

Manual scene assembly is still possible, but it is no longer required for the first stable pass.

## Recommended MainHubScene Hierarchy

Create `MainHubScene.unity` with a calm lobby environment and use this structure:

1. `MainHubRoot`
2. `WorldSpaceHubCanvas`
3. `Panels`
4. `HomePanel`
5. `PracticeModePanel`
6. `EnvironmentSelectionPanel`
7. `SessionSetupPanel`
8. `ReadyPanel`
9. `ProgressPanel`
10. `SettingsPanel`
11. `TransitionOverlayRoot`

Recommended component placement:

- `MainHubRoot`
  - `AppFlowManager`
  - `UIStateController`
- `HomePanel`
  - `AppPanelView` with `PanelType = Home`
  - `HomePanelPresenter`
- `PracticeModePanel`
  - `AppPanelView` with `PanelType = PracticeMode`
  - `PracticeModePanelPresenter`
- `EnvironmentSelectionPanel`
  - `AppPanelView` with `PanelType = EnvironmentSelection`
  - `EnvironmentSelectionController`
- `SessionSetupPanel`
  - `AppPanelView` with `PanelType = SessionSetup`
  - `SessionConfigController`
- `ReadyPanel`
  - `AppPanelView` with `PanelType = Ready`
  - `ReadyPanelPresenter`
- `ProgressPanel`
  - `AppPanelView` with `PanelType = Progress`
  - optional `DashboardAdapter`
- `SettingsPanel`
  - `AppPanelView` with `PanelType = Settings`
- `TransitionOverlayRoot`
  - `TransitionManager`
  - fullscreen world-space fade canvas with `CanvasGroup`

## Panel Hierarchy Notes

Use one world-space canvas for the hub and keep each major panel as a sibling under `Panels`.

Recommended default active panel order:

1. `HomePanel`
2. `PracticeModePanel`
3. `EnvironmentSelectionPanel`
4. `SessionSetupPanel`
5. `ReadyPanel`

Recommended VR layout rules:

- keep the main panel cluster centered in front of the player
- keep action buttons large enough for XR ray interaction
- avoid long horizontal button rows
- keep environment cards to 3 visible cards per row max
- use short summary text, not dense paragraphs

## Environment Catalog Setup

Create the catalog asset with:

`Tools -> VR Public Speaking -> App Shell -> Create Default Environment Catalog`

This generates:

- `Assets/AppShell/Config/DefaultEnvironmentCatalog.asset`

It will auto-detect the current environment scenes:

- `Scene_Classroom`
- `Scene_ConferenceHall`
- `Scene_MeetingRoom`

Then:

1. open `EnvironmentSelectionPanel`
2. assign the generated catalog to `EnvironmentSelectionController`
3. wire each `EnvironmentCardView` into the controller's `cardViews` list
4. add preview sprites and optional spawn point names in the catalog asset

## Manual Inspector Assignments Still Needed

The generator now covers the structural wiring, but these content-facing assignments are still intentionally manual:

- `Assets/AppShell/Config/DefaultEnvironmentCatalog.asset`
  - assign `PreviewSprite` for each environment card so the shell shows real thumbnails instead of placeholder visuals
  - set `SpawnPointName` only for scenes that do **not** use the default fallback names `PlayerSpawnPoint` or `SpawnPoint`
  - if an environment should remain visible but disabled, keep `Available` off and fill `AvailabilityReason`
- `DashboardAdapter` on:
  - `MainHubScene -> ProgressPanel`
  - `MainHubScene -> ResultsSummaryPanel`
  - `ResultsScene -> ResultsPanel`
  - wire either `dashboardRoot` or `dashboardController`
  - if the legacy dashboard opens through a non-default method, keep `openMessage = Open` and add alternate method names such as `OpenDashboard` or `ShowDashboard`

These are product-content decisions, so they are safer as inspector assignments than hardcoded guesses.

## Hub Wiring Checklist

Wire the following references in `MainHubScene`:

- `AppFlowManager`
  - `uiStateController`
  - `environmentSelectionController`
  - `sessionConfigController`
  - `readyPanelPresenter`
  - optional `resultsSummaryPresenter`
  - `sessionLaunchController`
- `SessionLaunchController`
  - `environmentSelectionController`
  - `sessionConfigController`
  - `transitionManager`
  - optional validation label
- `ReadyPanelPresenter`
  - `appFlowManager`
- each presenter button event
  - point to the matching presenter methods instead of scene-loading directly

## Environment Scene Setup

For each existing environment scene, create one root installer object:

1. `AppShellSceneBindings`

Add:

- `EnvironmentSceneInstaller`
- `ExistingSceneFlowAdapter`
- `TrackingAdapter`
- `ScoringAdapter`
- `PlayerRigAdapter`

Optional HUD root:

1. `InSessionHUD`
2. `CanvasGroup`
3. `InSessionHudPresenter`

HUD placement recommendation:

- world-space, slightly below the natural eye line
- small timer, one line of status text
- non-interactive

## Build Settings

Use:

`Tools -> VR Public Speaking -> App Shell -> Add App Shell Scenes To Build Settings`

This adds:

- `MainHubScene` if it exists
- `ResultsScene` if it exists
- all detected `Scene_*` environment scenes

You can also run:

`Tools -> VR Public Speaking -> App Shell -> Validate App Shell`

The validator now checks the high-risk shell wiring automatically:

- missing hub/results presenters
- missing `AppShellSceneBindings`
- missing `MainController`
- missing `PlayerController`
- missing spawn points
- missing build-settings entries
- catalog entries that point to missing scenes

`Build Or Refresh Full App Shell` also triggers this validator at the end of the generator pass.

## Runtime Flow After Wiring

1. launch app
2. `AppRuntimeBootstrap` creates runtime state automatically
3. hub writes choices into `SessionConfig`
4. `SessionLaunchController` prepares runtime launch state
5. selected environment scene loads
6. `EnvironmentSceneInstaller` applies adapters
7. `ExistingSceneFlowAdapter` starts and monitors `MainController`
8. result summary is captured back into runtime state
9. app routes to results panel or results scene

## Current Safe Assumptions

- existing environment scenes remain the source of truth for speaking-session logic
- `MainController` remains the source of truth for session lifecycle
- scoring comes from `GazeScoringSystem` and `PerformanceScoringEngine` when present
- dashboard remains optional and should be connected through `DashboardAdapter`

## Next Recommended Unity Pass

After this wiring is in place, the next practical Unity-editor task is:

1. assemble `MainHubScene`
2. test hub -> scene launch for one environment
3. add the same installer/HUD root into the other environment scenes
4. verify retry and return-to-hub routing

## Current QA Checklist

When you are back in Unity, this is the shortest high-value manual pass:

1. open `Assets/Scenes/MainHubScene.unity`
2. verify `Start Practice -> Environment -> Setup -> Ready -> Launch`
3. finish one session and confirm routing into `Assets/Scenes/ResultsScene.unity`
4. press `Retry Same Setup` and verify the same environment relaunches with the last config
5. finish again and verify `Change Environment` returns to the hub on the environment panel
6. verify `Return To Hub` lands on the home panel without shifting the fixed shell canvas
