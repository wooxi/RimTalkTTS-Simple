namespace RimTalkTTS.Simple.Data
{
    public class EndpointTestResult
    {
        public bool Success;
        public string ErrorMessage;
        public int HttpStatusCode;
        public long LatencyMs;
        public long AudioSizeBytes;
        public string RequestJson;
        public string ResponseJson;
        public string RequestUrl;
    }
}
