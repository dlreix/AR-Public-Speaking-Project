using UnityEngine;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class HomePanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppFlowManager appFlowManager;

        public void OpenStartPractice()
        {
            appFlowManager?.OpenPracticeModePanel();
        }

        public void OpenEnvironments()
        {
            appFlowManager?.OpenEnvironmentSelectionPanel();
        }

        public void OpenResults()
        {
            appFlowManager?.OpenProgressPanel();
        }

        public void OpenSettings()
        {
            appFlowManager?.OpenSettingsPanel();
        }

        public void ExitApplication()
        {
            appFlowManager?.QuitApplication();
        }
    }
}
