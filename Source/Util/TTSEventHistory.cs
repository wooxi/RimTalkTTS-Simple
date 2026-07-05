using System;
using System.Collections.Generic;
using System.Linq;

namespace RimTalkTTS.Simple.Util
{
    public static class TTSEventHistory
    {
        private const int MaxEvents = 500;
        private static readonly List<TTSEventLog> _events = new List<TTSEventLog>();
        private static readonly Dictionary<Guid, TTSEventLog> _dialogueMap = new Dictionary<Guid, TTSEventLog>();

        public static IReadOnlyList<TTSEventLog> GetAll() => _events;

        public static IReadOnlyList<TTSEventLog> GetRecent(int count = 20)
        {
            return _events.OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        public static IReadOnlyList<TTSEventLog> GetByState(TTSEventLog.State state, int count = 50)
        {
            return _events.Where(e => e.EventState == state)
                .OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        public static void Add(TTSEventLog evt)
        {
            _events.Add(evt);
            while (_events.Count > MaxEvents)
            {
                _events.RemoveAt(0);
            }
        }

        public static void AddForDialogue(TTSEventLog evt, Guid dialogueId)
        {
            Add(evt);
            _dialogueMap[dialogueId] = evt;
        }

        public static TTSEventLog FindByDialogueId(Guid dialogueId)
        {
            _dialogueMap.TryGetValue(dialogueId, out var evt);
            return evt;
        }

        public static void Clear()
        {
            _events.Clear();
            _dialogueMap.Clear();
        }

        public static TTSEventLog Find(Guid id)
        {
            return _events.FirstOrDefault(e => e.Id == id);
        }
    }
}
