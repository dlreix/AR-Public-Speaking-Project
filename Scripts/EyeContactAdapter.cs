using UnityEngine;

public class EyeContactAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera vrCamera;
    [SerializeField] private PerformanceScoringEngine scoringEngine;

    [Header("Audience Detection")]
    [SerializeField] private LayerMask audienceLayer;
    [SerializeField] private float maxRayDistance = 100f;

    [Header("Tracking")]
    private float totalTrackingTime = 0f;
    private float lookingAtAudienceTime = 0f;

    [Header("Debug")]
    [Range(0f, 1f)] [SerializeField] private float eyeContactRatio = 0f;
    [SerializeField] private bool drawRay = true;

    private const float UPDATE_THRESHOLD = 0.001f;

    void Awake()
    {
        if (vrCamera == null)
            vrCamera = Camera.main;
    }

    void Update()
    {
        if (!IsValid())
            return;

        float deltaTime = Time.deltaTime;
        totalTrackingTime += deltaTime;

        bool isLookingAtAudience = IsLookingAtAudience();

        if (isLookingAtAudience)
            lookingAtAudienceTime += deltaTime;

        UpdateEyeContactRatio();

#if UNITY_EDITOR
        DrawDebugRay(isLookingAtAudience);
#endif
    }

    // ---------------- CORE LOGIC ----------------

    private bool IsLookingAtAudience()
    {
        Ray ray = new Ray(vrCamera.transform.position, vrCamera.transform.forward);
        return Physics.Raycast(ray, maxRayDistance, audienceLayer);
    }

    private void UpdateEyeContactRatio()
    {
        float newRatio = Mathf.Clamp01(lookingAtAudienceTime / totalTrackingTime);

        if (Mathf.Abs(newRatio - eyeContactRatio) > UPDATE_THRESHOLD)
        {
            eyeContactRatio = newRatio;
            scoringEngine.SetEyeContactRatio(eyeContactRatio);
        }
    }

    private bool IsValid()
    {
        return vrCamera != null && scoringEngine != null;
    }

    // ---------------- DEBUG ----------------

    private void DrawDebugRay(bool isLooking)
    {
        if (!drawRay) return;

        Debug.DrawRay(
            vrCamera.transform.position,
            vrCamera.transform.forward * maxRayDistance,
            isLooking ? Color.green : Color.red
        );
    }

    // ---------------- PUBLIC API ----------------

    public void ResetTracking()
    {
        totalTrackingTime = 0f;
        lookingAtAudienceTime = 0f;
        eyeContactRatio = 0f;
    }

    public float GetEyeContactRatio()
    {
        return eyeContactRatio;
    }
}