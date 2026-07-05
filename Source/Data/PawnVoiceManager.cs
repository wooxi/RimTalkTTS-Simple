using System.Collections.Generic;
using Verse;

namespace RimTalkTTS.Simple.Data
{
    public static class PawnVoiceManager
    {
        public const string DEFAULT = "DEFAULT";
        public const string NONE = "NONE";

        private static Dictionary<int, string> _pawnVoiceMap = new Dictionary<int, string>();

        public static string GetVoiceModel(Pawn pawn)
        {
            if (pawn == null) return DEFAULT;

            if (_pawnVoiceMap.TryGetValue(pawn.thingIDNumber, out string voiceId)
                && !string.IsNullOrEmpty(voiceId))
                return voiceId;

            return DEFAULT;
        }

        public static void SetVoiceModel(Pawn pawn, string voiceModelId)
        {
            if (pawn == null) return;

            if (string.IsNullOrEmpty(voiceModelId) || voiceModelId == DEFAULT)
                _pawnVoiceMap.Remove(pawn.thingIDNumber);
            else
                _pawnVoiceMap[pawn.thingIDNumber] = voiceModelId;
        }

        public static void RemovePawn(Pawn pawn)
        {
            if (pawn == null) return;
            _pawnVoiceMap.Remove(pawn.thingIDNumber);
        }

        public static void Clear()
        {
            _pawnVoiceMap.Clear();
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref _pawnVoiceMap, "pawnVoiceMapTTSSimple", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && _pawnVoiceMap == null)
                _pawnVoiceMap = new Dictionary<int, string>();
        }
    }
}
