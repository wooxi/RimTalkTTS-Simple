namespace RimTalkTTS.Simple.Data
{
    public static class TTSConfig
    {
        public static TTSSettings Settings => TTSModule.Instance?.GetSettings();
        public static bool IsEnabled => TTSModule.Instance?.IsActive ?? false;
        public static bool IsInitialized => Settings != null;
    }
}
