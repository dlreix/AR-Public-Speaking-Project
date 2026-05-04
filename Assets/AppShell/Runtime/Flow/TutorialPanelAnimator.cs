using UnityEngine;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Drives reveal animations and proximity glow on individual tutorial panels.
    /// Attach this to the same GameObject as the panel Canvas that MainHubTutorialController creates.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialPanelAnimator : MonoBehaviour
    {
        [Header("Reveal")]
        [SerializeField] private float revealDuration = 0.55f;
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Proximity Glow")]
        [SerializeField] private float glowPulseSpeed = 2.5f;
        [SerializeField] private float glowMinAlpha = 0.0f;
        [SerializeField] private float glowMaxAlpha = 0.22f;

        [Header("Completion")]
        [SerializeField] private Color completedAccentColor = new Color(0.2f, 0.95f, 0.5f, 1f);

        private enum PanelState { Hidden, Revealing, Idle, Completed }

        private PanelState state = PanelState.Hidden;
        private float revealTimer;
        private Vector3 targetScale;

        // Cached references set by the tutorial controller
        private CanvasGroup canvasGroup;
        private Image accentBarImage;
        private Image glowBorderImage;
        private Image progressFillImage;
        private Color originalAccentColor;

        private bool isInProximity;
        private float dwellProgress;

        /// <summary>
        /// Called by MainHubTutorialController once it finishes building the panel UI.
        /// </summary>
        public void Initialize(CanvasGroup group, Image accentBar, Image glowBorder, Image progressFill)
        {
            canvasGroup = group;
            accentBarImage = accentBar;
            glowBorderImage = glowBorder;
            progressFillImage = progressFill;

            if (accentBarImage != null)
            {
                originalAccentColor = accentBarImage.color;
            }

            targetScale = transform.localScale;
            transform.localScale = Vector3.zero;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (glowBorderImage != null)
            {
                Color c = glowBorderImage.color;
                c.a = 0f;
                glowBorderImage.color = c;
            }

            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = 0f;
            }

            state = PanelState.Hidden;
        }

        /// <summary>Begin the scale + fade-in reveal animation.</summary>
        public void TriggerReveal()
        {
            if (state != PanelState.Hidden)
            {
                return;
            }

            state = PanelState.Revealing;
            revealTimer = 0f;
        }

        /// <summary>Mark the panel as completed (checkmark accent, glow freeze).</summary>
        public void MarkCompleted()
        {
            state = PanelState.Completed;

            if (accentBarImage != null)
            {
                accentBarImage.color = completedAccentColor;
            }

            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = 1f;
            }
        }

        /// <summary>Set proximity state from the progress tracker.</summary>
        public void SetProximity(bool inRange, float currentDwellProgress)
        {
            isInProximity = inRange;
            dwellProgress = Mathf.Clamp01(currentDwellProgress);
        }

        private void Update()
        {
            switch (state)
            {
                case PanelState.Hidden:
                    break;

                case PanelState.Revealing:
                    UpdateReveal();
                    break;

                case PanelState.Idle:
                    UpdateIdleGlow();
                    UpdateProgressFill();
                    break;

                case PanelState.Completed:
                    UpdateCompletedGlow();
                    break;
            }
        }

        private void UpdateReveal()
        {
            revealTimer += Time.deltaTime;
            float t = revealDuration > 0f ? Mathf.Clamp01(revealTimer / revealDuration) : 1f;
            float curveValue = revealCurve.Evaluate(t);

            transform.localScale = targetScale * curveValue;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = curveValue;
            }

            if (t >= 1f)
            {
                transform.localScale = targetScale;
                if (canvasGroup != null) canvasGroup.alpha = 1f;
                state = PanelState.Idle;
            }
        }

        private void UpdateIdleGlow()
        {
            if (glowBorderImage == null)
            {
                return;
            }

            Color c = glowBorderImage.color;

            if (isInProximity)
            {
                float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
                c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, pulse);
            }
            else
            {
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * 4f);
            }

            glowBorderImage.color = c;
        }

        private void UpdateProgressFill()
        {
            if (progressFillImage == null)
            {
                return;
            }

            progressFillImage.fillAmount = Mathf.Lerp(progressFillImage.fillAmount, dwellProgress, Time.deltaTime * 6f);
        }

        private void UpdateCompletedGlow()
        {
            if (glowBorderImage == null)
            {
                return;
            }

            Color c = completedAccentColor;
            c.a = Mathf.Lerp(0.08f, 0.16f, (Mathf.Sin(Time.time * 1.2f) + 1f) * 0.5f);
            glowBorderImage.color = c;
        }
    }
}
