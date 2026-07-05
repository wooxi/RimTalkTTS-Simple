using System.Threading;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Service;

namespace RimTalkTTS.Simple.Provider
{
    public class EdgeTTSProvider : ITTSProvider
    {
        public async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            return await EdgeTTSClient.GenerateSpeechAsync(request, cancellationToken);
        }

        public void Shutdown() { }

        public bool IsApiKeyValid(string apiKey)
        {
            return true;
        }
    }
}
