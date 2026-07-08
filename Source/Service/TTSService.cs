using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Provider;
using RimTalkTTS.Simple.Util;
using RimTalkPatches = RimTalkTTS.Simple.Patch.RimTalkPatches;
using Verse;
using RimWorld;

namespace RimTalkTTS.Simple.Service
{
    public static class TTSService
    {
        private static ITTSProvider _edgeProvider = new EdgeTTSProvider();
        private static ITTSProvider _mimoProvider = new MiMoTTSProvider();

        private static readonly Regex RichTextPattern = new Regex(@"<[^>]*>", RegexOptions.Compiled);

        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return RichTextPattern.Replace(text, "").Trim();
        }

        public static async Task<byte[]> GenerateSpeechAsync(string text, string persona, Pawn pawn, TTSSettings settings, TTSEventLog evtLog = null)
        {
            text = StripRichTextTags(text);
            if (string.IsNullOrEmpty(text)) return null;
            ITTSProvider provider = settings.Provider == TTSSettings.TTSProvider.MiMoTTS
                ? _mimoProvider
                : _edgeProvider;

            string voice;
            string model = null;
            string apiKey = "";

            string perPawnVoice = PawnVoiceManager.GetVoiceModel(pawn);
            if (perPawnVoice == PawnVoiceManager.NONE) return null;

            if (settings.Provider == TTSSettings.TTSProvider.MiMoTTS)
            {
                model = settings.GetModelForPawn(pawn);
                bool isVoiceDesignModel = (model ?? "").Contains("voicedesign");
                voice = isVoiceDesignModel ? null
                    : (perPawnVoice != PawnVoiceManager.DEFAULT ? perPawnVoice : settings.MiMoVoice);
                apiKey = settings.GetEffectiveApiKey();

                if (evtLog != null)
                {
                    evtLog.Model = model;
                    evtLog.Voice = isVoiceDesignModel ? "(voice design)" : voice;
                }
            }
            else
            {
                voice = perPawnVoice != PawnVoiceManager.DEFAULT ? perPawnVoice : settings.EdgeVoice;

                if (evtLog != null)
                {
                    evtLog.Model = "";
                    evtLog.Voice = voice;
                }
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
                EnableStreaming = settings.EnableStreaming,
                BaseUrl = settings.GetEffectiveEndpointUrl(),
                EventLog = evtLog
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
