using UnityEngine;

/// <summary>
/// Hareketli takip noktası event'i (Özellik 2).
///
/// Rastgele aralıklarla sahnede iki waypoint arasında ileri-geri hareket eden
/// bir nokta belirir. Kullanıcı <see cref="totalDuration"/> saniye boyunca
/// bu noktayı takip ederse (kümülatif bakış süresi / toplam süre oranı
/// <see cref="requiredGazeRatio"/> eşiğini aşarsa) bonus puan kazanır ve VFX oynar.
///
/// Süre sonunda nokta her durumda kaybolur; başarısızlık sessizdir.
/// Puan aktarımı <see cref="GazeScoringSystem.ReportBonus(float)"/> üzerindendir.
/// </summary>
public class MovingGazeDotSystem : MonoBehaviour, IGazeEvent
{
    // ──────────────────────────────────────────────
    //  MASTER TOGGLE (DEBUG)
    // ──────────────────────────────────────────────
    [Header("Debug / Master Toggle")]
    [Tooltip("Event sistemini tamamen devre dışı bırakır.")]
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

    [Tooltip("Noktanın fiziksel olarak algılanacağı layer")]
    public LayerMask dotLayer;

    [Tooltip("Event aktifken gaze detection'ın duraklatılacağı sistem")]
    public EyeTrackingSystem eyeTracking;

    // ──────────────────────────────────────────────
    //  HAREKET YOLU
    // ──────────────────────────────────────────────
    [Header("Hareket Yolu")]
    [Tooltip("Noktanın hareket edeceği başlangıç ve bitiş noktaları.\n" +
             "İki Transform arasında pingpong şeklinde ileri-geri gider.")]
    public Transform pathStart;
    public Transform pathEnd;

    // ──────────────────────────────────────────────
    //  ZAMANLAMA
    // ──────────────────────────────────────────────
    [Header("Zamanlama")]
    [Tooltip("Event toplam süresi (saniye). Sonunda nokta kaybolur.")]
    [Range(1f, 20f)]
    public float totalDuration = 5f;

    [Tooltip("Noktanın dünya-uzayında hareket hızı (metre/saniye)")]
    [Range(0.1f, 5f)]
    public float movementSpeed = 1.2f;

    [Tooltip("Bir event'ten sonraki minimum bekleme süresi")]
    [Range(1f, 120f)]
    public float minSpawnInterval = 20f;

    [Tooltip("Bir event'ten sonraki maksimum bekleme süresi")]
    [Range(1f, 180f)]
    public float maxSpawnInterval = 45f;

    // ──────────────────────────────────────────────
    //  PUAN VE BAŞARI EŞİĞİ
    // ──────────────────────────────────────────────
    [Header("Puan")]
    [Tooltip("Takip başarıyla tamamlandığında kazanılacak bonus puan. " +
             "GazeScoringSystem tarafında 0–15 aralığında clamp edilir.")]
    [Range(0f, 15f)]
    public float bonusPoints = 12f;

    [Tooltip("Başarı için gereken minimum kümülatif bakış oranı.\n" +
             "0.8 = toplam sürenin %80'i boyunca noktaya bakılmalı.")]
    [Range(0.1f, 1f)]
    public float requiredGazeRatio = 0.8f;

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
    [SerializeField] private Shader standardShader;
    [SerializeField] private Shader spritesDefaultShader;
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
    private MovingGazeDot activeDot;
    private Transform camTransform;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void Awake()
    {
        if (mainCamera != null) camTransform = mainCamera.transform;
        Debug.Log("[MovingGazeDotSystem] Initialized.");
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

    public void OnSessionStarted()
    {
        sessionActive = true;
        ScheduleNextSpawn();
        Debug.Log("[MovingGazeDotSystem] Session started, event system armed.");
    }

    public void OnSessionEnded()
    {
        sessionActive = false;
        ForceStop();
        Debug.Log("[MovingGazeDotSystem] Session ended, event system disarmed.");
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

        Debug.Log("[MovingGazeDotSystem] Event force-stopped.");
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
        if (pathStart == null || pathEnd == null)
        {
            Debug.LogWarning("[MovingGazeDotSystem] pathStart/pathEnd not assigned — skipping spawn.");
            return;
        }

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
        GameObject dotObj = new GameObject("MovingGazeDot");
        dotObj.transform.position = pathStart.position;
        dotObj.layer = LayerMaskUtils.ToLayer(dotLayer);

        SphereCollider col = dotObj.AddComponent<SphereCollider>();
        col.radius = colliderRadius;

        activeDot = dotObj.AddComponent<MovingGazeDot>();
        activeDot.Setup(
            pathStart.position, pathEnd.position,
            totalDuration, movementSpeed, dotScale, requiredGazeRatio,
            OnDotSucceeded, OnDotFailed,
            standardShader, spritesDefaultShader, particlesUnlitShader);

        if (eyeTracking != null) eyeTracking.SetPaused(true);

        Debug.Log($"[MovingGazeDotSystem] Dot spawned. Duration: {totalDuration:F1}s, " +
                  $"speed: {movementSpeed:F2}m/s, required ratio: {requiredGazeRatio:F2}");
    }

    // ══════════════════════════════════════════════
    //  UPDATE LOOP — GAZE DETECTION
    // ══════════════════════════════════════════════

    void UpdateActiveDot()
    {
        Ray ray = new Ray(camTransform.position, camTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, dotLayer))
        {
            MovingGazeDot target = hit.collider.GetComponent<MovingGazeDot>();
            if (target != null) target.OnGazed();
        }
    }

    // ══════════════════════════════════════════════
    //  CALLBACK'LER
    // ══════════════════════════════════════════════

    void OnDotSucceeded(float gazeRatio)
    {
        if (scoring != null) scoring.ReportBonus(bonusPoints);
        Debug.Log($"[MovingGazeDotSystem] Track success! Gaze ratio: {gazeRatio:F2}, " +
                  $"awarded {bonusPoints:F1} pts.");

        activeDot = null;
        if (eyeTracking != null) eyeTracking.SetPaused(false);
        coordinator?.Release(this);
    }

    void OnDotFailed(float gazeRatio)
    {
        Debug.Log($"[MovingGazeDotSystem] Track failed. Gaze ratio: {gazeRatio:F2} " +
                  $"(required {requiredGazeRatio:F2}). No points awarded.");

        activeDot = null;
        if (eyeTracking != null) eyeTracking.SetPaused(false);
        coordinator?.Release(this);
    }
}

// ══════════════════════════════════════════════════════════════════
//  MOVING GAZE DOT — Tek bir hareketli noktanın davranışı
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Tek bir MovingGazeDotSystem dot instance'ı.
/// • İki nokta arasında pingpong hareket eder.
/// • Bakış süresini kümülatif olarak toplar.
/// • Süre bitince callback'lerden birini çağırır (başarı/başarısızlık).
/// </summary>
public class MovingGazeDot : MonoBehaviour
{
    // Hareket parametreleri
    private Vector3 pathStart;
    private Vector3 pathEnd;
    private float totalDuration;
    private float movementSpeed;
    private float scale;
    private float requiredGazeRatio;

    // Callback'ler
    private System.Action<float> onSucceeded;
    private System.Action<float> onFailed;

    // Shader referansları
    private Shader standardShader;
    private Shader spritesDefaultShader;
    private Shader particlesUnlitShader;

    // İç durum
    private float elapsed;
    private float cumulativeGazeTime;
    private bool wasGazedThisFrame;
    private bool resolved;
    private float pathDistance;
    private float travelRatePerSecond;

    // Alt bileşenler
    private GameObject sphereObj;
    private Material sphereMat;
    private TrailRenderer trail;
    private ParticleSystem burstVFX;

    // Renkler — tracking hedefi olduğu için mor/pembe tonları kullanıyoruz
    private static readonly Color IDLE_COLOR    = new Color(0.75f, 0.35f, 1f, 0.9f);
    private static readonly Color TRACKED_COLOR = new Color(1f, 0.5f, 0.9f, 1f);
    private static readonly Color TRAIL_START   = new Color(0.75f, 0.35f, 1f, 0.8f);
    private static readonly Color TRAIL_END     = new Color(0.75f, 0.35f, 1f, 0f);
    private static readonly Color VFX_COLOR_A   = new Color(1f, 0.4f, 0.9f, 1f);
    private static readonly Color VFX_COLOR_B   = new Color(0.5f, 0.2f, 1f, 1f);

    // ══════════════════════════════════════════════
    //  KURULUM
    // ══════════════════════════════════════════════

    public void Setup(Vector3 start, Vector3 end,
                      float totalDuration, float movementSpeed,
                      float scale, float requiredGazeRatio,
                      System.Action<float> onSucceeded, System.Action<float> onFailed,
                      Shader standardShader, Shader spritesDefaultShader, Shader particlesUnlitShader)
    {
        this.pathStart = start;
        this.pathEnd = end;
        this.totalDuration = totalDuration;
        this.movementSpeed = movementSpeed;
        this.scale = scale;
        this.requiredGazeRatio = requiredGazeRatio;
        this.onSucceeded = onSucceeded;
        this.onFailed = onFailed;
        this.standardShader = standardShader;
        this.spritesDefaultShader = spritesDefaultShader;
        this.particlesUnlitShader = particlesUnlitShader;

        pathDistance = Vector3.Distance(start, end);
        travelRatePerSecond = pathDistance > 0.001f ? movementSpeed / pathDistance : 0f;

        transform.position = start;

        CreateSphere();
        CreateTrail();
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
            cumulativeGazeTime += Time.deltaTime;
            AnimateTracked();
        }
        else
        {
            AnimateIdle();
        }
        wasGazedThisFrame = false;

        MoveAlongPath();

        if (elapsed >= totalDuration)
        {
            ResolveOnTimeout();
        }
    }

    /// <summary>MovingGazeDotSystem her frame raycast hit olunca çağırır.</summary>
    public void OnGazed()
    {
        if (resolved) return;
        wasGazedThisFrame = true;
    }

    // ══════════════════════════════════════════════
    //  HAREKET
    // ══════════════════════════════════════════════

    void MoveAlongPath()
    {
        // Pingpong: elapsed * rate → [0,1] arası gidip gelen değer.
        float t = Mathf.PingPong(elapsed * travelRatePerSecond, 1f);
        transform.position = Vector3.Lerp(pathStart, pathEnd, t);
    }

    // ══════════════════════════════════════════════
    //  ÇÖZÜMLEME
    // ══════════════════════════════════════════════

    void ResolveOnTimeout()
    {
        resolved = true;
        float ratio = totalDuration > 0.001f
            ? cumulativeGazeTime / totalDuration
            : 0f;

        if (ratio >= requiredGazeRatio)
        {
            PlaySuccessVFX();
            onSucceeded?.Invoke(ratio);
            Destroy(gameObject, 1.5f);
        }
        else
        {
            onFailed?.Invoke(ratio);
            Destroy(gameObject);
        }
    }

    void PlaySuccessVFX()
    {
        sphereMat.color = TRACKED_COLOR;
        sphereMat.SetColor("_EmissionColor", TRACKED_COLOR);
        burstVFX.Play();
        sphereObj.SetActive(false);
        if (trail != null) trail.enabled = false;
    }

    // ══════════════════════════════════════════════
    //  ANİMASYON
    // ══════════════════════════════════════════════

    void AnimateIdle()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 6.28f * 2f) * 0.08f;
        sphereObj.transform.localScale = Vector3.one * (scale * pulse);
        sphereMat.color = IDLE_COLOR;
        sphereMat.SetColor("_EmissionColor", IDLE_COLOR * 0.5f);
    }

    void AnimateTracked()
    {
        // Takip edildiğinde daha parlak ve biraz büyük
        float pulse = 1f + Mathf.Sin(Time.time * 6.28f * 3f) * 0.12f;
        sphereObj.transform.localScale = Vector3.one * (scale * 1.15f * pulse);
        sphereMat.color = TRACKED_COLOR;
        sphereMat.SetColor("_EmissionColor", TRACKED_COLOR * 0.9f);
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
        mat.SetFloat("_Metallic", 0.25f);
        mat.SetFloat("_Glossiness", 0.8f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.5f);

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

    void CreateTrail()
    {
        GameObject trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = Vector3.zero;

        trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.startWidth = scale * 0.6f;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.02f;

        Material trailMat = new Material(spritesDefaultShader);
        trail.material = trailMat;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(TRAIL_START, 0f),
                new GradientColorKey(TRAIL_END, 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
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
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 4f;
        main.startSize = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(VFX_COLOR_A, VFX_COLOR_B);

        var emission = burstVFX.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });

        var shape = burstVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sol = burstVFX.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0, 1, 1, 0));

        var rend = burstVFX.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(particlesUnlitShader);
        rend.material.color = Color.white;
    }
}
