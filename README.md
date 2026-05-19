# AR/VR Public Speaking Trainer

This project is a Unity-based XR public speaking training application. It lets users practice presentations in virtual classroom, conference hall, and meeting room environments while receiving feedback from gaze tracking, speech analysis, audience simulation, session scoring, presentation support, and post-session dashboards.

The application is organized around a central App Shell. The shell handles menu navigation, session setup, environment selection, runtime overlays, pause/resume flow, results, and dashboard routing.

## Overview

The user starts from the main hub, chooses a practice mode, selects an environment, configures session duration and analysis systems, then launches a live speaking session. During the session, the app tracks audience eye contact, speech pace, filler word usage, pause duration, tone variation, head movement, and audience engagement. At the end, it generates a performance score out of 100, identifies the user's strongest and weakest areas, and saves the result to local session history.

The project supports VR workflows and editor/PC testing. When real XR eye tracking is unavailable, the gaze system can fall back to headset/camera direction.

## Technology Stack

| Area | Technology |
| --- | --- |
| Engine | Unity `6000.3.10f1` |
| Render Pipeline | Universal Render Pipeline `17.3.0` |
| XR | XR Interaction Toolkit `3.3.1`, XR Hands `1.7.3`, XR Management, OpenXR, Oculus, Meta XR SDK |
| Input | Unity Input System `1.18.0` |
| Speech Analysis | Offline Vosk speech-to-text |
| Presentation Conversion | Poppler and LibreOffice bundled for Windows runtime |
| LLM Question Generation | Gemini or OpenAI runtime configuration |
| Data Storage | JSON files under `Application.persistentDataPath` |

## Main Features

- App Shell flow for login, home, practice mode selection, environment selection, session setup, ready screen, pause overlay, results, and dashboard.
- Three primary training environments: Classroom, Conference Hall, and Meeting Room.
- Runtime scene installation through `EnvironmentSceneInstaller`, reducing manual wiring across environment scenes.
- Gaze tracking and gaze scoring with audience look ratio, confidence, stare warnings, head-speed penalties, and event bonus/penalty support.
- Offline voice analysis using Vosk for transcript, words per minute, filler words, pause duration, and tone variation.
- Audience simulation that reacts to the user's performance with attentive, neutral, distracted, bored, note-taking, nodding, stretching, and applause states.
- Combined performance scoring across speech, eye contact, and posture metrics.
- PDF/PPTX presentation import, slide rendering, in-scene presentation board display, and optional audience question generation.
- Local user/session history saved as JSON and shown in the dashboard.

## Project Structure

```text
Assets/
  AppShell/
    Config/                         Environment catalog asset
    Editor/                         App Shell scene generation, setup, and validation tools
    Generated/                      Generated App Shell materials
    Runtime/
      Core/                         AppRuntimeState and bootstrap logic
      Data/                         SessionConfig, result summary, enums, environment models
      Flow/                         Main hub flow, scene transitions, tutorial/hub controllers
      Integration/                  Environment installer and scoring/tracking/player/audience adapters
      Presentation/                 PDF/PPTX import, slide image conversion, presentation board
      PresentationQuestioning/      LLM question generation and audience Q&A overlay
      Results/                      Results flow and dashboard adapter
      UI/                           Panel presenters, HUD, and world-space UI helpers

  AudienceSimulation_Arda/
    Scripts/                        Audience behavior, reaction engine, spawner, procedural animation
    Models/                         Audience character models and materials
    Prefabs/                        Audience and scene prefabs
    Scenes/                         Module test/environment scenes

  Scripts/                          Main gaze, session, scoring, dashboard, and environment generator scripts
  SpeechPipeline/Scripts/           Vosk-based speech analysis pipeline
  Scenes/                           Main app and practice scenes
  StreamingAssets/                  Vosk models, presentation converters, LLM config templates
  Plugins/Vosk/                     Vosk native libraries
  Models/, Prefabs/, Materials/     3D assets used by environments and UI

Packages/                           Unity package manifest and lock file
ProjectSettings/                    Unity project settings
docs/                               Reports and supporting documentation
BackupScripts/                      Backup copies of earlier scripts
```

## Main Scenes

Enabled scenes in Build Settings:

| Scene | Purpose |
| --- | --- |
| `Assets/Scenes/MainHubScene.unity` | Main menu, login, dashboard, practice mode, environment selection, and setup flow |
| `Assets/Scenes/ResultsScene.unity` | Post-session results scene |
| `Assets/Scenes/Overview.unity` | Supporting overview/dashboard scene |
| `Assets/Scenes/Scene_Classroom.unity` | Classroom practice environment |
| `Assets/Scenes/Scene_ConferenceHall.unity` | Large conference hall environment |
| `Assets/Scenes/Scene_MeetingRoom.unity` | Meeting room environment |

Additional test/demo scenes:

- `Assets/Scenes/GazeTrackingSampleScene.unity`
- `Assets/Scenes/SampleScene/SampleScene.unity`
- `Assets/AudienceSimulation_Arda/Scenes/*`
- Unity XR sample scenes

## Application Flow

```text
MainHubScene
-> Login / Home
-> Practice Mode
-> Environment Selection
-> Session Setup
-> Ready
-> Environment Scene
-> Live Session
-> Pause / Resume / End Session
-> Optional Audience Q&A
-> Results Summary / Dashboard
```

The flow is coordinated through `AppRuntimeState`. `SessionConfig` stores the selected mode, environment, duration, difficulty, audience preset, analysis toggles, and selected presentation. When an environment scene loads, `EnvironmentSceneInstaller` resolves or creates the required runtime systems.

## App Shell Architecture

The App Shell is responsible for the user-facing flow and session state.

Important files:

- `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`
- `Assets/AppShell/Runtime/Data/SessionConfig.cs`
- `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- `Assets/AppShell/Runtime/Flow/SessionLaunchController.cs`
- `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- `Assets/AppShell/Runtime/UI/EnvironmentSessionOverlayController.cs`
- `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs`
- `Assets/AppShell/Runtime/UI/MainHubDashboardPresenter.cs`

Session options:

- Practice mode: Guided Practice, Free Practice, Evaluation Mode, Challenge Mode
- Difficulty: Easy, Normal, Hard, Expert
- Audience preset: Supportive, Neutral, Distracted, Challenging
- Feedback level: Minimal, Standard, Detailed
- Analysis toggles: Eye Tracking, Gaze Scoring, Performance Scoring, Voice Analysis, Posture Analysis
- Default duration: 300 seconds
- Minimum duration: 60 seconds
- Maximum duration: 900 seconds

## Environment Catalog

The environment list is stored in:

```text
Assets/AppShell/Config/DefaultEnvironmentCatalog.asset
```

| Environment | Scene | Recommended Mode | Use Case |
| --- | --- | --- | --- |
| Classroom | `Scene_Classroom` | Guided Practice | Classroom speaking, lecture pacing, basic eye-contact practice |
| Conference Hall | `Scene_ConferenceHall` | Evaluation Mode | Formal presentations, stage presence, large-room pressure |
| Meeting Room | `Scene_MeetingRoom` | Free Practice | Team updates, stakeholder briefings, close-range communication |

Editor menu tools:

- `Tools/VR Public Speaking/App Shell/Create Default Environment Catalog`
- `Tools/VR Public Speaking/App Shell/Add App Shell Scenes To Build Settings`
- `Tools/VR Public Speaking/App Shell/Create Or Update MainHubScene`
- `Tools/VR Public Speaking/App Shell/Create Or Update ResultsScene`
- `Tools/VR Public Speaking/App Shell/Validate App Shell`

## Runtime Installer and Adapter Layer

`EnvironmentSceneInstaller` automatically sets up environment scenes at runtime. It resolves or creates:

- VR scene rig and active camera links
- `EyeTrackingSystem`
- `GazeScoringSystem`
- `GazeEventCoordinator`
- `CircleEventSystem`
- Optional `QuickGazeDotSystem` and `MovingGazeDotSystem`
- `MainController`
- `PerformanceScoringEngine`
- `SpeechAdapter`
- `SpeechPipelineController`
- `AudienceIntegrationAdapter`
- `PresentationBoardController` when a presentation is selected
- `PresentationQuestionSessionController` for audience Q&A
- EventSystem and XR/UI raycaster support

This keeps environment scenes lightweight and reduces repeated manual setup. New environments should be added to Build Settings and the environment catalog, then integrated through the installer/adapters instead of duplicating runtime systems by hand.

## Gaze and Eye Tracking

Main files:

- `Assets/Scripts/EyeTrackingSystem.cs`
- `Assets/Scripts/GazeScoringSystem.cs`
- `Assets/Scripts/CircleEventSystem.cs`
- `Assets/Scripts/QuickGazeDotSystem.cs`
- `Assets/Scripts/MovingGazeDotSystem.cs`
- `Assets/Scripts/GazeEventCoordinator.cs`
- `Assets/Scripts/MainController.cs`

`EyeTrackingSystem` attempts to use XR eye tracking data. If the device does not support it, the system can use the camera/head direction as a fallback. It exposes:

- `IsLookingAtAudience`
- `IsGazeValid`
- `SmoothedHeadSpeed`
- `IsStareWarning`
- `IsHeadWarning`
- `IsPaused`

`GazeScoringSystem` computes a 0-100 gaze score from a recent ring buffer window. Score components include:

- Audience look ratio
- Data confidence
- Long-stare penalty
- Head-speed penalty
- Event bonuses and penalties

`CircleEventSystem`, `QuickGazeDotSystem`, and `MovingGazeDotSystem` create gaze focus events. Successful events can call `ReportBonus`, and missed events can call `ReportPenalty`.

## Speech Analysis

The speech pipeline lives under:

```text
Assets/SpeechPipeline/Scripts/
```

Main files:

- `SpeechPipelineController.cs`
- `AudioCaptureBuffer.cs`
- `VoskSTTEngine.cs`
- `RMSPauseDetector.cs`
- `PitchDetector.cs`
- `PaceTracker.cs`
- `FillerDetector.cs`
- `SpeechScorer.cs`
- `SpeechAdapter.cs`

Pipeline:

1. Microphone permission is requested.
2. The Vosk model is loaded from `Assets/StreamingAssets`.
3. Audio chunks are split into speech/silence using RMS pause detection.
4. When Vosk returns a final transcript, utterance metrics are computed.
5. At session end, average WPM, filler words per minute, average pause duration, and tone variation score are generated.
6. `SpeechAdapter` pushes these metrics into `PerformanceScoringEngine`.

Expected default model folder:

```text
Assets/StreamingAssets/vosk-model-en-us-0.42-gigaspeech/
```

The older small model folder also exists in the project:

```text
Assets/StreamingAssets/vosk-model-small-en-us-0.15/
```

However, `SpeechPipelineController.UseDefaultModelWhenUnsetOrLegacy()` maps an empty or legacy model name to the default full model folder. If Voice Analysis is enabled, the full default model folder should exist and contain model files.

## Performance Scoring

Main files:

- `Assets/Scripts/PerformanceScoringEngine.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`
- `Assets/AppShell/Runtime/Data/SessionResultSummary.cs`

Metric groups:

| Group | Metrics |
| --- | --- |
| Speech | WPM, filler words/min, average pause, tone variation |
| Eye Contact | Eye contact ratio / gaze score |
| Posture | Head movement, rapid head movement events, crossed arms placeholder |

Default final score weights:

| Component | Weight |
| --- | --- |
| Speech | 40% |
| Eye Contact | 35% |
| Posture | 25% |

Speech sub-score weights:

| Component | Weight |
| --- | --- |
| WPM | 35% |
| Filler words | 35% |
| Pause duration | 15% |
| Tone variation | 15% |

Default scoring ranges:

- Ideal WPM: 120-160
- Acceptable WPM: 80-220
- Ideal pause duration: 0.5-1.5 seconds
- Maximum filler threshold: 10 filler words/min

Generated result fields include:

- Total score
- Speech score
- Eye contact score
- Posture score
- Strongest area
- Weakest area
- Performance band: Excellent, Good, Needs Improvement, Weak Performance
- Strength list
- Improvement list
- Detailed feedback items

## Audience Simulation

The audience system is located under:

```text
Assets/AudienceSimulation_Arda/
```

Main files:

- `AudienceSpawner.cs`
- `AudienceBehaviorController.cs`
- `AudienceReactionEngine.cs`
- `AudienceMember.cs`
- `ProceduralAudienceAnimator.cs`
- `AudienceIntegrationAdapter.cs`

The system changes audience behavior based on environment type and performance score.

Supported audience states:

- Idle
- Attentive
- Neutral
- Distracted
- Bored
- Applauding
- Nodding
- Stretching
- NoteTaking
- ChinResting

Difficulty mapping:

| Session Difficulty | Audience Stress |
| --- | --- |
| Easy | Easy |
| Normal | Medium |
| Hard | Hard |
| Expert | Hard |

Audience preset behavior:

| Preset | Effect |
| --- | --- |
| Supportive | More tolerant and more likely to react positively |
| Neutral | Balanced behavior |
| Challenging | Less tolerant and more sensitive to weak performance |

`AudienceReactionEngine` reads WPM, filler words, tone variation, eye contact, and posture signals to generate dominant factors such as:

- `wpm_too_slow`
- `wpm_too_fast`
- `high_filler_words`
- `monotone_voice`
- `eye_contact_low`
- `good_eye_contact`
- `bad_posture_slouch`
- `bad_posture_sway`

When the session ends, the audience controller transitions the audience into applause.

## Presentation Import and Audience Q&A

Presentation support lives under:

```text
Assets/AppShell/Runtime/Presentation/
```

Supported formats:

- PDF
- PPTX

Import flow:

1. The user selects a presentation from the Ready screen.
2. The file is copied to `Application.persistentDataPath/Presentations/{deckId}`.
3. PDFs are rendered to PNG slide images using Poppler `pdftoppm`.
4. PPTX files are converted to PDF using LibreOffice, then rendered to PNG using Poppler.
5. A manifest and slide text files are generated.
6. In the environment scene, `PresentationBoardController` displays slides on a projection screen, whiteboard, or runtime fallback board.

Bundled converter files:

```text
Assets/StreamingAssets/PresentationConverters/win-x64/
  LibreOffice/
  poppler/
```

Note: Presentation conversion V1 is Windows-focused. PPTX animations, embedded media, and speaker notes are outside the current scope.

Audience Q&A:

- `PresentationTextExtractionService` extracts readable slide text.
- `PresentationQuestionGenerationService` uses Gemini or OpenAI to generate short audience questions.
- `PresentationQuestionSessionController` displays the questions after the session in a VR overlay/bubble flow.
- The current Q&A flow is focused on asking questions and practicing responses. Data classes for answer feedback exist, but the current question prompt does not generate expected answers or grading rubrics.

LLM config templates:

```text
Assets/StreamingAssets/LLM/llm_config.template.json
Assets/StreamingAssets/Gemini/gemini_config.template.json
Assets/StreamingAssets/OpenAI/openai_config.template.json
```

Local key files are ignored by `.gitignore`:

```text
Assets/StreamingAssets/LLM/llm_config.local.json
Assets/StreamingAssets/Gemini/gemini_config.local.json
Assets/StreamingAssets/OpenAI/openai_config.local.json
```

Environment variables can also be used:

```text
GEMINI_API_KEY
GOOGLE_API_KEY
OPENAI_API_KEY
```

## Data Storage and Dashboard

`DataManager` saves post-session data to a user-specific JSON file.

Main file:

```text
Assets/Scripts/DataManager.cs
```

Save location:

```text
Application.persistentDataPath/history_{currentUser}.json
```

Saved fields include:

- Session id
- Timestamp and display date
- Overall score
- Eye contact
- Pace / speech score
- Posture
- Duration
- Filler word count
- WPM
- Filler words/min
- Average pause
- Tone variation
- Head movement
- Detailed feedback report
- Q&A result

The dashboard displays the latest session and historical session data.

## Procedural Environment Generators

The structural parts of the environments can be regenerated from editor scripts.

| Script | Generated Environment |
| --- | --- |
| `ClassroomGenerator.cs` | Classroom shell, whiteboard, student rows, lighting, spawn point |
| `ConferenceHallGenerator (5).cs` | Stage, projection screen, curved/tiered seating, hall architecture |
| `MeetingRoomGenerator.cs` | Meeting room shell, windows, door, whiteboard, projection screen, lighting |

Usage:

1. Add an empty GameObject to the scene.
2. Attach the relevant generator script.
3. Run the `Generate Scene` context menu or toggle `generateNow` in the Inspector.

The generators do not rebuild geometry at runtime. Scene geometry is serialized for demo performance.

## Setup

1. Open the project with Unity Hub.
2. Use Unity editor version `6000.3.10f1`.
3. Wait for Unity package restore to finish.
4. Open `Assets/Scenes/MainHubScene.unity`.
5. Confirm the following scenes are enabled in Build Settings:
   - `MainHubScene`
   - `ResultsScene`
   - `Overview`
   - `Scene_Classroom`
   - `Scene_ConferenceHall`
   - `Scene_MeetingRoom`
6. If Voice Analysis is required, confirm that the Vosk model folder exists under `Assets/StreamingAssets/`.
7. If Q&A generation is required, configure a Gemini or OpenAI API key through local config or environment variables.
8. Start Play Mode from `MainHubScene`.

## Editor Test Flow

Recommended full demo flow:

```text
MainHubScene
-> Login
-> Home
-> Practice Mode
-> Environment Selection
-> Session Setup
-> Ready
-> Launch Session
-> Live Session
-> Pause / Resume
-> End Session
-> Results Summary
-> Dashboard
```

PC/editor shortcuts:

| Key / Input | Action |
| --- | --- |
| `R` | Start/stop session in an environment scene |
| `Esc` | Toggle pause during a session |
| `D` | Toggle debug mode |
| `C` or left click | Trigger/test gaze event |

VR input notes:

- Primary button: main session action such as start/stop
- Secondary button short press: debug
- Secondary button long press or menu button: pause
- Grip: can trigger gaze/circle event tests

## Build and Submission Notes

When zipping the Unity project, keep:

```text
Assets/
Packages/
ProjectSettings/
README.md
docs/                 If reports/documents are required
BackupScripts/        If backup scripts are required
```

Do not include:

```text
Library/
Temp/
Obj/
Logs/
UserSettings/
.git/
.vs/
.idea/
.vscode/
Build/
Builds/
*.csproj
*.sln
```

Important: Keep all `.meta` files under `Assets`. Unity uses GUIDs inside `.meta` files to preserve asset references.

Voice models and presentation converter folders can be large. If the submitted zip must run without extra setup, make sure the required files under `Assets/StreamingAssets` are included.

Do not include API-key local config files in public submissions. Template files are enough if Q&A generation will not be tested by the evaluator.

## Known Limitations

- If real eye tracking hardware is unavailable, gaze tracking uses a camera/head-direction fallback.
- Posture analysis is based mainly on head movement and head-speed warnings; it is not full-body posture tracking.
- Voice analysis will not start if the expected Vosk model folder is missing.
- Presentation conversion V1 is Windows-focused.
- PPTX animations, media, and speaker notes are not rendered.
- LLM-based audience question generation requires internet access and an API key.
- `Library/` should not be included in Git or submission zips; Unity regenerates it on open.

## Development Rules

- Add new session settings to `SessionConfig` first, then update `SessionConfigController` and the related UI presenter.
- Add new environment scenes with the `Scene_` prefix under `Assets/Scenes` so App Shell setup tools can discover them.
- Integrate runtime dependencies through `EnvironmentSceneInstaller` and adapter scripts instead of manually duplicating systems in every scene.
- Add new result metrics through `SessionResultSummary`, then update `ScoringAdapter`, `ResultsSummaryPresenter`, and dashboard display code.
- Systems that sample runtime data should stop or ignore samples during pause/resume.
- Never share secret API keys stored in `Assets/StreamingAssets/*local.json`.

## Quick File Reference

| Need | File |
| --- | --- |
| App state | `Assets/AppShell/Runtime/Core/AppRuntimeState.cs` |
| Session settings | `Assets/AppShell/Runtime/Data/SessionConfig.cs` |
| Main menu flow | `Assets/AppShell/Runtime/Flow/AppFlowManager.cs` |
| Session launch | `Assets/AppShell/Runtime/Flow/SessionLaunchController.cs` |
| Runtime environment setup | `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs` |
| Session start/end/pause | `Assets/Scripts/MainController.cs` |
| Gaze data | `Assets/Scripts/EyeTrackingSystem.cs` |
| Gaze score | `Assets/Scripts/GazeScoringSystem.cs` |
| Combined scoring | `Assets/Scripts/PerformanceScoringEngine.cs` |
| Speech pipeline | `Assets/SpeechPipeline/Scripts/SpeechPipelineController.cs` |
| Speech-to-scoring bridge | `Assets/Scripts/SpeechAdapter.cs` |
| Audience behavior | `Assets/AudienceSimulation_Arda/Scripts/AudienceBehaviorController.cs` |
| Audience reaction engine | `Assets/AudienceSimulation_Arda/Scripts/AudienceReactionEngine.cs` |
| Audience spawning | `Assets/AudienceSimulation_Arda/Scripts/AudienceSpawner.cs` |
| Presentation import | `Assets/AppShell/Runtime/Presentation/PresentationImportService.cs` |
| Presentation board | `Assets/AppShell/Runtime/Presentation/PresentationBoardController.cs` |
| Question generation | `Assets/AppShell/Runtime/PresentationQuestioning/PresentationQuestionGenerationService.cs` |
| Results summary | `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs` |
| Session history | `Assets/Scripts/DataManager.cs` |

## Status

This README was generated after scanning the project structure, Unity settings, package manifest, scenes, runtime scripts, speech pipeline, audience simulation, presentation system, scoring system, and dashboard data flow.
