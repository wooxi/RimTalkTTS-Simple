namespace RimTalkTTS.Simple.Service
{
    public class TTSRequest
    {
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public string Input { get; set; }
        public string Voice { get; set; }
        public string Persona { get; set; }
        public float Speed { get; set; } = 1.0f;
        public float Volume { get; set; } = 1.0f;
        public bool EnableStreaming { get; set; } = false;
    }
}
