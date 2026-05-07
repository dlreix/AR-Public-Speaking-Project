using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Presents tutorial panels one at a time in front of the user as a guided slideshow.
    /// After the welcome panel dismisses, each tutorial step fades in centrally,
    /// and the user advances with a "Continue" action (trigger, A button, or click).
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialSequentialPresenter : MonoBehaviour
    {
        [Header("Positioning")]
        [SerializeField] private float distanceFromCamera = 3.2f;
        [SerializeField] private float verticalOffset = -0.15f;
        [SerializeField] private float panelScale = 0.0038f;

        [Header("Timing")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private float autoShowDelayAfterWelcome = 1.2f;

        [Header("Colors")]
        // Yarı şeffaf, daha premium "glass" hissiyatı veren koyu lacivert
        [SerializeField] private Color panelBgColor = new Color(0.015f, 0.02f, 0.035f, 0.88f); 
        // Canlı bir cyan/mavi accent, turuncu yerine daha teknolojik bir hava katar
        [SerializeField] private Color accentColor = new Color(0.12f, 0.78f, 0.96f, 1f);
        [SerializeField] private Color bodyTextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        [SerializeField] private Color mutedTextColor = new Color(0.55f, 0.65f, 0.75f, 0.8f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.45f, 0.8f, 1f);
        [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.55f, 0.9f, 1f);
        // Tamamlanma için neon yeşili
        [SerializeField] private Color completedDotColor = new Color(0.2f, 0.95f, 0.5f, 1f);
        [SerializeField] private Color skipTextColor = new Color(0.4f, 0.5f, 0.62f, 0.6f);

        private readonly List<TutorialSlide> slides = new List<TutorialSlide>();
        private int currentIndex = -1;
        private GameObject activePanelRoot;
        private CanvasGroup activeCanvasGroup;
        private Transform cameraTransform;
        private bool isTransitioning;
        private bool isPresenting;
        private bool isCompleted;

        // Input detection
        private bool prevTriggerState;
        private bool prevPrimaryState;

        /// <summary>Raised when all slides have been viewed.</summary>
        public event Action AllSlidesCompleted;

        /// <summary>Raised when the user skips the tutorial.</summary>
        public event Action TutorialSkipped;

        /// <summary>Raised for each slide shown (passes slide index).</summary>
        public event Action<int> SlideShown;

        public bool IsPresenting => isPresenting;
        public bool IsCompleted => isCompleted;
        public int CurrentSlideIndex => currentIndex;
        public int TotalSlides => slides.Count;

        /// <summary>
        /// Add a slide to the presentation queue.
        /// Call this before StartPresentation().
        /// </summary>
        public void AddSlide(string title, string body, string footer, string iconSymbol = "")
        {
            slides.Add(new TutorialSlide
            {
                Title = title,
                Body = body,
                Footer = footer,
                IconSymbol = iconSymbol
            });
        }

        /// <summary>
        /// Begin the sequential presentation from the first slide.
        /// Call after all slides have been added.
        /// </summary>
        public void StartPresentation()
        {
            if (slides.Count == 0 || isPresenting) return;

            cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null) return;

            isPresenting = true;
            isCompleted = false;
            currentIndex = -1;
            ShowNextSlide();
        }

        /// <summary>
        /// Begin after a delay (e.g., after welcome panel fades).
        /// </summary>
        public void StartPresentationDelayed(float delay)
        {
            StartCoroutine(DelayedStart(delay));
        }

        private IEnumerator DelayedStart(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartPresentation();
        }

        private void Update()
        {
            if (!isPresenting || isTransitioning) return;

            // Detect advance input: trigger, A/X button, left click, or Space
            bool triggerNow = false;
            bool primaryNow = false;

            // Keyboard / mouse
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    triggerNow = true;
                }
            }

            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                triggerNow = true;
            }

            // XR controllers
            var controllers = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller, controllers);
            foreach (var device in controllers)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trig) && trig)
                    triggerNow = true;
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool prim) && prim)
                    primaryNow = true;
            }

            // Advance on press (not hold)
            bool advancePressed = (triggerNow && !prevTriggerState) || (primaryNow && !prevPrimaryState);
            prevTriggerState = triggerNow;
            prevPrimaryState = primaryNow;

            // Back with B/Y or Backspace
            bool backPressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                backPressed = true;
            }

            foreach (var device in controllers)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sec) && sec)
                    backPressed = true;
            }

            // Skip with Escape
            bool skipPressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                skipPressed = true;
            }

            if (skipPressed)
            {
                SkipTutorial();
                return;
            }

            if (advancePressed)
            {
                ShowNextSlide();
            }
            else if (backPressed && currentIndex > 0)
            {
                ShowPreviousSlide();
            }

            // Keep panel facing the camera smoothly
            KeepPanelFacingCamera();
        }

        private void ShowNextSlide()
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex >= slides.Count)
            {
                CompletePresentation();
                return;
            }

            StartCoroutine(TransitionToSlide(nextIndex));
        }

        private void ShowPreviousSlide()
        {
            if (currentIndex <= 0) return;
            StartCoroutine(TransitionToSlide(currentIndex - 1));
        }

        private void SkipTutorial()
        {
            StartCoroutine(FadeOutAndFinish(true));
        }

        private void CompletePresentation()
        {
            StartCoroutine(FadeOutAndFinish(false));
        }

        private IEnumerator TransitionToSlide(int targetIndex)
        {
            isTransitioning = true;

            // Fade out current panel
            if (activePanelRoot != null && activeCanvasGroup != null)
            {
                yield return FadeCanvasGroup(activeCanvasGroup, 1f, 0f, transitionDuration * 0.5f);
                Destroy(activePanelRoot);
                activePanelRoot = null;
                activeCanvasGroup = null;
            }

            // Small pause between slides
            yield return new WaitForSeconds(0.15f);

            currentIndex = targetIndex;
            TutorialSlide slide = slides[currentIndex];

            // Build and position new panel
            Vector3 panelPos = GetPanelPositionInFrontOfCamera();
            Quaternion panelRot = GetPanelRotationFacingCamera();

            activePanelRoot = BuildSlidePanel(slide, currentIndex);
            activePanelRoot.transform.position = panelPos;
            activePanelRoot.transform.rotation = panelRot;

            activeCanvasGroup = activePanelRoot.GetComponent<CanvasGroup>();
            if (activeCanvasGroup != null) activeCanvasGroup.alpha = 0f;

            // Fade in
            yield return FadeCanvasGroup(activeCanvasGroup, 0f, 1f, transitionDuration);

            isTransitioning = false;
            SlideShown?.Invoke(currentIndex);
        }

        private IEnumerator FadeOutAndFinish(bool wasSkipped)
        {
            isTransitioning = true;

            if (activePanelRoot != null && activeCanvasGroup != null)
            {
                yield return FadeCanvasGroup(activeCanvasGroup, 1f, 0f, transitionDuration);
                Destroy(activePanelRoot);
                activePanelRoot = null;
                activeCanvasGroup = null;
            }

            isPresenting = false;
            isCompleted = true;
            isTransitioning = false;

            if (wasSkipped)
            {
                Debug.Log("[TutorialSequential] Tutorial skipped by user.");
                TutorialSkipped?.Invoke();
            }
            else
            {
                Debug.Log("[TutorialSequential] All tutorial slides completed.");
                AllSlidesCompleted?.Invoke();
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth ease
                t = t * t * (3f - 2f * t);
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
        }

        private Vector3 GetPanelPositionInFrontOfCamera()
        {
            if (cameraTransform == null) return Vector3.zero;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            return cameraTransform.position + forward * distanceFromCamera + Vector3.up * verticalOffset;
        }

        private Quaternion GetPanelRotationFacingCamera()
        {
            if (cameraTransform == null) return Quaternion.identity;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            return Quaternion.LookRotation(forward);
        }

        private void KeepPanelFacingCamera()
        {
            if (activePanelRoot == null || cameraTransform == null) return;

            // Smoothly rotate to face camera (only Y axis)
            Vector3 targetForward = cameraTransform.forward;
            targetForward.y = 0f;
            targetForward.Normalize();

            Quaternion targetRot = Quaternion.LookRotation(targetForward);
            activePanelRoot.transform.rotation = Quaternion.Slerp(
                activePanelRoot.transform.rotation, targetRot, Time.deltaTime * 3f);

            // Smoothly follow camera position
            Vector3 targetPos = GetPanelPositionInFrontOfCamera();
            activePanelRoot.transform.position = Vector3.Lerp(
                activePanelRoot.transform.position, targetPos, Time.deltaTime * 2.5f);
        }

        // ─── Panel Building ─────────────────────────────────────────────

        private GameObject BuildSlidePanel(TutorialSlide slide, int slideIndex)
        {
            GameObject canvasObj = new GameObject("TutorialSlide_" + slideIndex,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObj.transform.SetParent(transform, true);
            canvasObj.transform.localScale = Vector3.one * panelScale;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1200f, 800f);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 15;
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            // Background
            CreateStretchImage(canvasRect, "BG", panelBgColor);

            // Accent top bar
            CreateAnchoredImage(canvasRect, "AccentTop",
                new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), Vector2.zero, accentColor);

            // Accent bottom bar
            CreateAnchoredImage(canvasRect, "AccentBottom",
                Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 4f), accentColor);

            // Step badge
            string stepText = $"Step {slideIndex + 1} of {slides.Count}";
            CreateLabel(canvasRect, "StepBadge", stepText,
                new Vector2(0.6f, 1f), Vector2.one, new Vector2(1f, 1f),
                new RectOffset(0, 42, 30, 0), new Vector2(0f, 50f),
                22f, mutedTextColor, TextAlignmentOptions.TopRight, FontStyles.Normal);

            // Icon symbol (if provided)
            if (!string.IsNullOrEmpty(slide.IconSymbol))
            {
                CreateLabel(canvasRect, "Icon", slide.IconSymbol,
                    new Vector2(0f, 1f), new Vector2(0.12f, 1f), new Vector2(0f, 1f),
                    new RectOffset(42, 0, 32, 0), new Vector2(0f, 85f),
                    52f, accentColor, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            }

            // Title
            float titleLeftPad = string.IsNullOrEmpty(slide.IconSymbol) ? 52f : 108f;
            CreateLabel(canvasRect, "Title", slide.Title,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new RectOffset((int)titleLeftPad, 52, 36, 0), new Vector2(0f, 110f),
                52f, accentColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            // Separator line
            CreateAnchoredImage(canvasRect, "Separator",
                new Vector2(0.04f, 1f), new Vector2(0.96f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(0f, -138f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f));

            // Body
            CreateLabel(canvasRect, "Body", slide.Body,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new RectOffset(58, 58, 165, 180), Vector2.zero,
                33f, bodyTextColor, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            // Footer (contextual tip)
            CreateLabel(canvasRect, "Footer", slide.Footer,
                new Vector2(0f, 0f), new Vector2(0.65f, 0f), new Vector2(0f, 0f),
                new RectOffset(58, 0, 0, 80), new Vector2(0f, 55f),
                23f, mutedTextColor, TextAlignmentOptions.BottomLeft, FontStyles.Italic);

            // Navigation hint
            bool isLast = slideIndex == slides.Count - 1;
            bool isFirst = slideIndex == 0;
            string navHint = BuildNavHint(isFirst, isLast);
            CreateLabel(canvasRect, "NavHint", navHint,
                new Vector2(0.35f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new RectOffset(0, 42, 0, 42), new Vector2(0f, 50f),
                22f, new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.9f),
                TextAlignmentOptions.BottomRight, FontStyles.Bold);

            // Integrated Progress Bar at the bottom
            BuildIntegratedProgressBar(canvasRect, slideIndex);

            // Progress dots
            BuildProgressDots(canvasRect, slideIndex);

            return canvasObj;
        }

        private string BuildNavHint(bool isFirst, bool isLast)
        {
            // Detect input mode
            bool hasKeyboard = UnityEngine.InputSystem.Keyboard.current != null;

            string advance = isLast ? "Finish" : "Continue";
            string back = isFirst ? "" : "  ◂ Back";

            if (hasKeyboard)
            {
                return $"[Space / Click] {advance}{back}   |   [Esc] Skip";
            }
            else
            {
                return $"[Trigger / A] {advance}{back}   |   [B] Skip";
            }
        }

        private void BuildProgressDots(RectTransform parent, int activeIndex)
        {
            float dotSize = 16f;
            float dotSpacing = 28f;
            float totalWidth = slides.Count * dotSpacing;
            float startX = -totalWidth * 0.5f + dotSpacing * 0.5f;

            for (int i = 0; i < slides.Count; i++)
            {
                GameObject dotObj = new GameObject("Dot_" + i, typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(parent, false);

                RectTransform dotRect = dotObj.GetComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0f);
                dotRect.anchorMax = new Vector2(0.5f, 0f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(startX + i * dotSpacing, 52f); // Moved up slightly to make room for bar
                dotRect.sizeDelta = new Vector2(dotSize, dotSize);

                Image dotImage = dotObj.GetComponent<Image>();
                dotImage.raycastTarget = false;

                if (i < activeIndex)
                {
                    // Completed
                    dotImage.color = completedDotColor;
                    dotRect.sizeDelta = new Vector2(dotSize, dotSize);
                }
                else if (i == activeIndex)
                {
                    // Active
                    dotImage.color = accentColor;
                    dotRect.sizeDelta = new Vector2(dotSize * 1.35f, dotSize * 1.35f);
                }
                else
                {
                    // Future
                    dotImage.color = new Color(mutedTextColor.r, mutedTextColor.g, mutedTextColor.b, 0.3f);
                    dotRect.sizeDelta = new Vector2(dotSize * 0.8f, dotSize * 0.8f);
                }
            }
        }

        private void BuildIntegratedProgressBar(RectTransform parent, int activeIndex)
        {
            // Background track
            Image track = CreateAnchoredImage(parent, "ProgressBar_Track",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 8f), 
                new Color(panelBgColor.r, panelBgColor.g, panelBgColor.b, 1f));
            
            // Fill
            float progressRatio = (float)(activeIndex + 1) / slides.Count;
            Image fill = CreateAnchoredImage(parent, "ProgressBar_Fill",
                new Vector2(0f, 0f), new Vector2(progressRatio, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 8f), 
                activeIndex == slides.Count - 1 ? completedDotColor : accentColor);
        }

        // ─── UI Helpers ─────────────────────────────────────────────────

        private static Image CreateStretchImage(RectTransform parent, string name, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform r = obj.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            Image img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image CreateAnchoredImage(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform r = obj.GetComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
            Image img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            RectOffset padding, Vector2 sizeDelta,
            float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform r = obj.GetComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.offsetMin = new Vector2(padding.left, padding.bottom);
            r.offsetMax = new Vector2(-padding.right, -padding.top);
            if (sizeDelta != Vector2.zero) r.sizeDelta = sizeDelta;

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

        private struct TutorialSlide
        {
            public string Title;
            public string Body;
            public string Footer;
            public string IconSymbol;
        }
    }
}
