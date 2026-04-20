using System;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Core
{
    public class AppRuntimeState : MonoBehaviour
    {
        public static AppRuntimeState Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        [SerializeField] private SessionConfig currentSessionConfig = new SessionConfig();
        [SerializeField] private SessionRuntimeState currentRuntimeState = new SessionRuntimeState();
        [SerializeField] private SessionResultSummary lastSessionResult = new SessionResultSummary();
        [SerializeField] private AppEnvironmentDefinition selectedEnvironment;
        [SerializeField] private string mainHubSceneName = "MainHubScene";
        [SerializeField] private string resultsSceneName = string.Empty;

        public event Action<SessionConfig> SessionConfigChanged;
        public event Action<SessionRuntimeState> SessionLaunchPrepared;
        public event Action<SessionResultSummary> SessionResultUpdated;

        public SessionConfig CurrentSessionConfig => currentSessionConfig;
        public SessionRuntimeState CurrentRuntimeState => currentRuntimeState;
        public SessionResultSummary LastSessionResult => lastSessionResult;
        public AppEnvironmentDefinition SelectedEnvironment => selectedEnvironment;
        public string MainHubSceneName => mainHubSceneName;
        public string ResultsSceneName => resultsSceneName;

        public bool HasSelectedEnvironment =>
            selectedEnvironment != null &&
            selectedEnvironment.Available &&
            selectedEnvironment.IsConfigured;

        public static AppRuntimeState GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AppRuntimeState existingRuntime =
                FindFirstObjectByType<AppRuntimeState>(FindObjectsInactive.Include);

            if (existingRuntime != null)
            {
                return existingRuntime;
            }

            var runtimeRoot = new GameObject(nameof(AppRuntimeState));
            return runtimeRoot.AddComponent<AppRuntimeState>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            currentSessionConfig ??= new SessionConfig();
            currentRuntimeState ??= new SessionRuntimeState();
            lastSessionResult ??= new SessionResultSummary();
        }

        public void ConfigureNavigation(string hubSceneName, string resultsScene)
        {
            mainHubSceneName = string.IsNullOrWhiteSpace(hubSceneName) ? mainHubSceneName : hubSceneName;
            resultsSceneName = resultsScene ?? string.Empty;
        }

        public SessionConfig GetSessionConfigCopy()
        {
            SessionConfig configCopy = currentSessionConfig?.Clone() ?? new SessionConfig();
            configCopy.Normalize();
            return configCopy;
        }

        public SessionResultSummary GetLastSessionResultCopy()
        {
            return lastSessionResult?.Clone() ?? new SessionResultSummary();
        }

        public void ApplySessionConfig(SessionConfig config)
        {
            currentSessionConfig = SanitizeSessionConfig(config);
            SessionConfigChanged?.Invoke(currentSessionConfig);
        }

        public void SetSelectedEnvironment(AppEnvironmentDefinition environmentDefinition)
        {
            selectedEnvironment = environmentDefinition?.Clone();

            var config = GetSessionConfigCopy();
            config.ApplyEnvironment(selectedEnvironment);
            ApplySessionConfig(config);
        }

        public void PrepareSessionLaunch(AppEnvironmentDefinition environmentDefinition, SessionConfig config)
        {
            if (environmentDefinition != null)
            {
                selectedEnvironment = environmentDefinition.Clone();
            }

            currentSessionConfig = SanitizeSessionConfig(config);
            currentSessionConfig.ApplyEnvironment(selectedEnvironment);

            currentRuntimeState.ResetForLaunch(
                currentSessionConfig.SelectedEnvironmentSceneName,
                currentSessionConfig.SelectedSpawnPointName);

            SessionConfigChanged?.Invoke(currentSessionConfig);
            SessionLaunchPrepared?.Invoke(currentRuntimeState);
        }

        public void MarkSessionStarted(string sceneName, float durationSeconds)
        {
            currentRuntimeState.MarkSessionStarted(sceneName, durationSeconds);
        }

        public void MarkSessionEnded()
        {
            currentRuntimeState.MarkSessionEnded();
        }

        public void UpdateTimeRemaining(float timeRemainingSeconds)
        {
            currentRuntimeState.TimeRemainingSeconds = timeRemainingSeconds;
        }

        public void StoreResult(SessionResultSummary summary)
        {
            lastSessionResult = summary?.Clone() ?? new SessionResultSummary();
            currentRuntimeState.ResultsAvailable = true;
            SessionResultUpdated?.Invoke(lastSessionResult);
        }

        public void RequestHubPanel(AppPanelType panelType)
        {
            currentRuntimeState.RequestedHubPanel = panelType;
        }

        public void ResetRequestedHubPanel()
        {
            currentRuntimeState.RequestedHubPanel = AppPanelType.Home;
        }

        private SessionConfig SanitizeSessionConfig(SessionConfig config)
        {
            SessionConfig sanitizedConfig = config?.Clone() ?? new SessionConfig();
            sanitizedConfig.Normalize();

            if (selectedEnvironment != null && !sanitizedConfig.HasSelectedEnvironment)
            {
                sanitizedConfig.ApplyEnvironment(selectedEnvironment);
            }

            return sanitizedConfig;
        }
    }
}
