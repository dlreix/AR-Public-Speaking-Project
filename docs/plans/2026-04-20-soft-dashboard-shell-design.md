# Soft Dashboard Shell Design

**Date:** 2026-04-20

**Goal**

Refresh the existing VR app shell menu into a more modern, softer, product-like interface without changing the current flow, controller wiring, session launch logic, or results routing.

## Non-Negotiables

- Keep the current AppShell flow architecture intact.
- Do not replace `AppFlowManager`, `SessionConfigController`, `EnvironmentSelectionController`, `SessionLaunchController`, or existing adapters unless strictly required for presentation.
- Preserve the current scene generation workflow based on `AppShellSceneGenerator`.
- Treat this as a presentation-layer refactor, not a business-logic rewrite.

## Visual Direction

The UI should move away from stacked generic buttons and toward a premium soft-dashboard layout:

- large visual cards
- softened corners
- layered dark surfaces
- desaturated blue accent
- larger spacing
- compact helper copy
- fewer button-like elements, more card-like selections

The target mood is calm, modern, and product-ready rather than “demo menu”.

## Primary Layout

The main hub should use a dashboard composition with four zones:

1. `HeaderBar`
   - centered brand
   - lightweight navigation affordances
   - subtle utility actions on the right

2. `LeftExperienceColumn`
   - large practice cards
   - image-led visual hierarchy
   - primary entry into the practice flow

3. `RightUtilityColumn`
   - quick access tiles for environments, results, settings, and progress
   - compact snapshot modules

4. `FooterSessionStrip`
   - shows current mode, environment, and target duration

## Panel Strategy

### Home Panel

- left side: featured experience cards
- right side: utility tiles and current setup summary
- goal: feel like a hub dashboard instead of a vertical list

### Practice Mode Panel

- large selectable mode cards
- each card contains title, short explanation, and availability state
- unavailable modes remain visible but visually softened

### Environment Selection Panel

- stronger environment previews
- selected environment gets a larger emphasis area
- confirmation action remains explicit

### Session Setup Panel

- setup grouped into compact soft cards
- duration, difficulty, audience, feedback, and analysis systems presented in sections
- live summary remains visible

### Ready Panel

- left side: selected experience summary card
- right side: quick review of setup choices
- final start button remains obvious and safe

## Technical Approach

The visual refresh should be implemented by extending the editor generation layer:

- add new theme tokens in `AppShellEditorCommon`
- add reusable soft-dashboard UI builders in `AppShellEditorUi`
- refactor panel build methods in `AppShellSceneGenerator`

This keeps the logic and data-binding architecture stable while modernizing the generated visual structure.

## Risk Management

To avoid breaking the current app shell:

- keep existing presenters and controller fields intact where possible
- preserve button event wiring
- keep panel names and root references stable unless a wrapper container is added
- validate with a build immediately after the generator refactor

## Success Criteria

- the menu looks softer and more modern
- visual hierarchy is driven by cards, not stacked default controls
- existing navigation still works
- setup, environment selection, ready, and session launch continue to function
- no existing session/runtime systems are reimplemented
