using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daire event sistemi: Ekranda belirli aralıklarla hedef daireler oluşturur,
/// kullanıcının bakışıyla doldurulmasını yönetir.
/// GameManager veya ayrı bir objeye atanır.
/// </summary>
public class CircleEventSystem : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  REFERANSLAR
    // ──────────────────────────────────────────────
    [Header("Referanslar")]
    [Tooltip("Main Camera üzerindeki EyeTrackingSystem")]
    public EyeTrackingSystem eyeTracking;

    [Tooltip("VR kamerası (XR Origin > Camera Offset > Main Camera)")]
    public Camera mainCamera;

    [Tooltip("Dairelerin çıkabileceği noktalar")]
    public Transform[] spawnPoints;

    // ──────────────────────────────────────────────
    //  ZAMANLAMA
    // ──────────────────────────────────────────────
    [Header("Zamanlama")]
    [Tooltip("İki daire arası bekleme süresi (saniye)")]
    [Range(1f, 15f)]
    public float timeBetweenCircles = 5f;

    [Tooltip("Dairenin dolması için gereken süre (saniye)")]
    [Range(1f, 10f)]
    public float fillDuration = 3.5f;

    [Tooltip("Daire raycast mesafesi")]
    public float maxRayDistance = 100f;

    // ──────────────────────────────────────────────
    //  DAİRE GÖRSELLERİ
    // ──────────────────────────────────────────────
    [Header("Daire Boyutları")]
    [Tooltip("Genel ölçek çarpanı — küçültmek için düşür")]
    [Range(0.001f, 0.01f)]
    public float canvasScale = 0.003f;

    [Tooltip("Merkez daire boyutu")]
    [Range(20f, 150f)]
    public float bgSize = 60f;

    [Tooltip("Dolum halkası boyutu")]
    [Range(50f, 250f)]
    public float fillRingSize = 100f;

    [Tooltip("Dış parıltı boyutu")]
    [Range(60f, 300f)]
    public float outerGlowSize = 120f;

    [Tooltip("Daire tıklama/bakış collider boyutu")]
    [Range(0.1f, 2f)]
    public float colliderSize = 0.5f;

    // ──────────────────────────────────────────────
    //  SHADER REFERANSLARI (Inspector'dan atanır — build'de Shader.Find() null döner)
    // ──────────────────────────────────────────────
    [Header("Shader Referansları")]
    [Tooltip("Sphere için Standard shader (Always Included Shaders'a ekle)")]
    [SerializeField] private Shader standardShader;

    [Tooltip("Ring LineRenderer için Sprites/Default shader")]
    [SerializeField] private Shader spritesDefaultShader;

    [Tooltip("Particle VFX için Particles/Standard Unlit shader")]
    [SerializeField] private Shader particlesUnlitShader;

    // ──────────────────────────────────────────────
    //  LAYER
    // ──────────────────────────────────────────────
    [Header("Layer")]
    [Tooltip("Daireler için ayrılmış fizik layer'ı")]
    public LayerMask circleLayer;

    // ──────────────────────────────────────────────
    //  ÖZEL DEĞİŞKENLER
    // ──────────────────────────────────────────────
    private bool eventActive;
    private CircleTarget currentCircle;
    private float nextSpawnTimer;
    private int lastSpawnIndex = -1;

    // Önbellek
    private Transform camTransform;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void Awake()
    {
        if (mainCamera != null)
            camTransform = mainCamera.transform;
    }

    void Update()
    {
        if (!eventActive) return;

        if (currentCircle == null)
        {
            nextSpawnTimer -= Time.deltaTime;
            if (nextSpawnTimer <= 0f)
                SpawnCircle();
        }
        else
        {
            CheckGazeOnCircle();
        }
    }

    // ══════════════════════════════════════════════
    //  KAMU API
    // ══════════════════════════════════════════════

    /// <summary>Event'i başlat veya durdur (toggle).</summary>
    public void ToggleEvent()
    {
        if (!eventActive) StartEvent();
        else              StopEvent();
    }

    /// <summary>Event'i zorla durdur (oturum bittiğinde çağrılır).</summary>
    public void ForceStop()
    {
        if (eventActive) StopEvent();
    }

    // ══════════════════════════════════════════════
    //  EVENT YÖNETİMİ
    // ══════════════════════════════════════════════

    void StartEvent()
    {
        eventActive = true;
        eyeTracking.SetPaused(true);
        nextSpawnTimer = 1f;
    }

    void StopEvent()
    {
        eventActive = false;

        if (currentCircle != null)
        {
            Destroy(currentCircle.gameObject);
            currentCircle = null;
        }

        eyeTracking.SetPaused(false);
    }

    // ══════════════════════════════════════════════
    //  DAİRE OLUŞTURMA
    // ══════════════════════════════════════════════

    void SpawnCircle()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        // Öncekinden farklı bir nokta seç
        int index;
        do { index = Random.Range(0, spawnPoints.Length); }
        while (index == lastSpawnIndex && spawnPoints.Length > 1);
        lastSpawnIndex = index;

        // Obje oluştur
        GameObject circleObj = new GameObject("GazeCircle");
        circleObj.transform.position = spawnPoints[index].position;
        circleObj.layer = LayerMaskToLayer(circleLayer);

        SphereCollider col = circleObj.AddComponent<SphereCollider>();
        col.radius = colliderSize;

        // CircleTarget bileşeni ekle ve ayarla
        currentCircle = circleObj.AddComponent<CircleTarget>();
        currentCircle.Setup(
            fillDuration, canvasScale, bgSize,
            fillRingSize, outerGlowSize, colliderSize,
            OnCircleComplete,
            standardShader, spritesDefaultShader, particlesUnlitShader
        );
    }

    void CheckGazeOnCircle()
    {
        Ray ray = new Ray(camTransform.position, camTransform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRayDistance, circleLayer))
        {
            CircleTarget target = hit.collider.GetComponent<CircleTarget>();
            if (target != null)
                target.OnGaze();
        }
    }

    void OnCircleComplete()
    {
        currentCircle = null;
        nextSpawnTimer = timeBetweenCircles;
    }

    // ══════════════════════════════════════════════
    //  YARDIMCI
    // ══════════════════════════════════════════════

    static int LayerMaskToLayer(LayerMask mask)
    {
        int val = mask.value;
        int layer = 0;
        while (val > 1) { val >>= 1; layer++; }
        return layer;
    }
}

// ══════════════════════════════════════════════════════════════════
//  CIRCLE TARGET — Tek bir dairenin görsel ve dolum mantığı
// ══════════════════════════════════════════════════════════════════

public class CircleTarget : MonoBehaviour
{
    private float fillDuration;
    private System.Action onCompleted;

    private GameObject sphereObj;
    private LineRenderer ringLine;
    private ParticleSystem burstVFX;
    private Material sphereMat;

    private float currentFill;
    private bool isGazed;
    private bool isCompleted;
    private Transform camTransform;

    // Ring ayarları
    private const int RING_SEGMENTS = 64;
    private float ringRadius;

    // Shader referansları (Shader.Find() yerine Inspector'dan alınır)
    private Shader _standardShader;
    private Shader _spritesDefaultShader;
    private Shader _particlesUnlitShader;

    // Sphere temel scale'i (pulse animasyonu bu değer üzerine uygulanır)
    private float sphereBaseScale;

    // Başlangıç renkleri
    private static readonly Color SPHERE_COLOR = new Color(0.2f, 0.6f, 1f, 0.85f);
    private static readonly Color RING_START_COLOR = new Color(0.3f, 0.8f, 1f, 0.9f);
    private static readonly Color RING_END_COLOR = new Color(0f, 1f, 0.5f, 1f);
    private static readonly Color SPHERE_COMPLETE_COLOR = new Color(0f, 1f, 0.5f, 1f);

    public void Setup(float fill, float scale, float bg,
                      float ring, float glow, float colRadius,
                      System.Action callback,
                      Shader standardShader, Shader spritesDefaultShader, Shader particlesUnlitShader)
    {
        fillDuration = fill;
        onCompleted = callback;
        camTransform = Camera.main.transform;

        // Shader referanslarını sakla
        _standardShader       = standardShader;
        _spritesDefaultShader = spritesDefaultShader;
        _particlesUnlitShader = particlesUnlitShader;

        // Collider — bakış algılama için (world-space, parent scale etkilemez)
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null) col.radius = colRadius;

        // Sphere ve ring boyutlarını canvasScale ile doğrudan world-space'e çevir.
        // Parent scale = 1 kalır; böylece LineRenderer genişliği ve
        // ParticleSystem boyutları ölçeklenmez, görünür kalır.
        sphereBaseScale = bg * scale;   // world-space çap (metre)
        ringRadius      = ring * scale; // world-space halka yarıçapı (metre)

        CreateSphere();
        CreateRingLine();
        CreateParticleVFX();
    }

    void Update()
    {
        if (isCompleted) return;

        // Ring her zaman kameraya baksın
        ringLine.transform.LookAt(camTransform);

        if (isGazed)
        {
            currentFill += Time.deltaTime / fillDuration;

            // Ring'i güncelle
            UpdateRing(currentFill);

            // Sphere renk geçişi (mavi → yeşil)
            Color sc = Color.Lerp(SPHERE_COLOR, SPHERE_COMPLETE_COLOR, currentFill);
            sphereMat.color = sc;

            // Sphere pulse efekti (temel scale üzerine uygulanır)
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.05f * currentFill;
            sphereObj.transform.localScale = Vector3.one * (sphereBaseScale * pulse);

            // Sphere emisyon (parıltı)
            Color emissionColor = Color.Lerp(Color.black, RING_END_COLOR * 0.5f, currentFill);
            sphereMat.SetColor("_EmissionColor", emissionColor);

            if (currentFill >= 1f)
                Complete();
        }
        else if (currentFill > 0f)
        {
            // Bakılmıyorsa yavaşça azalt
            currentFill = Mathf.Max(0f, currentFill - Time.deltaTime / (fillDuration * 2f));
            UpdateRing(currentFill);

            // Rengi geri al
            sphereMat.color = Color.Lerp(SPHERE_COLOR, SPHERE_COMPLETE_COLOR, currentFill);
            sphereObj.transform.localScale = Vector3.one * sphereBaseScale;
        }

        isGazed = false;
    }

    public void OnGaze()
    {
        if (!isCompleted) isGazed = true;
    }

    void Complete()
    {
        isCompleted = true;

        sphereObj.SetActive(false);
        ringLine.gameObject.SetActive(false);

        burstVFX.Play();
        onCompleted?.Invoke();
        Destroy(gameObject, 2f);
    }

    // ──────────────────────────────────────────────
    //  SPHERE
    // ──────────────────────────────────────────────

    void CreateSphere()
    {
        sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObj.transform.SetParent(transform);
        sphereObj.transform.localPosition = Vector3.zero;
        // bgSize canvas-birimi cinsinden temel boyut (parent'ın canvasScale'i world-space'e çevirir)
        sphereObj.transform.localScale = Vector3.one * sphereBaseScale;

        // Sphere'in kendi collider'ını kaldır (ana objede zaten var)
        Destroy(sphereObj.GetComponent<SphereCollider>());

        // Materyal — Shader.Find() build'de null döner; Inspector'dan atanan referans kullanılır
        sphereMat = new Material(_standardShader);
        sphereMat.color = SPHERE_COLOR;
        sphereMat.SetFloat("_Metallic", 0.3f);
        sphereMat.SetFloat("_Glossiness", 0.8f);
        sphereMat.EnableKeyword("_EMISSION");
        sphereMat.SetColor("_EmissionColor", Color.black);

        // Transparan mod
        sphereMat.SetFloat("_Mode", 3); // Transparent
        sphereMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        sphereMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        sphereMat.SetInt("_ZWrite", 0);
        sphereMat.DisableKeyword("_ALPHATEST_ON");
        sphereMat.EnableKeyword("_ALPHABLEND_ON");
        sphereMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        sphereMat.renderQueue = 3000;

        sphereObj.GetComponent<Renderer>().material = sphereMat;
    }

    // ──────────────────────────────────────────────
    //  DOLUM RING'İ (LineRenderer)
    // ──────────────────────────────────────────────

    void CreateRingLine()
    {
        GameObject ringObj = new GameObject("FillRing");
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;

        ringLine = ringObj.AddComponent<LineRenderer>();
        ringLine.useWorldSpace = false;
        ringLine.loop = false;
        ringLine.startWidth = 0.08f;
        ringLine.endWidth = 0.08f;
        ringLine.positionCount = 0;
        ringLine.numCapVertices = 4;
        ringLine.numCornerVertices = 4;

        // Materyal — Shader.Find() build'de null döner; Inspector'dan atanan referans kullanılır
        Material lineMat = new Material(_spritesDefaultShader);
        lineMat.color = RING_START_COLOR;
        ringLine.material = lineMat;

        // Renk gradyan
        ringLine.colorGradient = CreateRingGradient();
    }

    void UpdateRing(float fillAmount)
    {
        if (fillAmount <= 0f)
        {
            ringLine.positionCount = 0;
            return;
        }

        int pointCount = Mathf.Max(2, Mathf.RoundToInt(RING_SEGMENTS * fillAmount) + 1);
        ringLine.positionCount = pointCount;

        float totalAngle = fillAmount * 360f;
        float startAngle = 90f; // Üstten başla

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            float angle = startAngle - (t * totalAngle);
            float rad = angle * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(
                Mathf.Cos(rad) * ringRadius,
                Mathf.Sin(rad) * ringRadius,
                0f
            );

            ringLine.SetPosition(i, pos);
        }

        // Genişlik: dolumla biraz artsın
        float width = 0.08f + (fillAmount * 0.04f);
        ringLine.startWidth = width;
        ringLine.endWidth = width;

        // Renk gradyan güncelle
        ringLine.colorGradient = CreateRingGradient();
    }

    Gradient CreateRingGradient()
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(RING_START_COLOR, 0f),
                new GradientColorKey(RING_END_COLOR, 1f)
            },
            new[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        return grad;
    }

    // ──────────────────────────────────────────────
    //  PARTICLE VFX
    // ──────────────────────────────────────────────

    void CreateParticleVFX()
    {
        GameObject vfxObj = new GameObject("BurstVFX");
        vfxObj.transform.SetParent(transform);
        vfxObj.transform.localPosition = Vector3.zero;

        burstVFX = vfxObj.AddComponent<ParticleSystem>();
        burstVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = burstVFX.main;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 4f;
        main.startSize = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0f, 1f, 0.8f, 1f),
            new Color(0.3f, 0.8f, 1f, 1f));

        var emission = burstVFX.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        var shape = burstVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sol = burstVFX.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0, 1, 1, 0));

        var col = burstVFX.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.3f, 1f, 0.8f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.5f), 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Materyal — Shader.Find() build'de null döner; Inspector'dan atanan referans kullanılır
        var rend = burstVFX.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(_particlesUnlitShader);
        rend.material.color = Color.white;
    }
}