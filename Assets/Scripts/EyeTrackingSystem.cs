using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Göz/kafa takip sistemi — v3 (Stabilite odaklı, skor harici)
///
/// Bu script SADECE bakış tespiti, stabilizasyon ve heatmap ile ilgilenir.
/// Puanlama işlemi GazeScoringSystem tarafından yapılır.
///
/// Dışarıya açılan veriler (GazeScoringSystem bunları okur):
///   • IsLookingAtAudience  — bu frame'de izleyiciye bakılıyor mu
///   • IsGazeValid          — bu frame'de veri güvenilir mi
///   • SmoothedHeadSpeed    — yumuşatılmış kafa hızı (°/s)
///   • IsStareWarning       — uzun bakış uyarısı aktif mi
///
/// XR Origin içindeki Main Camera'ya atanır.
/// </summary>
public class EyeTrackingSystem : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  GAZE AYARLARI
    // ──────────────────────────────────────────────
    [Header("Gaze Ayarları")]
    [Tooltip("Bakış noktası kaydedilmesi için gereken minimum süre (saniye)")]
    [Range(0.01f, 1f)]
    public float dwellTime = 0.1f;

    [Tooltip("Raycast maksimum mesafesi")]
    public float maxDistance = 100f;

    [Tooltip("Raycast'in hangi layer'lara çarpacağı")]
    public LayerMask gazeLayer;

    // ──────────────────────────────────────────────
    //  VR GÖZ TAKİBİ
    // ──────────────────────────────────────────────
    [Header("VR Göz Takibi")]
    [Tooltip("VR cihazından XR göz takibi verisi kullan.\n" +
             "Devre dışı bırakılırsa veya cihaz desteklemiyorsa kafa yönü kullanılır.")]
    public bool useXREyeTracking = true;

    // ──────────────────────────────────────────────
    //  HEATMAP AYARLARI
    // ──────────────────────────────────────────────
    [Header("Heatmap")]
    [Tooltip("Isı noktası prefab'ı (küçük Sphere)")]
    public GameObject heatmapDotPrefab;

    [Tooltip("Bu yarıçap içindeki noktalar birleştirilir")]
    [Range(0.05f, 1f)]
    public float dotMergeRadius = 0.15f;

    // ──────────────────────────────────────────────
    //  UZUN BAKIŞ UYARISI
    // ──────────────────────────────────────────────
    [Header("Uzun Bakış Uyarısı")]
    [Tooltip("Aynı bölgeye bu kadar saniye bakınca uyarı gelir")]
    [Range(1f, 10f)]
    public float stareWarningTime = 3.5f;

    // ──────────────────────────────────────────────
    //  KAFA HIZI UYARISI
    // ──────────────────────────────────────────────
    [Header("Kafa Hızı Uyarısı")]
    [Tooltip("Uyarı tetikleyen açısal hız (derece/saniye)")]
    [Range(50f, 400f)]
    public float headWarningSpeed = 150f;

    [Tooltip("Hız ölçümünü yumuşatma penceresi (saniye)")]
    [Range(0.05f, 0.5f)]
    public float headSmoothingWindow = 0.2f;

    [Tooltip("İki uyarı arası minimum bekleme süresi")]
    [Range(1f, 10f)]
    public float headWarningCooldown = 3f;

    [Tooltip("Uyarı tetiklenmesi için gereken üst üste hızlı hareket sayısı")]
    [Range(1, 10)]
    public int rapidCountThreshold = 3;

    [Tooltip("Hızlı hareket sayma penceresi (saniye)")]
    [Range(0.5f, 5f)]
    public float rapidCountWindow = 2f;

    // ──────────────────────────────────────────────
    //  STABİLİTE AYARLARI
    // ──────────────────────────────────────────────
    [Header("Stabilite — Gaze Yumuşatma")]
    [Tooltip("Bakış yönü EMA yumuşatma faktörü.\n" +
             "Düşük = daha pürüzsüz ama gecikmeli, Yüksek = duyarlı ama titrek")]
    [Range(0.05f, 0.8f)]
    public float gazeSmoothingFactor = 0.3f;

    [Tooltip("Tek karede bu dereceden fazla sıçrama → outlier olarak reddedilir")]
    [Range(3f, 30f)]
    public float gazeOutlierThreshold = 15f;

    [Tooltip("Üst üste bu kadar outlier frame sonrası yeni konuma atla")]
    [Range(2, 15)]
    public int outlierToleranceFrames = 5;

    [Header("Stabilite — Frame-Drop Koruması")]
    [Tooltip("DeltaTime bu değeri aşarsa frame-drop sayılır ve işlem atlanır")]
    [Range(0.04f, 0.2f)]
    public float maxAllowedDeltaTime = 0.1f;

    // ──────────────────────────────────────────────
    //  İZLEYİCİ TAG
    // ──────────────────────────────────────────────
    [Header("İzleyici Tespiti")]
    [Tooltip("İzleyiciye bakıyor sayılması için hit objesinin tag'i.\n" +
             "Boş bırakılırsa herhangi bir hit 'izleyiciye bakıyor' sayılır.")]
    public string audienceTag = "Audience";

    // ──────────────────────────────────────────────
    //  DIŞARIYA AÇIK VERİLER
    // ──────────────────────────────────────────────

    /// <summary>Toplam heatmap nokta sayısı.</summary>
    public int TotalPoints => heatmapPoints.Count;

    /// <summary>Sistem aktif mi?</summary>
    public bool IsActive => isActive;

    /// <summary>Uzun bakış uyarısı aktif mi?</summary>
    public bool IsStareWarning => stareWarningActive;

    /// <summary>Hızlı kafa hareketi uyarısı aktif mi?</summary>
    public bool IsHeadWarning => headWarningActive;

    /// <summary>Bu frame'de izleyiciye bakılıyor mu?</summary>
    public bool IsLookingAtAudience => lookingAtAudience;

    /// <summary>Bu frame'deki gaze verisi güvenilir mi?</summary>
    public bool IsGazeValid => !lastFrameWasDropped && consecutiveOutliers == 0;

    /// <summary>Yumuşatılmış kafa açısal hızı (derece/saniye).</summary>
    public float SmoothedHeadSpeed => headAngularSpeed;

    // ──────────────────────────────────────────────
    //  ÖZEL DEĞİŞKENLER
    // ──────────────────────────────────────────────

    private bool isActive;
    private bool isPaused;
    private bool debugVisible;
    private bool reviewMode;

    private Transform currentTarget;
    private float gazeTimer;
    private bool lookingAtAudience;

    private Vector3 stareAnchor;
    private float stareTimer;
    private bool stareWarningActive;
    private const float STARE_ANCHOR_RADIUS = 0.5f;

    private Quaternion lastHeadRotation;
    private float headAngularSpeed;
    private float lastHeadWarningTime = -999f;
    private int rapidMoveCount;
    private float firstRapidTime;
    private bool headWarningActive;

    private readonly List<HeatmapPoint> heatmapPoints = new List<HeatmapPoint>();

    private Transform cachedTransform;

    // Stabilite
    private Vector3 smoothedGazeDir;
    private bool gazeInitialized;
    private int consecutiveOutliers;
    private Vector3 lastRawGazeDir;
    private bool lastFrameWasDropped;

    // XR cihaz önbelleği
    private readonly List<InputDevice> cachedEyeDevices = new List<InputDevice>();
    private float eyeDeviceRefreshTimer;
    private const float EYE_DEVICE_REFRESH_INTERVAL = 2f;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void Awake()
    {
        cachedTransform = transform;
    }

    void Update()
    {
        if (!isActive) return;

        RefreshEyeDevicesIfNeeded();

        if (!isPaused)
        {
            bool frameDrop = Time.deltaTime > maxAllowedDeltaTime;
            lastFrameWasDropped = frameDrop;

            if (!frameDrop)
            {
                ProcessGaze();
                ProcessHeadSpeed();
            }
            else
            {
                lastHeadRotation = cachedTransform.rotation;
                lookingAtAudience = false;
            }
        }
    }

    // ══════════════════════════════════════════════
    //  KAMU API
    // ══════════════════════════════════════════════

    public void Activate()
    {
        ClearHeatmapData();
        isActive = true;
        isPaused = false;
        reviewMode = false;
        stareWarningActive = false;
        headWarningActive = false;
        lookingAtAudience = false;
        stareTimer = 0f;
        gazeTimer = 0f;
        headAngularSpeed = 0f;
        rapidMoveCount = 0;
        lastHeadRotation = cachedTransform.rotation;

        gazeInitialized = false;
        consecutiveOutliers = 0;
        lastFrameWasDropped = false;
    }

    public void Deactivate()
    {
        isActive = false;
        stareWarningActive = false;
        headWarningActive = false;
        lookingAtAudience = false;
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (paused)
        {
            stareWarningActive = false;
            headWarningActive = false;
            lookingAtAudience = false;
            stareTimer = 0f;
        }
    }

    public void SetDebugVisible(bool visible)
    {
        debugVisible = visible;
        bool shouldShow = visible && isActive;
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
        {
            GameObject visual = heatmapPoints[i].visual;
            if (visual != null) visual.SetActive(shouldShow);
        }
    }

    public void EnterReviewMode()
    {
        reviewMode = true;
        float maxIntensity = GetMaxIntensity();
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
        {
            HeatmapPoint p = heatmapPoints[i];
            if (p.visual != null)
            {
                p.visual.SetActive(true);
                UpdateDotVisual(p, maxIntensity, true);
            }
        }
    }

    public void ExitReviewMode()
    {
        reviewMode = false;
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
        {
            GameObject visual = heatmapPoints[i].visual;
            if (visual != null) visual.SetActive(false);
        }
    }

    // ══════════════════════════════════════════════
    //  GAZE İŞLEME (STABİLİZE)
    // ══════════════════════════════════════════════

    Vector3 GetRawGazeDirection()
    {
        if (useXREyeTracking && cachedEyeDevices.Count > 0)
        {
            if (cachedEyeDevices[0].TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes) &&
                eyes.TryGetFixationPoint(out Vector3 fixationPoint))
            {
                return (fixationPoint - cachedTransform.position).normalized;
            }
        }
        return cachedTransform.forward;
    }

    Ray GetStabilizedGazeRay()
    {
        Vector3 rawDir = GetRawGazeDirection();

        if (!gazeInitialized)
        {
            smoothedGazeDir = rawDir;
            lastRawGazeDir = rawDir;
            gazeInitialized = true;
            consecutiveOutliers = 0;
            return new Ray(cachedTransform.position, smoothedGazeDir);
        }

        float angleDelta = Vector3.Angle(lastRawGazeDir, rawDir);

        if (angleDelta > gazeOutlierThreshold)
        {
            consecutiveOutliers++;
            if (consecutiveOutliers >= outlierToleranceFrames)
            {
                smoothedGazeDir = rawDir;
                lastRawGazeDir = rawDir;
                consecutiveOutliers = 0;
            }
        }
        else
        {
            consecutiveOutliers = 0;
            lastRawGazeDir = rawDir;

            float tau = Mathf.Max(0.01f, (1f - gazeSmoothingFactor) / gazeSmoothingFactor * 0.016f);
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / tau);
            smoothedGazeDir = Vector3.Slerp(smoothedGazeDir, rawDir, alpha).normalized;
        }

        return new Ray(cachedTransform.position, smoothedGazeDir);
    }

    void ProcessGaze()
    {
        Ray ray = GetStabilizedGazeRay();

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, gazeLayer))
        {
            ResetGazeState();
            lookingAtAudience = false;
            return;
        }

        lookingAtAudience = string.IsNullOrEmpty(audienceTag) || hit.transform.CompareTag(audienceTag);

        if (hit.transform == currentTarget)
        {
            gazeTimer += Time.deltaTime;
            if (gazeTimer >= dwellTime)
            {
                RegisterGazePoint(hit.point);
                gazeTimer = 0f;
            }
        }
        else
        {
            currentTarget = hit.transform;
            gazeTimer = 0f;
        }

        if (Vector3.Distance(hit.point, stareAnchor) < STARE_ANCHOR_RADIUS)
        {
            stareTimer += Time.deltaTime;
            stareWarningActive = stareTimer >= stareWarningTime;
        }
        else
        {
            stareAnchor = hit.point;
            stareTimer = 0f;
            stareWarningActive = false;
        }
    }

    void ResetGazeState()
    {
        currentTarget = null;
        gazeTimer = 0f;
        stareTimer = 0f;
        stareWarningActive = false;
    }

    // ══════════════════════════════════════════════
    //  KAFA HIZI (FRAME-RATE BAĞIMSIZ)
    // ══════════════════════════════════════════════

    void ProcessHeadSpeed()
    {
        Quaternion currentRotation = cachedTransform.rotation;
        float deltaAngle = Quaternion.Angle(lastHeadRotation, currentRotation);

        float dt = Time.deltaTime;
        if (dt < 0.0001f)
        {
            lastHeadRotation = currentRotation;
            return;
        }

        float rawSpeed = deltaAngle / dt;
        float smoothAlpha = 1f - Mathf.Exp(-dt / headSmoothingWindow);
        headAngularSpeed = Mathf.Lerp(headAngularSpeed, rawSpeed, smoothAlpha);
        lastHeadRotation = currentRotation;

        if (headAngularSpeed > headWarningSpeed)
        {
            if (rapidMoveCount == 0) firstRapidTime = Time.time;
            rapidMoveCount++;

            if (rapidMoveCount >= rapidCountThreshold &&
                Time.time - lastHeadWarningTime > headWarningCooldown)
            {
                headWarningActive = true;
                lastHeadWarningTime = Time.time;
                rapidMoveCount = 0;
            }
        }

        if (rapidMoveCount > 0 && Time.time - firstRapidTime > rapidCountWindow)
            rapidMoveCount = 0;

        if (headWarningActive && headAngularSpeed < headWarningSpeed * 0.5f)
            headWarningActive = false;
    }

    // ══════════════════════════════════════════════
    //  XR CİHAZ ÖNBELLEĞİ
    // ══════════════════════════════════════════════

    void RefreshEyeDevicesIfNeeded()
    {
        eyeDeviceRefreshTimer -= Time.deltaTime;
        if (eyeDeviceRefreshTimer <= 0f)
        {
            cachedEyeDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.EyeTracking, cachedEyeDevices);
            eyeDeviceRefreshTimer = EYE_DEVICE_REFRESH_INTERVAL;
        }
    }

    // ══════════════════════════════════════════════
    //  HEATMAP
    // ══════════════════════════════════════════════

    void RegisterGazePoint(Vector3 worldPoint)
    {
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
        {
            HeatmapPoint p = heatmapPoints[i];
            if (Vector3.Distance(p.position, worldPoint) < dotMergeRadius)
            {
                p.intensity++;
                if (debugVisible) UpdateDotVisual(p, 10f, false);
                return;
            }
        }

        Vector3 offset = (cachedTransform.position - worldPoint).normalized * 0.01f;
        GameObject visual = Instantiate(heatmapDotPrefab, worldPoint + offset, Quaternion.identity);
        visual.SetActive(debugVisible);

        heatmapPoints.Add(new HeatmapPoint
        {
            position = worldPoint,
            intensity = 1,
            visual = visual
        });

        if (debugVisible) UpdateDotVisual(heatmapPoints[heatmapPoints.Count - 1], 10f, false);
    }

    void UpdateDotVisual(HeatmapPoint point, float maxIntensity, bool isReview)
    {
        float t = Mathf.Clamp01(point.intensity / maxIntensity);

        Color color;
        if (t < 0.33f)
            color = Color.Lerp(Color.green, Color.yellow, t / 0.33f);
        else if (t < 0.66f)
            color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (t - 0.33f) / 0.33f);
        else
            color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.66f) / 0.34f);

        color.a = isReview ? 0.75f : 0.6f;

        Renderer rend = point.visual.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;

        float scale = 0.3f + (t * 0.5f);
        point.visual.transform.localScale = new Vector3(scale, scale, scale);
    }

    float GetMaxIntensity()
    {
        float max = 1f;
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
            if (heatmapPoints[i].intensity > max)
                max = heatmapPoints[i].intensity;
        return max;
    }

    void ClearHeatmapData()
    {
        for (int i = 0, count = heatmapPoints.Count; i < count; i++)
            if (heatmapPoints[i].visual != null)
                Destroy(heatmapPoints[i].visual);
        heatmapPoints.Clear();
    }
}

// ══════════════════════════════════════════════
//  VERİ YAPISI
// ══════════════════════════════════════════════

[System.Serializable]
public class HeatmapPoint
{
    public Vector3 position;
    public int intensity;
    public GameObject visual;
}