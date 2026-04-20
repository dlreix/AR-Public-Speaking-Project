using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRPublicSpeaking.AppShell.Flow
{
    public class TransitionManager : MonoBehaviour
    {
        public static TransitionManager Instance { get; private set; }

        [SerializeField] private CanvasGroup overlayCanvasGroup;
        [SerializeField] private TMP_Text loadingLabel;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private string defaultLoadingMessage = "Loading...";
        [SerializeField] private bool persistAcrossSceneLoads = true;

        private Coroutine activeTransition;

        private void Awake()
        {
            if (persistAcrossSceneLoads)
            {
                RegisterPersistentInstance();
            }

            SetOverlayState(0f, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void LoadScene(string sceneName, string loadingMessage = null, Action beforeLoad = null, Action afterLoad = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[TransitionManager] Refused to load an empty scene name.");
                return;
            }

            if (activeTransition != null)
            {
                StopCoroutine(activeTransition);
            }

            activeTransition = StartCoroutine(LoadSceneRoutine(sceneName, loadingMessage, beforeLoad, afterLoad));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, string loadingMessage, Action beforeLoad, Action afterLoad)
        {
            if (loadingLabel != null)
            {
                loadingLabel.text = string.IsNullOrWhiteSpace(loadingMessage) ? defaultLoadingMessage : loadingMessage;
            }

            yield return FadeTo(1f, true);

            beforeLoad?.Invoke();

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            if (loadOperation == null)
            {
                yield return FadeTo(0f, false);
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            afterLoad?.Invoke();

            yield return FadeTo(0f, false);
            activeTransition = null;
        }

        private IEnumerator FadeTo(float targetAlpha, bool forceVisible)
        {
            if (overlayCanvasGroup == null)
            {
                yield break;
            }

            if (forceVisible &&
                overlayCanvasGroup.gameObject != gameObject &&
                !overlayCanvasGroup.gameObject.activeSelf)
            {
                overlayCanvasGroup.gameObject.SetActive(true);
            }

            float initialAlpha = overlayCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
                overlayCanvasGroup.alpha = Mathf.Lerp(initialAlpha, targetAlpha, t);
                yield return null;
            }

            SetOverlayState(targetAlpha, targetAlpha > 0.001f);
        }

        private void SetOverlayState(float alpha, bool active)
        {
            if (overlayCanvasGroup == null)
            {
                return;
            }

            overlayCanvasGroup.alpha = alpha;
            overlayCanvasGroup.interactable = active;
            overlayCanvasGroup.blocksRaycasts = active;

            if (overlayCanvasGroup.gameObject == gameObject)
            {
                return;
            }

            if (overlayCanvasGroup.gameObject.activeSelf != active)
            {
                overlayCanvasGroup.gameObject.SetActive(active);
            }
        }

        private void RegisterPersistentInstance()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent != null)
            {
                Debug.LogWarning(
                    "[TransitionManager] Place the TransitionManager on a standalone root object when persisting it across scene loads.");
            }

            DontDestroyOnLoad(gameObject);
        }
    }
}
