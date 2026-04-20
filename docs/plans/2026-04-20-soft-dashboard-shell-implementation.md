# Soft Dashboard Shell Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Modernize the existing AppShell menu into a soft dashboard layout without changing the current flow architecture or runtime integration logic.

**Architecture:** Keep all flow and session systems intact and perform the redesign inside the editor-driven presentation generator. Extend shared editor UI helpers first, then refactor panel builders to compose the new visual system from reusable card and utility-tile primitives.

**Tech Stack:** Unity 6, C#, TextMeshPro, Unity UI, editor-driven scene generation.

---

### Task 1: Expand shared visual tokens

**Files:**
- Modify: `Assets/AppShell/Editor/AppShellEditorCommon.cs`

**Step 1: Add soft-dashboard color tokens**

- Introduce colors for:
  - header surface
  - elevated surface
  - tile surface
  - muted accent
  - subtle border
  - warning/destructive states

**Step 2: Keep old tokens compatible**

- Preserve existing tokens used elsewhere so current generator code does not break while panel methods migrate incrementally.

### Task 2: Add reusable dashboard UI helpers

**Files:**
- Modify: `Assets/AppShell/Editor/AppShellEditorUi.cs`

**Step 1: Add layout helpers**

- Create helpers for:
  - section container
  - dashboard row
  - soft feature card
  - utility tile
  - summary strip

**Step 2: Keep bindings simple**

- Return standard Unity components or generated roots so existing presenter wiring can still be attached from `AppShellSceneGenerator`.

### Task 3: Refactor the Home panel

**Files:**
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`

**Step 1: Replace stacked buttons with dashboard structure**

- Build a header/subtitle area.
- Create a left feature-card area for the primary practice path.
- Create a right utility-tile area for environments, results, settings, and exit.
- Preserve existing button callbacks.

### Task 4: Refactor the mode, environment, setup, and ready panels

**Files:**
- Modify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`

**Step 1: Practice modes**

- Convert buttons into large selectable cards while preserving the same presenter fields.

**Step 2: Environment selection**

- Improve card hierarchy and confirmation area while preserving `EnvironmentCardView` wiring.

**Step 3: Session setup**

- Group controls into clearer sections with a stronger summary block.

**Step 4: Ready panel**

- Convert the review screen into a two-column confirmation layout with stronger launch emphasis.

### Task 5: Validate the refactor

**Files:**
- Verify: `Assets/AppShell/Editor/AppShellEditorCommon.cs`
- Verify: `Assets/AppShell/Editor/AppShellEditorUi.cs`
- Verify: `Assets/AppShell/Editor/AppShellSceneGenerator.cs`

**Step 1: Run build validation**

Run: `dotnet build 'VR_project.sln' -nologo`
Expected: `0 Hata`

**Step 2: Sanity-check generated structure**

- Ensure event wiring still points to the same presenters.
- Ensure panel roots still exist and can be found by the current flow system.
- Ensure no runtime-only logic was moved into the visual layer.

Plan complete and saved to `docs/plans/2026-04-20-soft-dashboard-shell-implementation.md`. Execution will continue in this same session using the approved low-risk, generator-based refactor path.
