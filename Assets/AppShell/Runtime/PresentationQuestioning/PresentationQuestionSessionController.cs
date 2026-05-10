using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.XR;
using SpeechPipeline;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Presentation;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public class PresentationQuestionSessionController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private SpeechPipelineController speechPipelineController;
        [SerializeField] private PresentationAnswerRecorder answerRecorder;
        [SerializeField] private AppPanelView questionPanel;
        [SerializeField] private CanvasGroup dimmerCanvasGroup;
        [SerializeField] private float dimAlpha = 0.58f;
        [SerializeField] private float silenceStopSeconds = 2f;

        private TMP_Text titleLabel;
        private TMP_Text progressLabel;
        private TMP_Text personaLabel;
        private TMP_Text questionLabel;
        private TMP_Text statusLabel;
        private TMP_Text transcriptLabel;
        private TMP_InputField typedAnswerInput;
        private Button primaryButton;
        private Button skipButton;
        private Button finishButton;

        private bool primaryRequested;
        private bool skipRequested;
        private bool finishRequested;
        private bool legacySubmitWasPressed;
        private bool legacySkipWasPressed;

        private static readonly List<UnityEngine.XR.InputDevice> LegacyControllers =
            new List<UnityEngine.XR.InputDevice>();

        public bool CanRun(SessionConfig config, out string reason)
        {
            reason = string.Empty;
            PresentationDeckReference deck = config != null ? config.SelectedPresentation : null;
            if (deck == null || !deck.HasPages)
            {
                reason = "No presentation deck selected.";
                return false;
            }

            if (!PresentationQuestionGenerationService.TryLoadQuestionSet(deck, out PresentationQuestionSet questionSet) ||
                questionSet == null ||
                !questionSet.HasQuestions)
            {
                reason = string.IsNullOrWhiteSpace(deck.QuestionStatus)
                    ? "No generated question set."
                    : deck.QuestionStatus;
                return false;
            }

            return true;
        }

        public IEnumerator Run(
            SessionConfig config,
            SpeechPipelineController speechController,
            Action<PresentationQaResult> completed)
        {
            runtimeState ??= AppRuntimeState.GetOrCreate();
            speechPipelineController = speechController != null ? speechController : speechPipelineController;
            EnsureUi();
            EnsureRecorder();

            PresentationDeckReference deck = config != null ? config.SelectedPresentation : null;
            if (deck == null ||
                !PresentationQuestionGenerationService.TryLoadQuestionSet(deck, out PresentationQuestionSet questionSet) ||
                questionSet == null ||
                !questionSet.HasQuestions)
            {
                completed?.Invoke(null);
                yield break;
            }

            var result = new PresentationQaResult
            {
                deckId = deck.DeckId,
                deckName = deck.DisplayName,
                status = "Started"
            };

            ShowPanel();

            for (int index = 0; index < questionSet.questions.Count; index++)
            {
                PresentationQuestion question = questionSet.questions[index];
                if (question == null)
                {
                    continue;
                }

                ForceAudienceQuestionState(index);
                yield return AskQuestionRoutine(question, index + 1, questionSet.questions.Count, result);
                if (finishRequested)
                {
                    break;
                }
            }

            result.completedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            result.status = result.HasAnswers ? "Completed" : "Skipped";
            result.summary = BuildResultSummary(result);
            SetStatus(result.summary);
            yield return new WaitForSecondsRealtime(0.35f);

            HidePanel();
            completed?.Invoke(result);
        }

        private IEnumerator AskQuestionRoutine(
            PresentationQuestion question,
            int questionNumber,
            int questionCount,
            PresentationQaResult result)
        {
            primaryRequested = false;
            skipRequested = false;
            finishRequested = false;

            if (typedAnswerInput != null)
            {
                typedAnswerInput.text = string.Empty;
            }

            SetQuestion(question, questionNumber, questionCount);
            SetPrimaryButtonLabel("Start Answer");
            SetStatus("Listen to the audience question. Trigger/A or Enter starts your answer. B/Y skips.");
            SetTranscript(string.Empty);

            bool recording = false;
            bool answerReady = false;
            string transcript = string.Empty;

            while (!answerReady && !skipRequested && !finishRequested)
            {
                if (WasSubmitPressedThisFrame() || primaryRequested)
                {
                    primaryRequested = false;
                    if (recording)
                    {
                        transcript = StopRecording();
                        recording = false;
                        answerReady = true;
                    }
                    else if (HasTypedAnswer())
                    {
                        transcript = typedAnswerInput.text.Trim();
                        answerReady = true;
                    }
                    else
                    {
                        recording = StartRecording();
                        if (!recording)
                        {
                            SetStatus("Speech capture is unavailable here. Type an answer, then press Submit Answer.");
                            SetPrimaryButtonLabel("Submit Answer");
                            FocusTypedInput();
                        }
                    }
                }

                if (WasSkipPressedThisFrame())
                {
                    skipRequested = true;
                }

                if (recording)
                {
                    UpdateRecordingStatus();
                    if (answerRecorder != null &&
                        answerRecorder.HasTranscript &&
                        Time.realtimeSinceStartup - answerRecorder.LastTranscriptTime >= silenceStopSeconds)
                    {
                        transcript = StopRecording();
                        recording = false;
                        answerReady = true;
                    }
                }

                yield return null;
            }

            if (recording)
            {
                transcript = StopRecording();
            }

            var answer = new PresentationQaAnswer
            {
                questionId = question.id,
                question = question.question,
                expectedAnswer = question.expectedAnswer,
                answerTranscript = transcript,
                skipped = skipRequested || string.IsNullOrWhiteSpace(transcript)
            };

            if (finishRequested && string.IsNullOrWhiteSpace(transcript))
            {
                answer.skipped = true;
            }

            if (answer.skipped)
            {
                answer.feedback = new PresentationAnswerFeedback
                {
                    accuracy = 0f,
                    coverage = 0f,
                    clarity = 0f,
                    summary = "Question skipped or no answer was captured.",
                    betterAnswer = question.expectedAnswer ?? string.Empty,
                    status = "Skipped"
                };
                result.answers.Add(answer);
                SetStatus("Question skipped.");
                yield return new WaitForSecondsRealtime(0.2f);
                yield break;
            }

            SetTranscript(answer.answerTranscript);
            SetStatus("Evaluating answer with LLM...");
            PresentationAnswerFeedback feedback = null;
            yield return PresentationAnswerEvaluationService.EvaluateAnswer(
                question,
                answer.answerTranscript,
                value => feedback = value);
            answer.feedback = feedback;
            result.answers.Add(answer);
            SetStatus(feedback != null ? feedback.summary : "Evaluation unavailable.");
            yield return new WaitForSecondsRealtime(0.35f);
        }

        private bool StartRecording()
        {
            EnsureRecorder();
            if (answerRecorder == null)
            {
                return false;
            }

            answerRecorder.Configure(speechPipelineController);
            bool started = answerRecorder.BeginRecording();
            if (started)
            {
                SetPrimaryButtonLabel("Stop Answer");
                SetStatus("Recording answer...");
            }

            return started;
        }

        private string StopRecording()
        {
            string speechTranscript = answerRecorder != null ? answerRecorder.EndRecording() : string.Empty;
            string typedTranscript = typedAnswerInput != null ? typedAnswerInput.text.Trim() : string.Empty;
            SetPrimaryButtonLabel("Submit Answer");
            return !string.IsNullOrWhiteSpace(speechTranscript) ? speechTranscript : typedTranscript;
        }

        private bool HasTypedAnswer()
        {
            return typedAnswerInput != null && !string.IsNullOrWhiteSpace(typedAnswerInput.text);
        }

        private void UpdateRecordingStatus()
        {
            string transcript = answerRecorder != null ? answerRecorder.CurrentTranscript : string.Empty;
            SetTranscript(transcript);
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                SetStatus("Recording answer... pause for 2 seconds or press Trigger/A to stop.");
            }
        }

        private void EnsureRecorder()
        {
            if (answerRecorder == null)
            {
                answerRecorder = GetComponent<PresentationAnswerRecorder>();
                if (answerRecorder == null)
                {
                    answerRecorder = gameObject.AddComponent<PresentationAnswerRecorder>();
                }
            }

            answerRecorder.Configure(speechPipelineController);
        }

        private void EnsureUi()
        {
            if (questionPanel != null)
            {
                return;
            }

            Transform existing = transform.Find("AudienceQaOverlayPanel");
            if (existing != null)
            {
                questionPanel = existing.GetComponent<AppPanelView>();
                if (questionPanel == null)
                {
                    questionPanel = existing.gameObject.AddComponent<AppPanelView>();
                }
                BindExistingUi();
                return;
            }

            BuildUi();
        }

        private void BuildUi()
        {
            GameObject panelObject = new GameObject(
                "AudienceQaOverlayPanel",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(AppPanelView));
            panelObject.transform.SetParent(transform, false);
            questionPanel = panelObject.GetComponent<AppPanelView>();
            questionPanel.SetPanelType(AppPanelType.AudienceQa);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 780f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.035f, 0.055f, 0.085f, 0.97f);
            panelImage.raycastTarget = true;
            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.78f, 0.96f, 0.34f);
            outline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleLabel = CreateText(panelObject.transform, "QaTitle", "Audience Q&A", 38f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.12f, 0.78f, 0.96f, 1f), 48f);
            progressLabel = CreateText(panelObject.transform, "QaProgress", "Question 1 / 3", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.72f, 0.82f, 0.92f, 1f), 30f);
            personaLabel = CreateText(panelObject.transform, "QaPersona", "Curious audience member", 18f, FontStyles.Italic, TextAlignmentOptions.Left, new Color(0.58f, 0.68f, 0.78f, 1f), 28f);
            questionLabel = CreateText(panelObject.transform, "QaQuestion", string.Empty, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, 150f);

            typedAnswerInput = CreateInputField(panelObject.transform);
            transcriptLabel = CreateText(panelObject.transform, "QaTranscript", string.Empty, 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.82f, 0.88f, 0.94f, 1f), 98f);
            statusLabel = CreateText(panelObject.transform, "QaStatus", string.Empty, 19f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.92f, 0.95f, 0.98f, 1f), 86f);

            GameObject buttonRow = new GameObject("QaButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttonRow.transform.SetParent(panelObject.transform, false);
            LayoutElement rowLayout = buttonRow.GetComponent<LayoutElement>();
            rowLayout.minHeight = 84f;
            rowLayout.preferredHeight = 84f;
            HorizontalLayoutGroup row = buttonRow.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;

            primaryButton = CreateButton(buttonRow.transform, "QaPrimaryButton", "Start Answer", () => primaryRequested = true, new Color(0.21f, 0.63f, 0.96f, 1f));
            skipButton = CreateButton(buttonRow.transform, "QaSkipButton", "Skip", () => skipRequested = true, new Color(0.11f, 0.19f, 0.27f, 0.96f));
            finishButton = CreateButton(buttonRow.transform, "QaFinishButton", "Finish Q&A", () => finishRequested = true, new Color(0.16f, 0.16f, 0.22f, 0.96f));

            questionPanel.Hide();
        }

        private void BindExistingUi()
        {
            titleLabel = FindText("QaTitle");
            progressLabel = FindText("QaProgress");
            personaLabel = FindText("QaPersona");
            questionLabel = FindText("QaQuestion");
            statusLabel = FindText("QaStatus");
            transcriptLabel = FindText("QaTranscript");
            typedAnswerInput = questionPanel.GetComponentInChildren<TMP_InputField>(true);
            primaryButton = FindButton("QaPrimaryButton");
            skipButton = FindButton("QaSkipButton");
            finishButton = FindButton("QaFinishButton");
        }

        private TMP_InputField CreateInputField(Transform parent)
        {
            GameObject root = new GameObject("QaTypedAnswerInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.minHeight = 78f;
            layout.preferredHeight = 78f;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
            image.raycastTarget = true;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 21f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderObject.transform.SetParent(root.transform, false);
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(18f, 8f);
            placeholderRect.offsetMax = new Vector2(-18f, -8f);
            TextMeshProUGUI placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.text = "Desktop fallback: type answer here, then press Submit Answer.";
            placeholder.fontSize = 19f;
            placeholder.color = new Color(0.58f, 0.68f, 0.78f, 0.8f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 1200;
            return input;
        }

        private TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            float preferredHeight)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action,
            Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 78f;
            layout.preferredHeight = 78f;
            layout.flexibleWidth = 1f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(0.24f, 0.42f, 0.55f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.35f, 0.62f, 0.78f, 1f);
            button.colors = colors;

            TMP_Text buttonText = CreateText(buttonObject.transform, "Label", label, 22f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, 78f);
            RectTransform textRect = buttonText.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            LayoutElement textLayout = buttonText.GetComponent<LayoutElement>();
            textLayout.ignoreLayout = true;
            return button;
        }

        private void ShowPanel()
        {
            EnsureUi();
            runtimeState?.SetPauseMenuVisible(false);
            runtimeState?.SetResultsOverlayVisible(true);
            if (dimmerCanvasGroup == null)
            {
                Transform dimmer = transform.Find("Dimmer");
                dimmerCanvasGroup = dimmer != null ? dimmer.GetComponent<CanvasGroup>() : null;
            }

            if (dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = dimAlpha;
                dimmerCanvasGroup.interactable = false;
                dimmerCanvasGroup.blocksRaycasts = false;
            }

            questionPanel.transform.SetAsLastSibling();
            questionPanel.Show();
        }

        private void HidePanel()
        {
            answerRecorder?.CancelRecording();
            questionPanel?.Hide();
            runtimeState?.SetResultsOverlayVisible(false);
            if (dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = 0f;
            }
        }

        private void SetQuestion(PresentationQuestion question, int questionNumber, int questionCount)
        {
            SetText(titleLabel, "Audience Q&A");
            SetText(progressLabel, $"Question {questionNumber} / {questionCount}");
            SetText(personaLabel, string.IsNullOrWhiteSpace(question.audiencePersona) ? "Audience member" : question.audiencePersona);
            SetText(questionLabel, question.question);
        }

        private void SetStatus(string value) => SetText(statusLabel, value);
        private void SetTranscript(string value) => SetText(transcriptLabel, string.IsNullOrWhiteSpace(value) ? "Transcript will appear here after speech is captured." : value);

        private void SetPrimaryButtonLabel(string value)
        {
            TMP_Text label = primaryButton != null ? primaryButton.GetComponentInChildren<TMP_Text>(true) : null;
            SetText(label, value);
        }

        private void FocusTypedInput()
        {
            if (typedAnswerInput == null || Keyboard.current == null)
            {
                return;
            }

            typedAnswerInput.Select();
            typedAnswerInput.ActivateInputField();
        }

        private TMP_Text FindText(string name)
        {
            if (questionPanel == null)
            {
                return null;
            }

            TMP_Text[] texts = questionPanel.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null && texts[index].gameObject.name == name)
                {
                    return texts[index];
                }
            }

            return null;
        }

        private Button FindButton(string name)
        {
            if (questionPanel == null)
            {
                return null;
            }

            Button[] buttons = questionPanel.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index] != null && buttons[index].gameObject.name == name)
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private void ForceAudienceQuestionState(int questionIndex)
        {
            AudienceBehaviorController audience = FindFirstObjectByType<AudienceBehaviorController>(FindObjectsInactive.Include);
            if (audience == null || audience.audienceMembers == null || audience.audienceMembers.Count == 0)
            {
                return;
            }

            int index = Mathf.Abs(questionIndex) % audience.audienceMembers.Count;
            AudienceMember member = audience.audienceMembers[index];
            if (member != null)
            {
                member.SetState(questionIndex % 2 == 0 ? AudienceState.NoteTaking : AudienceState.Attentive, true);
            }
        }

        private static string BuildResultSummary(PresentationQaResult result)
        {
            if (result == null || !result.HasAnswers)
            {
                return "Audience Q&A skipped.";
            }

            int evaluated = 0;
            float accuracy = 0f;
            for (int index = 0; index < result.answers.Count; index++)
            {
                PresentationQaAnswer answer = result.answers[index];
                if (answer == null || answer.skipped || answer.feedback == null || answer.feedback.status == "Evaluation unavailable")
                {
                    continue;
                }

                evaluated++;
                accuracy += answer.feedback.accuracy;
            }

            return evaluated > 0
                ? $"Audience Q&A complete. Average answer accuracy: {accuracy / evaluated:0}/100."
                : "Audience Q&A complete. Evaluation feedback was unavailable or skipped.";
        }

        private bool WasSubmitPressedThisFrame()
        {
            bool keyboardPressed = Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

            bool inputSystemPressed =
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "primaryButton") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "primaryButton");

            bool legacyPressed =
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.triggerButton) ||
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.primaryButton);
            bool legacyPressedThisFrame = legacyPressed && !legacySubmitWasPressed;
            legacySubmitWasPressed = legacyPressed;

            return keyboardPressed || inputSystemPressed || legacyPressedThisFrame;
        }

        private bool WasSkipPressedThisFrame()
        {
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool inputSystemPressed =
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "secondaryButton") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "secondaryButton");
            bool legacyPressed = IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.secondaryButton);
            bool legacyPressedThisFrame = legacyPressed && !legacySkipWasPressed;
            legacySkipWasPressed = legacyPressed;
            return keyboardPressed || inputSystemPressed || legacyPressedThisFrame;
        }

        private static bool WasControllerButtonPressedThisFrame(
            UnityEngine.InputSystem.XR.XRController controller,
            string controlName)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controlName))
            {
                return false;
            }

            ButtonControl control = controller.TryGetChildControl<ButtonControl>(controlName);
            return control != null && control.wasPressedThisFrame;
        }

        private static bool IsLegacyControllerButtonPressed(InputFeatureUsage<bool> usage)
        {
            RefreshLegacyControllers();
            for (int index = 0; index < LegacyControllers.Count; index++)
            {
                UnityEngine.XR.InputDevice device = LegacyControllers[index];
                if (device.isValid &&
                    device.TryGetFeatureValue(usage, out bool value) &&
                    value)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshLegacyControllers()
        {
            if (LegacyControllers.Count > 0)
            {
                for (int index = 0; index < LegacyControllers.Count; index++)
                {
                    if (LegacyControllers[index].isValid)
                    {
                        return;
                    }
                }
            }

            LegacyControllers.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, LegacyControllers);
        }
    }
}
