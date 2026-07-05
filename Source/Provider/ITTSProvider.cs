using System.Threading;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Service;

namespace RimTalkTTS.Simple.Provider
{
    public interface ITTSProvider
    {
        Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default);
        void Shutdown();
        bool IsApiKeyValid(string apiKey);
    }
}
