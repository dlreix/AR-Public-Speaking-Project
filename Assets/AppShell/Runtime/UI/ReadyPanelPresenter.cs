using System.Collections;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.Presentation;
using VRPublicSpeaking.AppShell.PresentationQuestioning;

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
        private RectTransform presentationControlsRoot;
        private TMP_Text presentationStatusLabel;
        private Button uploadPresentationButton;
        private Button removePresentationButton;
        private bool importInProgress;

        private void OnEnable()
        {
            EnsurePresentationControls();
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
            PresentationDeckReference selectedDeck = config.SelectedPresentation;
            string presentationLine = config.HasPresentation
                ? $"Presentation: {selectedDeck.DisplayName} ({selectedDeck.PageCount} slide{(selectedDeck.PageCount == 1 ? string.Empty : "s")})"
                : "Presentation: None";

            summaryLabel.text =
                $"Environment: {fallbackEnvironmentName}\n" +
                $"Mode: {config.PracticeMode}  |  Duration: {config.GetDurationDisplay()}\n" +
                $"Difficulty: {config.DifficultyLevel}  |  Audience: {config.AudiencePreset}\n" +
                $"Feedback: {config.FeedbackLevel}\n" +
                $"Systems: {config.GetEnabledSystemsSummary()}\n" +
                presentationLine;
            RefreshEnvironmentPreview(environmentDefinition);
            RefreshPresentationControls(config);
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
            if (importInProgress)
            {
                SetWarning("Presentation conversion is still running. Start will be available when it finishes.");
                return;
            }

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

        private void EnsurePresentationControls()
        {
            if (presentationControlsRoot != null || summaryLabel == null)
            {
                return;
            }

            Transform parent = summaryLabel.transform.parent != null ? summaryLabel.transform.parent : transform;

            GameObject rootObject = new GameObject("PresentationControls", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.SetSiblingIndex(summaryLabel.transform.GetSiblingIndex() + 1);
            presentationControlsRoot = rootObject.GetComponent<RectTransform>();
            presentationControlsRoot.anchorMin = new Vector2(0f, 0f);
            presentationControlsRoot.anchorMax = new Vector2(1f, 0f);
            presentationControlsRoot.pivot = new Vector2(0.5f, 0f);
            presentationControlsRoot.anchoredPosition = new Vector2(0f, 18f);
            presentationControlsRoot.sizeDelta = new Vector2(0f, 92f);

            LayoutElement rootLayout = rootObject.GetComponent<LayoutElement>();
            rootLayout.minHeight = 116f;
            rootLayout.preferredHeight = 126f;
            rootLayout.flexibleWidth = 1f;

            VerticalLayoutGroup verticalLayout = rootObject.GetComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 8f;
            verticalLayout.padding = new RectOffset(0, 0, 0, 0);
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            presentationStatusLabel = CreatePresentationStatusLabel(rootObject.transform);

            GameObject rowObject = new GameObject("PresentationButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(rootObject.transform, false);
            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            uploadPresentationButton = CreatePresentationButton(rowObject.transform, "Upload Presentation");
            uploadPresentationButton.onClick.AddListener(HandleUploadPresentation);

            removePresentationButton = CreatePresentationButton(rowObject.transform, "Remove");
            removePresentationButton.onClick.AddListener(HandleRemovePresentation);
        }

        private TMP_Text CreatePresentationStatusLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("PresentationStatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = "Presentation: None";
            label.color = new Color(0.85f, 0.93f, 1f, 1f);
            label.fontSize = 20f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Left;
            if (summaryLabel != null && summaryLabel.font != null)
            {
                label.font = summaryLabel.font;
            }

            LayoutElement layoutElement = labelObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 48f;
            layoutElement.preferredHeight = 58f;
            return label;
        }

        private Button CreatePresentationButton(Transform parent, string labelText)
        {
            GameObject buttonObject = new GameObject($"{labelText.Replace(" ", string.Empty)}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.02f, 0.09f, 0.13f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.04f, 0.20f, 0.28f, 1f);
            colors.pressedColor = new Color(0.02f, 0.28f, 0.36f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.02f, 0.05f, 0.07f, 0.55f);
            button.colors = colors;

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 48f;
            layout.flexibleWidth = 1f;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = labelText;
            text.color = Color.white;
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (summaryLabel != null && summaryLabel.font != null)
            {
                text.font = summaryLabel.font;
            }

            return button;
        }

        private void RefreshPresentationControls(SessionConfig config)
        {
            if (presentationStatusLabel == null)
            {
                return;
            }

            PresentationDeckReference selectedDeck = config != null ? config.SelectedPresentation : null;
            if (importInProgress)
            {
                presentationStatusLabel.text = "Presentation: converting selected file...";
            }
            else if (config != null && config.HasPresentation && selectedDeck != null)
            {
                string questionStatus = string.IsNullOrWhiteSpace(selectedDeck.QuestionStatus)
                    ? selectedDeck.HasQuestionSet ? "Generated" : "Not generated"
                    : selectedDeck.QuestionStatus;
                presentationStatusLabel.text =
                    $"Presentation ready: {selectedDeck.DisplayName} ({selectedDeck.PageCount} slide{(selectedDeck.PageCount == 1 ? string.Empty : "s")})\n" +
                    $"Questions: {questionStatus}";
            }
            else
            {
                presentationStatusLabel.text = "Presentation: None";
            }

            if (uploadPresentationButton != null)
            {
                uploadPresentationButton.interactable = !importInProgress;
            }

            if (removePresentationButton != null)
            {
                removePresentationButton.interactable = !importInProgress && config != null && config.HasPresentation;
            }

            SetLaunchButtonsInteractable(!importInProgress);
        }

        private void HandleUploadPresentation()
        {
            if (importInProgress)
            {
                return;
            }

            StartCoroutine(ImportPresentationRoutine());
        }

        private IEnumerator ImportPresentationRoutine()
        {
            importInProgress = true;
            SetWarning("Presentation conversion is running. Large files can take a moment.");
            RefreshPresentationControls(runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig());
            yield return null;

            bool imported = PresentationImportService.TrySelectAndImportPresentation(
                out PresentationDeckReference deck,
                out string statusMessage);

            if (imported && deck != null)
            {
                if (runtimeState == null)
                {
                    runtimeState = AppRuntimeState.GetOrCreate();
                }

                SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();
                config.SetPresentation(deck);
                runtimeState?.ApplySessionConfig(config);

                if (!string.IsNullOrWhiteSpace(deck.SlideTextPath) &&
                    PresentationTextExtractionService.LoadSlideText(deck) != null)
                {
                    if (OpenAiRuntimeConfig.HasUsableConfiguration())
                    {
                        deck.QuestionStatus = "Generating";
                        config.SetPresentation(deck);
                        runtimeState?.ApplySessionConfig(config);
                        RefreshSummary();
                        SetWarning("Presentation imported. Generating audience questions...");

                        bool questionGenerationCompleted = false;
                        bool questionGenerationSucceeded = false;
                        string questionMessage = string.Empty;
                        yield return PresentationQuestionGenerationService.GenerateQuestionSet(
                            deck,
                            (ok, message, _) =>
                            {
                                questionGenerationSucceeded = ok;
                                questionMessage = message;
                                questionGenerationCompleted = true;
                            });

                        if (questionGenerationCompleted)
                        {
                            deck.QuestionStatus = questionGenerationSucceeded
                                ? "Generated"
                                : string.IsNullOrWhiteSpace(questionMessage) ? "Failed" : questionMessage;
                            config.SetPresentation(deck);
                            runtimeState?.ApplySessionConfig(config);

                            if (!string.IsNullOrWhiteSpace(questionMessage))
                            {
                                statusMessage = $"{statusMessage} {questionMessage}".Trim();
                            }
                        }
                    }
                    else
                    {
                        deck.QuestionStatus = "Missing API key";
                        config.SetPresentation(deck);
                        runtimeState?.ApplySessionConfig(config);
                    }
                }
            }

            importInProgress = false;
            RefreshSummary();

            if (!string.IsNullOrWhiteSpace(statusMessage) && !statusMessage.Contains("canceled"))
            {
                SetWarning(statusMessage);
            }
        }

        private void HandleRemovePresentation()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();
            config.ClearPresentation();
            runtimeState?.ApplySessionConfig(config);
            RefreshSummary();
        }

        private void SetLaunchButtonsInteractable(bool interactable)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null || button == uploadPresentationButton || button == removePresentationButton)
                {
                    continue;
                }

                string label = ResolveButtonLabel(button);
                if (ContainsIgnoreCase(label, "start") ||
                    ContainsIgnoreCase(label, "launch") ||
                    ContainsIgnoreCase(label, "begin"))
                {
                    button.interactable = interactable;
                }
            }
        }

        private static string ResolveButtonLabel(Button button)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            return label != null ? label.text : string.Empty;
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
