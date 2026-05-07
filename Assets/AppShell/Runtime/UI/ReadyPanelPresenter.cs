using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
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

        private static readonly Dictionary<string, Sprite> GeneratedPreviewSprites = new Dictionary<string, Sprite>();

        private void OnEnable()
        {
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

            summaryLabel.text =
                $"Environment: {fallbackEnvironmentName}\n" +
                $"Mode: {config.PracticeMode}  |  Duration: {config.GetDurationDisplay()}\n" +
                $"Difficulty: {config.DifficultyLevel}  |  Audience: {config.AudiencePreset}\n" +
                $"Feedback: {config.FeedbackLevel}\n" +
                $"Systems: {config.GetEnabledSystemsSummary()}";
            RefreshEnvironmentPreview(environmentDefinition);
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
            if (environmentPreviewImage == null)
            {
                return;
            }

            if (environmentDefinition != null && environmentDefinition.PreviewSprite != null)
            {
                environmentPreviewImage.sprite = environmentDefinition.PreviewSprite;
                environmentPreviewImage.type = Image.Type.Simple;
                environmentPreviewImage.preserveAspect = true;
                environmentPreviewImage.color = Color.white;
                return;
            }

            environmentPreviewImage.sprite = GetGeneratedPreviewSprite(environmentDefinition);
            environmentPreviewImage.type = Image.Type.Simple;
            environmentPreviewImage.preserveAspect = false;
            environmentPreviewImage.color = Color.white;
        }

        private static Sprite GetGeneratedPreviewSprite(AppEnvironmentDefinition environmentDefinition)
        {
            string key = environmentDefinition != null && !string.IsNullOrWhiteSpace(environmentDefinition.Id)
                ? environmentDefinition.Id
                : "launch-preview";

            if (GeneratedPreviewSprites.TryGetValue(key, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            ResolvePreviewPalette(key, out Color topColor, out Color bottomColor, out Color accentColor);

            const int width = 128;
            const int height = 72;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"ReadyPreview_{key}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = height <= 1 ? 0f : y / (float)(height - 1);
                Color rowColor = Color.Lerp(bottomColor, topColor, vertical);
                for (int x = 0; x < width; x++)
                {
                    float stripe = Mathf.Sin((x * 0.14f) + (y * 0.08f)) * 0.5f + 0.5f;
                    Color pixelColor = Color.Lerp(rowColor, accentColor, stripe * 0.11f);

                    bool floorLine = y < 10 && x > 10 && x < width - 10;
                    bool stageLine = y > 46 && y < 51 && x > 16 && x < width - 16;
                    if (floorLine || stageLine)
                    {
                        pixelColor = Color.Lerp(pixelColor, accentColor, 0.52f);
                    }

                    pixels[(y * width) + x] = pixelColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = $"ReadyPreviewSprite_{key}";
            GeneratedPreviewSprites[key] = sprite;
            return sprite;
        }

        private static void ResolvePreviewPalette(
            string key,
            out Color topColor,
            out Color bottomColor,
            out Color accentColor)
        {
            string normalized = key ?? string.Empty;
            if (ContainsIgnoreCase(normalized, "conference"))
            {
                topColor = new Color(0.42f, 0.26f, 0.22f, 1f);
                bottomColor = new Color(0.13f, 0.17f, 0.24f, 1f);
                accentColor = new Color(0.98f, 0.63f, 0.28f, 1f);
                return;
            }

            if (ContainsIgnoreCase(normalized, "meeting"))
            {
                topColor = new Color(0.20f, 0.32f, 0.36f, 1f);
                bottomColor = new Color(0.10f, 0.15f, 0.20f, 1f);
                accentColor = new Color(0.35f, 0.72f, 0.88f, 1f);
                return;
            }

            topColor = new Color(0.22f, 0.30f, 0.42f, 1f);
            bottomColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            accentColor = new Color(0.21f, 0.63f, 0.96f, 1f);
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
