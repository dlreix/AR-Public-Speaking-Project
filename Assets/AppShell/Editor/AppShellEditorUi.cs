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
        internal enum ButtonTone
        {
            Primary,
            Secondary,
            Utility,
            Danger
        }

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
            AppShellEditorCommon.ApplyOutline(panelRoot, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

            CanvasGroup canvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(panelRoot);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(panelRoot);
            layout.spacing = 20f;
            layout.padding = new RectOffset(42, 42, 36, 36);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(panelRoot);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            GameObject topGlow = AppShellEditorCommon.FindOrCreateChild(panelRoot.transform, "TopGlow");
            Image glowImage = AppShellEditorCommon.GetOrAddComponent<Image>(topGlow);
            AppShellEditorCommon.StyleSlicedImage(glowImage, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.16f), false);
            AppShellEditorCommon.ConfigureRect(
                topGlow.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 14f),
                new Vector2(0f, -6f));
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(topGlow).ignoreLayout = true;
            topGlow.transform.SetAsFirstSibling();

            AppPanelView panelView = AppShellEditorCommon.GetOrAddComponent<AppPanelView>(panelRoot);
            AppShellEditorCommon.SetField(panelView, "panelType", panelType);
            return panelView;
        }

        internal static void ClearGeneratedChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name == "TopGlow")
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
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
            button.targetGraphic = background;

            GameObject accentBar = AppShellEditorCommon.FindOrCreateChild(buttonObject.transform, "AccentBar");
            Image accentImage = AppShellEditorCommon.GetOrAddComponent<Image>(accentBar);
            AppShellEditorCommon.StyleSlicedImage(accentImage, AppShellEditorCommon.WithAlpha(Color.Lerp(backgroundColor, Color.white, 0.28f), 0.92f), false);
            AppShellEditorCommon.ConfigureRect(
                accentBar.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 6f),
                new Vector2(0f, -3f));
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(accentBar).ignoreLayout = true;
            accentBar.transform.SetAsFirstSibling();

            AppShellEditorCommon.ApplyOutline(
                buttonObject,
                AppShellEditorCommon.WithAlpha(Color.Lerp(backgroundColor, Color.white, 0.18f), 0.65f),
                new Vector2(1f, -1f));

            TextMeshProUGUI labelText = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.color = AppShellEditorCommon.GetContrastingTextColor(backgroundColor);
                labelText.fontSize = height >= 62f ? 23f : 20f;
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

        internal static Button CreateStyledButton(Transform parent, string name, string label, ButtonTone tone, float width = -1f, float height = 68f)
        {
            return CreateButton(parent, name, label, ResolveButtonColor(tone), width, height);
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
            AppShellEditorCommon.ApplyOutline(dropdownObject, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

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
            layout.preferredHeight = 44f;

            LegacyText legacyLabel = toggleObject.GetComponentInChildren<LegacyText>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = label;
                legacyLabel.color = AppShellEditorCommon.TextColor;
                legacyLabel.fontSize = 18;
                legacyLabel.alignment = TextAnchor.MiddleLeft;
            }

            Transform labelRoot = toggleObject.transform.Find("Label");
            if (labelRoot != null)
            {
                RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 1f);
                    labelRect.offsetMin = new Vector2(40f, 0f);
                    labelRect.offsetMax = Vector2.zero;
                }
            }

            Image background = toggleObject.GetComponent<Image>();
            if (background != null)
            {
                AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.UtilitySurfaceColor);
            }

            Transform backgroundRoot = toggleObject.transform.Find("Background");
            if (backgroundRoot != null)
            {
                RectTransform backgroundRect = backgroundRoot.GetComponent<RectTransform>();
                if (backgroundRect != null)
                {
                    backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                    backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                    backgroundRect.sizeDelta = new Vector2(20f, 20f);
                    backgroundRect.anchoredPosition = new Vector2(10f, 0f);
                }

                Image toggleBackground = backgroundRoot.GetComponent<Image>();
                if (toggleBackground != null)
                {
                    AppShellEditorCommon.StyleSlicedImage(toggleBackground, AppShellEditorCommon.UtilitySurfaceColor, false);
                }

                Transform checkmarkRoot = backgroundRoot.Find("Checkmark");
                if (checkmarkRoot != null)
                {
                    Image checkmark = checkmarkRoot.GetComponent<Image>();
                    if (checkmark != null)
                    {
                        checkmark.color = AppShellEditorCommon.AccentColor;
                    }
                }
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
            layout.childControlHeight = true;
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
            layout.childControlHeight = true;
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
            float spacing = 12f,
            Color? accentColor = null)
        {
            GameObject card = AppShellEditorCommon.FindOrCreateChild(parent, name);
            Image background = AppShellEditorCommon.GetOrAddComponent<Image>(card);
            AppShellEditorCommon.StyleSlicedImage(background, backgroundColor);
            AppShellEditorCommon.ApplyOutline(card, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

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

            GameObject accentBar = AppShellEditorCommon.FindOrCreateChild(card.transform, "AccentBar");
            Image accentImage = AppShellEditorCommon.GetOrAddComponent<Image>(accentBar);
            Color resolvedAccent = accentColor ?? Color.Lerp(backgroundColor, AppShellEditorCommon.AccentColor, 0.45f);
            AppShellEditorCommon.StyleSlicedImage(accentImage, AppShellEditorCommon.WithAlpha(resolvedAccent, 0.92f), false);
            AppShellEditorCommon.ConfigureRect(
                accentBar.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 8f),
                new Vector2(0f, -4f));
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(accentBar).ignoreLayout = true;
            accentBar.transform.SetAsFirstSibling();

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
            float preferredHeight,
            float previewHeight = 148f,
            float descriptionHeight = 72f,
            Color? backgroundColor = null,
            Color? accentColor = null)
        {
            GameObject card = CreateSectionCard(
                parent,
                name,
                backgroundColor ?? AppShellEditorCommon.HeroSurfaceColor,
                24,
                24,
                12f,
                accentColor ?? AppShellEditorCommon.HeroAccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(card, preferredWidth, preferredHeight);

            if (!string.IsNullOrWhiteSpace(badge))
            {
                CreateTextBlock(card.transform, "BadgeLabel", badge.ToUpperInvariant(), 15f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.HeroAccentColor);
            }

            GameObject previewRoot = AppShellEditorCommon.FindOrCreateChild(card.transform, "Preview");
            Image previewImage = AppShellEditorCommon.GetOrAddComponent<Image>(previewRoot);
            AppShellEditorCommon.StyleSlicedImage(previewImage, AppShellEditorCommon.PreviewSurfaceColor);
            AppShellEditorCommon.ApplyOutline(previewRoot, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.HeroAccentColor, 0.28f), new Vector2(1f, -1f));
            if (previewSprite != null)
            {
                previewImage.sprite = previewSprite;
                previewImage.type = Image.Type.Simple;
                previewImage.preserveAspect = true;
                previewImage.color = Color.white;
            }
            else
            {
                previewImage.type = Image.Type.Sliced;
                previewImage.preserveAspect = false;
            }
            AppShellEditorCommon.ConfigureLayoutElement(previewRoot, -1f, previewHeight);

            CreateTextBlock(card.transform, "TitleLabel", title, 30f, FontStyles.Bold, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.TextColor);
            TMP_Text descriptionLabel = CreateTextBlock(card.transform, "DescriptionLabel", description, 17f, FontStyles.Normal, TextAlignmentOptions.Left, descriptionHeight, AppShellEditorCommon.MutedTextColor);
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;

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
            AppShellEditorCommon.ApplyOutline(tileObject, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

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
                if (child.name != "KickerLabel" && child.name != "TitleLabel" && child.name != "SubtitleLabel")
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            CreateTextBlock(tileObject.transform, "KickerLabel", "SHORTCUT", 13f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            TMP_Text titleLabel = CreateTextBlock(tileObject.transform, "TitleLabel", title, 24f, FontStyles.Bold, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.TextColor);
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text subtitleLabel = CreateTextBlock(tileObject.transform, "SubtitleLabel", subtitle, 16f, FontStyles.Normal, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.MutedTextColor);
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
            GameObject strip = CreateSectionCard(parent, name, AppShellEditorCommon.UtilitySurfaceColor, 18, 16, 6f, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.72f));
            CreateTextBlock(strip.transform, "StripTitle", title.ToUpperInvariant(), 15f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
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
            AppShellEditorCommon.ApplyOutline(root, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            EnvironmentCardView cardView = AppShellEditorCommon.GetOrAddComponent<EnvironmentCardView>(root);

            Image selectionHighlight = AppShellEditorCommon.GetOrAddComponent<Image>(AppShellEditorCommon.FindOrCreateChild(root.transform, "SelectionHighlight"));
            selectionHighlight.color = AppShellEditorCommon.WithAlpha(AppShellEditorCommon.SelectedAccentColor, 0.20f);
            selectionHighlight.raycastTarget = false;
            AppShellEditorCommon.ConfigureStretchRect(selectionHighlight.rectTransform);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(selectionHighlight.gameObject).ignoreLayout = true;
            selectionHighlight.gameObject.SetActive(false);
            selectionHighlight.transform.SetAsFirstSibling();

            Image previewImage = AppShellEditorCommon.GetOrAddComponent<Image>(AppShellEditorCommon.FindOrCreateChild(root.transform, "PreviewImage"));
            LayoutElement previewLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(previewImage.gameObject);
            previewLayout.preferredHeight = 88f;
            AppShellEditorCommon.StyleSlicedImage(previewImage, AppShellEditorCommon.PreviewSurfaceColor, false);
            AppShellEditorCommon.ApplyOutline(previewImage.gameObject, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.24f), new Vector2(1f, -1f));
            previewImage.preserveAspect = true;

            TMP_Text titleLabel = CreateTextBlock(root.transform, "TitleLabel", "Environment", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 28f, AppShellEditorCommon.TextColor);
            titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text descriptionLabel = CreateTextBlock(root.transform, "DescriptionLabel", "Short environment description.", 14f, FontStyles.Normal, TextAlignmentOptions.Left, 44f, AppShellEditorCommon.MutedTextColor);
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;

            GameObject metaRow = CreateHorizontalContainer(root.transform, "MetaRow", 10f);
            TMP_Text stateLabel = CreateTextBlock(metaRow.transform, "StateLabel", "Ready", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(stateLabel.gameObject).preferredWidth = 180f;

            Button selectButton = CreateStyledButton(root.transform, "SelectButton", "Select Room", ButtonTone.Primary, -1f, 44f);
            TMP_Text buttonLabel = selectButton.GetComponentInChildren<TMP_Text>(true);

            GameObject unavailableBadge = AppShellEditorCommon.FindOrCreateChild(root.transform, "UnavailableBadge");
            Image badgeBackground = AppShellEditorCommon.GetOrAddComponent<Image>(unavailableBadge);
            AppShellEditorCommon.StyleSlicedImage(badgeBackground, AppShellEditorCommon.BadgeSurfaceColor, false);
            AppShellEditorCommon.ApplyOutline(unavailableBadge, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.34f), new Vector2(1f, -1f));
            TMP_Text badgeLabel = AppShellEditorCommon.GetOrAddComponent<TextMeshProUGUI>(AppShellEditorCommon.FindOrCreateChild(unavailableBadge.transform, "BadgeLabel"));
            badgeLabel.text = "Unavailable";
            badgeLabel.fontSize = 14f;
            badgeLabel.fontStyle = FontStyles.Bold;
            badgeLabel.alignment = TextAlignmentOptions.Center;
            badgeLabel.color = AppShellEditorCommon.TextColor;
            AppShellEditorCommon.ConfigureStretchRect(badgeLabel.rectTransform);
            AppShellEditorCommon.ConfigureRect(
                unavailableBadge.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(138f, 32f),
                new Vector2(-94f, -22f));
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(unavailableBadge).ignoreLayout = true;
            unavailableBadge.SetActive(false);

            AppShellEditorCommon.SetField(cardView, "titleLabel", titleLabel);
            AppShellEditorCommon.SetField(cardView, "descriptionLabel", descriptionLabel);
            AppShellEditorCommon.SetField(cardView, "previewImage", previewImage);
            AppShellEditorCommon.SetField(cardView, "selectButton", selectButton);
            AppShellEditorCommon.SetField(cardView, "selectionHighlight", selectionHighlight.gameObject);
            AppShellEditorCommon.SetField(cardView, "unavailableBadge", unavailableBadge);
            AppShellEditorCommon.SetField(cardView, "badgeLabel", badgeLabel);
            AppShellEditorCommon.SetField(cardView, "badgeBackground", badgeBackground);
            AppShellEditorCommon.SetField(cardView, "stateLabel", stateLabel);
            AppShellEditorCommon.SetField(cardView, "cardBackground", background);
            AppShellEditorCommon.SetField(cardView, "buttonLabel", buttonLabel);

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

        private static Color ResolveButtonColor(ButtonTone tone)
        {
            switch (tone)
            {
                case ButtonTone.Primary:
                    return AppShellEditorCommon.AccentColor;

                case ButtonTone.Utility:
                    return AppShellEditorCommon.UtilitySurfaceColor;

                case ButtonTone.Danger:
                    return AppShellEditorCommon.DangerColor;

                default:
                    return AppShellEditorCommon.SecondaryColor;
            }
        }
    }
}
