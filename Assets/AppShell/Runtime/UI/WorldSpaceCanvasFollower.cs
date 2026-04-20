using UnityEngine;

namespace VRPublicSpeaking.AppShell.UI
{
    public class WorldSpaceCanvasFollower : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 offset = new Vector3(0f, -0.18f, 1.2f);
        [SerializeField] private bool yawOnly = true;
        [SerializeField] private bool keepUpright;
        [SerializeField] private float positionLerpSpeed = 10f;
        [SerializeField] private float rotationLerpSpeed = 10f;
        [SerializeField] private bool followContinuously = true;

        private bool hasSnappedToTarget;

        public void Configure(
            Transform target,
            Vector3 canvasOffset,
            bool useYawOnly,
            bool keepWorldUpright,
            float positionSpeed,
            float rotationSpeed)
        {
            followTarget = target;
            offset = canvasOffset;
            yawOnly = useYawOnly;
            keepUpright = keepWorldUpright;
            positionLerpSpeed = positionSpeed;
            rotationLerpSpeed = rotationSpeed;
            followContinuously = true;
            hasSnappedToTarget = false;
        }

        public void SetFollowContinuously(bool shouldFollowContinuously)
        {
            followContinuously = shouldFollowContinuously;
            hasSnappedToTarget = false;
        }

        public void SnapToTarget()
        {
            Transform target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            ApplyPose(target, true);
            hasSnappedToTarget = true;
        }

        private void OnEnable()
        {
            hasSnappedToTarget = false;
        }

        private void LateUpdate()
        {
            Transform target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            if (!followContinuously)
            {
                if (!hasSnappedToTarget)
                {
                    ApplyPose(target, true);
                    hasSnappedToTarget = true;
                }

                return;
            }

            ApplyPose(target, false);
        }

        private void ApplyPose(Transform target, bool immediate)
        {
            Vector3 forward = yawOnly
                ? Vector3.ProjectOnPlane(target.forward, Vector3.up)
                : target.forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 up = yawOnly || keepUpright ? Vector3.up : target.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = target.right;
            }

            right.Normalize();

            if (!yawOnly && keepUpright)
            {
                up = Vector3.Cross(forward, right);
                if (up.sqrMagnitude < 0.0001f)
                {
                    up = Vector3.up;
                }

                up.Normalize();
            }

            Vector3 desiredPosition =
                target.position +
                right * offset.x +
                up * offset.y +
                forward * offset.z;

            // World-space canvases render their readable face opposite the transform forward.
            // Looking away from the camera keeps the front side visible instead of mirrored.
            Vector3 desiredLookDirection = desiredPosition - target.position;
            if (desiredLookDirection.sqrMagnitude < 0.0001f)
            {
                desiredLookDirection = forward;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(desiredLookDirection, up);

            if (immediate)
            {
                transform.position = desiredPosition;
                transform.rotation = desiredRotation;
                return;
            }

            float positionT = positionLerpSpeed <= 0f
                ? 1f
                : Mathf.Clamp01(Time.unscaledDeltaTime * positionLerpSpeed);
            float rotationT = rotationLerpSpeed <= 0f
                ? 1f
                : Mathf.Clamp01(Time.unscaledDeltaTime * rotationLerpSpeed);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }

        private Transform ResolveTarget()
        {
            if (followTarget != null && followTarget.gameObject.activeInHierarchy)
            {
                return followTarget;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                followTarget = mainCamera.transform;
                return followTarget;
            }

            Camera fallbackCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            if (fallbackCamera != null)
            {
                followTarget = fallbackCamera.transform;
            }

            return followTarget;
        }
    }
}
