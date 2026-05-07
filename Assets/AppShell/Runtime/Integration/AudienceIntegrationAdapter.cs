using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    /// <summary>
    /// Bu adaptör, App Shell'in ayarlari (SessionConfig) ile 
    /// Arda'nin Audience Simulation (AudienceBehaviorController) motorunu birlestirir.
    /// Environment (Ortam) sahnelerinde GameManager, AudienceParent veya sahne yöneticisi ustune konulabilir.
    /// </summary>
    public class AudienceIntegrationAdapter : MonoBehaviour
    {
        [Header("Arda'nin Sistemi")]
        [Tooltip("Sahnedeki AudienceBehaviorController (Arda'nin yoneticisi)")]
        public AudienceBehaviorController behaviorController;

        [Header("Bizim Sistem (Scoring)")]
        [Tooltip("Anlik skor hesabi yapan motor (Gaze, vs) - Eger bos birakilirsa otomatik bulunur.")]
        public PerformanceScoringEngine scoringEngine;

        void Start()
        {
            // Eger Inspector uzerinden verilmediyse sahnede otomatik olarak bul.
            if (behaviorController == null)
            {
                behaviorController = Object.FindFirstObjectByType<AudienceBehaviorController>();
            }

            if (scoringEngine == null)
            {
                scoringEngine = Object.FindFirstObjectByType<PerformanceScoringEngine>();
            }

            // 1. App Shell'den ayarlari cek ve zorluk/preset ayarini Arda'nin motoruna aktar
            if (AppRuntimeState.HasInstance && behaviorController != null)
            {
                SessionConfig config = AppRuntimeState.Instance.CurrentSessionConfig;
                
                // Difficulty skor baskısını, Audience ise seyirci toleransını belirler.
                StressLevel stressLevel = ConvertDifficultyToStress(config.DifficultyLevel);
                AudienceTemperament temperament = ConvertAudienceToTemperament(config.AudiencePreset);
                
                behaviorController.ApplyScenario(stressLevel);
                behaviorController.ChangeAudienceTemperament(temperament);
                Debug.Log($"[AudienceIntegrationAdapter] Arda'nin seyirci sistemi Difficulty='{stressLevel}', Audience='{temperament}' modunda baslatildi.");
            }

            // 2. PerformanceScoringEngine'i bulup, Arda'nin Reaction Engine'ine bagla
            if (scoringEngine != null && behaviorController != null && behaviorController.reactionEngine != null)
            {
                behaviorController.reactionEngine.scoringEngine = scoringEngine;
                Debug.Log("[AudienceIntegrationAdapter] ScoringEngine basariyla AudienceReactionEngine'e baglandi. Seyirci tepkileri senin goz temasina gore calisacak.");
            }
            else
            {
                Debug.LogWarning("[AudienceIntegrationAdapter] ScoringEngine veya ReactionEngine bulunamadigi icin performans baglantisi kurulamadi.");
            }
        }

        private StressLevel ConvertDifficultyToStress(SessionDifficulty difficulty)
        {
            if (difficulty == SessionDifficulty.Easy)
                return StressLevel.Easy;
            else if (difficulty == SessionDifficulty.Normal)
                return StressLevel.Medium;
            else if (difficulty == SessionDifficulty.Hard || difficulty == SessionDifficulty.Expert)
                return StressLevel.Hard;

            return StressLevel.Medium;
        }

        private AudienceTemperament ConvertAudienceToTemperament(AudiencePreset audiencePreset)
        {
            if (audiencePreset == AudiencePreset.Supportive)
                return AudienceTemperament.Supportive;
            if (audiencePreset == AudiencePreset.Challenging)
                return AudienceTemperament.Challenging;

            return AudienceTemperament.Neutral;
        }

        // --- PAUSE & RESUME ---
        // (Opsiyonel) Shell'in Pause Menu'sune kolayca hooklanabilir.
        
        public void PauseAudience()
        {
            if (behaviorController != null)
            {
                // İzleyicileri dondurabilir veya idle hale getirebiliriz
                // Time.timeScale = 0 yapildiginda animatörler zaten duracaktir.
                Debug.Log("[AudienceIntegrationAdapter] Seyirci simulasyonu duraklatildi.");
            }
        }

        public void ResumeAudience()
        {
            if (behaviorController != null)
            {
                Debug.Log("[AudienceIntegrationAdapter] Seyirci simulasyonu devam ediyor.");
            }
        }

        // --- SESSION END ---
        // Oturum bittiginde alkislama veya sonuc durumuna gecis
        public void TriggerSessionEnd()
        {
            if (behaviorController != null)
            {
                behaviorController.TriggerSessionEnd();
                Debug.Log("[AudienceIntegrationAdapter] Oturum bitti! Seyirciler alkisliyor.");
            }
        }
    }
}
