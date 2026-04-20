using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Editor
{
    internal static class AppShellEditorCommon
    {
        internal const string MainHubScenePath = "Assets/Scenes/MainHubScene.unity";
        internal const string ResultsScenePath = "Assets/Scenes/ResultsScene.unity";
        internal const string EnvironmentCatalogPath = "Assets/AppShell/Config/DefaultEnvironmentCatalog.asset";

        internal static readonly Color PanelColor = new Color(0.09f, 0.12f, 0.16f, 0.92f);
        internal static readonly Color AccentColor = new Color(0.18f, 0.55f, 0.84f, 1f);
        internal static readonly Color SecondaryColor = new Color(0.20f, 0.24f, 0.30f, 0.96f);
        internal static readonly Color TextColor = new Color(0.95f, 0.97f, 0.99f, 1f);
        internal static readonly Color MutedTextColor = new Color(0.74f, 0.80f, 0.86f, 1f);
        internal static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.94f);
        internal static readonly Color HeaderSurfaceColor = new Color(0.39f, 0.49f, 0.59f, 0.96f);
        internal static readonly Color ElevatedSurfaceColor = new Color(0.12f, 0.15f, 0.20f, 0.98f);
        internal static readonly Color TileSurfaceColor = new Color(0.18f, 0.24f, 0.31f, 0.98f);
        internal static readonly Color SoftAccentColor = new Color(0.67f, 0.79f, 0.91f, 1f);
        internal static readonly Color BorderColor = new Color(0.28f, 0.34f, 0.42f, 0.72f);
        internal static readonly Color DangerColor = new Color(0.45f, 0.23f, 0.22f, 1f);
        internal static readonly Color SuccessColor = new Color(0.34f, 0.62f, 0.54f, 1f);
        internal static readonly Color HeroSurfaceColor = new Color(0.10f, 0.13f, 0.18f, 0.99f);

        internal static Scene OpenOrCreateScene(string scenePath)
        {
            if (File.Exists(scenePath))
            {
                return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        internal static AppEnvironmentCatalog LoadEnvironmentCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<AppEnvironmentCatalog>(EnvironmentCatalogPath);
        }

        internal static List<string> FindEnvironmentScenePaths()
        {
            var paths = new List<string>();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

            for (int index = 0; index < sceneGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("Scene_", StringComparison.Ordinal))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        internal static Camera FindSceneCamera(Scene scene)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index] != null && cameras[index].gameObject.scene == scene)
                {
                    return cameras[index];
                }
            }

            return null;
        }

        internal static GameObject FindOrCreateSceneRoot(Scene scene, string name)
        {
            GameObject existing = FindSceneRoot(scene, name);
            if (existing != null)
            {
                return existing;
            }

            GameObject created = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        internal static GameObject FindSceneRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name)
                {
                    return roots[index];
                }
            }

            return null;
        }

        internal static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        internal static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        internal static Transform FindDescendant(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                Transform result = FindDescendant(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        internal static T FindDescendantComponent<T>(Transform root, string name) where T : Component
        {
            Transform match = FindDescendant(root, name);
            return match != null ? match.GetComponent<T>() : null;
        }

        internal static Component TryGetOrAddComponentByName(GameObject gameObject, string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                return null;
            }

            Component component = gameObject.GetComponent(type);
            return component != null ? component : gameObject.AddComponent(type);
        }

        internal static void ConfigureRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localScale = Vector3.one;
        }

        internal static void ConfigureStretchRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        internal static void ConfigureLayoutElement(GameObject gameObject, float preferredWidth = -1f, float preferredHeight = -1f)
        {
            if (gameObject == null)
            {
                return;
            }

            LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(gameObject);

            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
            }
        }

        internal static void StyleSlicedImage(Image image, Color color, bool raycastTarget = true)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycastTarget;
        }

        internal static T GetBuiltinExtraResource<T>(string resourcePath) where T : UnityEngine.Object
        {
            return AssetDatabase.GetBuiltinExtraResource<T>(resourcePath);
        }

        internal static void SetButtonEvent(Button button, UnityEngine.Events.UnityAction action)
        {
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }

            button.onClick.RemoveAllListeners();

            if (action != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            EditorUtility.SetDirty(button);
        }

        internal static void SetField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            Type targetType = target.GetType();
            FieldInfo fieldInfo = null;

            while (targetType != null && fieldInfo == null)
            {
                fieldInfo = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                targetType = targetType.BaseType;
            }

            if (fieldInfo == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            fieldInfo.SetValue(target, value);

            if (target is UnityEngine.Object unityObject)
            {
                EditorUtility.SetDirty(unityObject);
            }
        }

        internal static IReadOnlyList<string> EnumNames<TEnum>() where TEnum : Enum
        {
            return Enum.GetNames(typeof(TEnum));
        }

        internal static void MarkDirty(params object[] targets)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] is UnityEngine.Object unityObject)
                {
                    EditorUtility.SetDirty(unityObject);
                }
            }
        }

        internal static DefaultControls.Resources CreateUiResources()
        {
            return new DefaultControls.Resources
            {
                standard = GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        internal static TMP_DefaultControls.Resources CreateTmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }
    }
}
