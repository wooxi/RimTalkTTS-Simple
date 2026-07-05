using System;
using System.Collections.Generic;

namespace RimTalkTTS.Simple.Util
{
    public static class TTSStats
    {
        public static long TotalRequests { get; private set; }
        public static long TotalSuccess { get; private set; }
        public static long TotalFailed { get; private set; }
        public static long TotalCancelled { get; private set; }
        public static long TotalAudioBytes { get; private set; }
        public static long TotalElapsedMs { get; private set; }
        public static DateTime StartTime { get; private set; }
        public static string LastError { get; private set; }

        private static long _requestsThisMinute;
        private static long _callsThisSecond;
        public static readonly List<long> RequestsPerMinuteHistory = new List<long>();
        private static DateTime _nextMinuteRollover;
        private static DateTime _nextSecondRollover;

        static TTSStats()
        {
            Reset();
        }

        public static void RecordRequest()
        {
            TotalRequests++;
            _requestsThisMinute++;
            _callsThisSecond++;
        }

        public static void RecordSuccess(long audioBytes, long elapsedMs)
        {
            TotalSuccess++;
            TotalAudioBytes += audioBytes;
            TotalElapsedMs += elapsedMs;
        }

        public static void RecordFailure(string error)
        {
            TotalFailed++;
            LastError = error;
        }

        public static void RecordCancel()
        {
            TotalCancelled++;
        }

        public static double AvgElapsedMs => TotalSuccess > 0 ? (double)TotalElapsedMs / TotalSuccess : 0;
        public static double AvgAudioBytes => TotalSuccess > 0 ? (double)TotalAudioBytes / TotalSuccess : 0;

        public static void Update()
        {
            if (DateTime.Now >= _nextSecondRollover)
            {
                _callsThisSecond = 0;
                _nextSecondRollover = _nextSecondRollover.AddSeconds(1);
            }

            if (DateTime.Now < _nextMinuteRollover) return;

            RequestsPerMinuteHistory.Add(_requestsThisMinute);
            while (RequestsPerMinuteHistory.Count > 60) RequestsPerMinuteHistory.RemoveAt(0);
            _requestsThisMinute = 0;
            _nextMinuteRollover = _nextMinuteRollover.AddMinutes(1);
        }

        public static void Reset()
        {
            TotalRequests = 0;
            TotalSuccess = 0;
            TotalFailed = 0;
            TotalCancelled = 0;
            TotalAudioBytes = 0;
            TotalElapsedMs = 0;
            StartTime = DateTime.Now;
            LastError = "";
            _requestsThisMinute = 0;
            _callsThisSecond = 0;
            RequestsPerMinuteHistory.Clear();
            _nextMinuteRollover = DateTime.Now.AddMinutes(1);
            _nextSecondRollover = DateTime.Now.AddSeconds(1);
        }
    }
}
