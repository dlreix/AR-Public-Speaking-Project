using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public static class OpenAiResponsesClient
    {
        [Serializable]
        private class ResponsesRequest
        {
            public string model;
            public string input;
            public float temperature = 0.35f;
            public int max_output_tokens = 1200;
        }

        public static IEnumerator SendPrompt(
            string prompt,
            int maxOutputTokens,
            Action<bool, string> completed)
        {
            OpenAiRuntimeConfig config = OpenAiRuntimeConfig.Load();
            if (!config.HasApiKey)
            {
                completed?.Invoke(false, "Missing API key");
                yield break;
            }

            var requestBody = new ResponsesRequest
            {
                model = config.model,
                input = prompt ?? string.Empty,
                max_output_tokens = Mathf.Clamp(maxOutputTokens, 256, 4096)
            };

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
            using (UnityWebRequest request = new UnityWebRequest(config.baseUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = config.requestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(false, $"LLM request failed: {request.responseCode} {request.error} {responseText}");
                    yield break;
                }

                string output = ExtractResponseText(responseText);
                if (string.IsNullOrWhiteSpace(output))
                {
                    completed?.Invoke(false, "LLM response did not include output text.");
                    yield break;
                }

                completed?.Invoke(true, output.Trim());
            }
        }

        public static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return string.Empty;
            }

            return text.Substring(start, end - start + 1);
        }

        private static string ExtractResponseText(string json)
        {
            string outputText = ExtractJsonStringProperty(json, "output_text");
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return outputText;
            }

            var builder = new StringBuilder();
            int searchIndex = 0;
            while (TryExtractJsonStringProperty(json, "text", ref searchIndex, out string text))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (builder.Length > 0) builder.AppendLine();
                    builder.Append(text);
                }
            }

            return builder.ToString();
        }

        private static string ExtractJsonStringProperty(string json, string property)
        {
            int index = 0;
            return TryExtractJsonStringProperty(json, property, ref index, out string value) ? value : string.Empty;
        }

        private static bool TryExtractJsonStringProperty(string json, string property, ref int searchIndex, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(property))
            {
                return false;
            }

            string needle = $"\"{property}\"";
            int propertyIndex = json.IndexOf(needle, searchIndex, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return false;
            }

            int colonIndex = json.IndexOf(':', propertyIndex + needle.Length);
            int quoteIndex = colonIndex >= 0 ? json.IndexOf('"', colonIndex + 1) : -1;
            if (quoteIndex < 0)
            {
                searchIndex = propertyIndex + needle.Length;
                return false;
            }

            var builder = new StringBuilder();
            bool escaping = false;
            for (int index = quoteIndex + 1; index < json.Length; index++)
            {
                char c = json[index];
                if (escaping)
                {
                    builder.Append(DecodeEscapedChar(c));
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (c == '"')
                {
                    searchIndex = index + 1;
                    value = builder.ToString();
                    return true;
                }

                builder.Append(c);
            }

            searchIndex = json.Length;
            return false;
        }

        private static char DecodeEscapedChar(char c)
        {
            switch (c)
            {
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case '"': return '"';
                case '\\': return '\\';
                case '/': return '/';
                default: return c;
            }
        }
    }
}
