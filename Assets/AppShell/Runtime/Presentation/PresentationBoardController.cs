using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Presentation
{
    [DisallowMultipleComponent]
    public class PresentationBoardController : MonoBehaviour
    {
        private static readonly string[] BoardNameFallbacks =
        {
            "PresentationBoardAnchor",
            "Whiteboard",
            "WB_Surface",
            "Screen",
            "ProjectorScreen"
        };

        private const string SurfaceObjectName = "PresentationSurface";
        private const string StatusObjectName = "PresentationSlideStatus";

        [SerializeField] private Camera viewerCamera;
        [SerializeField] private PresentationDeckReference deck;
        [SerializeField] private int currentPageIndex;

        private readonly Dictionary<int, Texture2D> pageCache = new Dictionary<int, Texture2D>();
        private Transform boardTransform;
        private Transform surfaceTransform;
        private Renderer surfaceRenderer;
        private Material surfaceMaterial;
        private TextMeshPro statusText;
        private float boardWidth = 1.6f;
        private float boardHeight = 0.9f;

        public bool HasDeck => deck != null && deck.HasPages;
        public int CurrentPageIndex => currentPageIndex;
        public int PageCount => deck != null ? deck.PageCount : 0;

        public static PresentationBoardController EnsureForScene(SessionConfig config, Camera sceneCamera)
        {
            if (config == null || !config.HasPresentation)
            {
                return null;
            }

            PresentationBoardController controller =
                FindFirstObjectByType<PresentationBoardController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                GameObject host = new GameObject("PresentationBoardRuntime");
                controller = host.AddComponent<PresentationBoardController>();
            }

            controller.Configure(config.SelectedPresentation, sceneCamera);
            PresentationInputController.EnsureForScene(controller);
            return controller;
        }

        public void Configure(PresentationDeckReference deckReference, Camera camera)
        {
            viewerCamera = camera != null ? camera : viewerCamera;
            deck = deckReference != null ? deckReference.Clone() : PresentationDeckReference.Empty();

            if (!HasDeck)
            {
                Debug.Log("[PresentationBoardController] No presentation deck selected for this session.");
                return;
            }

            if (!deck.HasReadablePages)
            {
                Debug.LogWarning($"[PresentationBoardController] Presentation page folder is missing: {deck.ImportFolderPath}");
                return;
            }

            boardTransform = ResolveBoardTransform();
            if (boardTransform == null)
            {
                Debug.LogWarning("[PresentationBoardController] No board surface found. Session will continue without presentation slides.");
                return;
            }

            if (!BindPresentationSurface(boardTransform))
            {
                Debug.LogWarning("[PresentationBoardController] Could not bind a presentation surface to the board.");
                return;
            }

            ShowPage(Mathf.Clamp(currentPageIndex, 0, Mathf.Max(0, deck.PageCount - 1)));
            Debug.Log($"[PresentationBoardController] Bound {deck.DisplayName} to {boardTransform.name} ({deck.PageCount} page(s)).");
        }

        public void NextPage()
        {
            if (!HasDeck)
            {
                return;
            }

            ShowPage(Mathf.Min(currentPageIndex + 1, deck.PageCount - 1));
        }

        public void PreviousPage()
        {
            if (!HasDeck)
            {
                return;
            }

            ShowPage(Mathf.Max(currentPageIndex - 1, 0));
        }

        public void ShowPage(int pageIndex)
        {
            if (!HasDeck || surfaceRenderer == null)
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(pageIndex, 0, deck.PageCount - 1);
            Texture2D texture = LoadPageTexture(clampedIndex);
            if (texture == null)
            {
                return;
            }

            currentPageIndex = clampedIndex;
            ApplyTexture(texture);
            AdjustSurfaceAspect(texture);
            UpdateStatusText();
            WarmAdjacentPages();
            TrimTextureCache();
        }

        private Transform ResolveBoardTransform()
        {
            for (int index = 0; index < BoardNameFallbacks.Length; index++)
            {
                GameObject exactMatch = GameObject.Find(BoardNameFallbacks[index]);
                if (exactMatch != null)
                {
                    return exactMatch.transform;
                }
            }

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int fallbackIndex = 0; fallbackIndex < BoardNameFallbacks.Length; fallbackIndex++)
            {
                string fallbackName = BoardNameFallbacks[fallbackIndex];
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform candidate = transforms[transformIndex];
                    if (candidate == null || candidate.name == SurfaceObjectName)
                    {
                        continue;
                    }

                    if (candidate.name.IndexOf(fallbackName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private bool BindPresentationSurface(Transform targetBoard)
        {
            ResolveBoardPose(targetBoard, out Vector3 position, out Quaternion rotation, out boardWidth, out boardHeight);

            Transform existingSurface = targetBoard.Find(SurfaceObjectName);
            GameObject surfaceObject = existingSurface != null
                ? existingSurface.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Quad);

            surfaceObject.name = SurfaceObjectName;
            surfaceTransform = surfaceObject.transform;
            surfaceTransform.SetParent(targetBoard, true);

            Collider surfaceCollider = surfaceObject.GetComponent<Collider>();
            if (surfaceCollider != null)
            {
                Destroy(surfaceCollider);
            }

            surfaceRenderer = surfaceObject.GetComponent<Renderer>();
            if (surfaceRenderer == null)
            {
                surfaceRenderer = surfaceObject.AddComponent<MeshRenderer>();
            }

            MeshFilter meshFilter = surfaceObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = surfaceObject.AddComponent<MeshFilter>();
            }

            surfaceTransform.position = position;
            surfaceTransform.rotation = rotation;

            surfaceMaterial = CreateSurfaceMaterial();
            surfaceRenderer.sharedMaterial = surfaceMaterial;
            SetSurfaceWorldScale(boardWidth, boardHeight);
            EnsureStatusLabel(targetBoard);
            return true;
        }

        private void ResolveBoardPose(
            Transform targetBoard,
            out Vector3 surfacePosition,
            out Quaternion surfaceRotation,
            out float resolvedWidth,
            out float resolvedHeight)
        {
            Renderer boardRenderer = ResolveBoardRenderer(targetBoard);
            Bounds bounds = boardRenderer != null
                ? boardRenderer.bounds
                : new Bounds(targetBoard.position, new Vector3(1.6f, 0.9f, 0.05f));

            Vector3 normal = targetBoard.forward.sqrMagnitude > 0.0001f ? targetBoard.forward.normalized : Vector3.forward;
            if (viewerCamera != null)
            {
                Vector3 toViewer = viewerCamera.transform.position - bounds.center;
                if (toViewer.sqrMagnitude > 0.0001f && Vector3.Dot(normal, toViewer.normalized) < 0f)
                {
                    normal = -normal;
                }
            }

            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f
                ? targetBoard.up
                : Vector3.up;

            surfacePosition = bounds.center + normal * 0.025f;
            surfaceRotation = Quaternion.LookRotation(normal, up);

            Vector3 size = bounds.size;
            resolvedWidth = Mathf.Max(0.4f, Mathf.Max(size.x, size.z) * 0.92f);
            resolvedHeight = Mathf.Max(0.25f, size.y * 0.92f);

            if (resolvedWidth < resolvedHeight)
            {
                resolvedWidth = resolvedHeight * 1.6f;
            }
        }

        private static Renderer ResolveBoardRenderer(Transform targetBoard)
        {
            if (targetBoard == null)
            {
                return null;
            }

            Renderer directRenderer = targetBoard.GetComponent<Renderer>();
            if (directRenderer != null)
            {
                return directRenderer;
            }

            Renderer[] renderers = targetBoard.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer candidate = renderers[index];
                if (candidate == null ||
                    candidate.gameObject.name == SurfaceObjectName ||
                    candidate.gameObject.name == StatusObjectName)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private Material CreateSurfaceMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = "PresentationSurface_RuntimeMaterial",
                color = Color.white
            };

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", 0);
            }

            return material;
        }

        private void EnsureStatusLabel(Transform targetBoard)
        {
            Transform existingStatus = targetBoard.Find(StatusObjectName);
            GameObject statusObject = existingStatus != null
                ? existingStatus.gameObject
                : new GameObject(StatusObjectName);

            statusObject.transform.SetParent(targetBoard, true);
            statusText = statusObject.GetComponent<TextMeshPro>();
            if (statusText == null)
            {
                statusText = statusObject.AddComponent<TextMeshPro>();
            }

            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.fontSize = Mathf.Clamp(boardHeight * 0.09f, 0.06f, 0.18f);
            statusText.rectTransform.sizeDelta = new Vector2(boardWidth, 0.2f);
            UpdateStatusPose(boardWidth, boardHeight);
        }

        private Texture2D LoadPageTexture(int pageIndex)
        {
            if (pageCache.TryGetValue(pageIndex, out Texture2D cachedTexture) && cachedTexture != null)
            {
                return cachedTexture;
            }

            string pagePath = deck.GetPageImagePath(pageIndex);
            if (!File.Exists(pagePath))
            {
                Debug.LogWarning($"[PresentationBoardController] Slide image not found: {pagePath}");
                return null;
            }

            byte[] imageBytes = File.ReadAllBytes(pagePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"PresentationPage_{pageIndex + 1:0000}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!texture.LoadImage(imageBytes, false))
            {
                Destroy(texture);
                Debug.LogWarning($"[PresentationBoardController] Could not decode slide image: {pagePath}");
                return null;
            }

            pageCache[pageIndex] = texture;
            return texture;
        }

        private void ApplyTexture(Texture2D texture)
        {
            if (surfaceMaterial == null || texture == null)
            {
                return;
            }

            if (surfaceMaterial.HasProperty("_BaseMap"))
            {
                surfaceMaterial.SetTexture("_BaseMap", texture);
                surfaceMaterial.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                surfaceMaterial.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }

            if (surfaceMaterial.HasProperty("_MainTex"))
            {
                surfaceMaterial.SetTexture("_MainTex", texture);
                surfaceMaterial.SetTextureScale("_MainTex", new Vector2(-1f, 1f));
                surfaceMaterial.SetTextureOffset("_MainTex", new Vector2(1f, 0f));
            }
        }

        private void AdjustSurfaceAspect(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            float boardAspect = boardWidth / Mathf.Max(0.01f, boardHeight);
            float slideAspect = texture.width / (float)Mathf.Max(1, texture.height);
            float width = boardWidth;
            float height = boardHeight;

            if (slideAspect > boardAspect)
            {
                height = width / slideAspect;
            }
            else
            {
                width = height * slideAspect;
            }

            SetSurfaceWorldScale(width, height);
            UpdateStatusPose(width, height);
        }

        private void SetSurfaceWorldScale(float width, float height)
        {
            if (surfaceTransform == null)
            {
                return;
            }

            Transform parent = surfaceTransform.parent;
            if (parent == null)
            {
                surfaceTransform.localScale = new Vector3(width, height, 1f);
                return;
            }

            Vector3 parentScale = parent.lossyScale;
            surfaceTransform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                height / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = $"Slide {currentPageIndex + 1} / {deck.PageCount}";
        }

        private void UpdateStatusPose(float width, float height)
        {
            if (statusText == null || surfaceTransform == null)
            {
                return;
            }

            Transform statusTransform = statusText.transform;
            statusTransform.position =
                surfaceTransform.position -
                surfaceTransform.up * ((height * 0.5f) + 0.08f) +
                surfaceTransform.forward * 0.02f;
            statusTransform.rotation = surfaceTransform.rotation;
            statusText.rectTransform.sizeDelta = new Vector2(width, 0.2f);
        }

        private void WarmAdjacentPages()
        {
            LoadPageTexture(Mathf.Max(0, currentPageIndex - 1));
            LoadPageTexture(Mathf.Min(deck.PageCount - 1, currentPageIndex + 1));
        }

        private void TrimTextureCache()
        {
            var keysToRemove = new List<int>();
            foreach (KeyValuePair<int, Texture2D> pair in pageCache)
            {
                if (Mathf.Abs(pair.Key - currentPageIndex) > 1)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int index = 0; index < keysToRemove.Count; index++)
            {
                int key = keysToRemove[index];
                if (pageCache.TryGetValue(key, out Texture2D texture) && texture != null)
                {
                    Destroy(texture);
                }

                pageCache.Remove(key);
            }
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in pageCache.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            pageCache.Clear();
        }
    }
}
