using Verse;

namespace RimTalkTTS.Simple.Util
{
    public static class TTSLogger
    {
        private const string Tag = "[RimTalkTTS.Simple]";

        public static void Info(object message, string category = null)
        {
            string cat = category != null ? $" [{category}]" : "";
            Log.Message($"{Tag}{cat} {message}");
        }

        public static void Debug(object message, string category = null)
        {
            if (Prefs.LogVerbose)
            {
                string cat = category != null ? $" [{category}]" : "";
                Log.Message($"{Tag}{cat} {message}");
            }
        }

        public static void Warning(object message, string category = null)
        {
            string cat = category != null ? $" [{category}]" : "";
            Log.Warning($"{Tag}{cat} {message}");
        }

        public static void Error(object message, string category = null)
        {
            string cat = category != null ? $" [{category}]" : "";
            Log.Error($"{Tag}{cat} {message}");
        }
    }
}
