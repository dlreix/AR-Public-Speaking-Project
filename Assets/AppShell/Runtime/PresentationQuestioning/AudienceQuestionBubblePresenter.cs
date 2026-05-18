using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public class AudienceQuestionBubblePresenter : MonoBehaviour
    {
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 0.34f, 0f);
        [SerializeField] private Vector2 bubbleSize = new Vector2(900f, 380f);
        [SerializeField] private Vector2 bubbleSizeLimits = new Vector2(720f, 1040f);
        [SerializeField] private Vector2 questionHeightLimits = new Vector2(160f, 620f);
        [SerializeField] private float worldScale = 0.00175f;
        [SerializeField] private float distanceScalePerMeter = 0.0005f;
        [SerializeField] private float maxWorldScale = 0.0042f;
        [SerializeField] private bool anchorToViewer = false;
        [SerializeField] private bool showQuestionText = true;
        [SerializeField] private Vector3 viewerOffset = new Vector3(0f, 0.42f, 1.42f);
        [SerializeField] private Vector2 viewerHorizontalClamp = new Vector2(-0.08f, 0.08f);

        private Transform followTarget;
        private GameObject bubbleRoot;
        private CanvasGroup canvasGroup;
        private TMP_Text titleLabel;
        private TMP_Text questionLabel;
        private TMP_Text hintLabel;
        private LayoutElement questionLayout;
        private Camera targetCamera;
        private Vector3 resolvedTargetOffset;
        private RectTransform bubbleRect;
        private const string DefaultShortcutHint = "A/Trigger: next question  |  B/Y: finish Q&A";
        private const float PanelHorizontalPadding = 64f;
        private const float PanelVerticalPadding = 44f;
        private const float PanelSpacing = 8f;
        private const float TitleHeight = 48f;
        private const float HintHeight = 42f;
        private const float QuestionFontSizeMax = 54f;
        private const float QuestionFontSizeMin = 28f;
        private const float QuestionFontFitStep = 2f;
        private const float QuestionContentPadding = 12f;

        public void UseAudienceAnchoredQuestionBubble()
        {
            anchorToViewer = false;
            showQuestionText = true;

            if (bubbleRoot != null)
            {
                RectTransform rootRect = bubbleRoot.GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    rootRect.sizeDelta = bubbleSize;
                }
            }
        }

        public void Show(
            AudienceMember member,
            PresentationQuestion question,
            int questionNumber,
            int questionCount,
            string shortcutHint = null)
        {
            if (member == null || question == null)
            {
                Hide();
                return;
            }

            EnsureBubble();
            followTarget = member.transform;
            resolvedTargetOffset = ResolveTargetOffset(member);
            SetText(titleLabel, BuildTitle(question, questionNumber, questionCount));
            SetText(questionLabel, showQuestionText ? NormalizeQuestionText(question.question) : "?");
            SetText(hintLabel, NormalizeShortcutHint(shortcutHint));
            ApplyQuestionTextMode();
            ApplyContentSize();
            bubbleRoot.SetActive(true);
            canvasGroup.alpha = 1f;
            UpdateTransform();
        }

        public void Hide()
        {
            followTarget = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (bubbleRoot != null)
            {
                bubbleRoot.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (bubbleRoot == null || !bubbleRoot.activeSelf || followTarget == null)
            {
                return;
            }

            UpdateTransform();
        }

        private void OnDestroy()
        {
            if (bubbleRoot != null)
            {
                Destroy(bubbleRoot);
                bubbleRoot = null;
            }
        }

        private void EnsureBubble()
        {
            if (bubbleRoot != null)
            {
                return;
            }

            bubbleRoot = new GameObject(
                "AudienceQuestionBubble",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup),
                typeof(GraphicRaycaster));
            bubbleRoot.transform.SetParent(null, false);
            DontDestroyOnLoad(bubbleRoot);

            bubbleRect = bubbleRoot.GetComponent<RectTransform>();
            bubbleRect.sizeDelta = bubbleSize;
            bubbleRect.localScale = Vector3.one * worldScale;

            Canvas canvas = bubbleRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = bubbleRoot.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 64f;
            scaler.referencePixelsPerUnit = 100f;

            canvasGroup = bubbleRoot.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject panel = new GameObject("BubblePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(Outline));
            panel.transform.SetParent(bubbleRoot.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.98f, 0.99f, 1f, 0.96f);
            background.raycastTarget = false;

            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.12f, 0.18f, 0.34f);
            outline.effectDistance = new Vector2(3f, -3f);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 22, 22);
            layout.spacing = PanelSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleLabel = CreateText(panel.transform, "BubbleTitle", 36f, FontStyles.Bold, new Color(0.05f, 0.18f, 0.28f, 1f), TitleHeight);
            questionLabel = CreateText(panel.transform, "BubbleQuestion", QuestionFontSizeMax, FontStyles.Bold, new Color(0.04f, 0.07f, 0.10f, 1f), questionHeightLimits.x);
            questionLayout = questionLabel.GetComponent<LayoutElement>();
            hintLabel = CreateText(panel.transform, "BubbleHint", 30f, FontStyles.Bold, new Color(0.02f, 0.40f, 0.60f, 1f), HintHeight);
            hintLabel.alignment = TextAlignmentOptions.Center;

            GameObject tail = new GameObject("BubbleTail", typeof(RectTransform), typeof(Image));
            tail.transform.SetParent(bubbleRoot.transform, false);
            RectTransform tailRect = tail.GetComponent<RectTransform>();
            tailRect.anchorMin = new Vector2(0.5f, 0f);
            tailRect.anchorMax = new Vector2(0.5f, 0f);
            tailRect.pivot = new Vector2(0.5f, 0.5f);
            tailRect.sizeDelta = new Vector2(48f, 48f);
            tailRect.anchoredPosition = new Vector2(0f, -18f);
            tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image tailImage = tail.GetComponent<Image>();
            tailImage.color = background.color;
            tailImage.raycastTarget = false;

            bubbleRoot.SetActive(false);
        }

        private void ApplyContentSize()
        {
            if (bubbleRect == null || questionLabel == null)
            {
                return;
            }

            float minWidth = Mathf.Min(bubbleSizeLimits.x, bubbleSizeLimits.y);
            float maxWidth = Mathf.Max(bubbleSizeLimits.x, bubbleSizeLimits.y);
            float titleWidth = GetPreferredWidth(titleLabel);
            float questionWidth = GetPreferredWidth(questionLabel);
            float desiredWidth = Mathf.Clamp(
                Mathf.Max(titleWidth, questionWidth) + PanelHorizontalPadding,
                minWidth,
                maxWidth);

            float textWidth = Mathf.Max(100f, desiredWidth - PanelHorizontalPadding);
            if (showQuestionText)
            {
                FitQuestionFontToHeight(textWidth, Mathf.Max(questionHeightLimits.x, questionHeightLimits.y));
            }

            float preferredQuestionHeight = showQuestionText
                ? questionLabel.GetPreferredValues(questionLabel.text, textWidth, 0f).y + QuestionContentPadding
                : questionHeightLimits.x;
            float questionHeight = Mathf.Max(
                preferredQuestionHeight,
                Mathf.Min(questionHeightLimits.x, questionHeightLimits.y));

            if (questionLayout != null)
            {
                questionLayout.minHeight = questionHeight;
                questionLayout.preferredHeight = questionHeight;
            }

            float desiredHeight =
                PanelVerticalPadding +
                TitleHeight +
                questionHeight +
                HintHeight +
                (PanelSpacing * 2f);

            bubbleRect.sizeDelta = new Vector2(desiredWidth, desiredHeight);
        }

        private static float GetPreferredWidth(TMP_Text label)
        {
            if (label == null || string.IsNullOrWhiteSpace(label.text))
            {
                return 0f;
            }

            return label.GetPreferredValues(label.text, 0f, 0f).x;
        }

        private static string NormalizeShortcutHint(string shortcutHint)
        {
            string hint = string.IsNullOrWhiteSpace(shortcutHint)
                ? DefaultShortcutHint
                : shortcutHint.Trim();
            return hint
                .Replace("next question", "next")
                .Replace("finish Q&A", "finish");
        }

        private TMP_Text CreateText(Transform parent, string name, float fontSize, FontStyles style, Color color, float preferredHeight)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(14f, fontSize * 0.58f);
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private void FitQuestionFontToHeight(float textWidth, float targetHeight)
        {
            if (questionLabel == null)
            {
                return;
            }

            float width = Mathf.Max(100f, textWidth);
            for (float size = QuestionFontSizeMax; size >= QuestionFontSizeMin; size -= QuestionFontFitStep)
            {
                questionLabel.fontSize = size;
                questionLabel.fontSizeMax = size;
                questionLabel.fontSizeMin = QuestionFontSizeMin;

                float height = questionLabel.GetPreferredValues(questionLabel.text, width, 0f).y + QuestionContentPadding;
                if (height <= targetHeight || size <= QuestionFontSizeMin)
                {
                    return;
                }
            }
        }

        private void ApplyQuestionTextMode()
        {
            if (questionLabel == null)
            {
                return;
            }

            if (showQuestionText)
            {
                questionLabel.fontSize = QuestionFontSizeMax;
                questionLabel.fontSizeMax = QuestionFontSizeMax;
                questionLabel.fontSizeMin = QuestionFontSizeMin;
                questionLabel.alignment = TextAlignmentOptions.TopLeft;
                questionLabel.overflowMode = TextOverflowModes.Overflow;
                return;
            }

            questionLabel.fontSize = 78f;
            questionLabel.fontSizeMax = 78f;
            questionLabel.fontSizeMin = 56f;
            questionLabel.alignment = TextAlignmentOptions.Center;
            questionLabel.overflowMode = TextOverflowModes.Overflow;
        }

        private void UpdateTransform()
        {
            if (followTarget == null || bubbleRoot == null)
            {
                return;
            }

            targetCamera = ResolveCamera();
            if (targetCamera == null)
            {
                bubbleRoot.transform.localScale = Vector3.one * worldScale;
                bubbleRoot.transform.position = ResolveBubbleWorldPosition(worldScale);
                return;
            }

            if (anchorToViewer)
            {
                ApplyViewerAnchoredPose();
                return;
            }

            Vector3 anchorPosition = followTarget.position + resolvedTargetOffset;
            Vector3 toCamera = targetCamera.transform.position - anchorPosition;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                float distance = toCamera.magnitude;
                float readableScale = Mathf.Clamp(
                    distance * distanceScalePerMeter,
                    worldScale,
                    Mathf.Max(worldScale, maxWorldScale));
                bubbleRoot.transform.localScale = Vector3.one * readableScale;
                bubbleRoot.transform.position = ResolveBubbleWorldPosition(readableScale);
                bubbleRoot.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
                return;
            }

            bubbleRoot.transform.localScale = Vector3.one * worldScale;
            bubbleRoot.transform.position = ResolveBubbleWorldPosition(worldScale);
        }

        private Vector3 ResolveBubbleWorldPosition(float readableScale)
        {
            Vector3 anchorPosition = followTarget.position + resolvedTargetOffset;
            float halfHeight = bubbleRect != null
                ? bubbleRect.sizeDelta.y * readableScale * 0.5f
                : bubbleSize.y * readableScale * 0.5f;
            return anchorPosition + Vector3.up * halfHeight;
        }

        private void ApplyViewerAnchoredPose()
        {
            Transform cameraTransform = targetCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = cameraTransform.forward;
            }

            forward.Normalize();
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = cameraTransform.right;
            }

            right.Normalize();

            float xOffset = viewerOffset.x;
            Vector3 speakerPosition = followTarget.position + resolvedTargetOffset;
            Vector3 toSpeaker = speakerPosition - cameraTransform.position;
            if (!showQuestionText && toSpeaker.sqrMagnitude > 0.0001f)
            {
                float side = Mathf.Sign(Vector3.Dot(toSpeaker.normalized, right));
                xOffset = Mathf.Clamp(viewerOffset.x + (side * 0.12f), viewerHorizontalClamp.x, viewerHorizontalClamp.y);
            }

            Vector3 desiredPosition =
                cameraTransform.position +
                right * xOffset +
                up * viewerOffset.y +
                forward * viewerOffset.z;

            bubbleRoot.transform.position = desiredPosition;
            bubbleRoot.transform.localScale = Vector3.one * worldScale;
            bubbleRoot.transform.rotation = Quaternion.LookRotation(desiredPosition - cameraTransform.position, up);
        }

        private Vector3 ResolveTargetOffset(AudienceMember member)
        {
            Bounds bounds = new Bounds(member.transform.position, Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = member.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return new Vector3(0f, 2.05f, 0f) + headOffset;
            }

            Vector3 offset = bounds.center - member.transform.position;
            offset.y = Mathf.Max(1.45f, bounds.max.y - member.transform.position.y);
            return offset + headOffset;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return targetCamera;
            }

            return Camera.main ?? FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        private static string BuildTitle(PresentationQuestion question, int questionNumber, int questionCount)
        {
            string persona = string.IsNullOrWhiteSpace(question.audiencePersona)
                ? "Audience member"
                : question.audiencePersona.Trim();
            return $"Q{questionNumber}/{questionCount}  |  {persona}";
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static string NormalizeQuestionText(string value)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
