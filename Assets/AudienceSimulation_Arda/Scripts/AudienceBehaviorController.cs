using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StressLevel { Easy, Medium, Hard }

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
    public List<StressScenario> scenarios = new List<StressScenario>();

    [Header("Core Engines")]
    public AudienceReactionEngine reactionEngine;

    [Header("Audience Members")]
    public List<AudienceMember> audienceMembers = new List<AudienceMember>();

    private StressScenario _activeScenario;
    private float _audienceToleranceBias;
    private float _negativeReactionSensitivity = 1f;
    private float _positiveReactionSensitivity = 1f;

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
                if (member != null) member.SetState(AudienceState.Applauding, true);
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
        GetScoreThresholds(out float criticalThreshold, out float lowThreshold, out float highThreshold);

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
                + (member.personalEyeContactTolerance * 40f)
                + (member.personalWpmTolerance * 0.3f)
                + _audienceToleranceBias;
            personalScore = Mathf.Clamp(personalScore, 0f, 100f);

            // Stress seviyesine göre negatif tepkileri güçlendir
            personalScore = Mathf.Lerp(personalScore, personalScore * (2f - (negMult * _negativeReactionSensitivity)), 0.5f);

            // Warmup: İlk saniyelerde skoru merkeze (50) yakın tut, zamanla gerçeğe çek
            personalScore = Mathf.Lerp(50f, personalScore, warmupMultiplier);

            AudienceState targetState;

            // ---- BİREYSEL STATE SEÇİMİ YENİ (3 BAND - SCORE SCALING) ----
            if (personalScore < criticalThreshold)
            {
                if (!member.CanConsiderStateChange && IsNegativeState(member.CurrentState))
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    targetState = ChooseLowEngagementState(frame, true);
                }

                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 1.0f;
            }
            else if (personalScore < lowThreshold)
            {
                if (!member.CanConsiderStateChange && IsLowBandState(member.CurrentState))
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    targetState = ChooseLowEngagementState(frame, false);
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = Mathf.InverseLerp(lowThreshold, criticalThreshold, personalScore);
            }
            else if (personalScore <= highThreshold)
            {
                if (!member.CanConsiderStateChange && IsNeutralBandState(member.CurrentState))
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    targetState = ChooseMediumEngagementState(frame);
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }
            else
            {
                if (!member.CanConsiderStateChange && IsPositiveState(member.CurrentState))
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    targetState = ChooseHighEngagementState(frame, posMult * _positiveReactionSensitivity);
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }

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

    public void ConfigureAudienceTuning(float toleranceBias, float negativeSensitivity, float positiveSensitivity)
    {
        _audienceToleranceBias = toleranceBias;
        _negativeReactionSensitivity = Mathf.Max(0.1f, negativeSensitivity);
        _positiveReactionSensitivity = Mathf.Max(0.1f, positiveSensitivity);
    }

    private void GetScoreThresholds(out float criticalThreshold, out float lowThreshold, out float highThreshold)
    {
        float offset;
        switch (currentStressLevel)
        {
            case StressLevel.Easy:
                offset = -5f;
                break;
            case StressLevel.Hard:
                offset = 7f;
                break;
            default:
                offset = 0f;
                break;
        }

        criticalThreshold = 35f + offset;
        lowThreshold = 55f + offset;
        highThreshold = 75f + offset;
    }

    private AudienceState ChooseLowEngagementState(ReactionFrame frame, bool critical)
    {
        bool eyeContactLow = HasFactor(frame, "eye_contact_low");
        bool monotone = HasFactor(frame, "monotone_voice");
        bool filler = HasFactor(frame, "high_filler_words");

        float roll = Random.value * _negativeReactionSensitivity;
        if (critical && (eyeContactLow || monotone) && roll > 0.25f)
            return AudienceState.Distracted;

        if (filler && roll > 0.65f)
            return AudienceState.ChinResting;

        if (roll > 0.68f)
            return AudienceState.Distracted;
        if (roll > 0.38f)
            return AudienceState.Stretching;
        if (!critical && roll > 0.18f)
            return AudienceState.ChinResting;

        return AudienceState.Neutral;
    }

    private AudienceState ChooseMediumEngagementState(ReactionFrame frame)
    {
        float engagement = frame != null ? frame.engagement_level : 50f;
        if (HasFactor(frame, "good_eye_contact") && Random.value > 0.35f)
            return AudienceState.Nodding;

        if (engagement > 68f && Random.value > 0.45f)
            return AudienceState.NoteTaking;

        if (HasFactor(frame, "high_filler_words") && Random.value > 0.65f)
            return AudienceState.ChinResting;

        return AudienceState.Neutral;
    }

    private AudienceState ChooseHighEngagementState(ReactionFrame frame, float positiveMultiplier)
    {
        float roll = Random.value * positiveMultiplier;
        if (HasFactor(frame, "good_eye_contact") || roll > 0.62f)
            return AudienceState.Nodding;
        if (HasReaction(frame, "taking_notes") || roll > 0.34f)
            return AudienceState.NoteTaking;
        return AudienceState.Attentive;
    }

    private static bool IsNegativeState(AudienceState state)
    {
        return state == AudienceState.Distracted ||
            state == AudienceState.Stretching ||
            state == AudienceState.ChinResting ||
            state == AudienceState.Bored;
    }

    private static bool IsLowBandState(AudienceState state)
    {
        return IsNegativeState(state) || state == AudienceState.Neutral || state == AudienceState.NoteTaking;
    }

    private static bool IsNeutralBandState(AudienceState state)
    {
        return state == AudienceState.Neutral ||
            state == AudienceState.Attentive ||
            state == AudienceState.NoteTaking ||
            state == AudienceState.Nodding;
    }

    private static bool IsPositiveState(AudienceState state)
    {
        return state == AudienceState.Nodding ||
            state == AudienceState.NoteTaking ||
            state == AudienceState.Attentive;
    }

    private static bool HasFactor(ReactionFrame frame, string factor)
    {
        return frame != null && frame.dominant_factors != null && frame.dominant_factors.Contains(factor);
    }

    private static bool HasReaction(ReactionFrame frame, string reaction)
    {
        return frame != null && frame.reactions != null && frame.reactions.Contains(reaction);
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

    public void TriggerSessionEnd() => sessionEnded = true;
    public void ChangeStressLevel(StressLevel level) => ApplyScenario(level);
}
