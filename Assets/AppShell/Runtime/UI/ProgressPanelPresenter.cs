using TMPro;
using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.Results;

namespace VRPublicSpeaking.AppShell.UI
{
    public class ProgressPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private DashboardAdapter dashboardAdapter;
        [SerializeField] private TMP_Text summaryStatusLabel;
        [SerializeField] private TMP_Text dashboardStatusLabel;
        [SerializeField] private TMP_Text noteLabel;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            bool hasResults = runtimeState != null && runtimeState.CurrentRuntimeState.ResultsAvailable;
            bool dashboardAvailable = dashboardAdapter != null && dashboardAdapter.IsAvailable;

            if (summaryStatusLabel != null)
            {
                summaryStatusLabel.text = hasResults
                    ? "Latest session summary is ready."
                    : "No completed session yet. Finish one practice run to unlock the summary view.";
            }

            if (dashboardStatusLabel != null)
            {
                dashboardStatusLabel.text = dashboardAvailable
                    ? "Dashboard entry is ready."
                    : "Dashboard entry is reserved for the final analytics module.";
            }

            SetNote(hasResults
                ? "Open the latest summary, or continue into the dashboard entry."
                : "Run a practice session first. This panel will populate automatically.");
        }

        public void OpenLatestSummary()
        {
            appFlowManager?.OpenResultsPanel();
        }

        public void OpenDashboardEntry()
        {
            if (dashboardAdapter != null && dashboardAdapter.TryOpenDashboard())
            {
                SetNote("Dashboard entry opened.");
                return;
            }

            SetNote("Dashboard is being connected for the final build. Latest summary is still available here.");
        }

        public void GoBack()
        {
            appFlowManager?.GoBack();
        }

        private void SetNote(string message)
        {
            if (noteLabel != null)
            {
                noteLabel.text = message ?? string.Empty;
            }
        }
    }
}
