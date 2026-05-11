using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private AudienceQuestionBubblePresenter questionBubblePresenter;
        [SerializeField] private AppPanelView questionPanel;
        [SerializeField] private CanvasGroup dimmerCanvasGroup;
        [SerializeField] private bool showAudienceQuestionBubble = true;
        [SerializeField] private Vector3 qaPanelFollowOffset = new Vector3(0f, -0.78f, 1.05f);
        [SerializeField] private float qaPanelMinimumTargetY = 1.05f;

        private TMP_Text titleLabel;
        private TMP_Text progressLabel;
        private TMP_Text personaLabel;
        private TMP_Text questionLabel;
        private TMP_Text scoreLabel;
        private TMP_Text statusLabel;
        private TMP_Text transcriptLabel;
        private TMP_Text shortcutLabel;
        private TMP_InputField typedAnswerInput;
        private Button primaryButton;
        private Button skipButton;
        private Button finishButton;

        private bool primaryRequested;
        private bool finishRequested;
        private bool legacySubmitWasPressed;
        private bool legacySkipWasPressed;
        private bool currentQuestionUsesAudienceBubble;
        private WorldSpaceCanvasFollower qaPanelFollower;
        private global::MainController blockedMainController;
        private bool previousShellInputEnabled;
        private bool shellInputBlocked;
        private const float QuestionInputDebounceSeconds = 0.18f;

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

            BeginQuestionOverlay();
            finishRequested = false;
            int presentedQuestions = 0;

            for (int index = 0; index < questionSet.questions.Count; index++)
            {
                PresentationQuestion question = questionSet.questions[index];
                if (question == null)
                {
                    continue;
                }

                AudienceMember questionAsker = ForceAudienceQuestionState(index);
                ShowAudienceQuestionBubble(questionAsker, question, index + 1, questionSet.questions.Count);
                presentedQuestions++;
                yield return AskQuestionRoutine(question, index + 1, questionSet.questions.Count, result);
                if (finishRequested)
                {
                    break;
                }
            }

            HideAudienceQuestionBubble();
            result.completedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            result.status = "Completed";
            result.summary = BuildQuestionOnlySummary(presentedQuestions, questionSet.questions.Count);

            EndQuestionOverlay();
            completed?.Invoke(result);
        }

        private IEnumerator AskQuestionRoutine(
            PresentationQuestion question,
            int questionNumber,
            int questionCount,
            PresentationQaResult result)
        {
            ResetQuestionInputFlags();
            SetQuestion(question, questionNumber, questionCount);
            SetAnswerUiVisible(false);
            SetPrimaryButtonLabel(questionNumber < questionCount ? "Next Question" : "Finish Q&A");
            SetSkipButtonVisible(false);
            SetFinishButtonLabel("Finish Q&A");
            SetQuestionButtonInteractivity(true, false, questionNumber < questionCount);
            SetStatus("Answer aloud, then continue.");
            SetShortcutHint(BuildBubbleShortcutHint(questionNumber, questionCount));
            yield return WaitForQuestionControlsReleased();
            float inputReadyTime = Time.unscaledTime + QuestionInputDebounceSeconds;

            while (!primaryRequested && !finishRequested)
            {
                if (Time.unscaledTime < inputReadyTime)
                {
                    yield return null;
                    continue;
                }

                if (WasSubmitPressedThisFrame())
                {
                    primaryRequested = true;
                }

                if (WasSkipPressedThisFrame())
                {
                    finishRequested = true;
                }

                yield return null;
            }

            primaryRequested = false;
        }

        private IEnumerator WaitForQuestionControlsReleased()
        {
            ResetQuestionInputFlags();
            float timeout = Time.unscaledTime + 0.75f;
            while (Time.unscaledTime < timeout && (IsSubmitControlPressed() || IsSkipControlPressed()))
            {
                ResetQuestionInputFlags();
                yield return null;
            }

            ResetQuestionInputFlags();
            yield return null;
        }

        private void ResetQuestionInputFlags()
        {
            primaryRequested = false;
            legacySubmitWasPressed = IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.triggerButton) ||
                                     IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.primaryButton);
            legacySkipWasPressed = IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.secondaryButton);
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
            panelRect.sizeDelta = new Vector2(720f, 220f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.035f, 0.055f, 0.085f, 0.97f);
            panelImage.raycastTarget = true;
            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.78f, 0.96f, 0.34f);
            outline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 22, 22);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleLabel = CreateText(panelObject.transform, "QaTitle", "Q&A Controls", 26f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.12f, 0.78f, 0.96f, 1f), 32f);
            progressLabel = CreateText(panelObject.transform, "QaProgress", "Question 1 / 3", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.72f, 0.82f, 0.92f, 1f), 28f);
            personaLabel = CreateText(panelObject.transform, "QaPersona", "Curious audience member", 18f, FontStyles.Italic, TextAlignmentOptions.Left, new Color(0.58f, 0.68f, 0.78f, 1f), 26f);
            questionLabel = CreateText(panelObject.transform, "QaQuestion", string.Empty, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, 178f);
            statusLabel = CreateText(panelObject.transform, "QaStatus", string.Empty, 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.92f, 0.95f, 0.98f, 1f), 42f);
            shortcutLabel = CreateText(panelObject.transform, "QaShortcuts", string.Empty, 18f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.62f, 0.88f, 1f, 1f), 40f);

            GameObject buttonRow = new GameObject("QaButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttonRow.transform.SetParent(panelObject.transform, false);
            LayoutElement rowLayout = buttonRow.GetComponent<LayoutElement>();
            rowLayout.minHeight = 68f;
            rowLayout.preferredHeight = 68f;
            HorizontalLayoutGroup row = buttonRow.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;

            primaryButton = CreateButton(buttonRow.transform, "QaPrimaryButton", "Next Question", () => primaryRequested = true, new Color(0.21f, 0.63f, 0.96f, 1f));
            finishButton = CreateButton(buttonRow.transform, "QaFinishButton", "Finish Q&A", HandleFinishButtonPressed, new Color(0.16f, 0.16f, 0.22f, 0.96f));

            VrUiUsabilityUtility.ApplyReadablePanel(questionPanel, 18f, 68f, new Vector2(720f, 220f));
            questionPanel.Hide();
        }

        private void BindExistingUi()
        {
            titleLabel = FindText("QaTitle");
            progressLabel = FindText("QaProgress");
            personaLabel = FindText("QaPersona");
            questionLabel = FindText("QaQuestion");
            scoreLabel = FindText("QaScore");
            statusLabel = FindText("QaStatus");
            transcriptLabel = FindText("QaTranscript");
            shortcutLabel = FindText("QaShortcuts");
            typedAnswerInput = questionPanel.GetComponentInChildren<TMP_InputField>(true);
            primaryButton = FindButton("QaPrimaryButton");
            skipButton = FindButton("QaSkipButton");
            finishButton = FindButton("QaFinishButton");
            EnsureControlPanelExtras();
            WireQuestionButtons();
            SetAnswerUiVisible(false);
        }

        private void EnsureControlPanelExtras()
        {
            if (questionPanel == null)
            {
                return;
            }

            if (scoreLabel != null)
            {
                SetLabelVisible(scoreLabel, false);
            }

            if (shortcutLabel == null)
            {
                shortcutLabel = CreateText(questionPanel.transform, "QaShortcuts", string.Empty, 18f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.62f, 0.88f, 1f, 1f), 40f);
                shortcutLabel.transform.SetSiblingIndex(Mathf.Max(0, questionPanel.transform.childCount - 2));
            }
        }

        private void WireQuestionButtons()
        {
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
                primaryButton.onClick.AddListener(() => primaryRequested = true);
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(HandleSkipButtonPressed);
            }

            if (finishButton != null)
            {
                finishButton.onClick.RemoveAllListeners();
                finishButton.onClick.AddListener(HandleFinishButtonPressed);
            }
        }

        private void HandleSkipButtonPressed()
        {
            finishRequested = true;
        }

        private void HandleFinishButtonPressed()
        {
            finishRequested = true;
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
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(14f, fontSize * 0.72f);
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
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;
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

            TMP_Text buttonText = CreateText(buttonObject.transform, "Label", label, 20f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, 64f);
            RectTransform textRect = buttonText.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            LayoutElement textLayout = buttonText.GetComponent<LayoutElement>();
            textLayout.ignoreLayout = true;
            return button;
        }

        private void BeginQuestionOverlay()
        {
            runtimeState?.SetPauseMenuVisible(false);
            runtimeState?.SetResultsOverlayVisible(true);
            BlockShellSessionHotkeys();

            if (questionPanel == null)
            {
                Transform existing = transform.Find("AudienceQaOverlayPanel");
                questionPanel = existing != null ? existing.GetComponent<AppPanelView>() : null;
            }

            if (dimmerCanvasGroup == null)
            {
                Transform dimmer = transform.Find("Dimmer");
                dimmerCanvasGroup = dimmer != null ? dimmer.GetComponent<CanvasGroup>() : null;
            }

            if (dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = 0f;
                dimmerCanvasGroup.interactable = false;
                dimmerCanvasGroup.blocksRaycasts = false;
            }

            questionPanel?.Hide();
        }

        private void EndQuestionOverlay()
        {
            HideAudienceQuestionBubble();
            questionPanel?.Hide();
            runtimeState?.SetResultsOverlayVisible(false);
            if (dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = 0f;
            }

            RestoreShellSessionHotkeys();
        }

        private void BlockShellSessionHotkeys()
        {
            if (shellInputBlocked)
            {
                return;
            }

            blockedMainController = FindFirstObjectByType<global::MainController>(FindObjectsInactive.Include);
            if (blockedMainController == null)
            {
                return;
            }

            previousShellInputEnabled = blockedMainController.allowRuntimeInput;
            blockedMainController.SetShellInputEnabled(false);
            shellInputBlocked = true;
        }

        private void RestoreShellSessionHotkeys()
        {
            if (!shellInputBlocked)
            {
                return;
            }

            if (blockedMainController != null)
            {
                blockedMainController.SetShellInputEnabled(previousShellInputEnabled);
            }

            blockedMainController = null;
            shellInputBlocked = false;
        }

        private void SetQuestion(PresentationQuestion question, int questionNumber, int questionCount)
        {
            SetText(titleLabel, "Q&A Controls");
            SetText(progressLabel, $"Question {questionNumber} / {questionCount}");
            SetText(personaLabel, string.IsNullOrWhiteSpace(question.audiencePersona) ? "Audience member" : question.audiencePersona);
            SetText(questionLabel, question.question);
            ApplyQuestionPanelMode(currentQuestionUsesAudienceBubble);
        }

        private void ApplyQuestionPanelMode(bool audienceBubbleActive)
        {
            RectTransform panelRect = questionPanel != null ? questionPanel.transform as RectTransform : null;
            if (panelRect != null)
            {
                panelRect.sizeDelta = audienceBubbleActive
                    ? new Vector2(720f, 220f)
                    : new Vector2(980f, 560f);
            }

            SetLabelVisible(progressLabel, !audienceBubbleActive);
            SetLabelVisible(personaLabel, !audienceBubbleActive);
            SetLabelVisible(questionLabel, !audienceBubbleActive);
            SetAnswerUiVisible(false);
        }

        private void EnsureQuestionPanelFollowsViewer()
        {
            Canvas canvas = questionPanel != null
                ? questionPanel.GetComponentInParent<Canvas>()
                : GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            {
                return;
            }

            qaPanelFollower = canvas.GetComponent<WorldSpaceCanvasFollower>();
            if (qaPanelFollower == null)
            {
                qaPanelFollower = canvas.gameObject.AddComponent<WorldSpaceCanvasFollower>();
            }

            qaPanelFollower.enabled = true;
            qaPanelFollower.Configure(
                ResolveViewerTransform(),
                qaPanelFollowOffset,
                true,
                true,
                28f,
                28f);
            qaPanelFollower.SetMinimumTargetY(qaPanelMinimumTargetY);
            qaPanelFollower.SetFollowContinuously(true);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, 48f);
            }

            VrUiUsabilityUtility.EnsureCanvasInputSupport(canvas.gameObject, canvas);
        }

        private static Transform ResolveViewerTransform()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            Camera fallbackCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            return fallbackCamera != null ? fallbackCamera.transform : null;
        }

        private void SetStatus(string value) => SetText(statusLabel, value);
        private void SetShortcutHint(string value) => SetText(shortcutLabel, value);

        private void SetAnswerUiVisible(bool visible)
        {
            SetLabelVisible(scoreLabel, visible);
            SetLabelVisible(transcriptLabel, visible);
            SetTypedAnswerVisible(visible);
        }

        private void SetTypedAnswerVisible(bool visible)
        {
            if (typedAnswerInput != null && typedAnswerInput.gameObject.activeSelf != visible)
            {
                typedAnswerInput.gameObject.SetActive(visible);
            }
        }

        private void SetPrimaryButtonLabel(string value)
        {
            SetButtonLabel(primaryButton, value);
        }

        private void SetFinishButtonLabel(string label)
        {
            SetButtonLabel(finishButton, label);
        }

        private void SetSkipButtonVisible(bool visible)
        {
            if (skipButton != null && skipButton.gameObject.activeSelf != visible)
            {
                skipButton.gameObject.SetActive(visible);
            }
        }

        private void SetQuestionButtonInteractivity(bool primary, bool skip, bool finish)
        {
            if (primaryButton != null)
            {
                primaryButton.interactable = primary;
            }

            if (skipButton != null)
            {
                skipButton.interactable = skip;
            }

            if (finishButton != null)
            {
                finishButton.interactable = finish;
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            SetText(label, value);
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

        private static void SetLabelVisible(TMP_Text label, bool visible)
        {
            if (label != null)
            {
                label.gameObject.SetActive(visible);
            }
        }

        private AudienceMember ForceAudienceQuestionState(int questionIndex)
        {
            AudienceBehaviorController audience = FindFirstObjectByType<AudienceBehaviorController>(FindObjectsInactive.Include);
            if (audience == null || audience.audienceMembers == null || audience.audienceMembers.Count == 0)
            {
                return null;
            }

            AudienceMember member = ResolveVisibleQuestionAsker(audience.audienceMembers, questionIndex);
            if (member != null)
            {
                member.SetState(questionIndex % 2 == 0 ? AudienceState.NoteTaking : AudienceState.Attentive, true);
            }

            return member;
        }

        private static AudienceMember ResolveVisibleQuestionAsker(List<AudienceMember> members, int questionIndex)
        {
            if (members == null || members.Count == 0)
            {
                return null;
            }

            Transform viewer = ResolveViewerTransform();
            if (viewer == null)
            {
                int fallbackIndex = Mathf.Abs(questionIndex) % members.Count;
                return members[fallbackIndex];
            }

            var candidates = new List<AudienceMember>();
            for (int index = 0; index < members.Count; index++)
            {
                AudienceMember member = members[index];
                if (member != null && member.gameObject.activeInHierarchy)
                {
                    candidates.Add(member);
                }
            }

            candidates.Sort((left, right) =>
                ScoreQuestionAsker(left, viewer).CompareTo(ScoreQuestionAsker(right, viewer)));

            int visiblePool = Mathf.Min(6, candidates.Count);
            return visiblePool > 0
                ? candidates[Mathf.Abs(questionIndex) % visiblePool]
                : null;
        }

        private static float ScoreQuestionAsker(AudienceMember member, Transform viewer)
        {
            if (member == null || viewer == null)
            {
                return float.MaxValue;
            }

            Vector3 toMember = member.transform.position - viewer.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            Vector3 flatToMember = Vector3.ProjectOnPlane(toMember, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f || flatToMember.sqrMagnitude < 0.0001f)
            {
                return toMember.magnitude;
            }

            flatForward.Normalize();
            flatToMember.Normalize();
            float angle = Vector3.Angle(flatForward, flatToMember);
            float behindPenalty = Vector3.Dot(flatForward, flatToMember) < 0f ? 100f : 0f;
            return toMember.magnitude + (angle * 0.08f) + behindPenalty;
        }

        private void ShowAudienceQuestionBubble(AudienceMember member, PresentationQuestion question, int questionNumber, int questionCount)
        {
            if (!showAudienceQuestionBubble || member == null || question == null)
            {
                currentQuestionUsesAudienceBubble = false;
                HideAudienceQuestionBubble();
                return;
            }

            if (questionBubblePresenter == null)
            {
                questionBubblePresenter = GetComponent<AudienceQuestionBubblePresenter>();
                if (questionBubblePresenter == null)
                {
                    questionBubblePresenter = gameObject.AddComponent<AudienceQuestionBubblePresenter>();
                }
            }

            currentQuestionUsesAudienceBubble = true;
            questionBubblePresenter.UseAudienceAnchoredQuestionBubble();
            questionBubblePresenter.Show(
                member,
                question,
                questionNumber,
                questionCount,
                BuildBubbleShortcutHint(questionNumber, questionCount));
        }

        private void HideAudienceQuestionBubble()
        {
            currentQuestionUsesAudienceBubble = false;
            if (questionBubblePresenter != null)
            {
                questionBubblePresenter.Hide();
            }
        }

        private static string BuildQuestionOnlySummary(int presentedQuestions, int totalQuestions)
        {
            if (presentedQuestions <= 0)
            {
                return "Audience questions ended before any question was shown.";
            }

            return presentedQuestions >= totalQuestions
                ? $"Audience questions complete. Shown {presentedQuestions}/{totalQuestions} questions."
                : $"Audience questions ended early. Shown {presentedQuestions}/{totalQuestions} questions.";
        }

        private static string BuildBubbleShortcutHint(int questionNumber, int questionCount)
        {
            return questionNumber < questionCount
                ? "A/Trigger: next question  |  B/Y: finish Q&A"
                : "A/Trigger: finish Q&A";
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

        private static bool IsSubmitControlPressed()
        {
            bool keyboardPressed = Keyboard.current != null &&
                (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed);

            bool inputSystemPressed =
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.leftHand, "triggerPressed") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.rightHand, "triggerPressed") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.leftHand, "primaryButton") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.rightHand, "primaryButton");

            bool legacyPressed =
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.triggerButton) ||
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.primaryButton);

            return keyboardPressed || inputSystemPressed || legacyPressed;
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

        private static bool IsSkipControlPressed()
        {
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
            bool inputSystemPressed =
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.leftHand, "secondaryButton") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.rightHand, "secondaryButton");
            bool legacyPressed = IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.secondaryButton);
            return keyboardPressed || inputSystemPressed || legacyPressed;
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

        private static bool IsControllerButtonPressed(
            UnityEngine.InputSystem.XR.XRController controller,
            string controlName)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controlName))
            {
                return false;
            }

            ButtonControl control = controller.TryGetChildControl<ButtonControl>(controlName);
            return control != null && control.isPressed;
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
