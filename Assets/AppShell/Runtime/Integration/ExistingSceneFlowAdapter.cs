using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpeechPipeline;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class ExistingSceneFlowAdapter : MonoBehaviour
    {
        [SerializeField] private MainController mainController;
        [SerializeField] private ScoringAdapter scoringAdapter;
        [SerializeField] private SpeechPipelineController speechPipelineController;
        [SerializeField] private EnvironmentSessionOverlayController environmentSessionOverlayController;
        [SerializeField] private bool routeAfterSessionEnd = true;
        [SerializeField] private float autoStartDelay = 0.35f;
        [SerializeField] private string resultsLoadingMessage = "Loading results...";

        private AppRuntimeState runtimeState;
        private SessionConfig activeConfig;
        private bool sessionStopRequested;
        private bool resultsRouteRequested;

        private void Awake()
        {
            AutoWireIfNeeded();
        }

        private void OnDestroy()
        {
            DetachEvents();
        }

        private void Update()
        {
            if (mainController == null || activeConfig == null)
            {
                return;
            }

            if (!mainController.IsSessionRunning || activeConfig.SessionDurationSeconds <= 0f)
            {
                return;
            }

            float timeRemaining = Mathf.Max(0f, activeConfig.SessionDurationSeconds - mainController.LiveSessionElapsedSeconds);
            runtimeState?.UpdateTimeRemaining(timeRemaining);

            if (timeRemaining <= 0f && !sessionStopRequested)
            {
                sessionStopRequested = true;
                mainController.StopSessionFromShell();
            }
        }

        public void Configure(AppRuntimeState appRuntimeState, SessionConfig sessionConfig)
        {
            runtimeState = appRuntimeState;
            activeConfig = sessionConfig?.Clone() ?? new SessionConfig();
            activeConfig.Normalize();
            runtimeState?.UpdateTimeRemaining(activeConfig.SessionDurationSeconds);
            sessionStopRequested = false;
            resultsRouteRequested = false;
            AutoWireIfNeeded();

            if (mainController != null)
            {
                mainController.SetAutomaticReviewEnabled(!routeAfterSessionEnd);
            }
            else
            {
                Debug.LogWarning("[ExistingSceneFlowAdapter] MainController was not found. Session start/end routing will be unavailable.");
            }

            environmentSessionOverlayController?.Configure(runtimeState, mainController);
        }

        public void TryAutoStartSession()
        {
            StopAllCoroutines();
            StartCoroutine(StartSessionAfterDelay());
        }

        public void TryStartSession()
        {
            if (mainController == null)
            {
                Debug.LogWarning("[ExistingSceneFlowAdapter] Cannot start the session because MainController is missing.");
                return;
            }

            if (mainController.IsSessionRunning)
            {
                return;
            }

            mainController.StartSessionFromShell();
        }

        private IEnumerator StartSessionAfterDelay()
        {
            yield return new WaitForSeconds(autoStartDelay);
            TryStartSession();
        }

        private void HandleSessionStarted()
        {
            if (runtimeState == null || activeConfig == null)
            {
                return;
            }

            sessionStopRequested = false;
            resultsRouteRequested = false;
            runtimeState.MarkSessionStarted(
                SceneManager.GetActiveScene().name,
                activeConfig.SessionDurationSeconds);
            runtimeState.UpdateTimeRemaining(activeConfig.SessionDurationSeconds);
            StartSpeechPipelineIfEnabled();
            environmentSessionOverlayController?.HandleSessionStarted();
        }

        private void HandleSessionPaused()
        {
            speechPipelineController?.PauseRecordingFromShell();
        }

        private void HandleSessionResumed()
        {
            speechPipelineController?.ResumeRecordingFromShell();
        }

        private void HandleSessionEnded(float durationSeconds, float gazeScore)
        {
            if (runtimeState == null)
            {
                return;
            }

            if (resultsRouteRequested)
            {
                Debug.LogWarning("[ExistingSceneFlowAdapter] Session end was already processed for this launch.");
                return;
            }

            resultsRouteRequested = true;
            sessionStopRequested = false;
            StopSpeechPipelineIfEnabled();
            runtimeState.MarkSessionEnded();

            SessionResultSummary summary = scoringAdapter != null
                ? scoringAdapter.CaptureSummary(durationSeconds, gazeScore)
                : new SessionResultSummary();

            if (summary.DurationSeconds <= 0f)
            {
                summary.DurationSeconds = durationSeconds;
            }

            runtimeState.StoreResult(summary);

            if (!routeAfterSessionEnd)
            {
                return;
            }

            if (environmentSessionOverlayController != null &&
                environmentSessionOverlayController.ShowResultsOverlay())
            {
                return;
            }

            if (!RouteToResults())
            {
                resultsRouteRequested = false;
            }
        }

        private bool RouteToResults()
        {
            if (runtimeState == null)
            {
                return false;
            }

            TransitionManager transitionManager = TransitionManager.Instance;
            string resultsSceneName = runtimeState.ResultsSceneName;
            if (!string.IsNullOrWhiteSpace(resultsSceneName) &&
                Application.CanStreamedLevelBeLoaded(resultsSceneName))
            {
                if (transitionManager != null)
                {
                    transitionManager.LoadScene(resultsSceneName, resultsLoadingMessage);
                }
                else
                {
                    SceneManager.LoadScene(resultsSceneName);
                }

                return true;
            }

            if (!string.IsNullOrWhiteSpace(resultsSceneName))
            {
                Debug.LogWarning(
                    $"[ExistingSceneFlowAdapter] Results scene '{resultsSceneName}' is not in build settings. Falling back to the hub.");
            }

            if (!string.IsNullOrWhiteSpace(runtimeState.MainHubSceneName) &&
                Application.CanStreamedLevelBeLoaded(runtimeState.MainHubSceneName))
            {
                runtimeState.RequestHubPanel(AppPanelType.ResultsSummary);

                if (transitionManager != null)
                {
                    transitionManager.LoadScene(runtimeState.MainHubSceneName, resultsLoadingMessage);
                }
                else
                {
                    SceneManager.LoadScene(runtimeState.MainHubSceneName);
                }

                return true;
            }

            Debug.LogWarning("[ExistingSceneFlowAdapter] No valid results route target was found after session end.");
            return false;
        }

        public bool AutoWireIfNeeded()
        {
            MainController resolvedMainController = mainController;
            if (resolvedMainController == null)
            {
                resolvedMainController = FindFirstObjectByType<MainController>(FindObjectsInactive.Include);
            }

            AttachToMainController(resolvedMainController);

            if (scoringAdapter == null)
            {
                scoringAdapter = GetComponent<ScoringAdapter>();
            }

            if (environmentSessionOverlayController == null)
            {
                environmentSessionOverlayController = GetComponentInChildren<EnvironmentSessionOverlayController>(true);
            }

            if (speechPipelineController == null)
            {
                speechPipelineController = FindFirstObjectByType<SpeechPipelineController>(FindObjectsInactive.Include);
            }

            if (mainController == null)
            {
                Debug.LogWarning("[ExistingSceneFlowAdapter] No MainController was found in the active environment scene.");
            }

            return mainController != null;
        }

        public void SetSpeechPipelineController(SpeechPipelineController controller)
        {
            speechPipelineController = controller;
        }

        private void AttachToMainController(MainController controller)
        {
            if (mainController == controller)
            {
                EnsureEventRegistration();
                return;
            }

            DetachEvents();
            mainController = controller;
            EnsureEventRegistration();
        }

        private void EnsureEventRegistration()
        {
            if (mainController == null)
            {
                return;
            }

            mainController.SessionStarted -= HandleSessionStarted;
            mainController.SessionEnded   -= HandleSessionEnded;
            mainController.SessionPaused  -= HandleSessionPaused;
            mainController.SessionResumed -= HandleSessionResumed;
            mainController.SessionStarted += HandleSessionStarted;
            mainController.SessionEnded   += HandleSessionEnded;
            mainController.SessionPaused  += HandleSessionPaused;
            mainController.SessionResumed += HandleSessionResumed;
        }

        private void StartSpeechPipelineIfEnabled()
        {
            if (activeConfig == null || !activeConfig.VoiceAnalysisEnabled)
            {
                return;
            }

            AutoWireIfNeeded();
            if (speechPipelineController == null)
            {
                speechPipelineController = EnsureSpeechPipelineSystem();
            }

            if (speechPipelineController == null)
            {
                Debug.LogWarning("[ExistingSceneFlowAdapter] Voice analysis is enabled, but SpeechPipelineController could not be created.");
                return;
            }

            speechPipelineController.BeginRecordingFromShell();
        }

        private void StopSpeechPipelineIfEnabled()
        {
            if (activeConfig == null || !activeConfig.VoiceAnalysisEnabled)
            {
                return;
            }

            AutoWireIfNeeded();
            speechPipelineController?.EndRecordingFromShell();
        }

        private SpeechPipelineController EnsureSpeechPipelineSystem()
        {
            SpeechAdapter speechAdapter = FindFirstObjectByType<SpeechAdapter>(FindObjectsInactive.Include);
            if (speechAdapter == null)
            {
                speechAdapter = GetComponent<SpeechAdapter>() ?? gameObject.AddComponent<SpeechAdapter>();
            }

            PerformanceScoringEngine scoringEngine = null;
            if (scoringAdapter != null)
            {
                scoringAdapter.AutoWireIfNeeded();
                scoringEngine = scoringAdapter.PerformanceScoringEngine;
            }

            if (scoringEngine == null)
            {
                scoringEngine = FindFirstObjectByType<PerformanceScoringEngine>(FindObjectsInactive.Include);
            }

            if (scoringEngine == null)
            {
                GameObject scoringRoot = new GameObject("PerformanceScoringEngine_Auto");
                scoringEngine = scoringRoot.AddComponent<PerformanceScoringEngine>();
            }

            scoringAdapter?.SetPerformanceScoringEngine(scoringEngine);
            speechAdapter.SetScoringEngine(scoringEngine);

            SpeechPipelineController controller =
                FindFirstObjectByType<SpeechPipelineController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                GameObject speechRoot = new GameObject("SpeechPipeline_Auto");
                speechRoot.AddComponent<AudioSource>();
                controller = speechRoot.AddComponent<SpeechPipelineController>();
            }

            controller.SpeechAdapter = speechAdapter;
            controller.ScoringEngine = scoringEngine;
            Debug.Log("[ExistingSceneFlowAdapter] Speech pipeline runtime binding is ready.");
            return controller;
        }

        private void DetachEvents()
        {
            if (mainController == null)
            {
                return;
            }

            mainController.SessionStarted -= HandleSessionStarted;
            mainController.SessionEnded   -= HandleSessionEnded;
            mainController.SessionPaused  -= HandleSessionPaused;
            mainController.SessionResumed -= HandleSessionResumed;
        }
    }
}
