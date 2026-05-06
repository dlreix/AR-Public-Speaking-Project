using UnityEngine;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Spawns a celebratory particle burst and ambient effects when
    /// all tutorial panels have been completed. All effects are built
    /// from runtime primitives — no prefab or asset dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialCompletionCelebration : MonoBehaviour
    {
        [SerializeField] private Vector3 celebrationCenter = new Vector3(0f, 2.5f, 2.5f);
        [SerializeField] private float burstDuration = 6f;
        [SerializeField] private int particleCount = 28;

        private static readonly Color GoldColor = new Color(1f, 0.72f, 0.32f, 1f);
        private static readonly Color GreenColor = new Color(0.18f, 0.88f, 0.46f, 1f);
        private static readonly Color CyanColor = new Color(0.28f, 0.78f, 0.96f, 1f);

        private Transform effectRoot;
        private CelebrationParticle[] particles;
        private Light celebrationLight;
        private float elapsedTime;
        private bool isPlaying;

        public void PlayCelebration()
        {
            if (isPlaying) return;
            isPlaying = true;
            elapsedTime = 0f;

            effectRoot = new GameObject("CelebrationEffects").transform;
            effectRoot.SetParent(transform, false);

            // Central burst light
            GameObject lightObj = new GameObject("CelebrationLight");
            lightObj.transform.SetParent(effectRoot, false);
            lightObj.transform.position = celebrationCenter;
            celebrationLight = lightObj.AddComponent<Light>();
            celebrationLight.type = LightType.Point;
            celebrationLight.color = GoldColor;
            celebrationLight.intensity = 0f;
            celebrationLight.range = 12f;
            celebrationLight.shadows = LightShadows.None;

            // Spawn particles
            particles = new CelebrationParticle[particleCount];
            Color[] colors = { GoldColor, GreenColor, CyanColor, GoldColor, GreenColor };

            for (int i = 0; i < particleCount; i++)
            {
                float angle = (float)i / particleCount * Mathf.PI * 2f;
                float radiusVariance = Random.Range(0.6f, 1.4f);
                float heightVariance = Random.Range(0.8f, 2.2f);

                Vector3 direction = new Vector3(
                    Mathf.Sin(angle) * radiusVariance,
                    heightVariance,
                    Mathf.Cos(angle) * radiusVariance).normalized;

                float speed = Random.Range(1.8f, 4.2f);
                float size = Random.Range(0.06f, 0.14f);
                Color color = colors[i % colors.Length];

                PrimitiveType shape = (i % 3 == 0) ? PrimitiveType.Sphere :
                                      (i % 3 == 1) ? PrimitiveType.Cube : PrimitiveType.Sphere;

                GameObject particleObj = GameObject.CreatePrimitive(shape);
                particleObj.name = "Particle_" + i;
                particleObj.transform.SetParent(effectRoot, false);
                particleObj.transform.position = celebrationCenter;
                particleObj.transform.localScale = Vector3.one * size;

                Collider col = particleObj.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                MeshRenderer renderer = particleObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material mat = CreateEmissiveMaterial(color, "Particle_" + i);
                    renderer.sharedMaterial = mat;
                }

                particles[i] = new CelebrationParticle
                {
                    Transform = particleObj.transform,
                    Velocity = direction * speed,
                    RotationSpeed = new Vector3(
                        Random.Range(-180f, 180f),
                        Random.Range(-180f, 180f),
                        Random.Range(-180f, 180f)),
                    Gravity = Random.Range(1.2f, 2.8f),
                    Lifetime = Random.Range(3f, burstDuration),
                    Age = 0f,
                    InitialSize = size,
                    Renderer = renderer
                };
            }
        }

        private void Update()
        {
            if (!isPlaying) return;

            elapsedTime += Time.deltaTime;

            // Light flash
            if (celebrationLight != null)
            {
                float lightPhase = elapsedTime / burstDuration;
                if (lightPhase < 0.1f)
                    celebrationLight.intensity = Mathf.Lerp(0f, 5f, lightPhase / 0.1f);
                else if (lightPhase < 0.3f)
                    celebrationLight.intensity = Mathf.Lerp(5f, 1.5f, (lightPhase - 0.1f) / 0.2f);
                else
                    celebrationLight.intensity = Mathf.Lerp(1.5f, 0f, (lightPhase - 0.3f) / 0.7f);
            }

            // Update particles
            bool anyAlive = false;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].Transform == null) continue;

                particles[i].Age += Time.deltaTime;
                if (particles[i].Age >= particles[i].Lifetime)
                {
                    Destroy(particles[i].Transform.gameObject);
                    particles[i].Transform = null;
                    continue;
                }

                anyAlive = true;
                float lifeRatio = particles[i].Age / particles[i].Lifetime;

                // Physics
                particles[i].Velocity += Vector3.down * particles[i].Gravity * Time.deltaTime;
                particles[i].Transform.position += particles[i].Velocity * Time.deltaTime;
                particles[i].Transform.Rotate(particles[i].RotationSpeed * Time.deltaTime);

                // Fade out
                float alpha = 1f - Mathf.Pow(lifeRatio, 2f);
                float scale = particles[i].InitialSize * Mathf.Lerp(1f, 0.3f, lifeRatio);
                particles[i].Transform.localScale = Vector3.one * scale;

                if (particles[i].Renderer != null && particles[i].Renderer.material != null)
                {
                    Color c = particles[i].Renderer.material.color;
                    c.a = alpha;
                    particles[i].Renderer.material.color = c;
                }

                // Floor bounce
                if (particles[i].Transform.position.y < 0.05f)
                {
                    Vector3 pos = particles[i].Transform.position;
                    pos.y = 0.05f;
                    particles[i].Transform.position = pos;

                    Vector3 vel = particles[i].Velocity;
                    vel.y = Mathf.Abs(vel.y) * 0.4f;
                    vel.x *= 0.8f;
                    vel.z *= 0.8f;
                    particles[i].Velocity = vel;
                }
            }

            if (!anyAlive && elapsedTime > 1f)
            {
                isPlaying = false;
                if (effectRoot != null)
                {
                    Destroy(effectRoot.gameObject);
                    effectRoot = null;
                }
            }
        }

        private static Material CreateEmissiveMaterial(Color color, string matName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || shader.name == "Hidden/InternalErrorShader")
                shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material mat = new Material(shader) { name = matName, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.8f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.8f);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
            }

            return mat;
        }

        private struct CelebrationParticle
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 RotationSpeed;
            public float Gravity;
            public float Lifetime;
            public float Age;
            public float InitialSize;
            public MeshRenderer Renderer;
        }
    }
}
