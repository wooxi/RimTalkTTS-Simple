using System;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Provider;
using RimTalkPatches = RimTalkTTS.Simple.Patch.RimTalkPatches;
using Verse;
using RimWorld;

namespace RimTalkTTS.Simple.Service
{
    public static class TTSService
    {
        private static ITTSProvider _edgeProvider = new EdgeTTSProvider();
        private static ITTSProvider _mimoProvider = new MiMoTTSProvider();

        public static async Task<byte[]> GenerateSpeechAsync(string text, string persona, TTSSettings settings)
        {
            ITTSProvider provider = settings.Provider == TTSSettings.TTSProvider.MiMoTTS
                ? _mimoProvider
                : _edgeProvider;

            string voice;
            string model = null;
            string apiKey = "";

            if (settings.Provider == TTSSettings.TTSProvider.MiMoTTS)
            {
                voice = settings.MiMoVoice;
                model = settings.MiMoModel;
                apiKey = settings.MiMoApiKey;
            }
            else
            {
                voice = settings.EdgeVoice;
            }

            var request = new TTSRequest
            {
                ApiKey = apiKey,
                Model = model,
                Input = text,
                Voice = voice,
                Persona = persona,
                Speed = settings.Speed,
                Volume = settings.Volume,
                EnableStreaming = settings.EnableStreaming
            };

            return await provider.GenerateSpeechAsync(request);
        }

        public static string ExtractPersona(Pawn pawn)
        {
            try
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_PersonaData");
                if (def == null) return null;

                var hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(def);
                if (hediff == null) return null;

                var field = hediff.GetType().GetField("Personality");
                if (field == null) return null;

                return field.GetValue(hediff) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string GetPersonaOrDefault(Pawn pawn)
        {
            string persona = ExtractPersona(pawn);
            if (string.IsNullOrWhiteSpace(persona))
            {
                return "Use a natural, clear speaking voice suitable for the character.";
            }
            return persona;
        }

        public static void StopAll(bool permanentShutdown = false)
        {
            if (permanentShutdown)
            {
                _edgeProvider?.Shutdown();
                _mimoProvider?.Shutdown();
                AudioPlaybackService.FullReset();
            }
            else
            {
                AudioPlaybackService.StopAndClear();
            }

            RimTalkPatches.ClearAllBlocks();
        }
    }
}
