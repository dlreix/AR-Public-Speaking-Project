using System;
using System.IO;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    [Serializable]
    public class OpenAiRuntimeConfig
    {
        public string provider = "gemini";
        public string apiKey = string.Empty;
        public string baseUrl = string.Empty;
        public string model = string.Empty;
        public string openAiApiKey = string.Empty;
        public string geminiApiKey = string.Empty;
        public string openAiBaseUrl = "https://api.openai.com/v1/responses";
        public string geminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        public string openAiModel = "gpt-4.1-mini";
        public string geminiModel = "gemini-2.5-flash-lite";
        public int requestTimeoutSeconds = 45;
        public int maxQuestions = 3;

        public bool IsGemini => string.Equals(ResolvedProvider, "gemini", StringComparison.OrdinalIgnoreCase);
        public bool IsOpenAi => string.Equals(ResolvedProvider, "openai", StringComparison.OrdinalIgnoreCase);
        public bool HasValidProvider => IsGemini || IsOpenAi;
        public bool HasApiKey => HasValidProvider && !string.IsNullOrWhiteSpace(ResolvedApiKey);
        public string ResolvedProvider => ResolveProvider(provider);
        public string ResolvedApiKey => ResolveApiKey();
        public string ResolvedBaseUrl => ResolveBaseUrl();
        public string ResolvedModel => ResolveModel();
        public string ProviderDisplayName
        {
            get
            {
                if (IsGemini) return "Gemini";
                if (IsOpenAi) return "OpenAI";
                return string.IsNullOrWhiteSpace(provider) ? "LLM" : provider.Trim();
            }
        }

        public static bool HasUsableConfiguration()
        {
            return Load().HasApiKey;
        }

        public bool TryGetConfigurationError(out string message)
        {
            if (!HasValidProvider)
            {
                string providerName = string.IsNullOrWhiteSpace(provider) ? "(empty)" : provider.Trim();
                message = $"Unsupported LLM provider: {providerName}. Use 'gemini' or 'openai'.";
                return true;
            }

            if (!HasApiKey)
            {
                message = $"Missing {ProviderDisplayName} API key.";
                return true;
            }

            message = string.Empty;
            return false;
        }

        public static OpenAiRuntimeConfig Load()
        {
            var config = new OpenAiRuntimeConfig();
            string streamingLlmPath = Path.Combine(Application.streamingAssetsPath, "LLM", "llm_config.local.json");
            string persistentLlmPath = Path.Combine(Application.persistentDataPath, "LLM", "llm_config.local.json");
            string streamingGeminiPath = Path.Combine(Application.streamingAssetsPath, "Gemini", "gemini_config.local.json");
            string persistentGeminiPath = Path.Combine(Application.persistentDataPath, "Gemini", "gemini_config.local.json");
            string streamingOpenAiPath = Path.Combine(Application.streamingAssetsPath, "OpenAI", "openai_config.local.json");
            string persistentOpenAiPath = Path.Combine(Application.persistentDataPath, "OpenAI", "openai_config.local.json");

            LoadFileIfExists(streamingOpenAiPath, config, "openai");
            LoadFileIfExists(persistentOpenAiPath, config, "openai");
            LoadFileIfExists(streamingGeminiPath, config, "gemini");
            LoadFileIfExists(persistentGeminiPath, config, "gemini");
            LoadFileIfExists(streamingLlmPath, config, string.Empty);
            LoadFileIfExists(persistentLlmPath, config, string.Empty);

            string geminiEnvKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(geminiEnvKey))
            {
                geminiEnvKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            }

            if (!string.IsNullOrWhiteSpace(geminiEnvKey))
            {
                config.provider = "gemini";
                config.geminiApiKey = geminiEnvKey.Trim();
            }

            string openAiEnvKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(openAiEnvKey))
            {
                config.openAiApiKey = openAiEnvKey.Trim();
                if (!config.IsGemini || string.IsNullOrWhiteSpace(config.geminiApiKey))
                {
                    config.provider = "openai";
                }
            }

            config.maxQuestions = Mathf.Clamp(config.maxQuestions <= 0 ? 3 : config.maxQuestions, 1, 5);
            config.requestTimeoutSeconds = Mathf.Clamp(config.requestTimeoutSeconds <= 0 ? 45 : config.requestTimeoutSeconds, 10, 120);
            if (string.IsNullOrWhiteSpace(config.openAiBaseUrl))
            {
                config.openAiBaseUrl = "https://api.openai.com/v1/responses";
            }

            if (string.IsNullOrWhiteSpace(config.geminiBaseUrl))
            {
                config.geminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
            }

            if (string.IsNullOrWhiteSpace(config.openAiModel))
            {
                config.openAiModel = "gpt-4.1-mini";
            }

            if (string.IsNullOrWhiteSpace(config.geminiModel))
            {
                config.geminiModel = "gemini-2.5-flash-lite";
            }

            return config;
        }

        private static void LoadFileIfExists(string path, OpenAiRuntimeConfig target, string providerHint)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || target == null)
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                OpenAiRuntimeConfig fileConfig = JsonUtility.FromJson<OpenAiRuntimeConfig>(json);
                if (fileConfig == null)
                {
                    return;
                }

                bool hasExplicitProvider = HasJsonProperty(json, "provider");
                string effectiveProvider = hasExplicitProvider
                    ? ResolveProvider(fileConfig.provider)
                    : ResolveProvider(providerHint);

                if (!string.IsNullOrWhiteSpace(effectiveProvider))
                {
                    target.provider = effectiveProvider;
                }
                else if (hasExplicitProvider)
                {
                    target.provider = fileConfig.provider?.Trim() ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(fileConfig.apiKey))
                {
                    target.apiKey = fileConfig.apiKey.Trim();
                    if (string.Equals(effectiveProvider, "gemini", StringComparison.OrdinalIgnoreCase))
                    {
                        target.geminiApiKey = target.apiKey;
                    }
                    else if (string.Equals(effectiveProvider, "openai", StringComparison.OrdinalIgnoreCase))
                    {
                        target.openAiApiKey = target.apiKey;
                    }
                }

                if (!string.IsNullOrWhiteSpace(fileConfig.openAiApiKey)) target.openAiApiKey = fileConfig.openAiApiKey.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.geminiApiKey)) target.geminiApiKey = fileConfig.geminiApiKey.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.baseUrl)) target.baseUrl = fileConfig.baseUrl.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.model)) target.model = fileConfig.model.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.openAiBaseUrl)) target.openAiBaseUrl = fileConfig.openAiBaseUrl.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.geminiBaseUrl)) target.geminiBaseUrl = fileConfig.geminiBaseUrl.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.openAiModel)) target.openAiModel = fileConfig.openAiModel.Trim();
                if (!string.IsNullOrWhiteSpace(fileConfig.geminiModel)) target.geminiModel = fileConfig.geminiModel.Trim();
                if (fileConfig.requestTimeoutSeconds > 0) target.requestTimeoutSeconds = fileConfig.requestTimeoutSeconds;
                if (fileConfig.maxQuestions > 0) target.maxQuestions = fileConfig.maxQuestions;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OpenAiRuntimeConfig] Could not read config at {path}: {exception.Message}");
            }
        }

        private string ResolveApiKey()
        {
            if (IsGemini)
            {
                if (!string.IsNullOrWhiteSpace(geminiApiKey)) return geminiApiKey.Trim();
                return !string.IsNullOrWhiteSpace(apiKey) ? apiKey.Trim() : string.Empty;
            }

            if (IsOpenAi)
            {
                if (!string.IsNullOrWhiteSpace(openAiApiKey)) return openAiApiKey.Trim();
                return !string.IsNullOrWhiteSpace(apiKey) ? apiKey.Trim() : string.Empty;
            }

            return string.Empty;
        }

        private string ResolveBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                return baseUrl.Trim();
            }

            if (IsGemini)
            {
                return geminiBaseUrl.TrimEnd('/');
            }

            return IsOpenAi ? openAiBaseUrl.Trim() : string.Empty;
        }

        private string ResolveModel()
        {
            if (!string.IsNullOrWhiteSpace(model))
            {
                return model.Trim();
            }

            if (IsGemini)
            {
                return geminiModel.Trim();
            }

            return IsOpenAi ? openAiModel.Trim() : string.Empty;
        }

        private static string ResolveProvider(string value)
        {
            if (string.Equals(value, "openai", StringComparison.OrdinalIgnoreCase))
            {
                return "openai";
            }

            if (string.Equals(value, "gemini", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "google", StringComparison.OrdinalIgnoreCase))
            {
                return "gemini";
            }

            return string.Empty;
        }

        private static bool HasJsonProperty(string json, string propertyName)
        {
            return !string.IsNullOrWhiteSpace(json) &&
                !string.IsNullOrWhiteSpace(propertyName) &&
                json.IndexOf($"\"{propertyName}\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
