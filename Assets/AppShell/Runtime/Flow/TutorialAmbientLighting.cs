using UnityEngine;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Adds atmospheric spot lights aimed at each tutorial panel and a subtle ambient
    /// colour shift to make the tutorial hub feel alive and premium.
    /// All lights are created at runtime so nothing extra is needed in the scene file.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialAmbientLighting : MonoBehaviour
    {
        [SerializeField] private float spotIntensity = 1.8f;
        [SerializeField] private float spotRange = 8f;
        [SerializeField] private float spotAngle = 55f;
        [SerializeField] private Color warmAccent = new Color(1f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color coolAccent = new Color(0.36f, 0.58f, 0.96f, 1f);
        [SerializeField] private float ambientPulseSpeed = 0.25f;
        [SerializeField] private bool animateAmbient = true;

        private Transform lightRoot;
        private Color baseAmbientColor;
        private bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            baseAmbientColor = RenderSettings.ambientLight;

            lightRoot = new GameObject("TutorialLighting").transform;
            lightRoot.SetParent(transform, false);

            // Ceiling ambient fill (soft overhead)
            CreateSpotLight("AmbientCeiling", new Vector3(0f, 6.5f, 2.5f),
                new Vector3(90f, 0f, 0f), coolAccent, spotIntensity * 0.4f, 14f, 120f);

            // Floor up-wash (subtle bounce light feel)
            CreateSpotLight("FloorUpwash", new Vector3(0f, 0.1f, 2.5f),
                new Vector3(-85f, 0f, 0f), warmAccent, spotIntensity * 0.15f, 10f, 110f);
        }

        /// <summary>
        /// Create a spot light aimed at a specific tutorial panel position.
        /// Called by the tutorial controller each time a panel is placed.
        /// </summary>
        public void AddPanelSpotLight(string panelName, Vector3 panelPosition, Vector3 panelForward)
        {
            if (lightRoot == null) return;

            // Place the light slightly above and behind the panel, pointing at it
            Vector3 lightPos = panelPosition + Vector3.up * 2.8f - panelForward * 0.6f;
            Vector3 direction = (panelPosition - lightPos).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);

            Light spot = CreateSpotLight(
                panelName + "_Spot",
                lightPos,
                rotation.eulerAngles,
                warmAccent,
                spotIntensity,
                spotRange,
                spotAngle);

            // Add a subtle point light at the panel base for ground spill
            CreatePointLight(
                panelName + "_Base",
                panelPosition + Vector3.down * 0.3f,
                coolAccent,
                spotIntensity * 0.25f,
                3.2f);
        }

        private void Update()
        {
            if (!animateAmbient || !initialized) return;

            float t = (Mathf.Sin(Time.time * ambientPulseSpeed) + 1f) * 0.5f;
            Color pulse = Color.Lerp(baseAmbientColor, warmAccent * 0.15f + baseAmbientColor, t * 0.12f);
            RenderSettings.ambientLight = pulse;
        }

        private Light CreateSpotLight(string lightName, Vector3 position, Vector3 eulerAngles,
            Color color, float intensity, float range, float angle)
        {
            GameObject lightObj = new GameObject(lightName);
            lightObj.transform.SetParent(lightRoot, false);
            lightObj.transform.localPosition = position;
            lightObj.transform.localRotation = Quaternion.Euler(eulerAngles);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.6f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;

            return light;
        }

        private Light CreatePointLight(string lightName, Vector3 position, Color color,
            float intensity, float range)
        {
            GameObject lightObj = new GameObject(lightName);
            lightObj.transform.SetParent(lightRoot, false);
            lightObj.transform.localPosition = position;

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;

            return light;
        }

        private void OnDestroy()
        {
            if (initialized)
            {
                RenderSettings.ambientLight = baseAmbientColor;
            }
        }
    }
}
