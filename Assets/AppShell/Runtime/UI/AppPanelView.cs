using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class AppPanelView : MonoBehaviour
    {
        [SerializeField] private AppPanelType panelType = AppPanelType.Home;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool disableGameObjectWhenHidden = true;

        public AppPanelType PanelType => panelType;
        public bool IsVisible => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
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

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public virtual void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (disableGameObjectWhenHidden)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
