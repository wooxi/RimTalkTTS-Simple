using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Verse;

namespace RimTalkTTS.Simple.Service
{
    public static class MiMoTTSClient
    {
        private const string MIMO_API_URL = "https://api.xiaomimimo.com/v1/chat/completions";
        private static readonly HttpClient _httpClient;

        static MiMoTTSClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
        }

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    Log.Error("[RimTalkTTS.Simple] MiMo TTS: API Key not configured");
                    return null;
                }

                string personaPrompt = string.IsNullOrEmpty(request.Persona)
                    ? "Use a natural, clear speaking voice."
                    : request.Persona;

                bool streaming = request.EnableStreaming;
                string audioFormat = streaming ? "pcm16" : "wav";

                var payload = new
                {
                    model = request.Model ?? "mimo-v2.5-tts",
                    messages = new object[]
                    {
                        new { role = "user", content = personaPrompt },
                        new { role = "assistant", content = request.Input }
                    },
                    audio = new
                    {
                        format = audioFormat,
                        voice = request.Voice ?? "冰糖"
                    },
                    stream = streaming
                };

                string json = JsonConvert.SerializeObject(payload);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, MIMO_API_URL)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("api-key", request.ApiKey);

                if (streaming)
                {
                    return await HandleStreamResponseAsync(httpRequest, cancellationToken);
                }
                else
                {
                    return await HandleNonStreamResponseAsync(httpRequest, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkTTS.Simple] MiMo TTS: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static async Task<byte[]> HandleNonStreamResponseAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await _httpClient.SendAsync(request, ct);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"[RimTalkTTS.Simple] MiMo TTS: HTTP {response.StatusCode}: {responseJson.Substring(0, Math.Min(500, responseJson.Length))}");
                return null;
            }

            var obj = JObject.Parse(responseJson);
            var choices = obj["choices"] as JArray;
            if (choices == null || choices.Count == 0)
            {
                Log.Error("[RimTalkTTS.Simple] MiMo TTS: No choices in response");
                return null;
            }

            var message = choices[0]["message"];
            var audio = message?["audio"];
            var data = audio?["data"]?.ToString();

            if (string.IsNullOrEmpty(data))
            {
                Log.Error("[RimTalkTTS.Simple] MiMo TTS: No audio data in response");
                return null;
            }

            byte[] audioBytes = Convert.FromBase64String(data);

            if (Prefs.DevMode)
            {
                Log.Message($"[RimTalkTTS.Simple] MiMo TTS: Generated {audioBytes.Length} bytes (non-stream)");
            }

            return audioBytes;
        }

        private static async Task<byte[]> HandleStreamResponseAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Log.Error($"[RimTalkTTS.Simple] MiMo TTS stream: HTTP {response.StatusCode}: {errorBody.Substring(0, Math.Min(500, errorBody.Length))}");
                return null;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var pcmChunks = new System.Collections.Generic.List<byte[]>();
                int totalPcmBytes = 0;

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    string dataContent = line.Substring(6);
                    if (dataContent == "[DONE]") break;

                    try
                    {
                        var delta = JObject.Parse(dataContent);
                        var choices = delta["choices"] as JArray;
                        if (choices == null || choices.Count == 0) continue;

                        var deltaObj = choices[0]["delta"];
                        var audio = deltaObj?["audio"];
                        var audioData = audio?["data"]?.ToString();

                        if (!string.IsNullOrEmpty(audioData))
                        {
                            byte[] chunk = Convert.FromBase64String(audioData);
                            pcmChunks.Add(chunk);
                            totalPcmBytes += chunk.Length;
                        }
                    }
                    catch (JsonException) { }
                }

                if (pcmChunks.Count == 0)
                {
                    Log.Error("[RimTalkTTS.Simple] MiMo TTS: No PCM data in stream");
                    return null;
                }

                byte[] allPcm = new byte[totalPcmBytes];
                int offset = 0;
                foreach (var chunk in pcmChunks)
                {
                    Array.Copy(chunk, 0, allPcm, offset, chunk.Length);
                    offset += chunk.Length;
                }

                byte[] wavData = ConvertPcm16ToWav(allPcm, 24000, 1, 16);

                if (Prefs.DevMode)
                {
                    Log.Message($"[RimTalkTTS.Simple] MiMo TTS stream: {pcmChunks.Count} chunks, {totalPcmBytes} bytes PCM, {wavData.Length} bytes WAV");
                }

                return wavData;
            }
        }

        private static byte[] ConvertPcm16ToWav(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int dataSize = pcmData.Length;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM format
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(pcmData);

                return ms.ToArray();
            }
        }
    }
}
