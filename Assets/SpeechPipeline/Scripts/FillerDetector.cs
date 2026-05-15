using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpeechPipeline
{
    // Scans a transcript for filler words using whole-word regex boundaries.
    // Word boundaries prevent false positives (e.g. "like" inside "likewise").
    public sealed class FillerDetector
    {
        private static readonly string[] DefaultFillers =
        {
            "um", "uh", "er", "hm", "hmm", "hmmm",
            "like",
            "you know", "you see", "i mean",
            "so", "basically", "literally", "actually",
            "honestly", "right",
            "kind of", "kinda", "sort of", "sorta",
            "at the end of the day", "to be honest",
        };

        private readonly List<(string word, Regex rx)> _fillers;

        public FillerDetector(IEnumerable<string> custom = null)
        {
            _fillers = new List<(string, Regex)>();
            foreach (string f in custom ?? DefaultFillers)
                _fillers.Add((f, new Regex(
                    $@"\b{Regex.Escape(f)}\b",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)));
        }

        // Returns all filler occurrences found, including duplicates.
        public List<string> Detect(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return found;

            string norm = Normalise(text);
            foreach (var (word, rx) in _fillers)
            {
                var matches = rx.Matches(norm);
                for (int i = 0; i < matches.Count; i++)
                    found.Add(word);
            }
            return found;
        }

        private static string Normalise(string text) =>
            Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
    }
}
