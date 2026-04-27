# Module Integration Guide for the App Shell Menu

This document explains how other project members can connect their own modules to the UI shell, main menu, session setup, pause menu, and results flow without breaking the existing demo flow.

The goal is simple: each module should plug into the shell through the existing adapter/state system instead of directly editing unrelated menu logic.

## 1. Current Shell Structure

The menu system is generated and controlled mainly from these files:

- `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`
- `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- `Assets/AppShell/Runtime/UI/EnvironmentSessionOverlayController.cs`
- `Assets/AppShell/Runtime/Results/ResultsFlowController.cs`
- `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`
- `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`

Important rule:

- Do not manually rebuild the whole menu in the scene unless necessary.
- Prefer connecting your module through an adapter or presenter field.
- If a screen needs to be generated consistently, update the generator instead of only editing one scene object by hand.

## 2. Integration Points by Module Type

### Dashboard Module

The shell already has Dashboard Entry buttons in:

- Review Center / Progress panel
- Results Summary panel

These buttons call `DashboardAdapter`.

Relevant file:

- `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`

How to connect:

1. Add the dashboard UI root object or dashboard controller to the scene.
2. Select the object that has `DashboardAdapter`.
3. Assign one of these fields:
   - `dashboardRoot`: use this if opening the dashboard only requires activating a GameObject.
   - `dashboardController`: use this if your dashboard has an open method.
4. If using a controller, the adapter can call one of these methods automatically:
   - `Open`
   - `OpenDashboard`
   - `ShowDashboard`
   - `Show`
5. Test from the Results Summary screen by pressing `Dashboard Entry`.

Expected behavior:

- If wired correctly, Dashboard Entry opens the real dashboard.
- If not wired, the shell shows a placeholder message and logs that no dashboard integration is connected.

Recommended controller example:

```csharp
public class DashboardController : MonoBehaviour
{
    public void OpenDashboard()
    {
        gameObject.SetActive(true);
        // Refresh dashboard data here.
    }
}
```

## 3. Adding Data to the Results Screen

The results screen reads data from `SessionResultSummary`.

Relevant files:

- `Assets/AppShell/Runtime/Data/SessionResultSummary.cs`
- `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`
- `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`

Current supported result fields:

- `TotalScore`
- `EyeContactScore`
- `SpeechPaceScore`
- `PostureScore`
- `FillerWordCount`
- `DurationSeconds`
- `StrongestArea`
- `WeakestArea`
- `PerformanceBand`
- `Recommendations`

How result data reaches the UI:

1. A session ends.
2. `ExistingSceneFlowAdapter` calls `ScoringAdapter.CaptureSummary(...)`.
3. `ScoringAdapter` creates a `SessionResultSummary`.
4. `AppRuntimeState.StoreResult(summary)` stores it.
5. `ResultsSummaryPresenter.Refresh()` displays it.

If your module produces result data:

- Add the data into `ScoringAdapter.CaptureSummary(...)` if it belongs in the existing summary.
- If a new result field is required, add it to `SessionResultSummary`.
- Then update `ResultsSummaryPresenter` only if the shell summary needs to display it.

Important:

- Do not make the results screen directly search for every module.
- Let the module pass data into `SessionResultSummary` through an adapter.

## 4. Voice Analysis Integration

The Session Setup screen already contains a `Voice Analysis` option.

Relevant files:

- `Assets/AppShell/Runtime/Data/SessionConfig.cs`
- `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`

Current config flag:

```csharp
config.VoiceAnalysisEnabled
```

Recommended integration steps:

1. In the voice analysis module, read the current config from:

```csharp
AppRuntimeState.GetOrCreate().CurrentSessionConfig
```

2. Only run voice analysis if:

```csharp
CurrentSessionConfig.VoiceAnalysisEnabled
```

3. During or after the session, send speech metrics into the scoring/result layer.
4. If using the existing results screen, map values into:
   - `SpeechPaceScore`
   - `FillerWordCount`
   - `Recommendations`
5. If the module has a separate dashboard view, connect it through `DashboardAdapter`.

Minimum expected behavior:

- If Voice Analysis is enabled in setup, the voice module should start during the live session.
- If it is disabled, it should not collect or affect score data.

## 5. Posture Analysis Integration

The Session Setup screen already contains a `Posture Analysis` option.

Current config flag:

```csharp
config.PostureAnalysisEnabled
```

Recommended integration steps:

1. Read the flag from `AppRuntimeState.CurrentSessionConfig`.
2. Enable/disable posture processing based on `PostureAnalysisEnabled`.
3. Push final posture score into:

```csharp
SessionResultSummary.PostureScore
SessionResultSummary.HasPostureScore
```

4. If extra feedback is generated, add it to `Recommendations`.

Important:

- If posture is not fully implemented, keep the toggle disabled or clearly mark it as incomplete for the demo.

## 6. Eye Tracking / Gaze Scoring Integration

The shell already connects gaze-related systems through:

- `EnvironmentSceneInstaller`
- `TrackingAdapter`
- `ScoringAdapter`

Relevant files:

- `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`
- `Assets/AppShell/Runtime/Integration/TrackingAdapter.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`

Current config flags:

```csharp
config.EyeTrackingEnabled
config.GazeScoringEnabled
```

Current behavior:

- `EnvironmentSceneInstaller` tries to find or create the runtime tracking stack.
- `TrackingAdapter` enables/disables tracking based on session config.
- `ScoringAdapter` captures gaze score at session end.

If you update gaze or eye tracking:

- Keep `EyeTrackingSystem` discoverable in the scene.
- Keep `GazeScoringSystem.eyeTracking` assigned.
- Do not create duplicate competing gaze systems.
- Make sure pause state is respected so paused sessions do not collect false samples.

Pause-safe expectation:

- During pause, tracking/scoring should stop or ignore samples.
- After resume, tracking/scoring should continue normally.

## 7. Adding a New Menu Button

If a new button is needed in the main menu or another shell panel:

1. Decide which panel owns it:
   - Main Hub
   - Settings
   - Review Center
   - Results Summary
   - Session Setup
2. Add the button in `AppShellSceneGenerator`.
3. Add a public method in the related presenter/controller.
4. Wire the button using `AppShellEditorCommon.SetButtonEvent(...)`.
5. Keep the action safe if the target module is not connected yet.

Example pattern:

```csharp
public void OpenMyModule()
{
    if (myModuleAdapter != null && myModuleAdapter.TryOpen())
    {
        SetNote("Module opened.");
        return;
    }

    SetNote("Module is not connected yet.");
}
```

Important:

- Do not make buttons silently fail.
- If a module is missing, show a short status message.
- If the feature is not ready for demo, mark it as staged or coming soon.

## 8. Adding a New Setup Option

If a module needs a setup toggle or setting:

1. Add the field to `SessionConfig`.
2. Add UI control in `AppShellSceneGenerator.BuildSessionSetupPanel(...)`.
3. Add serialized field in `SessionConfigController`.
4. Update:
   - `BuildConfigSnapshot()`
   - `LoadFromRuntime()`
   - `RefreshSummaryPreview()`
5. Read the value from `AppRuntimeState.CurrentSessionConfig` in your module.

Do not store module settings only inside UI objects.

Correct data direction:

```text
Session Setup UI -> SessionConfig -> AppRuntimeState -> Environment Scene / Module
```

## 9. Connecting a Module Inside Environment Scenes

Environment scenes are prepared through `EnvironmentSceneInstaller`.

Current environment scenes:

- `Scene_Classroom`
- `Scene_ConferenceHall`
- `Scene_MeetingRoom`

If your module must exist in every environment:

1. Prefer adding or resolving it inside `EnvironmentSceneInstaller`.
2. Make sure it can be found with `FindFirstObjectByType<T>(FindObjectsInactive.Include)` if needed.
3. Avoid scene-specific hardcoding unless absolutely required.
4. Test in all three environments.

Good rule:

- If the module is required for every session, installer/adapter level is the right place.
- If the module is only visual or scene-specific, scene object wiring is acceptable.

## 10. Pause Menu Compatibility

During a live session, the shell uses `EnvironmentSessionOverlayController`.

Relevant file:

- `Assets/AppShell/Runtime/UI/EnvironmentSessionOverlayController.cs`

Pause behavior:

- Resume
- Restart Session
- End Session
- Return To Hub

Module requirement:

- Any module that records runtime samples must be pause-safe.
- Do not keep collecting analysis data while the session is paused.

Recommended module methods:

```csharp
public void PauseModule()
{
    // Stop collecting runtime samples.
}

public void ResumeModule()
{
    // Continue collecting samples.
}
```

If your module already depends on `MainController` or tracking systems, verify that pause does not corrupt your data.

## 11. Scene Routing Rules

Use existing flow methods instead of directly loading scenes from random module scripts.

Common routes:

- Main hub: `AppPanelType.Home`
- Environment selection: `AppPanelType.EnvironmentSelection`
- Session setup: `AppPanelType.SessionSetup`
- Results summary: `AppPanelType.ResultsSummary`

If a module needs to request a hub panel:

```csharp
AppRuntimeState.GetOrCreate().RequestHubPanel(AppPanelType.ResultsSummary);
```

Then route back to the hub scene through the existing flow/transition system.

Avoid:

```csharp
SceneManager.LoadScene("SomeScene");
```

unless the module is specifically responsible for scene loading.

## 12. Demo Readiness Checklist for Module Owners

Before saying a module is integrated, check:

- The module works from the shell flow, not only from a standalone test scene.
- The module respects Session Setup config.
- The module works in Classroom, Conference Hall, and Meeting Room if required.
- The module does not break pause/resume.
- The module does not create duplicate cameras, event systems, or audio listeners.
- The module does not require macOS-only paths.
- The module has a safe fallback if it is not connected.
- The Unity Console has no new errors after a full run.

Recommended full flow:

```text
Main Hub
-> Practice Mode
-> Environment Selection
-> Session Setup
-> Live Session
-> Pause / Resume
-> End Session
-> Results Overlay
-> Dashboard or Return To Hub
```

## 13. What Not To Do

Avoid these patterns:

- Do not directly edit generated UI objects without updating the generator if the change must persist.
- Do not create a separate main menu for your module.
- Do not duplicate `AppRuntimeState`.
- Do not bypass `SessionConfig` for settings that belong to session setup.
- Do not collect scoring data while paused.
- Do not assume only one environment scene exists.
- Do not make a button fail silently when your module is missing.

## 14. Quick Integration Summary

Use this mapping:

| Module Need | Best Integration Point |
| --- | --- |
| Add final dashboard | `DashboardAdapter` |
| Add result metrics | `ScoringAdapter` + `SessionResultSummary` |
| Read setup options | `AppRuntimeState.CurrentSessionConfig` |
| Add setup toggle | `SessionConfig` + `SessionConfigController` + generator |
| Add environment runtime dependency | `EnvironmentSceneInstaller` |
| Add menu navigation | `AppFlowManager` / panel presenter |
| Support pause | `MainController` events or module-level pause methods |
| Show post-session data | `ResultsSummaryPresenter` or dashboard module |

The safest approach is to connect modules through small adapters and keep the shell responsible only for navigation, setup state, pause/results display, and demo flow.
