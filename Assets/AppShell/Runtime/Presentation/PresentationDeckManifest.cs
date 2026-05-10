using System;

namespace VRPublicSpeaking.AppShell.Presentation
{
    [Serializable]
    public class PresentationDeckManifest
    {
        public string deckId;
        public string displayName;
        public string sourceExtension;
        public string sourceFileName;
        public string sourceHash;
        public string importedFileName;
        public int pageCount;
        public long createdUnixTime;
        public string[] pages;
    }
}
