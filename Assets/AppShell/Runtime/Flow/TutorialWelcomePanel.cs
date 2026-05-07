using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Creates and manages a world-space welcome panel that greets the user
    /// when they first enter the tutorial hub. The panel auto-dismisses after
    /// a timeout or when the user walks away.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialWelcomePanel : MonoBehaviour
    {
        [SerializeField] private float displayDurationSeconds = 4f;
        [SerializeField] private float fadeOutDuration = 1.2f;
        [SerializeField] private float panelScale = 0.0044f;
        [SerializeField] private Vector3 panelPosition = new Vector3(0f, 2.65f, 3.85f);
        [SerializeField] private Vector3 panelEulerAngles = Vector3.zero;

        private static readonly Color BackgroundColor = new Color(0.015f, 0.02f, 0.035f, 0.88f);
        private static readonly Color AccentColor = new Color(0.12f, 0.78f, 0.96f, 1f);
        private static readonly Color SubtitleColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color InstructionColor = new Color(0.55f, 0.65f, 0.75f, 0.8f);
        private static readonly Color GlowColor = new Color(0.12f, 0.78f, 0.96f, 0.12f);

        private GameObject panelRoot;
        private CanvasGroup canvasGroup;
        private float elapsedTime;
        private bool dismissed;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf && !dismissed;

        public void Show()
        {
            if (panelRoot != null)
            {
                Destroy(panelRoot);
            }

            dismissed = false;
            elapsedTime = 0f;
            panelRoot = BuildPanel();
        }

        public void Dismiss()
        {
            dismissed = true;
        }

        private void Update()
        {
            if (panelRoot == null || dismissed)
            {
                if (panelRoot != null && dismissed)
                {
                    FadeAndDestroy();
                }
                return;
            }

            elapsedTime += Time.deltaTime;

            // Reveal phase (first 0.6 seconds)
            if (elapsedTime < 0.6f)
            {
                float t = elapsedTime / 0.6f;
                float ease = t * t * (3f - 2f * t); // smoothstep
                if (canvasGroup != null) canvasGroup.alpha = ease;
                panelRoot.transform.localScale = Vector3.one * panelScale * Mathf.Lerp(0.85f, 1f, ease);
            }

            // Auto-dismiss after duration
            if (elapsedTime >= displayDurationSeconds)
            {
                dismissed = true;
            }
        }

        private void FadeAndDestroy()
        {
            if (canvasGroup == null)
            {
                Destroy(panelRoot);
                panelRoot = null;
                return;
            }

            canvasGroup.alpha -= Time.deltaTime / fadeOutDuration;

            if (canvasGroup.alpha <= 0f)
            {
                Destroy(panelRoot);
                panelRoot = null;
            }
        }

        private GameObject BuildPanel()
        {
            // Root canvas
            GameObject canvasObj = new GameObject("WelcomePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.position = panelPosition;
            canvasObj.transform.rotation = Quaternion.Euler(panelEulerAngles);
            canvasObj.transform.localScale = Vector3.one * panelScale * 0.85f;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1400f, 680f);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            canvasGroup = canvasObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // Background
            CreateStretchedImage(canvasRect, "Background", BackgroundColor);

            // Accent top bar
            CreateTopBar(canvasRect, "TopAccent", AccentColor, 10f);

            // Accent bottom bar
            CreateBottomBar(canvasRect, "BottomAccent", AccentColor, 4f);

            // Icon area: a decorative circle
            CreateIconCircle(canvasRect);

            // Title
            CreateTextElement(
                canvasRect,
                "Title",
                "Welcome to VR Public Speaking Trainer",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new RectOffset(60, 60, 50, 0),
                new Vector2(0f, 105f),
                48f, AccentColor, TextAlignmentOptions.Top, FontStyles.Bold);

            // Subtitle
            CreateTextElement(
                canvasRect,
                "Subtitle",
                "Explore the tutorial hub to learn the controls before your first session.",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new RectOffset(80, 80, 160, 0),
                new Vector2(0f, 70f),
                32f, SubtitleColor, TextAlignmentOptions.Top, FontStyles.Normal);

            // Instruction bullets
            string instructions =
                "◆  Walk to each wall panel to learn the controls\n" +
                "◆  Panels light up as you approach\n" +
                "◆  Stay near a panel for a few seconds to mark it as read\n" +
                "◆  Head to the front wall to access the main menu";

            CreateTextElement(
                canvasRect,
                "Instructions",
                instructions,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new RectOffset(100, 100, 250, 120),
                Vector2.zero,
                28f, InstructionColor, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            // Footer
            CreateTextElement(
                canvasRect,
                "Footer",
                "This panel will disappear automatically. You can start practicing anytime.",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new RectOffset(60, 60, 0, 28),
                new Vector2(0f, 50f),
                22f, new Color(0.42f, 0.52f, 0.64f, 0.9f), TextAlignmentOptions.Bottom, FontStyles.Italic);

            return canvasObj;
        }

        private static Image CreateStretchedImage(RectTransform parent, string objectName, Color color)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateTopBar(RectTransform parent, string objectName, Color color, float height)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateBottomBar(RectTransform parent, string objectName, Color color, float height)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, height);

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateIconCircle(RectTransform parent)
        {
            // A subtle decorative circle in the top-left
            GameObject obj = new GameObject("IconCircle", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-60f, 60f);
            rect.sizeDelta = new Vector2(240f, 240f);

            Image image = obj.GetComponent<Image>();
            image.color = new Color(1f, 0.64f, 0.24f, 0.04f);
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateTextElement(
            RectTransform parent,
            string objectName,
            string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            RectOffset padding,
            Vector2 sizeDelta,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = new Vector2(padding.left, padding.bottom);
            rect.offsetMax = new Vector2(-padding.right, -padding.top);
            if (sizeDelta != Vector2.zero) rect.sizeDelta = sizeDelta;

            TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }
    }
}
