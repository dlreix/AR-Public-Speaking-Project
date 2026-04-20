# Stage Timer Anchor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the head-locked in-session HUD with a small stage timer that snaps into a fixed world position near the front of the speaking area and remains there for the full session.

**Architecture:** Keep `InSessionHudPresenter` as the data/presentation source so timer and mode text continue to work unchanged. Update `WorldSpaceCanvasFollower` so it can place the HUD once from the initial player view, then stop following; wire the editor generator and current environment scenes to use a low, forward-facing stage-monitor placement instead of a screen-like overlay.

**Tech Stack:** Unity 6, C#, world-space canvas UI, existing AppShell editor scene generator.

---

### Task 1: Document the new HUD behavior

**Files:**
- Create: `docs/plans/2026-04-20-stage-timer-anchor-plan.md`

**Step 1: Write the implementation note**

- Explain that the session timer should no longer stay attached to the headset.
- Record that the HUD should appear as a fixed world element near the front/lower speaking area.
- Record that `InSessionHudPresenter` remains unchanged and only placement logic changes.

### Task 2: Add one-time snap support to HUD placement

**Files:**
- Modify: `Assets/AppShell/Runtime/UI/WorldSpaceCanvasFollower.cs`

**Step 1: Add a serialized follow mode switch**

- Add a boolean that allows the canvas to snap once and stop following.
- Preserve existing continuous-follow behavior as the default so other uses do not break.

**Step 2: Update runtime behavior**

- When continuous follow is disabled, resolve the target, snap immediately once, and then leave the transform untouched.
- Keep `SnapToTarget()` working for manual placement.

### Task 3: Move the in-session HUD to a front-lower fixed stage position

**Files:**
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Modify: `Assets/Scenes/Scene_Classroom.unity`
- Modify: `Assets/Scenes/Scene_ConferenceHall.unity`
- Modify: `Assets/Scenes/Scene_MeetingRoom.unity`

**Step 1: Change generator defaults**

- Set the HUD offset to a centered, low, forward position suitable for a stage monitor.
- Disable continuous follow for the generated in-session HUD.

**Step 2: Align current scenes**

- Patch the serialized HUD follower values in the three environment scenes so the current project behaves the same way without requiring manual inspector work.

### Task 4: Verify the change

**Files:**
- Verify: `Assets/AppShell/Runtime/UI/WorldSpaceCanvasFollower.cs`
- Verify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- Verify: `Assets/Scenes/Scene_Classroom.unity`
- Verify: `Assets/Scenes/Scene_ConferenceHall.unity`
- Verify: `Assets/Scenes/Scene_MeetingRoom.unity`

**Step 1: Run build validation**

Run: `dotnet build 'VR_project.sln' -nologo`
Expected: `0 Hata`

**Step 2: Verify scene serialization**

- Confirm each environment scene now uses the front-lower HUD offset.
- Confirm each environment scene disables continuous follow for the HUD follower.

Plan complete and saved to `docs/plans/2026-04-20-stage-timer-anchor-plan.md`. Execution will continue in this same session based on the approved direction.
