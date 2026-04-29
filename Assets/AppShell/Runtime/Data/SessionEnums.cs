using System;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public enum PracticeMode
    {
        GuidedPractice = 0,
        FreePractice = 1,
        EvaluationMode = 2,
        ChallengeMode = 3
    }

    [Serializable]
    public enum SessionDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    [Serializable]
    public enum AudiencePreset
    {
        Supportive = 0,
        Neutral = 1,
        Distracted = 2,
        Challenging = 3
    }

    [Serializable]
    public enum FeedbackLevel
    {
        Minimal = 0,
        Standard = 1,
        Detailed = 2
    }

    [Serializable]
    public enum AppPanelType
    {
        Home = 0,
        PracticeMode = 1,
        EnvironmentSelection = 2,
        SessionSetup = 3,
        Ready = 4,
        ResultsSummary = 5,
        Progress = 6,
        Settings = 7,
        PauseOverlay = 8
    }
}
