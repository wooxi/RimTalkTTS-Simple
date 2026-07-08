using System;
using System.Collections.Generic;
using System.Linq;
using RimTalkTTS.Simple.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.UI
{
    public class VoiceSelectionWindow : Window
    {
        private readonly Pawn _pawn;
        private string _selectedVoiceId;
        private Vector2 _scrollPos = Vector2.zero;
        private readonly TTSSettings _settings;

        public VoiceSelectionWindow(Pawn pawn)
        {
            _pawn = pawn;
            _settings = TTSModule.GetSettings();
            _selectedVoiceId = PawnVoiceManager.GetVoiceModel(pawn);

            doCloseX = true;
            draggable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(480f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label($"TTS 音色设置 - {_pawn.LabelShort}");
            Text.Font = GameFont.Small;

            listing.Gap();

            string currentDisplay = _selectedVoiceId == PawnVoiceManager.DEFAULT
                ? "默认 (全局设置)" : _selectedVoiceId == PawnVoiceManager.NONE
                ? "无 (禁用)" : _selectedVoiceId;
            listing.Label($"当前: {currentDisplay}");

            listing.Gap();
            listing.GapLine();
            listing.Gap();

            var voiceModels = GetAvailableVoices();

            float listHeight = inRect.height - 200f;
            Rect listOuter = listing.GetRect(listHeight);

            int itemCount = 2 + voiceModels.Count;
            float contentHeight = itemCount * 36f;
            Rect listInner = new Rect(0, 0, listOuter.width - 16f, contentHeight);

            Widgets.BeginScrollView(listOuter, ref _scrollPos, listInner);

            float y = 0f;
            DrawVoiceOption(ref y, listInner.width, PawnVoiceManager.DEFAULT, "默认 (跟随全局设置)",
                "使用 TTS 设置面板中的全局音色");
            DrawVoiceOption(ref y, listInner.width, PawnVoiceManager.NONE, "无 (禁用 TTS)",
                "此角色不会播放语音");

            foreach (var vm in voiceModels)
            {
                DrawVoiceOption(ref y, listInner.width, vm.ModelId, vm.Name, $"ID: {vm.ModelId}");
            }

            Widgets.EndScrollView();

            listing.Gap();

            float btnWidth = 120f;
            float btnHeight = 30f;
            Rect saveRect = new Rect(inRect.center.x - btnWidth - 10f, inRect.height - 40f, btnWidth, btnHeight);
            Rect cancelRect = new Rect(inRect.center.x + 10f, inRect.height - 40f, btnWidth, btnHeight);

            if (Widgets.ButtonText(saveRect, "保存"))
            {
                PawnVoiceManager.SetVoiceModel(_pawn, _selectedVoiceId);
                Messages.Message($"已保存 {_pawn.LabelShort} 的音色设置", MessageTypeDefOf.TaskCompletion, false);
                Close();
            }

            if (Widgets.ButtonText(cancelRect, "取消"))
            {
                Close();
            }

            listing.End();
        }

        private void DrawVoiceOption(ref float y, float width, string voiceId, string label, string desc)
        {
            Rect rowRect = new Rect(0, y, width, 32f);
            bool selected = _selectedVoiceId == voiceId;

            if (selected)
                Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.45f, 0.25f, 0.5f));
            else
                Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f, 0.3f));

            Widgets.DrawHighlightIfMouseover(rowRect);

            Rect radioRect = new Rect(rowRect.x + 5f, rowRect.y + 6f, 20f, 20f);
            if (Widgets.RadioButton(radioRect.position, selected))
                _selectedVoiceId = voiceId;

            Rect labelRect = new Rect(radioRect.xMax + 8f, rowRect.y + 2f, width - 40f, 16f);
            Widgets.Label(labelRect, label);

            Rect descRect = new Rect(labelRect.x, labelRect.yMax, labelRect.width, 14f);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(descRect, desc);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(rowRect))
                _selectedVoiceId = voiceId;

            y += 36f;
        }

        private List<VoiceModel> GetAvailableVoices()
        {
            if (_settings == null) return new List<VoiceModel>();

            if (_settings.Provider == TTSSettings.TTSProvider.MiMoTTS)
            {
                string effectiveModel = _settings.GetModelForPawn(_pawn);
                bool isVoiceDesign = (effectiveModel ?? "").Contains("voicedesign");
                if (isVoiceDesign) return new List<VoiceModel>();
                return TTSSettings.GetMiMoVoices();
            }

            return TTSSettings.GetEdgeVoices();
        }
    }
}
