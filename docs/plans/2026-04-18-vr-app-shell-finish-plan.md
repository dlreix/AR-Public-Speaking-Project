# VR App Shell Finish Plan Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Finish the remaining VR frontend shell work so the app feels like one coherent product from hub entry to session launch, in-session HUD, results routing, dashboard entry, retry, and return-to-hub.

**Architecture:** Keep `Assets/AppShell` as the orchestration layer above the existing `MainController`, tracking, scoring, dashboard, and scene-specific systems. Finish the shell by hardening scene stability, closing panel-flow gaps, improving generator/wiring safety, and validating the full user journey across every environment scene.

**Tech Stack:** Unity 6, C#, XR Interaction Toolkit, Unity UI, TextMeshPro, existing `MainController` / tracking / scoring scripts

## Current Status Snapshot

As of 2026-04-18, Tasks 1 through 7 are implemented in code and regenerated through the AppShell editor pipeline.

Automated verification completed:

- `dotnet build 'VR_project.sln' -nologo` returns `0 Error`
- `Tools -> VR Public Speaking -> App Shell -> Build Or Refresh Full App Shell` completes in batch mode
- `AppShellValidationUtility` runs after generator refresh and currently emits no AppShell warnings in batch

The remaining work is manual Unity/VR QA plus content-level inspector assignment, not architectural rewrites.

---

### Task 1: Stabilize all shell-only scenes

**Files:**
- Create: `Assets/AppShell/Runtime/Flow/ShellSceneRigController.cs`
- Modify: `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- Modify: `Assets/AppShell/Runtime/UI/WorldSpaceCanvasFollower.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/MainHubScene.unity`
- Modify: `Assets/Scenes/ResultsScene.unity`

**Step 1: Extract reusable shell rig logic**

- Move hub-only rig locking logic out of `AppFlowManager` into `ShellSceneRigController`.
- Keep these responsibilities together:
  - disable shell locomotion/gravity
  - lock shell rig origin position/rotation
  - force a stable `CameraYOffset`
  - keep shell canvases readable and non-mirrored

**Step 2: Attach the same controller to `MainHubScene` and `ResultsScene`**

- Add the new component to both shell scenes.
- Use the same shell-safe defaults in both scenes so they behave consistently.

**Step 3: Keep generated shell scenes collision-safe**

- Ensure `MainHubBackdrop/Floor` has a collider.
- Ensure shell walls needed for visual anchoring also have colliders.
- Ensure the scene generator reapplies this on refresh.

**Step 4: Refresh generated shell scenes**

Run in Unity:

- `Tools -> VR Public Speaking -> App Shell -> Create Or Update MainHubScene`
- `Tools -> VR Public Speaking -> App Shell -> Create Or Update ResultsScene`

Expected:

- Both scenes regenerate without losing AppShell references.

**Step 5: Verify compile**

Run:

```powershell
dotnet build 'VR_project.sln' -nologo
```

Expected:

- `0 Error`

**Step 6: Manual play-mode verification**

- Play `Assets/Scenes/MainHubScene.unity`
- Wait 10 seconds after load
- Rotate view left/right/up/down
- Confirm the menu stays readable, fixed, and stable
- Play `Assets/Scenes/ResultsScene.unity`
- Confirm the same behavior there

**Step 7: Commit**

```bash
git add Assets/AppShell/Runtime/Flow/ShellSceneRigController.cs Assets/AppShell/Runtime/Flow/AppFlowManager.cs Assets/AppShell/Runtime/UI/WorldSpaceCanvasFollower.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/Scenes/MainHubScene.unity Assets/Scenes/ResultsScene.unity
git commit -m "feat: stabilize shell scenes and fixed-view canvases"
```

### Task 2: Finish hub navigation modules and panel behavior

**Files:**
- Create: `Assets/AppShell/Runtime/UI/ProgressPanelPresenter.cs`
- Create: `Assets/AppShell/Runtime/UI/SettingsPanelPresenter.cs`
- Modify: `Assets/AppShell/Runtime/UI/UIStateController.cs`
- Modify: `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- Modify: `Assets/AppShell/Runtime/UI/HomePanelPresenter.cs`
- Modify: `Assets/AppShell/Runtime/UI/PracticeModePanelPresenter.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/MainHubScene.unity`

**Step 1: Add dedicated presenters for remaining scaffold panels**

- Create `ProgressPanelPresenter` for results-history/dashboard entry behavior.
- Create `SettingsPanelPresenter` for comfort/audio/calibration entry wiring.
- Stop routing those panels only through generic `AppFlowManager` button bindings.

**Step 2: Harden panel history behavior**

- Review `UIStateController.ShowPanel()` and `GoBack()`.
- Prevent duplicate history entries when reopening the current panel.
- Keep panel transitions predictable for:
  - Home -> Practice Mode -> Environment
  - Environment -> Setup -> Ready
  - Results -> Change Environment -> Setup

**Step 3: Keep unavailable options visible but safely disabled**

- In `PracticeModePanelPresenter`, show clear disabled state text for unsupported modes.
- Do not hide non-implemented modes; keep the architecture product-ready.

**Step 4: Rewire `MainHubScene` through the generator**

Run in Unity:

- `Tools -> VR Public Speaking -> App Shell -> Create Or Update MainHubScene`

Expected:

- `HomePanel`, `PracticeModePanel`, `ProgressPanel`, and `SettingsPanel` all point to dedicated presenters.

**Step 5: Manual navigation verification**

- Play `Assets/Scenes/MainHubScene.unity`
- Verify:
  - `Start Practice` opens practice modes
  - `Environments` opens environment selection directly
  - `Results` opens progress/results entry
  - `Settings` opens settings
  - `Back` returns to the previous logical panel

**Step 6: Commit**

```bash
git add Assets/AppShell/Runtime/UI/ProgressPanelPresenter.cs Assets/AppShell/Runtime/UI/SettingsPanelPresenter.cs Assets/AppShell/Runtime/UI/UIStateController.cs Assets/AppShell/Runtime/Flow/AppFlowManager.cs Assets/AppShell/Runtime/UI/HomePanelPresenter.cs Assets/AppShell/Runtime/UI/PracticeModePanelPresenter.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/Scenes/MainHubScene.unity
git commit -m "feat: finish hub panel routing and navigation behavior"
```

### Task 3: Strengthen environment selection and catalog content

**Files:**
- Modify: `Assets/AppShell/Runtime/Data/AppEnvironmentDefinition.cs`
- Modify: `Assets/AppShell/Runtime/Data/AppEnvironmentCatalog.cs`
- Modify: `Assets/AppShell/Runtime/UI/EnvironmentCardView.cs`
- Modify: `Assets/AppShell/Runtime/UI/EnvironmentSelectionController.cs`
- Modify: `Assets/AppShell/Editor/AppShellSetupUtility.cs`
- Modify: `Assets/AppShell\Config/DefaultEnvironmentCatalog.asset`

**Step 1: Add richer catalog metadata**

- Keep existing fields.
- Add any missing shell-facing metadata needed for product polish:
  - availability reason text
  - optional recommended mode text
  - optional audience hint text

**Step 2: Improve card state rendering**

- Show four clear card states:
  - available
  - selected
  - unavailable
  - misconfigured

**Step 3: Make catalog refresh safer**

- In `AppShellSetupUtility`, keep existing scene discovery.
- Preserve manually assigned preview sprites/descriptions when refreshing the catalog where possible.

**Step 4: Inspector content pass**

Open:

- `Assets/AppShell/Config/DefaultEnvironmentCatalog.asset`

Set for each environment:

- preview sprite
- description
- spawn point name if a scene uses a non-default spawn
- availability if an environment should stay visible but disabled

**Step 5: Manual environment verification**

- Open `MainHubScene`
- Confirm card previews appear
- Confirm unavailable/misconfigured cards cannot launch
- Confirm selection persists into `ReadyPanel`

**Step 6: Commit**

```bash
git add Assets/AppShell/Runtime/Data/AppEnvironmentDefinition.cs Assets/AppShell/Runtime/Data/AppEnvironmentCatalog.cs Assets/AppShell/Runtime/UI/EnvironmentCardView.cs Assets/AppShell/Runtime/UI/EnvironmentSelectionController.cs Assets/AppShell/Editor/AppShellSetupUtility.cs Assets/AppShell/Config/DefaultEnvironmentCatalog.asset
git commit -m "feat: improve environment catalog and selection states"
```

### Task 4: Finish session setup, validation, and ready summary

**Files:**
- Modify: `Assets/AppShell/Runtime/Data/SessionConfig.cs`
- Modify: `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`
- Modify: `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- Modify: `Assets/AppShell/Runtime/UI/ReadyPanelPresenter.cs`
- Modify: `Assets/AppShell/Runtime/Flow/SessionLaunchController.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/MainHubScene.unity`

**Step 1: Tighten setup defaults and limits**

- Keep duration clamped to valid values.
- Add shell-safe defaults for:
  - practice mode
  - duration
  - difficulty
  - audience preset
  - auto-start behavior

**Step 2: Improve `SessionConfigController` summary preview**

- Show environment, mode, difficulty, audience, duration, and enabled analysis systems.
- Make preview text match what the user sees later on `ReadyPanel`.

**Step 3: Improve `ReadyPanelPresenter`**

- Render a final, readable launch summary.
- Show warning text for:
  - no selected environment
  - unavailable scene
  - invalid duration
  - disabled scoring combinations

**Step 4: Harden launch validation**

- Keep launch validation in `SessionLaunchController`.
- Ensure invalid configuration blocks scene loading and writes a visible warning label.

**Step 5: Manual verification**

- Try to launch with no valid environment
- Try to launch with a valid environment
- Confirm the ready screen always matches the last setup choices

**Step 6: Verify compile**

Run:

```powershell
dotnet build 'VR_project.sln' -nologo
```

Expected:

- `0 Error`

**Step 7: Commit**

```bash
git add Assets/AppShell/Runtime/Data/SessionConfig.cs Assets/AppShell/Runtime/Core/AppRuntimeState.cs Assets/AppShell/Runtime/UI/SessionConfigController.cs Assets/AppShell/Runtime/UI/ReadyPanelPresenter.cs Assets/AppShell/Runtime/Flow/SessionLaunchController.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/Scenes/MainHubScene.unity
git commit -m "feat: finish setup validation and ready summary"
```

### Task 5: Harden environment-scene install flow and in-session HUD

**Files:**
- Modify: `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`
- Modify: `Assets/AppShell/Runtime/Integration/ExistingSceneFlowAdapter.cs`
- Modify: `Assets/AppShell/Runtime/Integration/PlayerRigAdapter.cs`
- Modify: `Assets/AppShell/Runtime/Integration/TrackingAdapter.cs`
- Modify: `Assets/AppShell/Runtime/UI/InSessionHudPresenter.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/Scene_Classroom.unity`
- Modify: `Assets/Scenes/Scene_ConferenceHall.unity`
- Modify: `Assets/Scenes/Scene_MeetingRoom.unity`

**Step 1: Make scene auto-wiring more explicit**

- In the installer/adapters, keep auto-find behavior, but log missing systems clearly:
  - `MainController`
  - `PlayerController`
  - `Main Camera`
  - spawn point

**Step 2: Improve spawn fallback order**

- Keep current spawn lookup:
  - requested spawn name
  - `PlayerSpawnPoint`
  - `SpawnPoint`
- Add a warning if nothing is found, instead of silent failure.

**Step 3: Finish HUD behavior**

- Show timer only while a session is active.
- Show mode + environment status text.
- Keep the HUD non-interactive and world-space.

**Step 4: Make session routing duration-safe**

- Keep `ExistingSceneFlowAdapter` responsible for duration monitoring.
- Ensure `StopSessionFromShell()` is only called once when time reaches zero.

**Step 5: Refresh environment scene bindings**

Run in Unity:

- `Tools -> VR Public Speaking -> App Shell -> Install App Shell Bindings In Environment Scenes`

Expected:

- each environment scene contains `AppShellSceneBindings` and `InSessionHUD`

**Step 6: Manual environment verification**

Test these scenes one by one:

- `Assets/Scenes/Scene_Classroom.unity`
- `Assets/Scenes/Scene_ConferenceHall.unity`
- `Assets/Scenes/Scene_MeetingRoom.unity`

For each scene, verify:

- session auto-starts after shell launch
- HUD appears only during active session
- configured duration counts down
- session ends cleanly

**Step 7: Commit**

```bash
git add Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs Assets/AppShell/Runtime/Integration/ExistingSceneFlowAdapter.cs Assets/AppShell/Runtime/Integration/PlayerRigAdapter.cs Assets/AppShell/Runtime/Integration/TrackingAdapter.cs Assets/AppShell/Runtime/UI/InSessionHudPresenter.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/Scenes/Scene_Classroom.unity Assets/Scenes/Scene_ConferenceHall.unity Assets/Scenes/Scene_MeetingRoom.unity
git commit -m "feat: harden scene install flow and in-session hud"
```

### Task 6: Finish results flow, dashboard entry, and retry loop

**Files:**
- Modify: `Assets/AppShell/Runtime/Results/ResultsFlowController.cs`
- Modify: `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs`
- Modify: `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`
- Modify: `Assets/AppShell/Runtime/Integration/ExistingSceneFlowAdapter.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/ResultsScene.unity`
- Modify: `Assets/Scenes/MainHubScene.unity`

**Step 1: Finish results summary rendering**

- Keep the current score summary.
- Add explicit rendering for:
  - duration
  - performance band
  - strongest area
  - weakest area
  - recommendations fallback text

**Step 2: Expand dashboard adapter safety**

- Support:
  - root activation
  - `SendMessage("Open")`
  - optional alternate message names if needed later
- Keep it adapter-only; do not replace an existing dashboard system.

**Step 3: Make results routing fully loop-safe**

- Verify:
  - `Retry Same Setup`
  - `Change Environment`
  - `Return To Hub`
  - dashboard entry
- Keep all routes transition-safe.

**Step 4: Manual end-to-end verification**

Run this user journey:

1. Main hub
2. Practice mode
3. Environment
4. Setup
5. Ready
6. Launch
7. Session end
8. Results scene
9. Retry
10. Session end again
11. Return to hub

Expected:

- runtime state survives the full loop
- selected environment and config stay correct on retry
- return-to-hub lands on the intended panel

**Step 5: Commit**

```bash
git add Assets/AppShell/Runtime/Results/ResultsFlowController.cs Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs Assets/AppShell/Runtime/Results/DashboardAdapter.cs Assets/AppShell/Runtime/Integration/ExistingSceneFlowAdapter.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/Scenes/ResultsScene.unity Assets/Scenes/MainHubScene.unity
git commit -m "feat: finish results routing and retry loop"
```

### Task 7: Add AppShell validation tooling

**Files:**
- Create: `Assets/AppShell/Editor/AppShellValidationUtility.cs`
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/AppShell/Editor/AppShellSetupUtility.cs`

**Step 1: Add a validation menu command**

Add:

- `Tools -> VR Public Speaking -> App Shell -> Validate App Shell`

**Step 2: Validate the high-risk integration points**

Check and log warnings for:

- hub scene missing required panel presenters
- results scene missing required results bindings
- environment scene missing `AppShellSceneBindings`
- missing `MainController`
- missing `PlayerController`
- missing spawn point
- shell scene not in build settings
- environment catalog entry pointing to a missing scene

**Step 3: Call validation after generator refresh**

- After shell scene generation, run the validator automatically or at least log the next required fix.

**Step 4: Manual verification**

- Intentionally break one inspector reference
- Run validation
- Confirm the warning is specific enough to fix quickly

**Step 5: Commit**

```bash
git add Assets/AppShell/Editor/AppShellValidationUtility.cs Assets/AppShell/Editor/AppShellSceneGenerator.cs Assets/AppShell/Editor/AppShellSetupUtility.cs
git commit -m "feat: add app shell validation tooling"
```

### Task 8: Final QA pass and handoff notes

**Files:**
- Modify: `docs/plans/2026-04-17-vr-app-shell-scene-setup.md`
- Modify: `docs/plans/2026-04-18-vr-app-shell-finish-plan.md`

**Step 1: Update setup notes**

- Add the exact inspector fields that still require manual Unity assignment:
  - preview sprites
  - spawn point names where needed
  - dashboard adapter target if one exists

**Step 2: Run final compile check**

Run:

```powershell
dotnet build 'VR_project.sln' -nologo
```

Expected:

- `0 Error`

**Step 3: Run final manual test matrix**

Verify:

- `MainHubScene`
- `ResultsScene`
- `Scene_Classroom`
- `Scene_ConferenceHall`
- `Scene_MeetingRoom`
- launch from hub
- in-session HUD
- results summary
- retry
- change environment
- return to hub

**Step 4: Record residual risks**

- Note any remaining optional integrations that are still adapter placeholders, especially dashboard wiring.

**Step 5: Commit**

```bash
git add docs/plans/2026-04-17-vr-app-shell-scene-setup.md docs/plans/2026-04-18-vr-app-shell-finish-plan.md
git commit -m "docs: finalize vr app shell finish checklist"
```

## Handoff Notes

### Manual inspector work still expected

- assign preview sprites in `Assets/AppShell/Config/DefaultEnvironmentCatalog.asset`
- set custom spawn point names only for environment scenes that do not use `PlayerSpawnPoint` or `SpawnPoint`
- connect an existing dashboard through `DashboardAdapter.dashboardRoot` or `DashboardAdapter.dashboardController` if a dashboard UI/controller already exists

### Residual risks

- `DashboardAdapter` is intentionally safe-by-default, but long-term progress remains a placeholder until a real dashboard target is wired
- environment scenes with unusual spawn naming still depend on catalog configuration
- the shell has automated validation and compile coverage, but VR comfort and transition feel still need live headset play-mode verification

### Recommended manual test matrix

Run this exact flow after reopening Unity:

1. `MainHubScene` loads and the shell canvas stays fixed in front of the player
2. `Start Practice -> Guided Practice -> Environment -> Setup -> Ready -> Launch`
3. `Scene_Classroom` launches, HUD appears only during session, and session auto-ends on duration
4. `ResultsScene` shows summary text, status note, retry, dashboard entry, and return actions
5. `Retry Same Setup` relaunches the same environment with the last config
6. `Change Environment` returns to `MainHubScene` on the environment-selection panel
7. `Return To Hub` returns to the home panel
