using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.Results;

namespace VRPublicSpeaking.AppShell.UI
{
    public class HomePanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private DashboardAdapter dashboardAdapter;
        [SerializeField] private Button primaryActionButton;
        [SerializeField] private TMP_Text primaryActionLabel;
        [SerializeField] private bool animatePrimaryAction = true;

        private RectTransform primaryActionTransform;
        private Image primaryActionImage;
        private Vector3 primaryBaseScale = Vector3.one;
        private readonly Color primaryBaseColor = new Color(0.21f, 0.63f, 0.96f, 1f);
        private readonly Color primaryPulseColor = new Color(0.32f, 0.72f, 1f, 1f);

        private void Awake()
        {
            EnsureDashboardAdapter();
            EnsureDashboardShortcut();
            AutoResolvePrimaryAction();
            ApplyPrimaryActionPolish();
        }

        private void OnEnable()
        {
            EnsureDashboardAdapter();
            EnsureDashboardShortcut();
            AutoResolvePrimaryAction();
            ApplyPrimaryActionPolish();
        }

        private void Update()
        {
            if (!animatePrimaryAction || primaryActionTransform == null || primaryActionButton == null)
            {
                return;
            }

            if (!primaryActionButton.interactable)
            {
                primaryActionTransform.localScale = primaryBaseScale;
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
            primaryActionTransform.localScale = primaryBaseScale * Mathf.Lerp(1f, 1.025f, pulse);

            if (primaryActionImage != null)
            {
                primaryActionImage.color = Color.Lerp(primaryBaseColor, primaryPulseColor, pulse * 0.55f);
            }
        }

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

        public void OpenDashboard()
        {
            EnsureDashboardAdapter();
            if (dashboardAdapter != null && dashboardAdapter.TryOpenDashboard())
            {
                return;
            }

            appFlowManager?.OpenProgressPanel();
        }

        private void EnsureDashboardAdapter()
        {
            if (dashboardAdapter == null)
            {
                dashboardAdapter = GetComponent<DashboardAdapter>();
            }

            if (dashboardAdapter == null)
            {
                dashboardAdapter = gameObject.AddComponent<DashboardAdapter>();
            }
        }

        public void OpenSettings()
        {
            appFlowManager?.OpenSettingsPanel();
        }

        public void ExitApplication()
        {
            appFlowManager?.QuitApplication();
        }

        private void AutoResolvePrimaryAction()
        {
            if (primaryActionButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int index = 0; index < buttons.Length; index++)
                {
                    Button button = buttons[index];
                    if (button == null)
                    {
                        continue;
                    }

                    TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                    string labelText = label != null ? label.text : string.Empty;
                    string objectName = button.gameObject.name;

                    if (ContainsIgnoreCase(labelText, "Practice") || ContainsIgnoreCase(objectName, "Practice"))
                    {
                        primaryActionButton = button;
                        primaryActionLabel = label;
                        break;
                    }
                }
            }

            primaryActionTransform = primaryActionButton != null
                ? primaryActionButton.transform as RectTransform
                : null;
            primaryActionImage = primaryActionButton != null
                ? primaryActionButton.targetGraphic as Image
                : null;

            if (primaryActionTransform != null)
            {
                primaryBaseScale = primaryActionTransform.localScale;
            }
        }

        private void EnsureDashboardShortcut()
        {
            if (FindDescendant(transform, "DashboardButton") != null)
            {
                return;
            }

            Transform settingsButtonTransform = FindDescendant(transform, "SettingsButton");
            Button settingsButton = settingsButtonTransform != null
                ? settingsButtonTransform.GetComponent<Button>()
                : null;
            if (settingsButton == null || settingsButton.transform.parent == null)
            {
                return;
            }

            GameObject dashboardButtonObject = Instantiate(settingsButton.gameObject, settingsButton.transform.parent);
            dashboardButtonObject.name = "DashboardButton";
            dashboardButtonObject.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex());

            Button dashboardButton = dashboardButtonObject.GetComponent<Button>();
            if (dashboardButton != null)
            {
                dashboardButton.onClick.RemoveAllListeners();
                dashboardButton.onClick.AddListener(OpenDashboard);
            }

            TMP_Text dashboardLabel = dashboardButtonObject.GetComponentInChildren<TMP_Text>(true);
            if (dashboardLabel != null)
            {
                dashboardLabel.text = "Dashboard";
            }

            Transform settingsInfoTransform = FindDescendant(transform, "SettingsInfo");
            if (settingsInfoTransform != null && settingsInfoTransform.parent == settingsButton.transform.parent)
            {
                GameObject dashboardInfoObject = Instantiate(settingsInfoTransform.gameObject, settingsInfoTransform.parent);
                dashboardInfoObject.name = "DashboardInfo";
                dashboardInfoObject.transform.SetSiblingIndex(dashboardButtonObject.transform.GetSiblingIndex() + 1);

                TMP_Text dashboardInfo = dashboardInfoObject.GetComponent<TMP_Text>();
                if (dashboardInfo != null)
                {
                    dashboardInfo.text = "Open analytics directly";
                }
            }
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(root.GetChild(index), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void ApplyPrimaryActionPolish()
        {
            if (primaryActionButton == null)
            {
                return;
            }

            if (primaryActionLabel != null)
            {
                primaryActionLabel.text = "Start Practice";
                primaryActionLabel.fontStyle = FontStyles.Bold;
            }

            if (primaryActionImage != null)
            {
                primaryActionImage.color = primaryBaseColor;
            }

            ColorBlock colors = primaryActionButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.91f, 1f, 1f);
            colors.selectedColor = new Color(0.95f, 0.98f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            primaryActionButton.colors = colors;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
