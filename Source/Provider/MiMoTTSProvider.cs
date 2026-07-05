using System;
using System.Threading;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Service;

namespace RimTalkTTS.Simple.Provider
{
    public class MiMoTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await MiMoTTSClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public void Shutdown() { }

        public bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey.Length >= 10;
        }
    }
}
