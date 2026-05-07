using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class ReadyPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private Image environmentPreviewImage;

        private void OnEnable()
        {
            ApplyFinalLaunchCopy();
            RefreshSummary();
        }

        public void RefreshSummary()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            if (summaryLabel == null)
            {
                return;
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();
            AppEnvironmentDefinition environmentDefinition = runtimeState != null ? runtimeState.SelectedEnvironment : null;
            string fallbackEnvironmentName = environmentDefinition != null
                ? environmentDefinition.DisplayName
                : "No environment selected";
            RefreshEnvironmentPreview(environmentDefinition);

            summaryLabel.text =
                $"Environment: {fallbackEnvironmentName}\n" +
                $"Mode: {config.PracticeMode}  |  Duration: {config.GetDurationDisplay()}\n" +
                $"Context: {config.DifficultyLevel} / {config.AudiencePreset}\n" +
                $"Feedback: {config.FeedbackLevel}\n" +
                $"Systems: {config.GetEnabledSystemsSummary()}";
            SetWarning(BuildWarningText(config, environmentDefinition));
        }

        public void SetWarning(string message)
        {
            if (warningLabel != null)
            {
                warningLabel.text = message ?? string.Empty;
            }
        }

        public void StartSession()
        {
            appFlowManager?.LaunchSession();
        }

        public void GoBack()
        {
            appFlowManager?.GoBack();
        }

        private static string BuildWarningText(SessionConfig config, AppEnvironmentDefinition environmentDefinition)
        {
            var warnings = new List<string>();

            if (environmentDefinition == null || !config.HasSelectedEnvironment)
            {
                warnings.Add("Select a launch-ready environment before starting the session.");
            }
            else if (!environmentDefinition.Available)
            {
                string reason = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                    ? "The selected environment is currently unavailable."
                    : environmentDefinition.AvailabilityReason;
                warnings.Add(reason);
            }
            else if (environmentDefinition.IsMisconfigured)
            {
                warnings.Add("The selected environment is visible in the shell, but its scene wiring is incomplete.");
            }

            if (config.SessionDurationSeconds < SessionConfig.MinDurationSeconds ||
                config.SessionDurationSeconds > SessionConfig.MaxDurationSeconds)
            {
                warnings.Add(
                    $"Duration must stay between {SessionConfig.MinDurationSeconds / 60f:0} and {SessionConfig.MaxDurationSeconds / 60f:0} minutes.");
            }

            if (!config.HasAnyScoringEnabled)
            {
                warnings.Add("Gaze Scoring and Performance Scoring are both off. The session can still launch, but the results summary will be limited.");
            }

            if (config.EyeTrackingEnabled && !config.GazeScoringEnabled)
            {
                warnings.Add("Eye Tracking is enabled while Gaze Scoring is off. Tracking can still run, but gaze-based scoring will not contribute to the score.");
            }

            if (warnings.Count == 0)
            {
                return "No launch blockers detected. The current session setup is ready to start.";
            }

            return string.Join("\n", warnings).Trim();
        }

        private void RefreshEnvironmentPreview(AppEnvironmentDefinition environmentDefinition)
        {
            ResolvePreviewImageIfNeeded();

            if (environmentPreviewImage == null)
            {
                return;
            }

            Sprite previewSprite = environmentDefinition != null ? environmentDefinition.PreviewSprite : null;
            if (previewSprite != null)
            {
                environmentPreviewImage.sprite = previewSprite;
                environmentPreviewImage.type = Image.Type.Simple;
                environmentPreviewImage.preserveAspect = true;
                environmentPreviewImage.color = Color.white;
                return;
            }

            environmentPreviewImage.sprite = null;
            environmentPreviewImage.type = Image.Type.Sliced;
            environmentPreviewImage.preserveAspect = false;
            environmentPreviewImage.color = new Color(0.14f, 0.20f, 0.28f, 1f);
        }

        private void ResolvePreviewImageIfNeeded()
        {
            if (environmentPreviewImage != null)
            {
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child != null && child.name == "Preview")
                {
                    environmentPreviewImage = child.GetComponent<Image>();
                    if (environmentPreviewImage != null)
                    {
                        return;
                    }
                }
            }
        }

        private void ApplyFinalLaunchCopy()
        {
            SetChildText(
                "PanelSubtitle",
                "Review the selected room, confirm the active systems, and start when everything is ready.");
            SetChildText(
                "DescriptionLabel",
                "One last review before entering the selected speaking environment.");
            SetSummaryStripText("LaunchStrip", "Flow", "Review setup -> Start session -> Enter room");
            SetSummaryStripText("LaunchNoteStrip", "Room", "The selected environment opens after confirmation.");
        }

        private void SetSummaryStripText(string stripName, string title, string value)
        {
            Transform strip = FindChild(stripName);
            if (strip == null)
            {
                return;
            }

            SetChildText(strip, "StripTitle", title.ToUpperInvariant());
            SetChildText(strip, "StripValue", value);
        }

        private void SetChildText(string objectName, string value)
        {
            Transform target = FindChild(objectName);
            if (target != null)
            {
                TMP_Text label = target.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.text = value ?? string.Empty;
                }
            }
        }

        private static void SetChildText(Transform root, string objectName, string value)
        {
            Transform target = FindChild(root, objectName);
            if (target != null)
            {
                TMP_Text label = target.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.text = value ?? string.Empty;
                }
            }
        }

        private Transform FindChild(string objectName)
        {
            return FindChild(transform, objectName);
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child != null && child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
