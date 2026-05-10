using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.PresentationQuestioning;

namespace VRPublicSpeaking.AppShell.UI
{
    public class MainHubDashboardPresenter : MonoBehaviour
    {
        private const string PanelName = "MainHubDashboardPanel";
        private const float ChartWidth = 470f;
        private const float ChartHeight = 170f;

        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RectTransform embeddedParent;
        [SerializeField] private AppPanelView panelView;
        [SerializeField] private bool showBackButton = true;
        [SerializeField] private bool showFooter = true;

        private readonly List<Button> tabButtons = new List<Button>(4);
        private readonly List<GameObject> tabPages = new List<GameObject>(4);
        private readonly List<MetricRow> performanceRows = new List<MetricRow>(4);
        private readonly List<TMP_Text> detailValueLabels = new List<TMP_Text>(8);
        private readonly List<TMP_Text> historyScoreLabels = new List<TMP_Text>(5);
        private readonly List<TMP_Text> historyDateLabels = new List<TMP_Text>(5);
        private readonly List<Image> chartSegments = new List<Image>(9);
        private readonly List<TMP_Text> chartLabels = new List<TMP_Text>(10);

        private TMP_Text headerScoreLabel;
        private TMP_Text headerScoreBandLabel;
        private TMP_Text overviewScoreLabel;
        private TMP_Text overviewScoreBandLabel;
        private TMP_Text overviewInfoLabel;
        private TMP_Text focusLabel;
        private TMP_Text coachSummaryLabel;
        private TMP_Text coachNotesLabel;
        private RectTransform chartRoot;
        private TMP_Text resetButtonLabel;
        private bool resetConfirmationArmed;
        private DashboardTab currentTab = DashboardTab.Overview;

        private readonly Color backgroundColor = new Color(0.015f, 0.020f, 0.035f, 0.98f);
        private readonly Color panelColor = new Color(0.035f, 0.055f, 0.085f, 0.96f);
        private readonly Color panelAltColor = new Color(0.055f, 0.085f, 0.125f, 0.94f);
        private readonly Color accentColor = new Color(0.12f, 0.78f, 0.96f, 1f);
        private readonly Color accentSoftColor = new Color(0.12f, 0.78f, 0.96f, 0.28f);
        private readonly Color goldColor = new Color(1f, 0.64f, 0.24f, 1f);
        private readonly Color greenColor = new Color(0.18f, 0.88f, 0.46f, 1f);
        private readonly Color bodyTextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private readonly Color mutedTextColor = new Color(0.58f, 0.68f, 0.78f, 1f);
        private readonly Color buttonColor = new Color(0.11f, 0.19f, 0.27f, 0.96f);
        private readonly Color selectedButtonColor = new Color(0.18f, 0.38f, 0.50f, 1f);

        public AppPanelView PanelView => panelView;

        private enum DashboardTab
        {
            Overview = 0,
            Performance = 1,
            Details = 2,
            CoachHistory = 3
        }

        private sealed class MetricRow
        {
            public TMP_Text NameLabel;
            public TMP_Text ValueLabel;
            public Image FillImage;
        }

        public void Configure(
            Canvas canvas,
            UIStateController stateController,
            AppFlowManager flowManager,
            AppRuntimeState appRuntimeState)
        {
            targetCanvas = canvas;
            embeddedParent = null;
            uiStateController = stateController;
            appFlowManager = flowManager;
            runtimeState = appRuntimeState ?? AppRuntimeState.GetOrCreate();
            showBackButton = true;
            showFooter = true;
            EnsurePanel();
        }

        public void ConfigureEmbedded(
            RectTransform parent,
            AppRuntimeState appRuntimeState,
            bool includeBackButton = false,
            bool includeFooter = true)
        {
            embeddedParent = parent;
            targetCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            uiStateController = null;
            appFlowManager = null;
            runtimeState = appRuntimeState ?? AppRuntimeState.GetOrCreate();
            showBackButton = includeBackButton;
            showFooter = includeFooter;
            EnsurePanel();
            panelView?.Show();
        }

        public void RefreshDashboard()
        {
            RefreshDashboardInternal();
        }

        public void ShowOverviewTab()
        {
            ShowTab(DashboardTab.Overview);
        }

        public void ShowPerformanceTab()
        {
            ShowTab(DashboardTab.Performance);
        }

        public void ShowDetailsTab()
        {
            ShowTab(DashboardTab.Details);
        }

        public void ShowCoachHistoryTab()
        {
            ShowTab(DashboardTab.CoachHistory);
        }

        public void ResetDashboardData()
        {
            EnsureRuntimeState();
            EnsureDataManager();

            if (!resetConfirmationArmed)
            {
                resetConfirmationArmed = true;
                SetText(resetButtonLabel, "Confirm Reset");
                SetText(coachNotesLabel, "Press Confirm Reset again to clear saved dashboard history.");
                return;
            }

            if (DataManager.Instance != null)
            {
                DataManager.Instance.DeleteAllData();
            }

            resetConfirmationArmed = false;
            SetText(resetButtonLabel, "Reset");
            RefreshDashboardInternal();
        }

        public void GoBack()
        {
            if (appFlowManager != null)
            {
                appFlowManager.GoBack();
                return;
            }

            uiStateController?.ShowDefaultPanel();
        }

        private void RefreshDashboardInternal()
        {
            EnsurePanel();
            EnsureRuntimeState();
            EnsureDataManager();

            SessionData latestSession = GetLatestSession();
            SessionResultSummary summary = runtimeState.GetLastSessionResultCopy();
            FeedbackReport report = latestSession != null && latestSession.detailedReport != null
                ? latestSession.detailedReport
                : summary.DetailedReport;

            RefreshOverview(latestSession, summary);
            RefreshPerformance(latestSession, summary);
            RefreshDetails(report, summary, latestSession);
            RefreshCoachAndHistory(report, latestSession, summary);
            ShowTab(currentTab);
        }

        private void EnsurePanel()
        {
            if (panelView != null)
            {
                panelView.SetPanelType(AppPanelType.Dashboard);
                uiStateController?.RegisterPanel(panelView);
                return;
            }

            Transform panelHost = embeddedParent != null ? embeddedParent : targetCanvas != null ? targetCanvas.transform : null;
            if (panelHost == null)
            {
                return;
            }

            Transform existingPanel = panelHost.Find(PanelName);
            if (existingPanel != null)
            {
                panelView = existingPanel.GetComponent<AppPanelView>();
                if (panelView == null)
                {
                    panelView = existingPanel.gameObject.AddComponent<AppPanelView>();
                }

                panelView.SetPanelType(AppPanelType.Dashboard);
                uiStateController?.RegisterPanel(panelView);
                return;
            }

            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(AppPanelView));
            panelObject.transform.SetParent(panelHost, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            if (embeddedParent != null)
            {
                Stretch(panelRect);
            }
            else
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(1260f, 790f);
                panelRect.anchoredPosition = Vector2.zero;
            }

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = backgroundColor;
            panelImage.raycastTarget = true;
            AddOutline(panelObject, accentSoftColor, new Vector2(1f, -1f));

            VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            bool isEmbedded = embeddedParent != null;
            rootLayout.padding = isEmbedded
                ? new RectOffset(24, 24, 20, 20)
                : new RectOffset(34, 34, 28, 28);
            rootLayout.spacing = isEmbedded ? 10f : 14f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            panelView = panelObject.GetComponent<AppPanelView>();
            panelView.SetPanelType(AppPanelType.Dashboard);

            BuildHeader(panelObject.transform);
            BuildTabs(panelObject.transform);
            BuildPages(panelObject.transform);
            if (showFooter)
            {
                BuildFooter(panelObject.transform);
            }

            uiStateController?.RegisterPanel(panelView);
            if (embeddedParent == null)
            {
                panelView.Hide();
            }
        }

        private void BuildHeader(Transform parent)
        {
            bool isEmbedded = embeddedParent != null;
            GameObject header = CreateLayoutObject("DashboardHeader", parent, new Vector2(0f, isEmbedded ? 78f : 86f));
            HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            GameObject titleBlock = CreateLayoutObject("DashboardTitleBlock", header.transform, new Vector2(0f, isEmbedded ? 72f : 80f), flexibleWidth: 1f);
            VerticalLayoutGroup titleLayout = titleBlock.AddComponent<VerticalLayoutGroup>();
            titleLayout.spacing = 2f;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = false;

            CreateText(titleBlock.transform, "DashboardTitle", "Dashboard", isEmbedded ? 38f : 42f, FontStyles.Bold, accentColor, TextAlignmentOptions.Left, new Vector2(760f, 48f));
            CreateText(titleBlock.transform, "DashboardSubtitle", "Latest speaking performance", 18f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.Left, new Vector2(760f, 24f));

            GameObject scoreBlock = CreateCard("DashboardScoreChip", header.transform, new Vector2(280f, 80f));
            VerticalLayoutGroup scoreLayout = scoreBlock.AddComponent<VerticalLayoutGroup>();
            scoreLayout.childAlignment = TextAnchor.MiddleCenter;
            scoreLayout.childControlWidth = true;
            scoreLayout.childControlHeight = false;
            scoreLayout.spacing = 0f;

            headerScoreLabel = CreateText(scoreBlock.transform, "DashboardScoreValue", "--", 38f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center, new Vector2(260f, 42f));
            headerScoreBandLabel = CreateText(scoreBlock.transform, "DashboardScoreBand", "Score pending", 15f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.Center, new Vector2(260f, 22f));
        }

        private void BuildTabs(Transform parent)
        {
            GameObject tabs = CreateLayoutObject("DashboardTabs", parent, new Vector2(0f, embeddedParent != null ? 58f : 64f));
            HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            CreateTabButton(tabs.transform, "Overview", ShowOverviewTab);
            CreateTabButton(tabs.transform, "Performance", ShowPerformanceTab);
            CreateTabButton(tabs.transform, "Details", ShowDetailsTab);
            CreateTabButton(tabs.transform, "Coach / History", ShowCoachHistoryTab);
        }

        private void BuildPages(Transform parent)
        {
            GameObject content = CreateLayoutObject("DashboardContent", parent, new Vector2(0f, embeddedParent != null ? 450f : 510f), flexibleHeight: 1f);
            RectTransform contentRect = content.GetComponent<RectTransform>();

            BuildOverviewPage(contentRect);
            BuildPerformancePage(contentRect);
            BuildDetailsPage(contentRect);
            BuildCoachHistoryPage(contentRect);
        }

        private void BuildOverviewPage(RectTransform parent)
        {
            GameObject page = CreatePage("DashboardOverviewPage", parent);
            HorizontalLayoutGroup layout = page.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            GameObject scoreCard = CreateCard("OverviewScoreCard", page.transform, new Vector2(360f, 0f));
            VerticalLayoutGroup scoreLayout = scoreCard.AddComponent<VerticalLayoutGroup>();
            scoreLayout.padding = new RectOffset(24, 24, 22, 22);
            scoreLayout.spacing = 10f;
            scoreLayout.childAlignment = TextAnchor.MiddleCenter;
            scoreLayout.childControlWidth = true;
            scoreLayout.childControlHeight = false;

            CreateText(scoreCard.transform, "OverviewScoreTitle", "Overall", 22f, FontStyles.Bold, accentColor, TextAlignmentOptions.Center, new Vector2(300f, 28f));
            overviewScoreLabel = CreateText(scoreCard.transform, "OverviewBigScore", "--", 86f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center, new Vector2(300f, 104f));
            overviewScoreBandLabel = CreateText(scoreCard.transform, "OverviewBand", "Score pending", 20f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.Center, new Vector2(300f, 32f));

            GameObject infoCard = CreateCard("OverviewInfoCard", page.transform, new Vector2(0f, 0f), flexibleWidth: 1f);
            VerticalLayoutGroup infoLayout = infoCard.AddComponent<VerticalLayoutGroup>();
            infoLayout.padding = new RectOffset(28, 28, 24, 24);
            infoLayout.spacing = 16f;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = false;

            CreateText(infoCard.transform, "OverviewInfoTitle", "Run Summary", 26f, FontStyles.Bold, accentColor, TextAlignmentOptions.Left, new Vector2(760f, 34f));
            overviewInfoLabel = CreateText(infoCard.transform, "OverviewInfoValue", "No completed session yet.", 22f, FontStyles.Normal, bodyTextColor, TextAlignmentOptions.TopLeft, new Vector2(760f, 150f));
            focusLabel = CreateText(infoCard.transform, "OverviewFocusValue", "Complete a practice session to populate the dashboard.", 21f, FontStyles.Bold, greenColor, TextAlignmentOptions.TopLeft, new Vector2(760f, 86f));
        }

        private void BuildPerformancePage(RectTransform parent)
        {
            GameObject page = CreatePage("DashboardPerformancePage", parent);
            VerticalLayoutGroup layout = page.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            CreateSectionHeader(page.transform, "Performance Scores");
            performanceRows.Add(CreateMetricRow(page.transform, "Overall", goldColor));
            performanceRows.Add(CreateMetricRow(page.transform, "Eye Contact", accentColor));
            performanceRows.Add(CreateMetricRow(page.transform, "Speech Pace", greenColor));
            performanceRows.Add(CreateMetricRow(page.transform, "Posture", new Color(0.52f, 0.72f, 1f, 1f)));
        }

        private void BuildDetailsPage(RectTransform parent)
        {
            GameObject page = CreatePage("DashboardDetailsPage", parent);
            GridLayoutGroup grid = page.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.spacing = new Vector2(14f, 14f);
            grid.cellSize = new Vector2(285f, 112f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            string[] labels =
            {
                "WPM",
                "Filler",
                "Pause",
                "Tone",
                "Eye Ratio",
                "Slouch",
                "Sway",
                "Crossed Arms"
            };

            for (int index = 0; index < labels.Length; index++)
            {
                GameObject card = CreateCard("DetailCard_" + labels[index].Replace(" ", string.Empty), page.transform, new Vector2(285f, 112f));
                VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(16, 16, 14, 14);
                layout.spacing = 4f;
                layout.childControlWidth = true;
                layout.childControlHeight = false;

                CreateText(card.transform, "DetailLabel", labels[index], 17f, FontStyles.Bold, mutedTextColor, TextAlignmentOptions.Left, new Vector2(250f, 24f));
                TMP_Text valueLabel = CreateText(card.transform, "DetailValue", "--", 32f, FontStyles.Bold, bodyTextColor, TextAlignmentOptions.Left, new Vector2(250f, 48f));
                detailValueLabels.Add(valueLabel);
            }
        }

        private void BuildCoachHistoryPage(RectTransform parent)
        {
            GameObject page = CreatePage("DashboardCoachHistoryPage", parent);
            HorizontalLayoutGroup layout = page.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            GameObject coachCard = CreateCard("CoachCard", page.transform, new Vector2(0f, 0f), flexibleWidth: 1f);
            VerticalLayoutGroup coachLayout = coachCard.AddComponent<VerticalLayoutGroup>();
            coachLayout.padding = new RectOffset(22, 22, 20, 20);
            coachLayout.spacing = 10f;
            coachLayout.childControlWidth = true;
            coachLayout.childControlHeight = false;

            CreateText(coachCard.transform, "CoachTitle", "AI Coach", 25f, FontStyles.Bold, accentColor, TextAlignmentOptions.Left, new Vector2(500f, 32f));
            coachSummaryLabel = CreateText(coachCard.transform, "CoachSummary", "No coach feedback yet.", 20f, FontStyles.Normal, bodyTextColor, TextAlignmentOptions.TopLeft, new Vector2(500f, 112f));
            coachNotesLabel = CreateText(coachCard.transform, "CoachNotes", string.Empty, 17f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.TopLeft, new Vector2(500f, 245f));

            GameObject historyCard = CreateCard("HistoryCard", page.transform, new Vector2(520f, 0f));
            VerticalLayoutGroup historyLayout = historyCard.AddComponent<VerticalLayoutGroup>();
            historyLayout.padding = new RectOffset(20, 20, 18, 18);
            historyLayout.spacing = 10f;
            historyLayout.childControlWidth = true;
            historyLayout.childControlHeight = false;

            CreateText(historyCard.transform, "HistoryTitle", "History", 25f, FontStyles.Bold, accentColor, TextAlignmentOptions.Left, new Vector2(470f, 32f));
            BuildHistoryCards(historyCard.transform);
            BuildChart(historyCard.transform);
        }

        private void BuildHistoryCards(Transform parent)
        {
            GameObject row = CreateLayoutObject("HistoryCards", parent, new Vector2(0f, 104f));
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            for (int index = 0; index < 5; index++)
            {
                GameObject card = CreateCard("HistoryMiniCard_" + index, row.transform, new Vector2(0f, 96f), flexibleWidth: 1f);
                VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
                cardLayout.padding = new RectOffset(8, 8, 8, 8);
                cardLayout.spacing = 0f;
                cardLayout.childAlignment = TextAnchor.MiddleCenter;
                cardLayout.childControlWidth = true;
                cardLayout.childControlHeight = false;

                historyScoreLabels.Add(CreateText(card.transform, "HistoryScore", "--", 24f, FontStyles.Bold, goldColor, TextAlignmentOptions.Center, new Vector2(80f, 34f)));
                historyDateLabels.Add(CreateText(card.transform, "HistoryDate", "--", 12f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.Center, new Vector2(80f, 34f)));
            }
        }

        private void BuildChart(Transform parent)
        {
            GameObject chartObject = CreateCard("HistoryChart", parent, new Vector2(ChartWidth, ChartHeight));
            chartRoot = chartObject.GetComponent<RectTransform>();

            for (int index = 0; index < 9; index++)
            {
                Image segment = CreateImage("ChartSegment_" + index, chartObject.transform, accentColor);
                RectTransform rect = segment.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(0f, 4f);
                segment.gameObject.SetActive(false);
                chartSegments.Add(segment);
            }

            for (int index = 0; index < 10; index++)
            {
                TMP_Text label = CreateText(chartObject.transform, "ChartLabel_" + index, string.Empty, 11f, FontStyles.Normal, mutedTextColor, TextAlignmentOptions.Center, new Vector2(44f, 18f));
                chartLabels.Add(label);
            }
        }

        private void BuildFooter(Transform parent)
        {
            GameObject footer = CreateLayoutObject("DashboardFooter", parent, new Vector2(0f, embeddedParent != null ? 74f : 66f));
            HorizontalLayoutGroup layout = footer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            if (showBackButton)
            {
                CreateButton(footer.transform, "DashboardBackButton", "Back", GoBack, new Vector2(170f, embeddedParent != null ? 68f : 58f));
            }
            CreateButton(footer.transform, "DashboardRefreshButton", "Refresh", RefreshDashboard, new Vector2(170f, embeddedParent != null ? 68f : 58f));
            Button resetButton = CreateButton(footer.transform, "DashboardResetButton", "Reset", ResetDashboardData, new Vector2(170f, embeddedParent != null ? 68f : 58f), new Color(0.32f, 0.15f, 0.18f, 1f));
            resetButtonLabel = resetButton.GetComponentInChildren<TMP_Text>();
        }

        private Button CreateTabButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(parent, "Tab_" + label.Replace(" ", string.Empty).Replace("/", string.Empty), label, action, new Vector2(0f, 58f));
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            tabButtons.Add(button);
            return button;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action,
            Vector2 size,
            Color? overrideColor = null)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minWidth = size.x > 0f ? size.x : 0f;
            layout.preferredWidth = size.x > 0f ? size.x : -1f;
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;

            Image image = buttonObject.GetComponent<Image>();
            Color baseColor = overrideColor ?? buttonColor;
            image.color = baseColor;
            image.raycastTarget = true;
            AddOutline(buttonObject, accentSoftColor, new Vector2(1f, -1f));

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = selectedButtonColor;
            colors.pressedColor = new Color(0.35f, 0.62f, 0.78f, 1f);
            colors.selectedColor = selectedButtonColor;
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.42f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text text = CreateText(buttonObject.transform, "Label", label, 19f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, size);
            RectTransform textRect = text.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 6f);
                textRect.offsetMax = new Vector2(-10f, -6f);
                textRect.sizeDelta = Vector2.zero;
                textRect.localScale = Vector3.one;
            }

            LayoutElement textLayout = text.GetComponent<LayoutElement>();
            if (textLayout != null)
            {
                textLayout.ignoreLayout = true;
            }

            text.enableAutoSizing = true;
            text.fontSizeMin = 13f;
            text.fontSizeMax = 19f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        private MetricRow CreateMetricRow(Transform parent, string label, Color fillColor)
        {
            GameObject row = CreateCard("MetricRow_" + label.Replace(" ", string.Empty), parent, new Vector2(0f, 92f));
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            TMP_Text nameLabel = CreateText(row.transform, "MetricName", label, 21f, FontStyles.Bold, bodyTextColor, TextAlignmentOptions.Left, new Vector2(190f, 56f));
            TMP_Text valueLabel = CreateText(row.transform, "MetricValue", "--", 24f, FontStyles.Bold, fillColor, TextAlignmentOptions.Center, new Vector2(90f, 56f));

            GameObject barRoot = new GameObject("MetricBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            barRoot.transform.SetParent(row.transform, false);
            LayoutElement barLayout = barRoot.GetComponent<LayoutElement>();
            barLayout.flexibleWidth = 1f;
            barLayout.minHeight = 24f;
            barLayout.preferredHeight = 24f;
            Image barBg = barRoot.GetComponent<Image>();
            barBg.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);
            barBg.raycastTarget = false;

            Image fill = CreateImage("MetricFill", barRoot.transform, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);

            return new MetricRow { NameLabel = nameLabel, ValueLabel = valueLabel, FillImage = fill };
        }

        private void RefreshOverview(SessionData latestSession, SessionResultSummary summary)
        {
            bool hasScore = latestSession != null || summary.HasOverallScore;
            float totalScore = latestSession != null ? latestSession.overallScore : summary.TotalScore;
            string band = latestSession != null && latestSession.detailedReport != null
                ? latestSession.detailedReport.performanceBand
                : summary.PerformanceBand;

            string scoreText = hasScore ? totalScore.ToString("0") : "--";
            string bandText = hasScore ? ResolveBandText(totalScore, band) : "Score pending";
            SetText(headerScoreLabel, scoreText);
            SetText(headerScoreBandLabel, bandText);
            SetText(overviewScoreLabel, scoreText);
            SetText(overviewScoreBandLabel, bandText);

            SessionConfig config = runtimeState.GetSessionConfigCopy();
            string environmentName = runtimeState.SelectedEnvironment != null
                ? runtimeState.SelectedEnvironment.DisplayName
                : "No environment selected";
            string dateText = latestSession != null ? latestSession.date : "--";
            float durationSeconds = latestSession != null ? latestSession.durationSeconds : summary.DurationSeconds;
            string durationText = durationSeconds > 0f ? FormatDuration(durationSeconds) : config.GetDurationDisplay();
            float filler = latestSession != null ? latestSession.fillerWordCount : summary.FillerWordCount;

            SetText(
                overviewInfoLabel,
                $"Date: {dateText}\nEnvironment: {environmentName}\nMode: {config.PracticeMode}\nDuration: {durationText}\nFiller Words: {filler:0}");

            string strongest = latestSession != null && latestSession.detailedReport != null
                ? latestSession.detailedReport.strongestArea
                : summary.StrongestArea;
            string weakest = latestSession != null && latestSession.detailedReport != null
                ? latestSession.detailedReport.weakestArea
                : summary.WeakestArea;

            SetText(focusLabel, BuildFocusText(strongest, weakest));
        }

        private void RefreshPerformance(SessionData latestSession, SessionResultSummary summary)
        {
            SetMetricRow(performanceRows, 0, "Overall", latestSession != null ? latestSession.overallScore : summary.TotalScore, latestSession != null || summary.HasOverallScore);
            SetMetricRow(performanceRows, 1, "Eye Contact", latestSession != null ? latestSession.eyeContact : summary.EyeContactScore, latestSession != null || summary.HasEyeContactScore);
            SetMetricRow(performanceRows, 2, "Speech Pace", latestSession != null ? latestSession.pace : summary.SpeechPaceScore, latestSession != null || summary.HasSpeechPaceScore);
            SetMetricRow(performanceRows, 3, "Posture", latestSession != null ? latestSession.posture : summary.PostureScore, latestSession != null || summary.HasPostureScore);
        }

        private void RefreshDetails(FeedbackReport report, SessionResultSummary summary, SessionData latestSession)
        {
            SetDetail(0, FormatRawMetric(latestSession?.hasWpm == true, latestSession != null ? latestSession.wpm : 0f, summary.HasWpm, summary.Wpm, null, " WPM"));
            SetDetail(1, FormatRawMetric(latestSession?.hasFillerWordsPerMinute == true, latestSession != null ? latestSession.fillerWordsPerMinute : 0f, summary.HasFillerWordsPerMinute, summary.FillerWordsPerMinute, null, " /min"));
            SetDetail(2, FormatRawMetric(latestSession?.hasAveragePauseDuration == true, latestSession != null ? latestSession.averagePauseDuration : 0f, summary.HasAveragePauseDuration, summary.AveragePauseDuration, null, "s"));
            SetDetail(3, FormatRawMetric(latestSession?.hasToneVariationScore == true, latestSession != null ? latestSession.toneVariationScore : 0f, summary.HasToneVariationScore, summary.ToneVariationScore, null, "%"));
            SetDetail(4, summary.HasEyeContactScore ? summary.EyeContactScore.ToString("0") : GetMetricValue(report, "Gaze Ratio"));
            SetDetail(5, FormatRawMetric(latestSession?.hasHeadSpeedEventsPerMinute == true, latestSession != null ? latestSession.headSpeedEventsPerMinute : 0f, summary.HasHeadSpeedEventsPerMinute, summary.HeadSpeedEventsPerMinute, null, " /min"));
            SetDetail(6, FormatRawMetric(latestSession?.hasHeadMovementPercent == true, latestSession != null ? latestSession.headMovementPercent : 0f, summary.HasHeadMovementPercent, summary.HeadMovementPercent, null, "%"));
            SetDetail(7, FormatRawMetric(latestSession?.hasCrossedArmsPercent == true, latestSession != null ? latestSession.crossedArmsPercent : 0f, summary.HasCrossedArmsPercent, summary.CrossedArmsPercent, null, "%"));
        }

        private void RefreshCoachAndHistory(FeedbackReport report, SessionData latestSession, SessionResultSummary summary)
        {
            PresentationQaResult qaResult = latestSession != null && latestSession.qaResult != null
                ? latestSession.qaResult
                : summary.QaResult;

            if (report == null)
            {
                SetText(coachSummaryLabel, "No coach feedback yet.");
                SetText(coachNotesLabel, BuildQaNotesOrFallback(qaResult, "Complete a scored session to populate strengths, weaknesses, and practice notes."));
            }
            else
            {
                SetText(
                    coachSummaryLabel,
                    $"{report.performanceBand}\nStrongest: {report.strongestArea}\nImprove: {report.weakestArea}\n\nSpeech: {GetCategorySummary(report, "Speech")}\nEye: {GetCategorySummary(report, "Eye Contact")}\nPosture: {GetCategorySummary(report, "Posture")}");
                SetText(coachNotesLabel, AppendQaNotes(BuildCoachNotes(report), qaResult));
            }

            RefreshHistoryCards();
            RefreshChart();
        }

        private void RefreshHistoryCards()
        {
            List<SessionData> sessions = GetSessions();
            for (int index = 0; index < historyScoreLabels.Count; index++)
            {
                int dataIndex = sessions.Count - 1 - index;
                if (dataIndex >= 0)
                {
                    SessionData session = sessions[dataIndex];
                    SetText(historyScoreLabels[index], session.overallScore.ToString("0"));
                    SetText(historyDateLabels[index], session.date);
                }
                else
                {
                    SetText(historyScoreLabels[index], "--");
                    SetText(historyDateLabels[index], "--");
                }
            }
        }

        private void RefreshChart()
        {
            List<SessionData> sessions = GetSessions();
            int count = Mathf.Min(sessions.Count, chartLabels.Count);
            int firstIndex = Mathf.Max(0, sessions.Count - count);

            for (int index = 0; index < chartSegments.Count; index++)
            {
                chartSegments[index].gameObject.SetActive(false);
            }

            for (int index = 0; index < chartLabels.Count; index++)
            {
                chartLabels[index].gameObject.SetActive(index < count);
            }

            if (count == 0)
            {
                return;
            }

            Vector2[] points = new Vector2[count];
            float left = -ChartWidth * 0.5f + 28f;
            float right = ChartWidth * 0.5f - 28f;
            float bottom = -ChartHeight * 0.5f + 34f;
            float top = ChartHeight * 0.5f - 22f;

            for (int index = 0; index < count; index++)
            {
                SessionData session = sessions[firstIndex + index];
                float x = count == 1 ? 0f : Mathf.Lerp(left, right, index / (float)(count - 1));
                float y = Mathf.Lerp(bottom, top, Mathf.Clamp01(session.overallScore / 100f));
                points[index] = new Vector2(x, y);

                RectTransform labelRect = chartLabels[index].transform as RectTransform;
                labelRect.anchoredPosition = new Vector2(x, -ChartHeight * 0.5f + 12f);
                chartLabels[index].text = (firstIndex + index + 1).ToString();
            }

            for (int index = 0; index < count - 1 && index < chartSegments.Count; index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[index + 1];
                Vector2 delta = end - start;
                RectTransform segmentRect = chartSegments[index].transform as RectTransform;
                segmentRect.anchoredPosition = start;
                segmentRect.sizeDelta = new Vector2(delta.magnitude, 4f);
                segmentRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                chartSegments[index].gameObject.SetActive(true);
            }
        }

        private void ShowTab(DashboardTab tab)
        {
            currentTab = tab;
            for (int index = 0; index < tabPages.Count; index++)
            {
                tabPages[index].SetActive(index == (int)tab);
            }

            for (int index = 0; index < tabButtons.Count; index++)
            {
                Color targetColor = index == (int)tab ? selectedButtonColor : buttonColor;
                Image image = tabButtons[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = targetColor;
                }

                ColorBlock colors = tabButtons[index].colors;
                colors.normalColor = targetColor;
                colors.highlightedColor = selectedButtonColor;
                colors.selectedColor = selectedButtonColor;
                tabButtons[index].colors = colors;

                Navigation navigation = tabButtons[index].navigation;
                if (navigation.mode == Navigation.Mode.None)
                {
                    navigation.mode = Navigation.Mode.Automatic;
                    tabButtons[index].navigation = navigation;
                }

                TMP_Text label = tabButtons[index].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = Color.white;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
            }
        }

        private void EnsureRuntimeState()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }
        }

        private static void EnsureDataManager()
        {
            if (DataManager.Instance != null)
            {
                return;
            }

            new GameObject("DataManager_Auto").AddComponent<DataManager>();
        }

        private static bool HasAnyScore(SessionResultSummary summary)
        {
            return summary != null &&
                (summary.HasOverallScore || summary.HasEyeContactScore || summary.HasSpeechPaceScore || summary.HasPostureScore);
        }

        private static SessionData GetLatestSession()
        {
            List<SessionData> sessions = GetSessions();
            return sessions.Count > 0 ? sessions[sessions.Count - 1] : null;
        }

        private static List<SessionData> GetSessions()
        {
            if (DataManager.Instance == null ||
                DataManager.Instance.history == null ||
                DataManager.Instance.history.allSessions == null)
            {
                return new List<SessionData>();
            }

            return DataManager.Instance.history.allSessions;
        }

        private static string GetMetricValue(FeedbackReport report, string metricName)
        {
            if (report == null || report.items == null)
            {
                return "--";
            }

            for (int index = 0; index < report.items.Count; index++)
            {
                FeedbackItem item = report.items[index];
                if (item != null && item.metric == metricName)
                {
                    return item.score.ToString("0.#");
                }
            }

            return "--";
        }

        private static string FormatRawMetric(
            bool hasSessionValue,
            float sessionValue,
            bool hasSummaryValue,
            float summaryValue,
            string fallback,
            string suffix)
        {
            if (hasSessionValue)
            {
                return $"{sessionValue:0.#}{suffix}";
            }

            if (hasSummaryValue)
            {
                return $"{summaryValue:0.#}{suffix}";
            }

            return string.IsNullOrWhiteSpace(fallback) ? "--" : fallback;
        }

        private static string GetCategorySummary(FeedbackReport report, string category)
        {
            if (report == null || report.items == null)
            {
                return "Analysis pending.";
            }

            FeedbackItem fallback = null;
            for (int index = 0; index < report.items.Count; index++)
            {
                FeedbackItem item = report.items[index];
                if (item == null || item.category != category)
                {
                    continue;
                }

                if (item.severity != FeedbackItem.Severity.Strength)
                {
                    return item.message;
                }

                fallback ??= item;
            }

            return fallback != null ? fallback.message : "Analysis complete.";
        }

        private static string BuildCoachNotes(FeedbackReport report)
        {
            StringBuilder builder = new StringBuilder();
            AppendList(builder, "Strengths", report.strengths);
            AppendList(builder, "Improve", report.improvements);

            if (builder.Length == 0 && report.items != null)
            {
                for (int index = 0; index < Mathf.Min(4, report.items.Count); index++)
                {
                    builder.Append("- ");
                    builder.AppendLine(report.items[index].message);
                }
            }

            return builder.Length == 0 ? "No detailed notes yet." : builder.ToString().TrimEnd();
        }

        private static string BuildQaNotesOrFallback(PresentationQaResult qaResult, string fallback)
        {
            string qaNotes = BuildQaNotes(qaResult);
            return string.IsNullOrWhiteSpace(qaNotes) ? fallback : qaNotes;
        }

        private static string AppendQaNotes(string existingNotes, PresentationQaResult qaResult)
        {
            string qaNotes = BuildQaNotes(qaResult);
            if (string.IsNullOrWhiteSpace(qaNotes))
            {
                return existingNotes;
            }

            if (string.IsNullOrWhiteSpace(existingNotes))
            {
                return qaNotes;
            }

            return $"{existingNotes}\n\n{qaNotes}";
        }

        private static string BuildQaNotes(PresentationQaResult qaResult)
        {
            if (qaResult == null || !qaResult.HasAnswers)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Q&A Feedback");
            if (!string.IsNullOrWhiteSpace(qaResult.summary))
            {
                builder.AppendLine(qaResult.summary);
            }

            for (int index = 0; index < Mathf.Min(3, qaResult.answers.Count); index++)
            {
                PresentationQaAnswer answer = qaResult.answers[index];
                if (answer == null)
                {
                    continue;
                }

                builder.Append("- ");
                if (answer.feedback != null && answer.feedback.status == "Evaluated")
                {
                    builder.Append($"A:{answer.feedback.accuracy:0} C:{answer.feedback.coverage:0} Cl:{answer.feedback.clarity:0} - ");
                    builder.AppendLine(CompactQaText(answer.feedback.summary, 96));
                }
                else if (answer.feedback != null)
                {
                    builder.AppendLine(CompactQaText(answer.feedback.summary, 110));
                }
                else
                {
                    builder.AppendLine(answer.skipped ? "Skipped." : CompactQaText(answer.answerTranscript, 110));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string CompactQaText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : trimmed.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
        }

        private static void AppendList(StringBuilder builder, string title, List<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            builder.AppendLine(title);
            for (int index = 0; index < Mathf.Min(4, items.Count); index++)
            {
                builder.Append("- ");
                builder.AppendLine(items[index]);
            }
            builder.AppendLine();
        }

        private static string BuildFocusText(string strongest, string weakest)
        {
            bool hasStrongest = !string.IsNullOrWhiteSpace(strongest);
            bool hasWeakest = !string.IsNullOrWhiteSpace(weakest);

            if (!hasStrongest && !hasWeakest)
            {
                return "Complete a practice session to populate focus guidance.";
            }

            if (!hasStrongest)
            {
                return $"Focus: Improve {weakest}";
            }

            if (!hasWeakest)
            {
                return $"Focus: Keep building {strongest}";
            }

            return $"Strongest: {strongest}\nImprove: {weakest}";
        }

        private static string ResolveBandText(float score, string band)
        {
            if (!string.IsNullOrWhiteSpace(band))
            {
                return band;
            }

            if (score >= 90f) return "Excellent";
            if (score >= 75f) return "Good";
            if (score >= 60f) return "Needs Improvement";
            return "Weak Performance";
        }

        private static string FormatDuration(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static void SetMetricRow(List<MetricRow> rows, int index, string label, float value, bool hasValue)
        {
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            MetricRow row = rows[index];
            row.NameLabel.text = label;
            row.ValueLabel.text = hasValue ? value.ToString("0") : "--";
            row.FillImage.fillAmount = hasValue ? Mathf.Clamp01(value / 100f) : 0f;
        }

        private void SetDetail(int index, string value)
        {
            if (index >= 0 && index < detailValueLabels.Count)
            {
                detailValueLabels[index].text = string.IsNullOrWhiteSpace(value) ? "--" : value;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private GameObject CreatePage(string name, RectTransform parent)
        {
            GameObject page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            Stretch(page.GetComponent<RectTransform>());
            tabPages.Add(page);
            return page;
        }

        private GameObject CreateCard(string name, Transform parent, Vector2 size, float flexibleWidth = 0f)
        {
            GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            card.transform.SetParent(parent, false);
            Image image = card.GetComponent<Image>();
            image.color = panelColor;
            image.raycastTarget = false;
            AddOutline(card, accentSoftColor, new Vector2(1f, -1f));

            LayoutElement layout = card.GetComponent<LayoutElement>();
            if (size.x > 0f)
            {
                layout.minWidth = size.x;
                layout.preferredWidth = size.x;
            }

            if (size.y > 0f)
            {
                layout.minHeight = size.y;
                layout.preferredHeight = size.y;
            }

            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = size.y <= 0f ? 1f : 0f;
            return card;
        }

        private static GameObject CreateLayoutObject(string name, Transform parent, Vector2 size, float flexibleWidth = 0f, float flexibleHeight = 0f)
        {
            GameObject layoutObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            layoutObject.transform.SetParent(parent, false);

            LayoutElement layout = layoutObject.GetComponent<LayoutElement>();
            if (size.x > 0f)
            {
                layout.minWidth = size.x;
                layout.preferredWidth = size.x;
            }

            if (size.y > 0f)
            {
                layout.minHeight = size.y;
                layout.preferredHeight = size.y;
            }

            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = flexibleHeight;
            return layoutObject;
        }

        private void CreateSectionHeader(Transform parent, string text)
        {
            CreateText(parent, "SectionHeader", text, 25f, FontStyles.Bold, accentColor, TextAlignmentOptions.Left, new Vector2(1000f, 34f));
        }

        private TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            if (size.x > 0f)
            {
                layout.preferredWidth = size.x;
            }

            if (size.y > 0f)
            {
                layout.preferredHeight = size.y;
            }

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
        }
    }
}
