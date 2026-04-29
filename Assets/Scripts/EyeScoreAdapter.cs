//selin
using UnityEngine;

public class EyeScoreAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EyeTrackingSystem eyeTrackingSystem;
    [SerializeField] private PerformanceScoringEngine scoringEngine;

    [Header("Tracking")]
    private float totalTrackingTime = 0f;
    private float lookingAtAudienceTime = 0f;

    [Header("Debug")]
    [Range(0f, 1f)] [SerializeField] private float eyeContactRatio = 0f;
    [SerializeField] private bool showDebugLog = false;

    private const float UPDATE_THRESHOLD = 0.001f;
void Start()
{
    if (eyeTrackingSystem != null)
        eyeTrackingSystem.Activate();
}
    void Update()
    {
        if (eyeTrackingSystem == null || scoringEngine == null) return;
        if (!eyeTrackingSystem.IsActive) return;

        float deltaTime = Time.deltaTime;
        totalTrackingTime += deltaTime;

        // Sadece güvenilir frame'lerde izleyiciye bakılıyorsa say
        if (eyeTrackingSystem.IsGazeValid && eyeTrackingSystem.IsLookingAtAudience)
            lookingAtAudienceTime += deltaTime;

        float newRatio = Mathf.Clamp01(lookingAtAudienceTime / totalTrackingTime);

        if (Mathf.Abs(newRatio - eyeContactRatio) > UPDATE_THRESHOLD)
        {
            eyeContactRatio = newRatio;
            scoringEngine.SetEyeContactRatio(eyeContactRatio);
        }

#if UNITY_EDITOR
        if (showDebugLog)
        {
            Debug.Log($"[EyeScoreAdapter] Ratio={eyeContactRatio:F3} " +
                      $"GazeValid={eyeTrackingSystem.IsGazeValid} " +
                      $"LookingAtAudience={eyeTrackingSystem.IsLookingAtAudience}");
        }
#endif
    }

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
