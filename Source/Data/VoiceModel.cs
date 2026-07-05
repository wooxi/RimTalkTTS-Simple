namespace RimTalkTTS.Simple.Data
{
    public class VoiceModel
    {
        public string ModelId;
        public string Name;
        public static readonly string NONE_MODEL_ID = "NONE";

        public VoiceModel() { }

        public VoiceModel(string modelId, string name)
        {
            ModelId = modelId;
            Name = name;
        }
    }
}
