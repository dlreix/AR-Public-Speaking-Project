using System.Collections;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class AppPanelView : MonoBehaviour
    {
        [SerializeField] private AppPanelType panelType = AppPanelType.Home;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool disableGameObjectWhenHidden = true;
        [SerializeField] [Min(0f)] private float transitionDuration = 0.14f;
        [SerializeField] [Range(0.9f, 1f)] private float hiddenScale = 0.985f;

        private Coroutine transitionRoutine;

        public AppPanelType PanelType => panelType;
        public bool IsVisible => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            ApplyInstantState(gameObject.activeSelf);
        }

        public virtual void Show()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (canvasGroup == null)
            {
                return;
            }

            if (!Application.isPlaying || transitionDuration <= 0f)
            {
                ApplyInstantState(true);
                return;
            }

            StartTransition(show: true);
        }

        public virtual void Hide()
        {
            if (canvasGroup == null)
            {
                if (disableGameObjectWhenHidden)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            if (!gameObject.activeSelf)
            {
                ApplyInstantState(false);
                return;
            }

            if (!Application.isPlaying || transitionDuration <= 0f)
            {
                ApplyInstantState(false);
                return;
            }

            StartTransition(show: false);
        }

        private void StartTransition(bool show)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(AnimateVisibility(show));
        }

        private IEnumerator AnimateVisibility(bool show)
        {
            if (show && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            float duration = Mathf.Max(0.01f, transitionDuration);
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = show ? 1f : 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = show ? Vector3.one : Vector3.one * hiddenScale;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                transform.localScale = Vector3.Lerp(startScale, targetScale, eased);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            transform.localScale = targetScale;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;

            if (!show && disableGameObjectWhenHidden)
            {
                gameObject.SetActive(false);
            }

            transitionRoutine = null;
        }

        private void ApplyInstantState(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            transform.localScale = visible ? Vector3.one : Vector3.one * hiddenScale;

            if (!visible && disableGameObjectWhenHidden)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
