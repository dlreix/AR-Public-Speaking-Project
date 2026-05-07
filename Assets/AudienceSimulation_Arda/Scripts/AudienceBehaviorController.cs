using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StressLevel { Easy, Medium, Hard }
public enum AudienceTemperament { Supportive, Neutral, Challenging }

public enum AudienceState
{
    Idle,
    Attentive,
    Neutral,
    Distracted,
    Bored,
    Applauding,
    Nodding,
    Stretching,
    NoteTaking,
    ChinResting
}

[System.Serializable]
public class StressScenario
{
    public StressLevel level;
    public int audienceSize;
    public float negativeReactionMultiplier;
    public float positiveReactionMultiplier;
    [TextArea] public string description;
}

public class AudienceBehaviorController : MonoBehaviour
{
    [Header("Scenario Settings")]
    public StressLevel currentStressLevel = StressLevel.Medium;
    public AudienceTemperament currentAudienceTemperament = AudienceTemperament.Neutral;
    public List<StressScenario> scenarios = new List<StressScenario>();

    [Header("Core Engines")]
    public AudienceReactionEngine reactionEngine;

    [Header("Audience Members")]
    public List<AudienceMember> audienceMembers = new List<AudienceMember>();

    private StressScenario _activeScenario;

    [Header("Live State")]
    public bool sessionEnded = false;
    private float _sessionStartTime;
    private float _gracePeriod = 8.0f;
    private float _warmupDuration = 10.0f; // 8s + 10s = 18. saniyede tam kapasite

    void Start()
    {
        _sessionStartTime = Time.time;
        ApplyScenario(currentStressLevel);
        foreach (var member in audienceMembers)
            if (member != null) member.SetState(AudienceState.Neutral);
    }

    void Update()
    {
        if (_activeScenario == null || _activeScenario.level != currentStressLevel)
            ApplyScenario(currentStressLevel);

        if (sessionEnded)
        {
            foreach (var member in audienceMembers)
                if (member != null) member.SetState(AudienceState.Applauding);
            return;
        }

        EvaluateAndSetStatePerMember();
    }

    private void EvaluateAndSetStatePerMember()
    {
        if (reactionEngine == null || reactionEngine.scoringEngine == null)
            return; // Wait for engines

        // Get the frame from Engine
        ReactionFrame frame = reactionEngine.currentReaction;
        float negMult = _activeScenario != null ? _activeScenario.negativeReactionMultiplier : 1f;
        float posMult = _activeScenario != null ? _activeScenario.positiveReactionMultiplier : 1f;
        float finalScore = reactionEngine.scoringEngine.GetFinalScore();

        float elapsedTime = Time.time - _sessionStartTime;
        
        // 1) 8 Saniyelik Alışma Süresi (Grace Period)
        if (elapsedTime < _gracePeriod)
        {
            foreach (var m in audienceMembers) if (m != null) m.SetState(AudienceState.Neutral);
            return;
        }

        // 2) Kademeli Devreye Girme (Warmup)
        float warmupMultiplier = Mathf.Clamp01((elapsedTime - _gracePeriod) / _warmupDuration);

        foreach (var member in audienceMembers)
        {
            if (member == null) continue;

            // ---- BİREYSEL SKOR HESABI ----
            // Her öğrencinin kişilik toleransı, genel skoru kendi perspektifinden kaydırır.
            float personalScore = finalScore 
                + GetAudienceTemperamentScoreOffset()
                + (member.personalEyeContactTolerance * 40f)
                + (member.personalWpmTolerance * 0.3f);
            personalScore = Mathf.Clamp(personalScore, 0f, 100f);

            // Difficulty skor baskısıdır: düşük skorda negatif, iyi skorda pozitif tepki eşiğini ayarlar.
            float stressMultiplier = personalScore >= 55f ? posMult : 2f - negMult;
            personalScore = Mathf.Lerp(personalScore, personalScore * stressMultiplier, 0.5f);

            // Warmup: İlk saniyelerde skoru merkeze (50) yakın tut, zamanla gerçeğe çek
            personalScore = Mathf.Lerp(50f, personalScore, warmupMultiplier);

            AudienceState targetState;

            // ---- BİREYSEL STATE SEÇİMİ YENİ (3 BAND - SCORE SCALING) ----
            if (personalScore < 35f)
            {
                // CRITICAL LOW SCORE (< 35)
                if (member.CurrentState == AudienceState.Stretching || member.CurrentState == AudienceState.Distracted)
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    float r = Random.value;
                    if (r > 0.6f) targetState = AudienceState.Stretching; 
                    else targetState = AudienceState.Distracted;
                }

                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 1.0f;
            }
            else if (personalScore < 55f)
            {
                // LOW/NEUTRAL SCORE (35 - 55 Arası)
                // Daha fazla tepki çeşitliliği
                if (member.CurrentState == AudienceState.Distracted || 
                    member.CurrentState == AudienceState.Stretching || 
                    member.CurrentState == AudienceState.NoteTaking || 
                    member.CurrentState == AudienceState.Neutral)
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    float rLow = Random.value;
                    if (rLow > 0.7f) targetState = AudienceState.Distracted; // Telefon/Tablet
                    else if (rLow > 0.45f) targetState = AudienceState.Stretching; // Esneme
                    else if (rLow > 0.25f) targetState = AudienceState.NoteTaking; // Not Tutma
                    else targetState = AudienceState.Neutral; // Sakin bekleme
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = Mathf.InverseLerp(55f, 35f, personalScore);
            }
            else if (personalScore <= 75f)
            {
                // NORMAL/GOOD SCORE (55 - 75 Arası)
                targetState = AudienceState.Neutral;
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }
            else
            {
                // HIGH SCORE (> 75)
                if (member.CurrentState == AudienceState.Nodding ||
                    member.CurrentState == AudienceState.NoteTaking ||
                    member.CurrentState == AudienceState.Attentive)
                {
                    targetState = member.CurrentState;
                    // Force Nodding if eye contact is exceptionally good
                    if (HasDominantFactor(frame, "good_eye_contact") && targetState != AudienceState.Nodding)
                    {
                        targetState = AudienceState.Nodding;
                    }
                }
                else
                {
                    float r = Random.value;
                    if (r > 0.6f || HasDominantFactor(frame, "good_eye_contact"))
                        targetState = AudienceState.Nodding;
                    else if (r > 0.3f)
                        targetState = AudienceState.NoteTaking;
                    else
                        targetState = AudienceState.Attentive;
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }

            targetState = ApplyReactionFrameInfluence(member, targetState, frame, personalScore);

            if (targetState == AudienceState.Stretching)
            {
                Debug.Log($"[Behavior] Member {member.name} - Score: {personalScore:F1} - Target State: {targetState}");
            }

            member.SetState(targetState);
        }
    }

    public void ApplyScenario(StressLevel level)
    {
        _activeScenario = scenarios.Find(s => s.level == level);
        if (_activeScenario == null)
        {
            _activeScenario = CreateDefaultScenario(level);
        }
        currentStressLevel = level;
    }

    private StressScenario CreateDefaultScenario(StressLevel level)
    {
        StressScenario scenario = new StressScenario();
        scenario.level = level;

        switch (level)
        {
            case StressLevel.Easy:
                scenario.audienceSize = 20;
                scenario.negativeReactionMultiplier = 0.85f;
                scenario.positiveReactionMultiplier = 1.1f;
                scenario.description = "Default easy scenario";
                break;
            case StressLevel.Hard:
                scenario.audienceSize = 50;
                scenario.negativeReactionMultiplier = 1.2f;
                scenario.positiveReactionMultiplier = 0.9f;
                scenario.description = "Default hard scenario";
                break;
            case StressLevel.Medium:
            default:
                scenario.audienceSize = 35;
                scenario.negativeReactionMultiplier = 1f;
                scenario.positiveReactionMultiplier = 1f;
                scenario.description = "Default medium scenario";
                break;
        }

        return scenario;
    }

    private float GetAudienceTemperamentScoreOffset()
    {
        switch (currentAudienceTemperament)
        {
            case AudienceTemperament.Supportive:
                return 8f;
            case AudienceTemperament.Challenging:
                return -8f;
            case AudienceTemperament.Neutral:
            default:
                return 0f;
        }
    }

    private AudienceState ApplyReactionFrameInfluence(AudienceMember member, AudienceState targetState, ReactionFrame frame, float personalScore)
    {
        if (frame == null)
        {
            return targetState;
        }

        if (personalScore < 35f)
        {
            if (HasReaction(frame, "phone_usage") || frame.overall_audience_state == AudienceState.Distracted)
            {
                return AudienceState.Distracted;
            }

            if (frame.overall_audience_state == AudienceState.Bored)
            {
                return AudienceState.Stretching;
            }

            return targetState;
        }

        if (personalScore < 55f)
        {
            if (HasReaction(frame, "phone_usage") || frame.overall_audience_state == AudienceState.Distracted)
            {
                return AudienceState.Distracted;
            }

            if (frame.overall_audience_state == AudienceState.Bored)
            {
                return AudienceState.Stretching;
            }

            if (HasReaction(frame, "taking_notes"))
            {
                return IsMemberInReactionGroup(member, 0.2f) ? AudienceState.NoteTaking : targetState;
            }

            return targetState;
        }

        if (personalScore <= 75f)
        {
            if (HasReaction(frame, "taking_notes"))
            {
                return IsMemberInReactionGroup(member, 0.3f) ? AudienceState.NoteTaking : targetState;
            }

            if (HasReaction(frame, "nodding") || HasReaction(frame, "professional_approval"))
            {
                return Random.value > 0.5f ? AudienceState.Nodding : AudienceState.Neutral;
            }

            return targetState;
        }

        if (HasReaction(frame, "taking_notes"))
        {
            if (member.CurrentState == AudienceState.Nodding || member.CurrentState == AudienceState.Attentive)
            {
                return member.CurrentState;
            }

            return IsMemberInReactionGroup(member, 0.35f) ? AudienceState.NoteTaking : targetState;
        }

        if (HasReaction(frame, "nodding") || HasReaction(frame, "professional_approval") || HasReaction(frame, "applause_and_laughs"))
        {
            return AudienceState.Nodding;
        }

        return targetState;
    }

    private static bool HasDominantFactor(ReactionFrame frame, string factor)
    {
        return frame != null
            && frame.dominant_factors != null
            && frame.dominant_factors.Contains(factor);
    }

    private static bool HasReaction(ReactionFrame frame, string reaction)
    {
        return frame != null
            && frame.reactions != null
            && frame.reactions.Contains(reaction);
    }

    private static bool IsMemberInReactionGroup(AudienceMember member, float ratio)
    {
        if (member == null)
        {
            return false;
        }

        int stableBucket = Mathf.Abs(member.GetInstanceID()) % 100;
        return stableBucket < Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f);
    }

    public void TriggerSessionEnd() => sessionEnded = true;
    public void ChangeStressLevel(StressLevel level) => ApplyScenario(level);
    public void ChangeAudienceTemperament(AudienceTemperament temperament) => currentAudienceTemperament = temperament;
}
