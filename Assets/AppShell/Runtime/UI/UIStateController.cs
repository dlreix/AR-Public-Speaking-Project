using System.Collections.Generic;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class UIStateController : MonoBehaviour
    {
        [SerializeField] private List<AppPanelView> panels = new List<AppPanelView>();
        [SerializeField] private AppPanelType defaultPanel = AppPanelType.Home;

        private readonly Dictionary<AppPanelType, AppPanelView> panelLookup = new Dictionary<AppPanelType, AppPanelView>();
        private readonly List<AppPanelType> history = new List<AppPanelType>();
        private AppPanelType currentPanel = AppPanelType.Home;
        private bool hasCurrentPanel;

        public AppPanelType CurrentPanel => hasCurrentPanel ? currentPanel : defaultPanel;
        public bool CanGoBack => TryGetPreviousPanel(out _);

        private void Awake()
        {
            CachePanels();
            ShowPanel(defaultPanel, false);
        }

        private void OnValidate()
        {
            if (panels.Count == 0)
            {
                panels.AddRange(GetComponentsInChildren<AppPanelView>(true));
            }
        }

        public void ShowPanel(AppPanelType panelType, bool rememberHistory = true)
        {
            CachePanels();

            if (!panelLookup.TryGetValue(panelType, out _))
            {
                Debug.LogWarning($"[UIStateController] Panel not registered: {panelType}");
                return;
            }

            TrackHistory(panelType, rememberHistory);
            ApplyPanelVisibility(panelType);

            currentPanel = panelType;
            hasCurrentPanel = true;
        }

        public void GoBack()
        {
            CachePanels();

            while (history.Count > 0)
            {
                int lastIndex = history.Count - 1;
                AppPanelType previousPanel = history[lastIndex];
                history.RemoveAt(lastIndex);

                if (hasCurrentPanel && previousPanel == currentPanel)
                {
                    continue;
                }

                if (!panelLookup.ContainsKey(previousPanel))
                {
                    continue;
                }

                ShowPanel(previousPanel, false);
                return;
            }

            ShowDefaultPanel();
        }

        public bool TryGetPanel(AppPanelType panelType, out AppPanelView panelView)
        {
            CachePanels();
            return panelLookup.TryGetValue(panelType, out panelView);
        }

        public void RegisterPanel(AppPanelView panelView)
        {
            if (panelView == null)
            {
                return;
            }

            if (!panels.Contains(panelView))
            {
                panels.Add(panelView);
            }

            panelLookup[panelView.PanelType] = panelView;
        }

        public void ShowDefaultPanel(bool rememberHistory = false)
        {
            ShowPanel(defaultPanel, rememberHistory);
        }

        public void ClearHistory()
        {
            history.Clear();
        }

        private void CachePanels()
        {
            AppPanelView[] discoveredPanels =
                FindObjectsByType<AppPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < discoveredPanels.Length; index++)
            {
                AppPanelView discoveredPanel = discoveredPanels[index];
                if (discoveredPanel != null &&
                    discoveredPanel.gameObject.scene == gameObject.scene &&
                    !panels.Contains(discoveredPanel))
                {
                    panels.Add(discoveredPanel);
                }
            }

            panelLookup.Clear();

            for (int index = 0; index < panels.Count; index++)
            {
                AppPanelView panel = panels[index];
                if (panel == null)
                {
                    continue;
                }

                panelLookup[panel.PanelType] = panel;
            }
        }

        private void ApplyPanelVisibility(AppPanelType panelType)
        {
            foreach (var pair in panelLookup)
            {
                if (pair.Key == panelType)
                {
                    pair.Value.Show();
                }
                else
                {
                    pair.Value.Hide();
                }
            }
        }

        private void TrackHistory(AppPanelType nextPanel, bool rememberHistory)
        {
            if (!rememberHistory || !hasCurrentPanel || nextPanel == currentPanel)
            {
                return;
            }

            int lastIndex = history.Count - 1;
            if (lastIndex >= 0 && history[lastIndex] == nextPanel)
            {
                history.RemoveAt(lastIndex);
                return;
            }

            if (lastIndex >= 0 && history[lastIndex] == currentPanel)
            {
                return;
            }

            history.Add(currentPanel);
        }

        private bool TryGetPreviousPanel(out AppPanelType panelType)
        {
            CachePanels();

            for (int index = history.Count - 1; index >= 0; index--)
            {
                AppPanelType candidate = history[index];
                if (hasCurrentPanel && candidate == currentPanel)
                {
                    continue;
                }

                if (!panelLookup.ContainsKey(candidate))
                {
                    continue;
                }

                panelType = candidate;
                return true;
            }

            panelType = defaultPanel;
            return false;
        }
    }
}
