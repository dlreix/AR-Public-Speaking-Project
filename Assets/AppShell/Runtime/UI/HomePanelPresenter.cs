using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class HomePanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppFlowManager appFlowManager;
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
            AutoResolvePrimaryAction();
            ApplyPrimaryActionPolish();
        }

        private void OnEnable()
        {
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

        private void ApplyPrimaryActionPolish()
        {
            if (primaryActionButton == null)
            {
                return;
            }

            if (primaryActionLabel != null)
            {
                primaryActionLabel.text = "Start Practice Demo";
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
