using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VRPublicSpeaking.AppShell.UI
{
    public static class VrUiUsabilityUtility
    {
        private static readonly HashSet<int> ConfiguredEventSystems = new HashSet<int>();

        public static void EnsureCanvasInputSupport(GameObject root, Canvas canvas = null)
        {
            if (root == null)
            {
                return;
            }

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            }

            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }

            Type trackedRaycasterType =
                Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedRaycasterType != null && root.GetComponent(trackedRaycasterType) == null)
            {
                root.AddComponent(trackedRaycasterType);
            }

            EnsureEventSystemInput();
        }

        public static void ApplyReadablePanel(
            AppPanelView panel,
            float minimumFontSize,
            float minimumButtonHeight,
            Vector2 minimumPanelSize)
        {
            if (panel == null)
            {
                return;
            }

            RectTransform panelRect = panel.transform as RectTransform;
            if (panelRect != null && minimumPanelSize.sqrMagnitude > 0f)
            {
                panelRect.sizeDelta = new Vector2(
                    Mathf.Max(panelRect.sizeDelta.x, minimumPanelSize.x),
                    Mathf.Max(panelRect.sizeDelta.y, minimumPanelSize.y));
            }

            TMP_Text[] textElements = panel.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < textElements.Length; index++)
            {
                TMP_Text textElement = textElements[index];
                if (textElement == null)
                {
                    continue;
                }

                bool isButtonLabel = textElement.GetComponentInParent<Button>() != null;
                textElement.fontSize = Mathf.Max(textElement.fontSize, minimumFontSize);
                textElement.textWrappingMode = isButtonLabel
                    ? TextWrappingModes.NoWrap
                    : TextWrappingModes.Normal;
                textElement.overflowMode = TextOverflowModes.Ellipsis;
                textElement.raycastTarget = false;
            }

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null)
                {
                    continue;
                }

                ApplyReadableButton(button, minimumButtonHeight);
            }
        }

        public static void ApplyReadableButton(Button button, float minimumButtonHeight)
        {
            if (button == null)
            {
                return;
            }

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
                button.targetGraphic = buttonImage;
            }

            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = Mathf.Max(layoutElement.minHeight, minimumButtonHeight);
            layoutElement.preferredHeight = Mathf.Max(layoutElement.preferredHeight, minimumButtonHeight);

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            Graphic[] childGraphics = button.GetComponentsInChildren<Graphic>(true);
            for (int index = 0; index < childGraphics.Length; index++)
            {
                Graphic graphic = childGraphics[index];
                if (graphic != null && graphic.gameObject != button.gameObject)
                {
                    graphic.raycastTarget = false;
                }
            }

            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                TMP_Text label = labels[index];
                if (label == null)
                {
                    continue;
                }

                float cappedSize = Mathf.Clamp(
                    label.fontSize,
                    Mathf.Max(13f, minimumButtonHeight * 0.18f),
                    Mathf.Max(18f, minimumButtonHeight * 0.32f));
                label.fontSize = cappedSize;
                label.enableAutoSizing = true;
                label.fontSizeMin = Mathf.Min(Mathf.Max(12f, minimumButtonHeight * 0.16f), cappedSize);
                label.fontSizeMax = cappedSize;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;
            }
        }

        private static void EnsureEventSystemInput()
        {
            EventSystem eventSystem = EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            Type inputSystemModuleType =
                Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType == null)
            {
                if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                }

                return;
            }

            Component inputModule = eventSystem.GetComponent(inputSystemModuleType);
            bool createdInputModule = false;
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent(inputSystemModuleType);
                createdInputModule = true;
            }

            int eventSystemId = eventSystem.GetInstanceID();
            if (createdInputModule || ConfiguredEventSystems.Add(eventSystemId))
            {
                inputSystemModuleType.GetMethod("AssignDefaultActions", Type.EmptyTypes)?.Invoke(inputModule, null);
            }

            StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
            {
                UnityEngine.Object.Destroy(standaloneModule);
            }
        }
    }
}
