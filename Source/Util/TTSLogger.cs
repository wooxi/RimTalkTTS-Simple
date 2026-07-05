using System.Collections.Concurrent;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimTalkTTS.Simple.Util
{
    public static class TTSLogger
    {
        private const string Tag = "[RimTalkTTS.Simple]";

        public class NotificationInfo
        {
            public string Message;
            public string Category;
            public MessageTypeDef Type;

            public string DedupKey => $"{Type?.defName ?? "msg"}:{Message}";
        }

        private static readonly ConcurrentQueue<NotificationInfo> _pendingNotifications = new ConcurrentQueue<NotificationInfo>();
        private static readonly Dictionary<string, int> _notifiedKeys = new Dictionary<string, int>();
        private const int DEDUP_INTERVAL_TICKS = 3600;

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

        public static void ErrorNotify(object message, string category = null)
        {
            Error(message, category);
            EnqueueNotification(message.ToString(), category, MessageTypeDefOf.NegativeEvent);
        }

        public static void WarningNotify(object message, string category = null)
        {
            Warning(message, category);
            EnqueueNotification(message.ToString(), category, MessageTypeDefOf.NeutralEvent);
        }

        private static void EnqueueNotification(string message, string category, MessageTypeDef type)
        {
            if (Data.TTSConfig.Settings?.EnableNotifications != true) return;

            var info = new NotificationInfo
            {
                Message = string.IsNullOrEmpty(category) ? message : $"[{category}] {message}",
                Category = category,
                Type = type
            };

            _pendingNotifications.Enqueue(info);
        }

        public static void FlushNotifications()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            while (_pendingNotifications.TryDequeue(out var info))
            {
                if (!ShouldNotify(info, currentTick)) continue;

                _notifiedKeys[info.DedupKey] = currentTick;
                Messages.Message($"[RimTalkTTS] {info.Message}", info.Type, false);
            }
        }

        private static bool ShouldNotify(NotificationInfo info, int currentTick)
        {
            if (_notifiedKeys.TryGetValue(info.DedupKey, out int lastTick))
            {
                if (currentTick - lastTick < DEDUP_INTERVAL_TICKS)
                    return false;
            }
            return true;
        }

        public static void ClearNotificationDedup()
        {
            _notifiedKeys.Clear();
            while (_pendingNotifications.TryDequeue(out _)) { }
        }
    }
}
