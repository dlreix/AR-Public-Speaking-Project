using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.UI;
using UiDefaultControls = UnityEngine.UI.DefaultControls;
using LegacyText = UnityEngine.UI.Text;

namespace VRPublicSpeaking.AppShell.Editor
{
    internal static class AppShellEditorUi
    {
        internal static AppPanelView CreatePanelRoot(
            Transform parent,
            string name,
            AppPanelType panelType,
            Vector2 size,
            Vector2 anchoredPosition,
            bool centerAnchored = true)
        {
            GameObject panelRoot = AppShellEditorCommon.FindOrCreateChild(parent, name);
            RectTransform rectTransform = panelRoot.GetComponent<RectTransform>();

            if (centerAnchored)
            {
                AppShellEditorCommon.ConfigureRect(
                    rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    size,
                    anchoredPosition);
            }

            Image background = AppShellEditorCommon.GetOrAddComponent<Image>(panelRoot);
            AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.PanelColor);

            CanvasGroup canvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(panelRoot);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(panelRoot);
            layout.spacing = 18f;
            layout.padding = new RectOffset(40, 40, 34, 34);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(panelRoot);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            AppPanelView panelView = AppShellEditorCommon.GetOrAddComponent<AppPanelView>(panelRoot);
            AppShellEditorCommon.SetField(panelView, "panelType", panelType);
            return panelView;
        }

        internal static void ClearGeneratedChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        internal static TMP_Text CreateTextBlock(
            Transform parent,
            string name,
            string value,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            float preferredHeight,
            Color color)
        {
            GameObject labelObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            TextMeshProUGUI text = AppShellEditorCommon.GetOrAddComponent<TextMeshProUGUI>(labelObject);
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;

            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(labelObject);
            layout.preferredHeight = preferredHeight;

            return text;
        }

        internal static Button CreateButton(Transform parent, string name, string label, Color backgroundColor, float width = -1f, float height = 68f)
        {
            GameObject buttonObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            if (buttonObject.GetComponent<Button>() == null)
            {
                GameObject generated = TMP_DefaultControls.CreateButton(AppShellEditorCommon.CreateTmpResources());
                buttonObject = ReplaceUiObject(buttonObject, generated);
            }

            Button button = AppShellEditorCommon.GetOrAddComponent<Button>(buttonObject);
            Image background = buttonObject.GetComponent<Image>();
            if (background != null)
            {
                AppShellEditorCommon.StyleSlicedImage(background, backgroundColor);
            }

            TextMeshProUGUI labelText = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.color = AppShellEditorCommon.TextColor;
                labelText.fontSize = 22f;
                labelText.fontStyle = FontStyles.Bold;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.enableAutoSizing = false;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(buttonObject);
            layout.preferredHeight = height;
            if (width > 0f)
            {
                layout.preferredWidth = width;
            }

            return button;
        }

        internal static Slider CreateSlider(Transform parent, string name, float min, float max, bool wholeNumbers, float value)
        {
            GameObject sliderObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            if (sliderObject.GetComponent<Slider>() == null)
            {
                GameObject generated = UiDefaultControls.CreateSlider(AppShellEditorCommon.CreateUiResources());
                sliderObject = ReplaceUiObject(sliderObject, generated);
            }

            Slider slider = AppShellEditorCommon.GetOrAddComponent<Slider>(sliderObject);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);

            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(sliderObject);
            layout.preferredHeight = 40f;
            return slider;
        }

        internal static TMP_Dropdown CreateDropdown(Transform parent, string name, IReadOnlyList<string> options)
        {
            GameObject dropdownObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            if (dropdownObject.GetComponent<TMP_Dropdown>() == null)
            {
                GameObject generated = TMP_DefaultControls.CreateDropdown(AppShellEditorCommon.CreateTmpResources());
                dropdownObject = ReplaceUiObject(dropdownObject, generated);
            }

            TMP_Dropdown dropdown = AppShellEditorCommon.GetOrAddComponent<TMP_Dropdown>(dropdownObject);
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(0);

            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(dropdownObject);
            layout.preferredHeight = 56f;

            Image background = dropdownObject.GetComponent<Image>();
            if (background != null)
            {
                AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.TileSurfaceColor);
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = AppShellEditorCommon.TextColor;
                dropdown.captionText.fontSize = 20f;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.color = AppShellEditorCommon.TextColor;
                dropdown.itemText.fontSize = 18f;
            }

            return dropdown;
        }

        internal static Toggle CreateToggle(Transform parent, string name, string label, bool defaultValue)
        {
            GameObject toggleObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            if (toggleObject.GetComponent<Toggle>() == null)
            {
                GameObject generated = UiDefaultControls.CreateToggle(AppShellEditorCommon.CreateUiResources());
                toggleObject = ReplaceUiObject(toggleObject, generated);
            }

            Toggle toggle = AppShellEditorCommon.GetOrAddComponent<Toggle>(toggleObject);
            toggle.SetIsOnWithoutNotify(defaultValue);

            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(toggleObject);
            layout.preferredHeight = 42f;

            LegacyText legacyLabel = toggleObject.GetComponentInChildren<LegacyText>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = label;
                legacyLabel.color = AppShellEditorCommon.TextColor;
                legacyLabel.fontSize = 18;
            }

            Image background = toggleObject.GetComponent<Image>();
            if (background != null)
            {
                AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.TileSurfaceColor);
            }

            return toggle;
        }

        internal static GameObject CreateSpacer(Transform parent, string name, float height)
        {
            GameObject spacer = AppShellEditorCommon.FindOrCreateChild(parent, name);
            LayoutElement layout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(spacer);
            layout.preferredHeight = height;
            return spacer;
        }

        internal static GameObject CreateVerticalContainer(Transform parent, string name, float spacing)
        {
            GameObject container = AppShellEditorCommon.FindOrCreateChild(parent, name);
            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(container);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(container);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return container;
        }

        internal static GameObject CreateHorizontalContainer(Transform parent, string name, float spacing)
        {
            GameObject container = AppShellEditorCommon.FindOrCreateChild(parent, name);
            HorizontalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<HorizontalLayoutGroup>(container);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(container);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return container;
        }

        internal static GameObject CreateDashboardRow(Transform parent, string name, float spacing)
        {
            GameObject container = AppShellEditorCommon.FindOrCreateChild(parent, name);
            HorizontalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<HorizontalLayoutGroup>(container);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(container);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return container;
        }

        internal static GameObject CreateSectionCard(
            Transform parent,
            string name,
            Color backgroundColor,
            int paddingHorizontal = 20,
            int paddingVertical = 18,
            float spacing = 12f)
        {
            GameObject card = AppShellEditorCommon.FindOrCreateChild(parent, name);
            Image background = AppShellEditorCommon.GetOrAddComponent<Image>(card);
            AppShellEditorCommon.StyleSlicedImage(background, backgroundColor);

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(card);
            layout.padding = new RectOffset(paddingHorizontal, paddingHorizontal, paddingVertical, paddingVertical);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(card);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return card;
        }

        internal static GameObject CreateFeatureCard(
            Transform parent,
            string name,
            string title,
            string description,
            string badge,
            Sprite previewSprite,
            float preferredWidth,
            float preferredHeight)
        {
            GameObject card = CreateSectionCard(parent, name, AppShellEditorCommon.ElevatedSurfaceColor, 22, 22, 12f);
            AppShellEditorCommon.ConfigureLayoutElement(card, preferredWidth, preferredHeight);

            if (!string.IsNullOrWhiteSpace(badge))
            {
                CreateTextBlock(card.transform, "BadgeLabel", badge, 15f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            }

            GameObject previewRoot = AppShellEditorCommon.FindOrCreateChild(card.transform, "Preview");
            Image previewImage = AppShellEditorCommon.GetOrAddComponent<Image>(previewRoot);
            AppShellEditorCommon.StyleSlicedImage(previewImage, new Color(0.17f, 0.22f, 0.29f, 1f));
            if (previewSprite != null)
            {
                previewImage.sprite = previewSprite;
                previewImage.preserveAspect = true;
            }
            else
            {
                previewImage.preserveAspect = false;
            }
            AppShellEditorCommon.ConfigureLayoutElement(previewRoot, -1f, 108f);

            CreateTextBlock(card.transform, "TitleLabel", title, 26f, FontStyles.Bold, TextAlignmentOptions.Left, 36f, AppShellEditorCommon.TextColor);
            CreateTextBlock(card.transform, "DescriptionLabel", description, 17f, FontStyles.Normal, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);

            return card;
        }

        internal static Button CreateUtilityTile(
            Transform parent,
            string name,
            string title,
            string subtitle,
            Color backgroundColor,
            float preferredWidth,
            float preferredHeight = 128f)
        {
            GameObject tileObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            if (tileObject.GetComponent<Button>() == null)
            {
                GameObject generated = TMP_DefaultControls.CreateButton(AppShellEditorCommon.CreateTmpResources());
                tileObject = ReplaceUiObject(tileObject, generated);
            }

            Button button = AppShellEditorCommon.GetOrAddComponent<Button>(tileObject);
            Image background = tileObject.GetComponent<Image>();
            if (background != null)
            {
                AppShellEditorCommon.StyleSlicedImage(background, backgroundColor);
            }

            HorizontalLayoutGroup existingHorizontal = tileObject.GetComponent<HorizontalLayoutGroup>();
            if (existingHorizontal != null)
            {
                UnityEngine.Object.DestroyImmediate(existingHorizontal);
            }

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(tileObject);
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            for (int index = tileObject.transform.childCount - 1; index >= 0; index--)
            {
                Transform child = tileObject.transform.GetChild(index);
                if (child.name != "TitleLabel" && child.name != "SubtitleLabel")
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            TMP_Text titleLabel = CreateTextBlock(tileObject.transform, "TitleLabel", title, 24f, FontStyles.Bold, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.TextColor);
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text subtitleLabel = CreateTextBlock(tileObject.transform, "SubtitleLabel", subtitle, 17f, FontStyles.Normal, TextAlignmentOptions.Left, 60f, AppShellEditorCommon.MutedTextColor);
            subtitleLabel.textWrappingMode = TextWrappingModes.Normal;
            subtitleLabel.overflowMode = TextOverflowModes.Ellipsis;

            AppShellEditorCommon.ConfigureLayoutElement(tileObject, preferredWidth, preferredHeight);
            return button;
        }

        internal static GameObject CreateSummaryStrip(
            Transform parent,
            string name,
            string title,
            string value)
        {
            GameObject strip = CreateSectionCard(parent, name, AppShellEditorCommon.TileSurfaceColor, 18, 16, 6f);
            CreateTextBlock(strip.transform, "StripTitle", title, 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            CreateTextBlock(strip.transform, "StripValue", value, 19f, FontStyles.Normal, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.TextColor);
            return strip;
        }

        internal static TMP_Text FindOrCreateHudLabel(Transform parent, string name, string value, float fontSize, Vector2 anchoredPosition)
        {
            GameObject labelObject = AppShellEditorCommon.FindOrCreateChild(parent, name);
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
            AppShellEditorCommon.ConfigureRect(
                rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(420f, 60f),
                anchoredPosition);

            TextMeshProUGUI text = AppShellEditorCommon.GetOrAddComponent<TextMeshProUGUI>(labelObject);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = AppShellEditorCommon.TextColor;
            return text;
        }

        internal static EnvironmentCardView CreateEnvironmentCard(Transform parent, string name)
        {
            GameObject root = AppShellEditorCommon.FindOrCreateChild(parent, name);
            Image background = AppShellEditorCommon.GetOrAddComponent<Image>(root);
            AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.ElevatedSurfaceColor);

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            EnvironmentCardView cardView = AppShellEditorCommon.GetOrAddComponent<EnvironmentCardView>(root);

            Image selectionHighlight = AppShellEditorCommon.GetOrAddComponent<Image>(AppShellEditorCommon.FindOrCreateChild(root.transform, "SelectionHighlight"));
            selectionHighlight.color = new Color(0.18f, 0.55f, 0.84f, 0.20f);
            selectionHighlight.raycastTarget = false;
            AppShellEditorCommon.ConfigureStretchRect(selectionHighlight.rectTransform);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(selectionHighlight.gameObject).ignoreLayout = true;
            selectionHighlight.gameObject.SetActive(false);
            selectionHighlight.transform.SetAsFirstSibling();

            Image previewImage = AppShellEditorCommon.GetOrAddComponent<Image>(AppShellEditorCommon.FindOrCreateChild(root.transform, "PreviewImage"));
            LayoutElement previewLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(previewImage.gameObject);
            previewLayout.preferredHeight = 96f;
            AppShellEditorCommon.StyleSlicedImage(previewImage, new Color(0.16f, 0.20f, 0.26f, 1f), false);
            previewImage.preserveAspect = true;

            TMP_Text titleLabel = CreateTextBlock(root.transform, "TitleLabel", "Environment", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 28f, AppShellEditorCommon.TextColor);
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text descriptionLabel = CreateTextBlock(root.transform, "DescriptionLabel", "Short environment description.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text stateLabel = CreateTextBlock(root.transform, "StateLabel", "Select", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            Button selectButton = CreateButton(root.transform, "SelectButton", "Select", AppShellEditorCommon.AccentColor, 160f, 44f);

            GameObject unavailableBadge = AppShellEditorCommon.FindOrCreateChild(root.transform, "UnavailableBadge");
            TMP_Text badgeLabel = AppShellEditorCommon.GetOrAddComponent<TextMeshProUGUI>(unavailableBadge);
            badgeLabel.text = "Unavailable";
            badgeLabel.fontSize = 16f;
            badgeLabel.alignment = TextAlignmentOptions.Center;
            badgeLabel.color = AppShellEditorCommon.TextColor;
            AppShellEditorCommon.ConfigureRect(
                unavailableBadge.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(120f, 28f),
                new Vector2(-84f, -18f));
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(unavailableBadge).ignoreLayout = true;
            unavailableBadge.SetActive(false);

            AppShellEditorCommon.SetField(cardView, "titleLabel", titleLabel);
            AppShellEditorCommon.SetField(cardView, "descriptionLabel", descriptionLabel);
            AppShellEditorCommon.SetField(cardView, "previewImage", previewImage);
            AppShellEditorCommon.SetField(cardView, "selectButton", selectButton);
            AppShellEditorCommon.SetField(cardView, "selectionHighlight", selectionHighlight.gameObject);
            AppShellEditorCommon.SetField(cardView, "unavailableBadge", unavailableBadge);
            AppShellEditorCommon.SetField(cardView, "stateLabel", stateLabel);

            return cardView;
        }

        private static GameObject ReplaceUiObject(GameObject target, GameObject generated)
        {
            generated.transform.SetParent(target.transform.parent, false);
            generated.name = target.name;
            generated.transform.SetSiblingIndex(target.transform.GetSiblingIndex());
            UnityEngine.Object.DestroyImmediate(target);
            return generated;
        }
    }
}
