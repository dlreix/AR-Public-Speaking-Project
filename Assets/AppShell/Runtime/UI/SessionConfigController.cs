using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class SessionConfigController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;

        [Header("Duration")]
        [SerializeField] private Slider durationSlider;
        [SerializeField] private TMP_Text durationValueLabel;
        [SerializeField] private bool durationSliderUsesMinutes = true;

        [Header("Dropdowns")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private TMP_Dropdown audienceDropdown;
        [SerializeField] private TMP_Dropdown feedbackDropdown;

        [Header("Feature Toggles")]
        [SerializeField] private Toggle eyeTrackingToggle;
        [SerializeField] private Toggle gazeScoringToggle;
        [SerializeField] private Toggle performanceScoringToggle;
        [SerializeField] private Toggle voiceAnalysisToggle;
        [SerializeField] private Toggle postureAnalysisToggle;

        [Header("Preview")]
        [SerializeField] private TMP_Text summaryPreviewLabel;
        [SerializeField] private Button recommendedDemoSetupButton;
        [SerializeField] private TMP_Text recommendedDemoSetupLabel;
        [SerializeField] private bool createRecommendedDemoSetupButton = false;

        private bool recommendedPresetApplied;

        private void Awake()
        {
            EnsureRecommendedDemoSetupButton();
            HideRecommendedSetupShortcut();
            HidePostureAnalysisToggle();
            RemoveCustomDropdownArtifacts();
            ApplyDropdownTheme();
            ApplyDropdownOptionLabels();
            ApplyFinalSetupCopy();
            ApplyContextDescriptionLabels();

            if (durationSlider != null)
            {
                durationSlider.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            AddToggleListener(eyeTrackingToggle);
            AddToggleListener(gazeScoringToggle);
            AddToggleListener(performanceScoringToggle);
            AddToggleListener(voiceAnalysisToggle);
            AddToggleListener(postureAnalysisToggle);

            if (recommendedDemoSetupButton != null && recommendedDemoSetupButton.gameObject.activeInHierarchy)
            {
                recommendedDemoSetupButton.onClick.AddListener(ApplyRecommendedDemoSetup);
            }
        }

        private void OnEnable()
        {
            EnsureRecommendedDemoSetupButton();
            HideRecommendedSetupShortcut();
            HidePostureAnalysisToggle();
            RemoveCustomDropdownArtifacts();
            ApplyDropdownTheme();
            ApplyDropdownOptionLabels();
            ApplyFinalSetupCopy();
            LoadFromRuntime();
            ApplyContextDescriptionLabels();
        }

        private void LateUpdate()
        {
            StyleOpenDropdownList(difficultyDropdown);
            StyleOpenDropdownList(audienceDropdown);
            StyleOpenDropdownList(feedbackDropdown);
        }

        public SessionConfig BuildConfigSnapshot()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();

            if (durationSlider != null)
            {
                config.SessionDurationSeconds = durationSliderUsesMinutes
                    ? durationSlider.value * 60f
                    : durationSlider.value;
            }

            if (difficultyDropdown != null)
            {
                config.DifficultyLevel = (SessionDifficulty)Mathf.Clamp(
                    difficultyDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(SessionDifficulty)).Length - 1);
            }

            if (audienceDropdown != null)
            {
                config.AudiencePreset = (AudiencePreset)Mathf.Clamp(
                    audienceDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(AudiencePreset)).Length - 1);
            }

            if (feedbackDropdown != null)
            {
                config.FeedbackLevel = (FeedbackLevel)Mathf.Clamp(
                    feedbackDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(FeedbackLevel)).Length - 1);
            }

            if (eyeTrackingToggle != null)
            {
                config.EyeTrackingEnabled = eyeTrackingToggle.isOn;
            }

            if (gazeScoringToggle != null)
            {
                config.GazeScoringEnabled = gazeScoringToggle.isOn;
            }

            if (performanceScoringToggle != null)
            {
                config.PerformanceScoringEnabled = performanceScoringToggle.isOn;
            }

            if (voiceAnalysisToggle != null)
            {
                config.VoiceAnalysisEnabled = voiceAnalysisToggle.isOn;
            }

            config.PostureAnalysisEnabled = false;

            config.Normalize();
            return config;
        }

        public void PushCurrentUIToRuntime()
        {
            SessionConfig config = BuildConfigSnapshot();
            runtimeState?.ApplySessionConfig(config);
        }

        public void LoadFromRuntime()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();

            if (durationSlider != null)
            {
                float sliderValue = durationSliderUsesMinutes
                    ? config.SessionDurationSeconds / 60f
                    : config.SessionDurationSeconds;
                sliderValue = Mathf.Clamp(sliderValue, durationSlider.minValue, durationSlider.maxValue);
                durationSlider.SetValueWithoutNotify(sliderValue);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)config.DifficultyLevel);
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.SetValueWithoutNotify((int)config.AudiencePreset);
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.SetValueWithoutNotify((int)config.FeedbackLevel);
            }

            SetToggleWithoutNotify(eyeTrackingToggle, config.EyeTrackingEnabled);
            SetToggleWithoutNotify(gazeScoringToggle, config.GazeScoringEnabled);
            SetToggleWithoutNotify(performanceScoringToggle, config.PerformanceScoringEnabled);
            SetToggleWithoutNotify(voiceAnalysisToggle, config.VoiceAnalysisEnabled);
            SetToggleWithoutNotify(postureAnalysisToggle, false);

            RefreshSummaryPreview();
        }

        public void RefreshSummaryPreview()
        {
            SessionConfig snapshot = BuildConfigSnapshot();

            if (durationValueLabel != null)
            {
                durationValueLabel.text = snapshot.GetDurationDisplay();
            }

            if (summaryPreviewLabel != null)
            {
                string environmentName = !string.IsNullOrWhiteSpace(snapshot.SelectedEnvironmentName)
                    ? snapshot.SelectedEnvironmentName
                    : "Choose an environment";
                string launchNote = !snapshot.HasAnyScoringEnabled
                    ? "\nNote: core scoring is off, so results will be limited."
                    : string.Empty;

                summaryPreviewLabel.text =
                    $"Room: {environmentName}\n" +
                    $"Mode: {snapshot.PracticeMode}  |  {snapshot.GetDurationDisplay()}\n" +
                    $"Context: {FormatDifficulty(snapshot.DifficultyLevel)} / {FormatAudience(snapshot.AudiencePreset)}\n" +
                    $"Feedback: {FormatFeedback(snapshot.FeedbackLevel)}\n" +
                    $"Systems: {BuildCompactSystemsSummary(snapshot)}" +
                    (recommendedPresetApplied ? "\nPreset: Recommended setup applied." : string.Empty) +
                    launchNote;
            }
        }

        public void ApplyRecommendedDemoSetup()
        {
            recommendedPresetApplied = true;

            if (durationSlider != null)
            {
                float recommendedDuration = durationSliderUsesMinutes ? 3f : 180f;
                durationSlider.SetValueWithoutNotify(Mathf.Clamp(
                    recommendedDuration,
                    durationSlider.minValue,
                    durationSlider.maxValue));
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)SessionDifficulty.Normal);
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.SetValueWithoutNotify((int)AudiencePreset.Neutral);
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.SetValueWithoutNotify((int)FeedbackLevel.Standard);
            }

            SetToggleWithoutNotify(eyeTrackingToggle, true);
            SetToggleWithoutNotify(gazeScoringToggle, true);
            SetToggleWithoutNotify(performanceScoringToggle, true);
            SetToggleWithoutNotify(voiceAnalysisToggle, true);
            SetToggleWithoutNotify(postureAnalysisToggle, false);

            PushCurrentUIToRuntime();
            ApplyContextDescriptionLabels();
            RefreshSummaryPreview();
        }

        private static void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        private void AddToggleListener(Toggle toggle)
        {
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }
        }

        private void HandleConfigInputChanged()
        {
            recommendedPresetApplied = false;
            ApplyContextDescriptionLabels();
            RefreshSummaryPreview();
        }

        private void ApplyDropdownTheme()
        {
            StyleDropdown(difficultyDropdown);
            StyleDropdown(audienceDropdown);
            StyleDropdown(feedbackDropdown);
        }

        private void HidePostureAnalysisToggle()
        {
            if (postureAnalysisToggle == null)
            {
                return;
            }

            postureAnalysisToggle.SetIsOnWithoutNotify(false);
            Transform row = postureAnalysisToggle.transform.parent;
            postureAnalysisToggle.gameObject.SetActive(false);
            if (row != null && row.name == "AnalysisRowC")
            {
                row.gameObject.SetActive(false);
            }
        }

        private void ApplyDropdownOptionLabels()
        {
            SetDropdownOptionsPreservingValue(
                difficultyDropdown,
                "Easy",
                "Normal",
                "Hard",
                "Expert");
            SetDropdownOptionsPreservingValue(
                audienceDropdown,
                "Supportive",
                "Neutral",
                "Challenging");
            SetDropdownOptionsPreservingValue(
                feedbackDropdown,
                "Minimal",
                "Standard",
                "Detailed");
        }

        private void RemoveCustomDropdownArtifacts()
        {
            RemoveCustomDropdownArtifact(difficultyDropdown);
            RemoveCustomDropdownArtifact(audienceDropdown);
            RemoveCustomDropdownArtifact(feedbackDropdown);

            Transform panel = transform.Find("CustomDropdownOptions");
            if (panel != null)
            {
                Destroy(panel.gameObject);
            }
        }

        private static void RemoveCustomDropdownArtifact(TMP_Dropdown dropdown)
        {
            Transform clickTarget = dropdown != null
                ? dropdown.transform.Find("CustomDropdownClickTarget")
                : null;
            if (clickTarget != null)
            {
                Destroy(clickTarget.gameObject);
            }
        }

        private static void SetDropdownOptionsPreservingValue(TMP_Dropdown dropdown, params string[] labels)
        {
            if (dropdown == null || labels == null || labels.Length == 0)
            {
                return;
            }

            int value = Mathf.Clamp(dropdown.value, 0, labels.Length - 1);
            dropdown.ClearOptions();

            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>(labels.Length);
            for (int index = 0; index < labels.Length; index++)
            {
                options.Add(new TMP_Dropdown.OptionData(labels[index]));
            }

            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(value);
            dropdown.RefreshShownValue();
        }

        private void ApplyContextDescriptionLabels()
        {
            SetFieldDescription(
                difficultyDropdown,
                "DIFFICULTY",
                FormatDifficulty((SessionDifficulty)Mathf.Clamp(
                    difficultyDropdown != null ? difficultyDropdown.value : (int)SessionDifficulty.Normal,
                    0,
                    System.Enum.GetValues(typeof(SessionDifficulty)).Length - 1)));
            SetFieldDescription(
                audienceDropdown,
                "AUDIENCE",
                FormatAudience((AudiencePreset)Mathf.Clamp(
                    audienceDropdown != null ? audienceDropdown.value : (int)AudiencePreset.Neutral,
                    0,
                    System.Enum.GetValues(typeof(AudiencePreset)).Length - 1)));
            SetFieldDescription(
                feedbackDropdown,
                "FEEDBACK",
                FormatFeedback((FeedbackLevel)Mathf.Clamp(
                    feedbackDropdown != null ? feedbackDropdown.value : (int)FeedbackLevel.Standard,
                    0,
                    System.Enum.GetValues(typeof(FeedbackLevel)).Length - 1)));
        }

        private static void SetFieldDescription(TMP_Dropdown dropdown, string title, string description)
        {
            if (dropdown == null)
            {
                return;
            }

            Transform labelTransform = dropdown.transform.parent != null
                ? dropdown.transform.parent.Find("FieldLabel")
                : null;
            TMP_Text label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label == null)
            {
                return;
            }

            label.text = $"{title}\n<size=11>{description}</size>";
            label.fontSize = 13f;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;

            LayoutElement layoutElement = label.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = 34f;
            }

            LayoutElement cardLayoutElement = dropdown.transform.parent.GetComponent<LayoutElement>();
            if (cardLayoutElement != null)
            {
                cardLayoutElement.preferredHeight = Mathf.Max(cardLayoutElement.preferredHeight, 108f);
            }
        }

        private static void StyleDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Color captionTextColor = new Color(0.92f, 0.96f, 1f, 1f);
            Color optionTextColor = new Color(0.04f, 0.07f, 0.11f, 1f);
            Color fieldColor = new Color(0.09f, 0.13f, 0.19f, 1f);
            Color popupColor = new Color(0.86f, 0.92f, 0.98f, 1f);
            Color optionColor = new Color(0.76f, 0.86f, 0.96f, 1f);
            Color accentColor = new Color(0.04f, 0.42f, 0.72f, 1f);

            Image fieldImage = dropdown.GetComponent<Image>();
            if (fieldImage != null)
            {
                fieldImage.color = fieldColor;
            }

            ColorBlock dropdownColors = dropdown.colors;
            dropdownColors.normalColor = Color.white;
            dropdownColors.highlightedColor = new Color(0.86f, 0.94f, 1f, 1f);
            dropdownColors.pressedColor = new Color(0.70f, 0.86f, 1f, 1f);
            dropdownColors.selectedColor = Color.white;
            dropdownColors.disabledColor = new Color(0.42f, 0.46f, 0.52f, 0.75f);
            dropdownColors.fadeDuration = 0.08f;
            dropdown.colors = dropdownColors;

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = captionTextColor;
                dropdown.captionText.fontSize = 17f;
                dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.color = optionTextColor;
                dropdown.itemText.fontSize = 18f;
                dropdown.itemText.fontStyle = FontStyles.Bold;
                dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
                dropdown.itemText.overrideColorTags = true;
            }

            RectTransform template = dropdown.template != null
                ? dropdown.template
                : dropdown.transform.Find("Template") as RectTransform;

            if (template == null)
            {
                return;
            }

            ConfigureDropdownTemplateLayout(dropdown, template);

            Image templateImage = template.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.color = popupColor;
            }

            Image[] images = template.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image == null)
                {
                    continue;
                }

                if (image.name == "Item Background")
                {
                    image.color = optionColor;
                }
                else if (image.name == "Checkmark")
                {
                    image.color = accentColor;
                }
                else if (image.name == "Scrollbar" || image.name == "Sliding Area")
                {
                    image.color = new Color(0.05f, 0.07f, 0.10f, 1f);
                }
                else if (image.name == "Handle")
                {
                    image.color = accentColor;
                }
            }

            TMP_Text[] labels = template.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                TMP_Text label = labels[index];
                if (label == null)
                {
                    continue;
                }

                label.color = optionTextColor;
                label.fontSize = Mathf.Max(label.fontSize, 18f);
                label.fontStyle = FontStyles.Bold;
                label.overrideColorTags = true;
                label.transform.SetAsLastSibling();
            }

            Toggle[] toggles = template.GetComponentsInChildren<Toggle>(true);
            for (int index = 0; index < toggles.Length; index++)
            {
                Toggle toggle = toggles[index];
                if (toggle == null)
                {
                    continue;
                }

                if (toggle.targetGraphic is Image targetImage)
                {
                    targetImage.color = optionColor;
                }

                ColorBlock toggleColors = toggle.colors;
                toggleColors.normalColor = Color.white;
                toggleColors.highlightedColor = Color.white;
                toggleColors.pressedColor = Color.white;
                toggleColors.selectedColor = Color.white;
                toggleColors.disabledColor = new Color(0.36f, 0.39f, 0.44f, 0.75f);
                toggleColors.fadeDuration = 0.08f;
                toggle.colors = toggleColors;

                TMP_Text[] toggleLabels = toggle.GetComponentsInChildren<TMP_Text>(true);
                for (int labelIndex = 0; labelIndex < toggleLabels.Length; labelIndex++)
                {
                    TMP_Text label = toggleLabels[labelIndex];
                    if (label == null)
                    {
                        continue;
                    }

                    label.color = optionTextColor;
                    label.fontSize = Mathf.Max(label.fontSize, 18f);
                    label.fontStyle = FontStyles.Bold;
                    label.overrideColorTags = true;
                    label.transform.SetAsLastSibling();
                }
            }
        }

        private static void ConfigureDropdownTemplateLayout(TMP_Dropdown dropdown, RectTransform template)
        {
            const float optionHeight = 36f;
            int optionCount = dropdown != null ? Mathf.Max(dropdown.options.Count, 1) : 1;
            template.sizeDelta = new Vector2(template.sizeDelta.x, Mathf.Max(108f, optionHeight * optionCount));

            Toggle itemToggle = template.GetComponentInChildren<Toggle>(true);
            RectTransform itemRect = itemToggle != null ? itemToggle.transform as RectTransform : null;
            if (itemRect != null)
            {
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, optionHeight);
            }

            TMP_Text itemLabel = dropdown != null ? dropdown.itemText : null;
            RectTransform labelRect = itemLabel != null ? itemLabel.rectTransform : null;
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(34f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);
                labelRect.localScale = Vector3.one;
            }
        }

        private static void StyleOpenDropdownList(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Transform dropdownList = dropdown.transform.Find("Dropdown List");
            if (dropdownList == null)
            {
                return;
            }

            Color textColor = new Color(0.04f, 0.07f, 0.11f, 1f);
            Color popupColor = new Color(0.86f, 0.92f, 0.98f, 1f);
            Color optionColor = new Color(0.76f, 0.86f, 0.96f, 1f);
            Color accentColor = new Color(0.04f, 0.42f, 0.72f, 1f);

            Image[] images = dropdownList.GetComponentsInChildren<Image>(true);
            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image == null)
                {
                    continue;
                }

                if (image.name == "Dropdown List" || image.name == "Template")
                {
                    image.color = popupColor;
                }
                else if (image.name == "Item Background")
                {
                    image.color = optionColor;
                }
                else if (image.name == "Checkmark" || image.name == "Handle")
                {
                    image.color = accentColor;
                }
            }

            Toggle[] toggles = dropdownList.GetComponentsInChildren<Toggle>(true);
            int optionIndex = 0;
            for (int index = 0; index < toggles.Length; index++)
            {
                Toggle toggle = toggles[index];
                if (toggle == null)
                {
                    continue;
                }

                if (!toggle.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (toggle.targetGraphic is Image targetImage)
                {
                    targetImage.color = optionColor;
                }

                ColorBlock toggleColors = toggle.colors;
                toggleColors.normalColor = Color.white;
                toggleColors.highlightedColor = Color.white;
                toggleColors.pressedColor = Color.white;
                toggleColors.selectedColor = Color.white;
                toggleColors.disabledColor = new Color(0.36f, 0.39f, 0.44f, 0.75f);
                toggleColors.fadeDuration = 0.08f;
                toggle.colors = toggleColors;

                if (optionIndex < dropdown.options.Count)
                {
                    TMP_Text optionLabel = EnsureDropdownOptionLabel(toggle.transform);
                    optionLabel.text = dropdown.options[optionIndex].text;
                    optionLabel.color = textColor;
                    optionLabel.alpha = 1f;
                    optionLabel.fontSize = Mathf.Max(optionLabel.fontSize, 18f);
                    optionLabel.fontStyle = FontStyles.Bold;
                    optionLabel.alignment = TextAlignmentOptions.MidlineLeft;
                    optionLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    optionLabel.overflowMode = TextOverflowModes.Ellipsis;
                    optionLabel.overrideColorTags = true;
                    optionLabel.raycastTarget = false;
                    optionLabel.transform.SetAsLastSibling();
                    optionIndex++;
                }
            }

            TMP_Text[] labels = dropdownList.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                TMP_Text label = labels[index];
                if (label == null)
                {
                    continue;
                }

                label.color = textColor;
                label.alpha = 1f;
                label.fontSize = Mathf.Max(label.fontSize, 18f);
                label.fontStyle = FontStyles.Bold;
                label.overrideColorTags = true;
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
            }
        }

        private static TMP_Text EnsureDropdownOptionLabel(Transform toggleTransform)
        {
            TMP_Text label = toggleTransform.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                GameObject labelObject = new GameObject("RuntimeOptionLabel", typeof(RectTransform));
                labelObject.transform.SetParent(toggleTransform, false);
                label = labelObject.AddComponent<TextMeshProUGUI>();
            }

            RectTransform rectTransform = label.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(36f, 0f);
            rectTransform.offsetMax = new Vector2(-8f, 0f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            return label;
        }

        private static string BuildCompactSystemsSummary(SessionConfig snapshot)
        {
            if (snapshot == null)
            {
                return "None";
            }

            System.Collections.Generic.List<string> enabledSystems = new System.Collections.Generic.List<string>();

            if (snapshot.EyeTrackingEnabled)
            {
                enabledSystems.Add("Eye");
            }

            if (snapshot.GazeScoringEnabled)
            {
                enabledSystems.Add("Gaze");
            }

            if (snapshot.PerformanceScoringEnabled)
            {
                enabledSystems.Add("Performance");
            }

            if (snapshot.VoiceAnalysisEnabled)
            {
                enabledSystems.Add("Voice");
            }

            if (enabledSystems.Count == 0)
            {
                return "None";
            }

            if (enabledSystems.Count <= 3)
            {
                return string.Join(", ", enabledSystems);
            }

            return string.Join(", ", enabledSystems.GetRange(0, 3)) + $" +{enabledSystems.Count - 3}";
        }

        private static string FormatDifficulty(SessionDifficulty difficulty)
        {
            return difficulty switch
            {
                SessionDifficulty.Easy => "Gentle pacing",
                SessionDifficulty.Normal => "Balanced practice",
                SessionDifficulty.Hard => "More pressure",
                SessionDifficulty.Expert => "Final rehearsal",
                _ => difficulty.ToString()
            };
        }

        private static string FormatAudience(AudiencePreset audience)
        {
            return audience switch
            {
                AudiencePreset.Supportive => "Supportive audience",
                AudiencePreset.Neutral => "Calm audience",
                AudiencePreset.Challenging => "Challenging audience",
                _ => audience.ToString()
            };
        }

        private static string FormatFeedback(FeedbackLevel feedbackLevel)
        {
            return feedbackLevel switch
            {
                FeedbackLevel.Minimal => "Key score only",
                FeedbackLevel.Standard => "Score + tips",
                FeedbackLevel.Detailed => "Full coaching",
                _ => feedbackLevel.ToString()
            };
        }

        private void ApplyFinalSetupCopy()
        {
            SetChildText("PanelSubtitle", "Set timing, audience context, and active scoring systems before launch.");
            SetChildText("SummaryLead", "Review the selected setup before continuing.");
            SetSummaryStripText("SummaryHint", "Launch Safety", "Configuration changes are applied before the room opens.");
            SetSummaryStripText("CapabilityHint", "Scoring Systems", "Enabled systems contribute to the session summary.");
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

        private void EnsureRecommendedDemoSetupButton()
        {
            if (!createRecommendedDemoSetupButton || recommendedDemoSetupButton != null)
            {
                return;
            }

            Transform parent = summaryPreviewLabel != null && summaryPreviewLabel.transform.parent != null
                ? summaryPreviewLabel.transform.parent
                : transform;

            Transform existing = parent.Find("RecommendedDemoSetupButton");
            GameObject buttonObject = existing != null
                ? existing.gameObject
                : new GameObject("RecommendedDemoSetupButton", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, 10f);
            rectTransform.sizeDelta = new Vector2(0f, 42f);

            Image background = buttonObject.GetComponent<Image>();
            if (background == null)
            {
                background = buttonObject.AddComponent<Image>();
            }

            background.color = new Color(0.21f, 0.63f, 0.96f, 1f);

            recommendedDemoSetupButton = buttonObject.GetComponent<Button>();
            if (recommendedDemoSetupButton == null)
            {
                recommendedDemoSetupButton = buttonObject.AddComponent<Button>();
            }

            recommendedDemoSetupButton.targetGraphic = background;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = buttonObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = 42f;
            layoutElement.minHeight = 38f;

            Transform labelTransform = buttonObject.transform.Find("Label");
            GameObject labelObject = labelTransform != null
                ? labelTransform.gameObject
                : new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, 0f);

            recommendedDemoSetupLabel = labelObject.GetComponent<TMP_Text>();
            if (recommendedDemoSetupLabel == null)
            {
                recommendedDemoSetupLabel = labelObject.AddComponent<TextMeshProUGUI>();
            }

            recommendedDemoSetupLabel.text = "Use Recommended Setup";
            recommendedDemoSetupLabel.fontSize = 17f;
            recommendedDemoSetupLabel.fontStyle = FontStyles.Bold;
            recommendedDemoSetupLabel.alignment = TextAlignmentOptions.Center;
            recommendedDemoSetupLabel.color = Color.white;
            recommendedDemoSetupLabel.raycastTarget = false;

            ColorBlock colors = recommendedDemoSetupButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.99f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.90f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            recommendedDemoSetupButton.colors = colors;
        }

        private void HideRecommendedSetupShortcut()
        {
            if (recommendedDemoSetupButton == null)
            {
                Transform parent = summaryPreviewLabel != null && summaryPreviewLabel.transform.parent != null
                    ? summaryPreviewLabel.transform.parent
                    : transform;
                Transform existing = parent.Find("RecommendedDemoSetupButton");
                if (existing != null)
                {
                    recommendedDemoSetupButton = existing.GetComponent<Button>();
                    recommendedDemoSetupLabel = existing.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (recommendedDemoSetupButton != null)
            {
                recommendedDemoSetupButton.onClick.RemoveListener(ApplyRecommendedDemoSetup);
                recommendedDemoSetupButton.gameObject.SetActive(false);
            }
        }
    }
}
