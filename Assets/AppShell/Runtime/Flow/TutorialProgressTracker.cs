using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Flow
{
    /// <summary>
    /// Tracks which tutorial panels the user has visited and completed.
    /// Supports proximity-based activation: panels "wake up" when the player is nearby.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialProgressTracker : MonoBehaviour
    {
        [SerializeField] private float proximityActivationRadius = 3.2f;
        [SerializeField] private float completionDwellSeconds = 4f;
        [SerializeField] private bool autoDetectCamera = true;

        private Transform cameraTransform;
        private readonly Dictionary<string, PanelProgress> panelStates = new Dictionary<string, PanelProgress>();
        private readonly List<string> orderedPanelIds = new List<string>();

        /// <summary>Raised the first time a specific panel enters proximity range.</summary>
        public event Action<string> PanelFirstVisited;

        /// <summary>Raised when a panel's dwell timer completes.</summary>
        public event Action<string> PanelCompleted;

        /// <summary>Raised every time the overall completion percentage changes.</summary>
        public event Action<float> OverallProgressChanged;

        public float ProximityRadius => proximityActivationRadius;
        public int TotalPanels => orderedPanelIds.Count;

        public float OverallProgress
        {
            get
            {
                if (orderedPanelIds.Count == 0) return 0f;
                int completed = 0;
                foreach (var kvp in panelStates)
                {
                    if (kvp.Value.IsCompleted) completed++;
                }
                return (float)completed / orderedPanelIds.Count;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                if (autoDetectCamera && Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }

                if (cameraTransform == null)
                {
                    return;
                }
            }

            Vector3 cameraPosition = cameraTransform.position;
            float previousProgress = OverallProgress;

            foreach (var kvp in panelStates)
            {
                PanelProgress progress = kvp.Value;
                if (progress.WorldTransform == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(cameraPosition, progress.WorldTransform.position);
                bool inRange = distance <= proximityActivationRadius;

                if (inRange && !progress.HasBeenVisited)
                {
                    progress.HasBeenVisited = true;
                    progress.VisitedTime = Time.time;
                    PanelFirstVisited?.Invoke(kvp.Key);
                }

                if (inRange && progress.HasBeenVisited && !progress.IsCompleted)
                {
                    progress.DwellAccumulator += Time.deltaTime;
                    if (progress.DwellAccumulator >= completionDwellSeconds)
                    {
                        progress.IsCompleted = true;
                        progress.CompletedTime = Time.time;
                        PanelCompleted?.Invoke(kvp.Key);
                    }
                }

                progress.IsInRange = inRange;
            }

            float newProgress = OverallProgress;
            if (Mathf.Abs(newProgress - previousProgress) > 0.001f)
            {
                OverallProgressChanged?.Invoke(newProgress);
            }
        }

        /// <summary>
        /// Register a tutorial panel so the tracker can follow it.
        /// </summary>
        public void RegisterPanel(string panelId, Transform worldTransform)
        {
            if (string.IsNullOrWhiteSpace(panelId) || worldTransform == null)
            {
                return;
            }

            if (!panelStates.ContainsKey(panelId))
            {
                panelStates[panelId] = new PanelProgress { WorldTransform = worldTransform };
                orderedPanelIds.Add(panelId);
            }
            else
            {
                panelStates[panelId].WorldTransform = worldTransform;
            }
        }

        public bool IsPanelVisited(string panelId)
        {
            return panelStates.TryGetValue(panelId, out var p) && p.HasBeenVisited;
        }

        public bool IsPanelCompleted(string panelId)
        {
            return panelStates.TryGetValue(panelId, out var p) && p.IsCompleted;
        }

        public bool IsPanelInRange(string panelId)
        {
            return panelStates.TryGetValue(panelId, out var p) && p.IsInRange;
        }

        public float GetPanelDwellProgress(string panelId)
        {
            if (!panelStates.TryGetValue(panelId, out var p))
            {
                return 0f;
            }

            if (p.IsCompleted) return 1f;
            return completionDwellSeconds > 0f ? Mathf.Clamp01(p.DwellAccumulator / completionDwellSeconds) : 0f;
        }

        public int GetCompletedCount()
        {
            int count = 0;
            foreach (var kvp in panelStates)
            {
                if (kvp.Value.IsCompleted) count++;
            }
            return count;
        }

        public void SetCameraTransform(Transform cam)
        {
            cameraTransform = cam;
        }

        private class PanelProgress
        {
            public Transform WorldTransform;
            public bool HasBeenVisited;
            public bool IsCompleted;
            public bool IsInRange;
            public float DwellAccumulator;
            public float VisitedTime;
            public float CompletedTime;
        }
    }
}
