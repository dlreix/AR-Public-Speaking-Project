using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class TrackingAdapter : MonoBehaviour
    {
        [SerializeField] private EyeTrackingSystem eyeTrackingSystem;
        [SerializeField] private GazeScoringSystem gazeScoringSystem;
        [SerializeField] private CircleEventSystem circleEventSystem;

        public void AutoWireIfNeeded()
        {
            if (eyeTrackingSystem == null)
            {
                eyeTrackingSystem = FindFirstObjectByType<EyeTrackingSystem>(FindObjectsInactive.Include);
            }

            if (gazeScoringSystem == null)
            {
                gazeScoringSystem = FindFirstObjectByType<GazeScoringSystem>(FindObjectsInactive.Include);
            }

            if (circleEventSystem == null)
            {
                circleEventSystem = FindFirstObjectByType<CircleEventSystem>(FindObjectsInactive.Include);
            }

            if (gazeScoringSystem != null && gazeScoringSystem.eyeTracking == null)
            {
                gazeScoringSystem.eyeTracking = eyeTrackingSystem;
            }
        }

        public void Apply(SessionConfig config)
        {
            AutoWireIfNeeded();

            if (config == null)
            {
                return;
            }

            if (config.EyeTrackingEnabled && eyeTrackingSystem == null)
            {
                Debug.LogWarning("[TrackingAdapter] Eye Tracking was requested, but no EyeTrackingSystem exists in the scene.");
            }

            if (config.GazeScoringEnabled && gazeScoringSystem == null)
            {
                Debug.LogWarning("[TrackingAdapter] Gaze Scoring was requested, but no GazeScoringSystem exists in the scene.");
            }

            if (eyeTrackingSystem != null)
            {
                eyeTrackingSystem.enabled = config.EyeTrackingEnabled || config.GazeScoringEnabled;
                eyeTrackingSystem.useXREyeTracking = config.EyeTrackingEnabled;
            }

            if (gazeScoringSystem != null)
            {
                gazeScoringSystem.enabled = config.GazeScoringEnabled;
                if (gazeScoringSystem.eyeTracking == null)
                {
                    gazeScoringSystem.eyeTracking = eyeTrackingSystem;
                }
            }

            if (circleEventSystem != null)
            {
                circleEventSystem.enabled = true;
            }
            else
            {
                Debug.LogWarning("[TrackingAdapter] CircleEventSystem was not found. Visual audience feedback events may be unavailable.");
            }
        }
    }
}
