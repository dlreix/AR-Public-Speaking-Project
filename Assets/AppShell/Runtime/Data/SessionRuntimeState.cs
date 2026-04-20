using System;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public class SessionRuntimeState
    {
        [SerializeField] private bool sessionLaunchRequested;
        [SerializeField] private bool sessionRunning;
        [SerializeField] private bool resultsAvailable;
        [SerializeField] private float timeRemainingSeconds;
        [SerializeField] private string currentSceneName = string.Empty;
        [SerializeField] private string selectedSpawnPointName = string.Empty;
        [SerializeField] private long sessionStartedAtUtcTicks;
        [SerializeField] private AppPanelType requestedHubPanel = AppPanelType.Home;

        public bool SessionLaunchRequested
        {
            get => sessionLaunchRequested;
            set => sessionLaunchRequested = value;
        }

        public bool SessionRunning
        {
            get => sessionRunning;
            set => sessionRunning = value;
        }

        public bool ResultsAvailable
        {
            get => resultsAvailable;
            set => resultsAvailable = value;
        }

        public float TimeRemainingSeconds
        {
            get => timeRemainingSeconds;
            set => timeRemainingSeconds = Mathf.Max(0f, value);
        }

        public string CurrentSceneName
        {
            get => currentSceneName;
            set => currentSceneName = value ?? string.Empty;
        }

        public string SelectedSpawnPointName
        {
            get => selectedSpawnPointName;
            set => selectedSpawnPointName = value ?? string.Empty;
        }

        public long SessionStartedAtUtcTicks
        {
            get => sessionStartedAtUtcTicks;
            set => sessionStartedAtUtcTicks = value;
        }

        public AppPanelType RequestedHubPanel
        {
            get => requestedHubPanel;
            set => requestedHubPanel = value;
        }

        public void ResetForLaunch(string sceneName, string spawnPointName)
        {
            sessionLaunchRequested = true;
            sessionRunning = false;
            resultsAvailable = false;
            timeRemainingSeconds = 0f;
            currentSceneName = sceneName ?? string.Empty;
            selectedSpawnPointName = spawnPointName ?? string.Empty;
            sessionStartedAtUtcTicks = 0L;
            requestedHubPanel = AppPanelType.Home;
        }

        public void MarkSessionStarted(string sceneName, float timeRemaining)
        {
            sessionLaunchRequested = false;
            sessionRunning = true;
            resultsAvailable = false;
            currentSceneName = sceneName ?? string.Empty;
            timeRemainingSeconds = Mathf.Max(0f, timeRemaining);
            sessionStartedAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        public void MarkSessionEnded()
        {
            sessionRunning = false;
            sessionLaunchRequested = false;
            resultsAvailable = true;
            timeRemainingSeconds = 0f;
        }
    }
}
