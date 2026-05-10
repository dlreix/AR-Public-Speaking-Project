using System;
using System.IO;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Presentation
{
    [Serializable]
    public class PresentationDeckReference
    {
        [SerializeField] private string deckId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string sourceExtension = string.Empty;
        [SerializeField] private string sourceHash = string.Empty;
        [SerializeField] private string sourceFilePath = string.Empty;
        [SerializeField] private string importedFilePath = string.Empty;
        [SerializeField] private string importFolderPath = string.Empty;
        [SerializeField] private string manifestPath = string.Empty;
        [SerializeField] private string slideTextPath = string.Empty;
        [SerializeField] private string questionSetPath = string.Empty;
        [SerializeField] private string questionStatus = string.Empty;
        [SerializeField] private int pageCount;
        [SerializeField] private bool isReady;
        [SerializeField] private string errorMessage = string.Empty;

        public string DeckId
        {
            get => deckId;
            set => deckId = value ?? string.Empty;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value ?? string.Empty;
        }

        public string SourceExtension
        {
            get => sourceExtension;
            set => sourceExtension = value ?? string.Empty;
        }

        public string SourceHash
        {
            get => sourceHash;
            set => sourceHash = value ?? string.Empty;
        }

        public string SourceFilePath
        {
            get => sourceFilePath;
            set => sourceFilePath = value ?? string.Empty;
        }

        public string ImportedFilePath
        {
            get => importedFilePath;
            set => importedFilePath = value ?? string.Empty;
        }

        public string ImportFolderPath
        {
            get => importFolderPath;
            set => importFolderPath = value ?? string.Empty;
        }

        public string ManifestPath
        {
            get => manifestPath;
            set => manifestPath = value ?? string.Empty;
        }

        public string SlideTextPath
        {
            get => slideTextPath;
            set => slideTextPath = value ?? string.Empty;
        }

        public string QuestionSetPath
        {
            get => questionSetPath;
            set => questionSetPath = value ?? string.Empty;
        }

        public string QuestionStatus
        {
            get => questionStatus;
            set => questionStatus = value ?? string.Empty;
        }

        public int PageCount
        {
            get => pageCount;
            set => pageCount = Mathf.Max(0, value);
        }

        public bool IsReady
        {
            get => isReady;
            set => isReady = value;
        }

        public string ErrorMessage
        {
            get => errorMessage;
            set => errorMessage = value ?? string.Empty;
        }

        public bool HasPages =>
            isReady &&
            pageCount > 0 &&
            !string.IsNullOrWhiteSpace(importFolderPath);

        public bool HasReadablePages => HasPages && Directory.Exists(importFolderPath);
        public bool HasQuestionSet => !string.IsNullOrWhiteSpace(questionSetPath) && File.Exists(questionSetPath);

        public PresentationDeckReference Clone()
        {
            return (PresentationDeckReference)MemberwiseClone();
        }

        public string GetPageImagePath(int zeroBasedPageIndex)
        {
            int safeIndex = Mathf.Clamp(zeroBasedPageIndex, 0, Mathf.Max(0, pageCount - 1));
            return Path.Combine(importFolderPath ?? string.Empty, $"page_{safeIndex + 1:0000}.png");
        }

        public static PresentationDeckReference Empty()
        {
            return new PresentationDeckReference();
        }
    }
}
