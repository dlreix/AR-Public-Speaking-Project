using System;
using System.Collections.Generic;

namespace SpeechPipeline
{
    public class LiveState
    {
        public bool   IsSpeaking;
        public float  CurrentRMS;
        public float  CurrentPitchHz;
        public bool   IsInPause;
        public float  PauseElapsed;
        public string PartialTranscript;
    }

    [Serializable]
    public class UtteranceMetrics
    {
        public string       Text;
        public float        WPM;
        public int          WordCount;
        public float        DurationSec;
        public float        AvgPitchHz;
        public float        PitchStdDevHz;
        public int          PauseCount;
        public float        LastPauseSec;
        public List<string> FillerWords = new List<string>();
        public int          FillerCount;
        public float        AvgRMS;
        public float        PeakRMS;
    }

    [Serializable]
    public class SessionData
    {
        public float        TotalSec;
        public float        SpeakingSec;
        public int          PauseCount;
        public float        PauseTotalSec;
        public int          WordCount;
        public float        AvgWpm;
        public float        AvgPitchStdDev;
        public int          FillerCount;
        public List<string> FillerWords;
        public List<string> Transcript;
    }
}
