using System;
using System.Threading;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Util;
using Verse;

namespace RimTalkTTS.Simple.Service
{
    public static class EdgeTTSClient
    {
        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));

                string voiceName = request.Voice;
                if (string.IsNullOrWhiteSpace(voiceName))
                {
                    voiceName = "zh-CN-XiaoxiaoNeural";
                }

                int ratePercent = (int)((request.Speed - 1.0f) * 100);
                string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

                int volumePercent = (int)((request.Volume - 1.0f) * 100);
                string volumeStr = volumePercent >= 0 ? $"+{volumePercent}%" : $"{volumePercent}%";

                using (var client = new EdgeTTSWebSocketClient())
                {
                    byte[] audioData = await client.SynthesizeAsync(
                        text: request.Input,
                        voice: voiceName,
                        rate: rateStr,
                        volume: volumeStr
                    );

                    if (audioData == null || audioData.Length == 0)
                    {
                        TTSLogger.WarningNotify("Edge TTS: 未收到音频数据", "EdgeTTS");
                        return null;
                    }

                    if (Prefs.DevMode)
                    {
                        TTSLogger.Debug($"EdgeTTS: Generated {audioData.Length} bytes of audio", "EdgeTTS");
                    }

                    return audioData;
                }
            }
            catch (Exception ex)
            {
                TTSLogger.ErrorNotify($"Edge TTS 异常: {ex.GetType().Name}: {ex.Message}", "EdgeTTS");
                return null;
            }
        }
    }
}
