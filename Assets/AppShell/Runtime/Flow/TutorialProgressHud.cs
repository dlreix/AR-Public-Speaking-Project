using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// A floating HUD that shows the user's tutorial completion progress.
    /// Positioned slightly above eye level near the central area, it updates
    /// in real time as panels are visited and completed.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialProgressHud : MonoBehaviour
    {
        [SerializeField] private float hudScale = 0.003f;
        [SerializeField] private Vector3 hudPosition = new Vector3(0f, 2.6f, 3.2f); // Indirildi ve biraz yaklaştırıldı
        [SerializeField] private Vector3 hudEulerAngles = new Vector3(10f, 0f, 0f);

        private static readonly Color BarBackgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.85f);
        private static readonly Color BarFillColor = new Color(1f, 0.64f, 0.24f, 1f);
        private static readonly Color BarCompletedColor = new Color(0.18f, 0.88f, 0.46f, 1f);
        private static readonly Color LabelColor = new Color(0.82f, 0.9f, 0.96f, 1f);
        private static readonly Color CountColor = new Color(1f, 0.64f, 0.24f, 1f);

        private GameObject hudRoot;
        private CanvasGroup canvasGroup;
        private Image progressFillImage;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI countLabel;

        private float targetFill;
        private int displayedCompleted;
        private int totalPanels;
        private bool allCompleted;

        public void Initialize(int panelCount)
        {
            totalPanels = panelCount;
            displayedCompleted = 0;
            targetFill = 0f;
            allCompleted = false;

            if (hudRoot != null)
            {
                Destroy(hudRoot);
            }

            hudRoot = BuildHud();
            UpdateLabels(0, panelCount);
        }

        public void UpdateProgress(int completed, int total, float fillPercent)
        {
            totalPanels = total;
            displayedCompleted = completed;
            targetFill = fillPercent;
            allCompleted = completed >= total && total > 0;
            UpdateLabels(completed, total);
        }

        private void Update()
        {
            if (hudRoot == null || progressFillImage == null)
            {
                return;
            }

            // Smooth fill animation
            float current = progressFillImage.fillAmount;
            float target = Mathf.Clamp01(targetFill);
            progressFillImage.fillAmount = Mathf.Lerp(current, target, Time.deltaTime * 4f);

            // Color shift on completion
            if (allCompleted)
            {
                progressFillImage.color = Color.Lerp(progressFillImage.color, BarCompletedColor, Time.deltaTime * 3f);
            }

            // Subtle float animation
            if (hudRoot != null)
            {
                float yOffset = Mathf.Sin(Time.time * 0.8f) * 0.015f;
                hudRoot.transform.position = hudPosition + new Vector3(0f, yOffset, 0f);
            }
        }

        private void UpdateLabels(int completed, int total)
        {
            if (progressLabel != null)
            {
                progressLabel.text = allCompleted
                    ? "Tutorial Complete!"
                    : "Tutorial Progress";
            }

            if (countLabel != null)
            {
                countLabel.text = $"{completed} / {total}";
                countLabel.color = allCompleted ? BarCompletedColor : CountColor;
            }
        }

        private GameObject BuildHud()
        {
            // Canvas root
            GameObject canvasObj = new GameObject("TutorialProgressHud",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.position = hudPosition;
            canvasObj.transform.rotation = Quaternion.Euler(hudEulerAngles);
            canvasObj.transform.localScale = Vector3.one * hudScale;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(900f, 120f);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 8;
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            canvasGroup = canvasObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // Background
            CreateImage(canvasRect, "Background", StretchAnchors(), new Color(0.02f, 0.03f, 0.05f, 0.92f));

            // Accent top line
            CreateImage(canvasRect, "TopLine",
                new AnchorSetup(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -3f), Vector2.zero),
                BarFillColor);

            // Title label
            progressLabel = CreateText(canvasRect, "ProgressTitle", "Tutorial Progress",
                new Vector2(0f, 0.55f), new Vector2(0.55f, 1f),
                new RectOffset(24, 0, 8, 0),
                28f, LabelColor, TextAlignmentOptions.Left, FontStyles.Bold);

            // Count label
            countLabel = CreateText(canvasRect, "CountLabel", "0 / 0",
                new Vector2(0.55f, 0.55f), new Vector2(1f, 1f),
                new RectOffset(0, 24, 8, 0),
                30f, CountColor, TextAlignmentOptions.Right, FontStyles.Bold);

            // Progress bar background
            CreateImage(canvasRect, "BarBackground",
                new AnchorSetup(new Vector2(0.025f, 0.15f), new Vector2(0.975f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                BarBackgroundColor);

            // Progress bar fill
            GameObject fillObj = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(canvasRect, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.025f, 0.15f);
            fillRect.anchorMax = new Vector2(0.975f, 0.45f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            progressFillImage = fillObj.GetComponent<Image>();
            progressFillImage.color = BarFillColor;
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillAmount = 0f;
            progressFillImage.raycastTarget = false;

            return canvasObj;
        }

        private static Image CreateImage(RectTransform parent, string objectName, AnchorSetup setup, Color color)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = setup.AnchorMin;
            rect.anchorMax = setup.AnchorMax;
            rect.pivot = setup.Pivot;
            rect.offsetMin = setup.OffsetMin;
            rect.offsetMax = setup.OffsetMax;

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, string text,
            Vector2 anchorMin, Vector2 anchorMax, RectOffset padding, float fontSize,
            Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding.left, padding.bottom);
            rect.offsetMax = new Vector2(-padding.right, -padding.top);

            TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private static AnchorSetup StretchAnchors()
        {
            return new AnchorSetup(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private readonly struct AnchorSetup
        {
            public readonly Vector2 AnchorMin;
            public readonly Vector2 AnchorMax;
            public readonly Vector2 Pivot;
            public readonly Vector2 OffsetMin;
            public readonly Vector2 OffsetMax;

            public AnchorSetup(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                Pivot = pivot;
                OffsetMin = offsetMin;
                OffsetMax = offsetMax;
            }
        }
    }
}
