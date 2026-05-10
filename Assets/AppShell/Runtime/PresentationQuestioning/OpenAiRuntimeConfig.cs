using System;
using System.IO;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    [Serializable]
    public class OpenAiRuntimeConfig
    {
        public string apiKey = string.Empty;
        public string baseUrl = "https://api.openai.com/v1/responses";
        public string model = "gpt-4.1-mini";
        public int requestTimeoutSeconds = 45;
        public int maxQuestions = 3;

        public bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);

        public static bool HasUsableConfiguration()
        {
            return Load().HasApiKey;
        }

        public static OpenAiRuntimeConfig Load()
        {
            var config = new OpenAiRuntimeConfig();
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "OpenAI", "openai_config.local.json");
            string persistentPath = Path.Combine(Application.persistentDataPath, "OpenAI", "openai_config.local.json");

            LoadFileIfExists(streamingPath, config);
            LoadFileIfExists(persistentPath, config);

            string envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                config.apiKey = envKey.Trim();
            }

            config.maxQuestions = Mathf.Clamp(config.maxQuestions <= 0 ? 3 : config.maxQuestions, 1, 5);
            config.requestTimeoutSeconds = Mathf.Clamp(config.requestTimeoutSeconds <= 0 ? 45 : config.requestTimeoutSeconds, 10, 120);
            if (string.IsNullOrWhiteSpace(config.baseUrl))
            {
                config.baseUrl = "https://api.openai.com/v1/responses";
            }

            if (string.IsNullOrWhiteSpace(config.model))
            {
                config.model = "gpt-4.1-mini";
            }

            return config;
        }

        private static void LoadFileIfExists(string path, OpenAiRuntimeConfig target)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || target == null)
            {
                return;
            }

            try
            {
                OpenAiRuntimeConfig fileConfig = JsonUtility.FromJson<OpenAiRuntimeConfig>(File.ReadAllText(path));
                if (fileConfig == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(fileConfig.apiKey)) target.apiKey = fileConfig.apiKey;
                if (!string.IsNullOrWhiteSpace(fileConfig.baseUrl)) target.baseUrl = fileConfig.baseUrl;
                if (!string.IsNullOrWhiteSpace(fileConfig.model)) target.model = fileConfig.model;
                if (fileConfig.requestTimeoutSeconds > 0) target.requestTimeoutSeconds = fileConfig.requestTimeoutSeconds;
                if (fileConfig.maxQuestions > 0) target.maxQuestions = fileConfig.maxQuestions;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OpenAiRuntimeConfig] Could not read config at {path}: {exception.Message}");
            }
        }
    }
}
