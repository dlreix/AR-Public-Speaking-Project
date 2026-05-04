using UnityEngine;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Creates illuminated floor guide paths between tutorial panel positions.
    /// Thin glowing strips on the ground lead the user from one station to the next,
    /// with animated directional chevrons that pulse toward the target.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialFloorGuideSystem : MonoBehaviour
    {
        [SerializeField] private float stripWidth = 0.18f;
        [SerializeField] private float stripHeight = 0.025f;
        [SerializeField] private float chevronSpacing = 1.1f;
        [SerializeField] private float chevronSize = 0.32f;
        [SerializeField] private float pulseSpeed = 2.8f;

        private static readonly Color PathColor = new Color(1f, 0.64f, 0.24f, 0.35f);
        private static readonly Color ChevronColor = new Color(1f, 0.72f, 0.38f, 0.65f);
        private static readonly Color CompletedPathColor = new Color(0.18f, 0.88f, 0.46f, 0.25f);
        private static readonly Color ActiveGlowColor = new Color(1f, 0.64f, 0.24f, 0.8f);

        private Transform guideRoot;
        private Material pathMaterial;
        private Material chevronMaterial;
        private Material completedMaterial;
        private Material activeGlowMaterial;
        private int segmentCount;

        // Station markers - pulsing circles at each panel location
        private GameObject[] stationMarkers;
        private Material[] stationMaterials;
        private bool[] stationCompleted;
        private int stationCount;

        public void Initialize()
        {
            if (guideRoot != null) return;

            guideRoot = new GameObject("TutorialFloorGuides").transform;
            guideRoot.SetParent(transform, false);

            pathMaterial = CreateMaterial(PathColor, "FloorGuide_Path");
            chevronMaterial = CreateMaterial(ChevronColor, "FloorGuide_Chevron");
            completedMaterial = CreateMaterial(CompletedPathColor, "FloorGuide_Completed");
            activeGlowMaterial = CreateMaterial(ActiveGlowColor, "FloorGuide_ActiveGlow");

            stationMarkers = new GameObject[8];
            stationMaterials = new Material[8];
            stationCompleted = new bool[8];
            stationCount = 0;
        }

        /// <summary>
        /// Draw a floor path strip between two world positions.
        /// Chevrons are placed along the strip pointing from start to end.
        /// </summary>
        public void CreatePathSegment(string segmentName, Vector3 startFloor, Vector3 endFloor)
        {
            if (guideRoot == null) return;

            Vector3 start = new Vector3(startFloor.x, stripHeight, startFloor.z);
            Vector3 end = new Vector3(endFloor.x, stripHeight, endFloor.z);
            Vector3 direction = (end - start);
            float length = direction.magnitude;
            if (length < 0.1f) return;

            direction.Normalize();
            Vector3 midpoint = (start + end) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Main path strip
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = segmentName + "_Strip";
            strip.transform.SetParent(guideRoot, false);
            strip.transform.position = midpoint;
            strip.transform.rotation = rotation;
            strip.transform.localScale = new Vector3(stripWidth, 0.008f, length);
            ApplyMaterial(strip, pathMaterial);
            DisableCollider(strip);

            // Chevrons along the path
            int chevronCount = Mathf.Max(1, Mathf.FloorToInt(length / chevronSpacing));
            for (int i = 0; i < chevronCount; i++)
            {
                float t = (i + 0.5f) / chevronCount;
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y = stripHeight + 0.005f;

                CreateChevron(segmentName + "_Chevron" + i, pos, rotation, i, chevronCount);
            }

            segmentCount++;
        }

        /// <summary>
        /// Place a station marker (glowing ring) at a panel's floor position.
        /// </summary>
        public void CreateStationMarker(string stationName, Vector3 panelPosition, int stationIndex)
        {
            if (guideRoot == null || stationCount >= stationMarkers.Length) return;

            Vector3 floorPos = new Vector3(panelPosition.x, stripHeight + 0.01f, panelPosition.z);

            // Outer ring
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = stationName + "_Ring";
            ring.transform.SetParent(guideRoot, false);
            ring.transform.position = floorPos;
            ring.transform.localScale = new Vector3(0.95f, 0.008f, 0.95f);

            Material ringMat = CreateMaterial(PathColor, stationName + "_Mat");
            ApplyMaterial(ring, ringMat);
            DisableCollider(ring);

            // Inner disc (darker)
            GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = stationName + "_Inner";
            inner.transform.SetParent(ring.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            inner.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
            ApplyMaterial(inner, CreateMaterial(new Color(0.02f, 0.03f, 0.05f, 0.7f), stationName + "_InnerMat"));
            DisableCollider(inner);

            // Number indicator - small raised pillar
            GameObject numberPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            numberPost.name = stationName + "_Post";
            numberPost.transform.SetParent(guideRoot, false);
            numberPost.transform.position = floorPos + new Vector3(0.55f, 0.15f, 0f);
            numberPost.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
            ApplyMaterial(numberPost, CreateMaterial(ChevronColor, stationName + "_PostMat"));
            DisableCollider(numberPost);

            stationMarkers[stationCount] = ring;
            stationMaterials[stationCount] = ringMat;
            stationCompleted[stationCount] = false;
            stationCount++;
        }

        /// <summary>Mark a station as completed — turns green.</summary>
        public void MarkStationCompleted(int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= stationCount) return;

            stationCompleted[stationIndex] = true;
            if (stationMaterials[stationIndex] != null)
            {
                SetMaterialColor(stationMaterials[stationIndex], CompletedPathColor);
            }
        }

        private void Update()
        {
            if (guideRoot == null) return;

            // Pulse station markers
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            for (int i = 0; i < stationCount; i++)
            {
                if (stationMarkers[i] == null || stationCompleted[i]) continue;

                float scale = Mathf.Lerp(0.9f, 1.05f, pulse);
                Vector3 s = stationMarkers[i].transform.localScale;
                stationMarkers[i].transform.localScale = new Vector3(
                    0.95f * scale, s.y, 0.95f * scale);
            }
        }

        private void CreateChevron(string name, Vector3 position, Quaternion rotation, int index, int total)
        {
            // Chevron is a flattened cube rotated 45° to form a diamond/arrow shape
            GameObject chevron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chevron.name = name;
            chevron.transform.SetParent(guideRoot, false);
            chevron.transform.position = position;
            chevron.transform.rotation = rotation * Quaternion.Euler(0f, 45f, 0f);
            chevron.transform.localScale = new Vector3(chevronSize, 0.006f, chevronSize);
            ApplyMaterial(chevron, chevronMaterial);
            DisableCollider(chevron);

            // Add a subtle phase-based pulse component
            TutorialChevronPulse pulseComp = chevron.AddComponent<TutorialChevronPulse>();
            pulseComp.SetPhase((float)index / Mathf.Max(1, total), pulseSpeed);
        }

        private static Material CreateMaterial(Color color, string matName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || shader.name == "Hidden/InternalErrorShader")
                shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material mat = new Material(shader) { name = matName, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);

            // Make it emissive for glow effect
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.4f);
            }

            return mat;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * 0.4f);
        }

        private static void ApplyMaterial(GameObject obj, Material mat)
        {
            if (obj == null || mat == null) return;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
        }

        private static void DisableCollider(GameObject obj)
        {
            Collider col = obj != null ? obj.GetComponent<Collider>() : null;
            if (col != null) col.enabled = false;
        }
    }

    /// <summary>
    /// Simple pulse component for individual chevron arrows.
    /// Makes chevrons appear to "flow" along the path direction.
    /// </summary>
    public class TutorialChevronPulse : MonoBehaviour
    {
        private float phase;
        private float speed;
        private MeshRenderer meshRenderer;
        private Color baseColor;

        public void SetPhase(float phaseOffset, float pulseSpeed)
        {
            phase = phaseOffset * Mathf.PI * 2f;
            speed = pulseSpeed;
        }

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                baseColor = meshRenderer.sharedMaterial.color;
            }
        }

        private void Update()
        {
            if (meshRenderer == null || meshRenderer.sharedMaterial == null) return;

            float wave = (Mathf.Sin(Time.time * speed + phase) + 1f) * 0.5f;
            Color c = baseColor;
            c.a = Mathf.Lerp(0.15f, 0.7f, wave);

            // Scale pulse
            float s = Mathf.Lerp(0.85f, 1.1f, wave);
            transform.localScale = new Vector3(
                transform.localScale.x,
                transform.localScale.y,
                transform.localScale.x) * s / transform.localScale.x * transform.localScale.x;

            // Just do alpha pulse via material instance
            if (Application.isPlaying)
            {
                meshRenderer.material.color = c;
            }
        }
    }
}
