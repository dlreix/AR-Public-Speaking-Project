using UnityEngine;

namespace VRPublicSpeaking.MainBranchGaze
{
/// <summary>
/// Tek bakımlık hızlı nokta event'i (Özellik 1).
///
/// Rastgele aralıklarla sahnede bir nokta belirir; kullanıcı <see cref="dotLifetime"/>
/// saniye içinde bu noktaya bakarsa bonus puan kazanır ve VFX oynar. Süre içinde
/// bakılmazsa nokta sessizce kaybolur.
///
/// Puan aktarımı tamamen <see cref="GazeScoringSystem.ReportBonus(float)"/> üzerinden
/// yapılır; bu script kendi içinde puan tutmaz.
/// </summary>
public class QuickGazeDotSystem : MonoBehaviour, IGazeEvent
{
    // ──────────────────────────────────────────────
    //  MASTER TOGGLE (DEBUG)
    // ──────────────────────────────────────────────
    [Header("Debug / Master Toggle")]
    [Tooltip("Event sistemini tamamen devre dışı bırakır. Geliştirme sırasında " +
             "diğer sistemleri izole test etmek için kapatılabilir.")]
    public bool enableEvent = true;

    // ──────────────────────────────────────────────
    //  REFERANSLAR
    // ──────────────────────────────────────────────
    [Header("Referanslar")]
    [Tooltip("Çakışmayı önleyen merkezi koordinatör (zorunlu)")]
    public GazeEventCoordinator coordinator;

    [Tooltip("Bonus puanı bildireceğimiz puanlama sistemi")]
    public GazeScoringSystem scoring;

    [Tooltip("VR kamerası (XR Origin > Camera Offset > Main Camera)")]
    public Camera mainCamera;

    [Tooltip("Oturum aktifken noktanın fiziksel olarak algılanacağı layer")]
    public LayerMask dotLayer;

    [Tooltip("Event aktifken gaze detection'ın duraklatılacağı sistem")]
    public EyeTrackingSystem eyeTracking;

    // ──────────────────────────────────────────────
    //  SPAWN NOKTALARI
    // ──────────────────────────────────────────────
    [Header("Spawn Noktaları")]
    [Tooltip("Noktanın çıkabileceği rastgele konumlar")]
    public Transform[] spawnPoints;

    // ──────────────────────────────────────────────
    //  ZAMANLAMA
    // ──────────────────────────────────────────────
    [Header("Zamanlama")]
    [Tooltip("Nokta ekranda kaç saniye kalsın (bakılmazsa kaybolur)")]
    [Range(0.5f, 10f)]
    public float dotLifetime = 3f;

    [Tooltip("Bir event'ten sonraki minimum bekleme süresi")]
    [Range(1f, 60f)]
    public float minSpawnInterval = 10f;

    [Tooltip("Bir event'ten sonraki maksimum bekleme süresi")]
    [Range(1f, 120f)]
    public float maxSpawnInterval = 25f;

    // ──────────────────────────────────────────────
    //  PUAN
    // ──────────────────────────────────────────────
    [Header("Puan")]
    [Tooltip("Noktaya bakıldığında kazanılacak bonus puan. " +
             "GazeScoringSystem tarafında 0–15 aralığında clamp edilir.")]
    [Range(0f, 15f)]
    public float bonusPoints = 8f;

    // ──────────────────────────────────────────────
    //  GÖRSEL AYARLAR
    // ──────────────────────────────────────────────
    [Header("Görsel")]
    [Tooltip("Noktanın dünya-uzayı çapı (metre)")]
    [Range(0.05f, 1f)]
    public float dotScale = 0.18f;

    [Tooltip("Bakış tespit collider yarıçapı")]
    [Range(0.1f, 2f)]
    public float colliderRadius = 0.35f;

    [Tooltip("Nokta raycast mesafesi")]
    public float maxRayDistance = 100f;

    // ──────────────────────────────────────────────
    //  SHADER REFERANSLARI
    // ──────────────────────────────────────────────
    [Header("Shader Referansları (Inspector'dan atanmalı)")]
    [Tooltip("Sphere için Standard shader")]
    [SerializeField] private Shader standardShader;

    [Tooltip("Particle VFX için Particles/Standard Unlit shader")]
    [SerializeField] private Shader particlesUnlitShader;

    // ──────────────────────────────────────────────
    //  IGazeEvent
    // ──────────────────────────────────────────────
    public bool IsRunning => activeDot != null;

    // ──────────────────────────────────────────────
    //  ÖZEL DEĞİŞKENLER
    // ──────────────────────────────────────────────
    private bool sessionActive;
    private float nextSpawnTimer;
    private QuickGazeDot activeDot;
    private Transform camTransform;
    private int lastSpawnIndex = -1;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void Awake()
    {
        if (mainCamera != null) camTransform = mainCamera.transform;
        Debug.Log("[QuickGazeDotSystem] Initialized. Spawn points: " +
                  (spawnPoints != null ? spawnPoints.Length.ToString() : "0"));
    }

    void Update()
    {
        if (!sessionActive || !enableEvent) return;

        if (activeDot != null)
        {
            UpdateActiveDot();
            return;
        }

        TickSpawnTimer();
    }

    // ══════════════════════════════════════════════
    //  OTURUM YÖNETİMİ
    // ══════════════════════════════════════════════

    /// <summary>MainController oturum başlayınca çağırır.</summary>
    public void OnSessionStarted()
    {
        sessionActive = true;
        ScheduleNextSpawn();
        Debug.Log("[QuickGazeDotSystem] Session started, event system armed.");
    }

    /// <summary>MainController oturum bitince çağırır.</summary>
    public void OnSessionEnded()
    {
        sessionActive = false;
        ForceStop();
        Debug.Log("[QuickGazeDotSystem] Session ended, event system disarmed.");
    }

    // ══════════════════════════════════════════════
    //  IGazeEvent IMPLEMENTATION
    // ══════════════════════════════════════════════

    public void ForceStop()
    {
        if (activeDot == null)
        {
            coordinator?.Release(this);
            return;
        }

        Destroy(activeDot.gameObject);
        activeDot = null;

        if (eyeTracking != null) eyeTracking.SetPaused(false);
        coordinator?.Release(this);

        Debug.Log("[QuickGazeDotSystem] Event force-stopped.");
    }

    // ══════════════════════════════════════════════
    //  SPAWN ZAMANLAMA
    // ══════════════════════════════════════════════

    void TickSpawnTimer()
    {
        nextSpawnTimer -= Time.deltaTime;
        if (nextSpawnTimer > 0f) return;

        TrySpawnDot();
        ScheduleNextSpawn();
    }

    void ScheduleNextSpawn()
    {
        nextSpawnTimer = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    void TrySpawnDot()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (coordinator == null)
        {
            SpawnDotInternal();
            return;
        }

        if (!coordinator.TryAcquire(this)) return;
        SpawnDotInternal();
    }

    // ══════════════════════════════════════════════
    //  SPAWN
    // ══════════════════════════════════════════════

    void SpawnDotInternal()
    {
        int index = PickRandomSpawnIndex();
        Transform spawn = spawnPoints[index];

        GameObject dotObj = new GameObject("QuickGazeDot");
        dotObj.transform.position = spawn.position;
        dotObj.layer = LayerMaskUtils.ToLayer(dotLayer);

        SphereCollider col = dotObj.AddComponent<SphereCollider>();
        col.radius = colliderRadius;

        activeDot = dotObj.AddComponent<QuickGazeDot>();
        activeDot.Setup(dotLifetime, dotScale, OnDotHit, OnDotMissed,
                        standardShader, particlesUnlitShader);

        if (eyeTracking != null) eyeTracking.SetPaused(true);

        Debug.Log($"[QuickGazeDotSystem] Dot spawned at point #{index} — lifetime: {dotLifetime:F1}s");
    }

    int PickRandomSpawnIndex()
    {
        int index;
        do { index = Random.Range(0, spawnPoints.Length); }
        while (index == lastSpawnIndex && spawnPoints.Length > 1);
        lastSpawnIndex = index;
        return index;
    }

    // ══════════════════════════════════════════════
    //  UPDATE LOOP — GAZE DETECTION
    // ══════════════════════════════════════════════

    void UpdateActiveDot()
    {
        Ray ray = new Ray(camTransform.position, camTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, dotLayer))
        {
            QuickGazeDot target = hit.collider.GetComponent<QuickGazeDot>();
            if (target != null) target.OnGazed();
        }
    }

    // ══════════════════════════════════════════════
    //  CALLBACK'LER
    // ══════════════════════════════════════════════

    void OnDotHit()
    {
        if (scoring != null) scoring.ReportBonus(bonusPoints);
        Debug.Log($"[QuickGazeDotSystem] Dot hit! Awarded {bonusPoints:F1} pts.");

        activeDot = null;
        if (eyeTracking != null) eyeTracking.SetPaused(false);
        coordinator?.Release(this);
    }

    void OnDotMissed()
    {
        Debug.Log("[QuickGazeDotSystem] Dot missed (timeout). No points awarded.");

        activeDot = null;
        if (eyeTracking != null) eyeTracking.SetPaused(false);
        coordinator?.Release(this);
    }
}

// ══════════════════════════════════════════════════════════════════
//  QUICK GAZE DOT — Tek bir noktanın görsel ve yaşam mantığı
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Tek bir QuickGazeDotSystem dot instance'ının davranışı.
/// Kendi lifetime timer'ını, gaze flag'ini ve görsel efektlerini yönetir.
/// </summary>
public class QuickGazeDot : MonoBehaviour
{
    // Davranış parametreleri
    private float lifetime;
    private float scale;
    private System.Action onHit;
    private System.Action onMissed;

    // Shader referansları
    private Shader standardShader;
    private Shader particlesUnlitShader;

    // İç durum
    private float elapsed;
    private bool wasGazedThisFrame;
    private bool resolved;

    // Alt bileşenler
    private GameObject sphereObj;
    private Material sphereMat;
    private ParticleSystem burstVFX;

    // Renkler — mavi/yeşil olan CircleEvent'ten ayırmak için altın-sarı seçildi
    private static readonly Color IDLE_COLOR   = new Color(1f, 0.85f, 0.25f, 0.9f);
    private static readonly Color GAZED_COLOR  = new Color(1f, 1f, 0.5f, 1f);
    private static readonly Color VFX_COLOR_A  = new Color(1f, 0.9f, 0.3f, 1f);
    private static readonly Color VFX_COLOR_B  = new Color(1f, 0.6f, 0.1f, 1f);

    // ══════════════════════════════════════════════
    //  KURULUM
    // ══════════════════════════════════════════════

    public void Setup(float lifetime, float scale,
                      System.Action onHit, System.Action onMissed,
                      Shader standardShader, Shader particlesUnlitShader)
    {
        this.lifetime = lifetime;
        this.scale = scale;
        this.onHit = onHit;
        this.onMissed = onMissed;
        this.standardShader = standardShader;
        this.particlesUnlitShader = particlesUnlitShader;

        CreateSphere();
        CreateBurstVFX();
    }

    // ══════════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════════

    void Update()
    {
        if (resolved) return;

        elapsed += Time.deltaTime;

        if (wasGazedThisFrame)
        {
            Resolve(true);
            wasGazedThisFrame = false;
            return;
        }

        if (elapsed >= lifetime)
        {
            Resolve(false);
            return;
        }

        AnimateIdle();
        wasGazedThisFrame = false;
    }

    /// <summary>QuickGazeDotSystem her frame raycast hit olunca çağırır.</summary>
    public void OnGazed()
    {
        if (resolved) return;
        wasGazedThisFrame = true;
    }

    // ══════════════════════════════════════════════
    //  İÇ MANTIK
    // ══════════════════════════════════════════════

    void AnimateIdle()
    {
        // Nefes alma pulse (ritm: 2 Hz, genlik: ±%10)
        float pulse = 1f + Mathf.Sin(Time.time * 6.28f * 2f) * 0.1f;
        sphereObj.transform.localScale = Vector3.one * (scale * pulse);

        // Son saniyede kırmızıya yaklaşarak ivedilik hissi
        float remaining = Mathf.Clamp01(1f - (elapsed / lifetime));
        Color c = Color.Lerp(new Color(1f, 0.3f, 0.2f, 0.95f), IDLE_COLOR, remaining);
        sphereMat.color = c;
        sphereMat.SetColor("_EmissionColor", c * 0.6f);
    }

    void Resolve(bool wasHit)
    {
        resolved = true;

        if (wasHit)
        {
            sphereMat.color = GAZED_COLOR;
            sphereMat.SetColor("_EmissionColor", GAZED_COLOR);
            burstVFX.Play();
            sphereObj.SetActive(false);
            onHit?.Invoke();
            Destroy(gameObject, 1.5f);
        }
        else
        {
            onMissed?.Invoke();
            Destroy(gameObject);
        }
    }

    // ══════════════════════════════════════════════
    //  VİSÜEL KURULUM
    // ══════════════════════════════════════════════

    void CreateSphere()
    {
        sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObj.transform.SetParent(transform);
        sphereObj.transform.localPosition = Vector3.zero;
        sphereObj.transform.localScale = Vector3.one * scale;

        Destroy(sphereObj.GetComponent<SphereCollider>());

        sphereMat = CreateEmissiveMaterial(IDLE_COLOR);
        sphereObj.GetComponent<Renderer>().material = sphereMat;
    }

    Material CreateEmissiveMaterial(Color color)
    {
        Material mat = new Material(standardShader);
        mat.color = color;
        mat.SetFloat("_Metallic", 0.2f);
        mat.SetFloat("_Glossiness", 0.75f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.6f);

        // Transparan mod
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        return mat;
    }

    void CreateBurstVFX()
    {
        GameObject vfxObj = new GameObject("BurstVFX");
        vfxObj.transform.SetParent(transform);
        vfxObj.transform.localPosition = Vector3.zero;

        burstVFX = vfxObj.AddComponent<ParticleSystem>();
        burstVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = burstVFX.main;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 3.5f;
        main.startSize = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(VFX_COLOR_A, VFX_COLOR_B);

        var emission = burstVFX.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        var shape = burstVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sol = burstVFX.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0, 1, 1, 0));

        var rend = burstVFX.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(particlesUnlitShader);
        rend.material.color = Color.white;
    }
}

// ══════════════════════════════════════════════════════════════════
//  LAYER YARDIMCISI (DRY — CircleEventSystem ile paylaşımlı mantık)
// ══════════════════════════════════════════════════════════════════

internal static class LayerMaskUtils
{
    /// <summary>Tek-bitli LayerMask'ı layer index'ine çevirir.</summary>
    public static int ToLayer(LayerMask mask)
    {
        int val = mask.value;
        int layer = 0;
        while (val > 1) { val >>= 1; layer++; }
        return layer;
    }
}
}
