using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.Patch
{
    [StaticConstructorOnStartup]
    public static class BioTabVoicePatch
    {
        private static readonly Texture2D VoiceIcon = ContentFinder<Texture2D>.Get("UI/VoiceSettings");

        private static void AddVoiceElement(Pawn pawn)
        {
            if (pawn == null) return;
            if (!ShouldShowVoiceUI(pawn)) return;

            var tmpStackElements = (List<GenUI.AnonymousStackElement>)
                AccessTools.Field(typeof(CharacterCardUtility), "tmpStackElements").GetValue(null);
            if (tmpStackElements == null) return;

            string voiceLabelText = "TTS 音色";
            float textWidth = Text.CalcSize(voiceLabelText).x;
            float totalLabelWidth = 22f + 5f + textWidth + 5f;

            tmpStackElements.Add(new GenUI.AnonymousStackElement
            {
                width = totalLabelWidth,
                drawer = rect =>
                {
                    Widgets.DrawOptionBackground(rect, false);
                    Widgets.DrawHighlightIfMouseover(rect);

                    string currentVoice = PawnVoiceManager.GetVoiceModel(pawn);
                    string displayVoice = currentVoice == PawnVoiceManager.DEFAULT
                        ? "默认" : currentVoice == PawnVoiceManager.NONE
                        ? "无" : currentVoice;

                    TooltipHandler.TipRegion(rect,
                        $"TTS 音色: {displayVoice}\n\n当前角色独立音色设置，点击可更改。\n默认 = 使用全局设置\n无 = 禁用此角色 TTS");

                    Rect iconRect = new Rect(rect.x + 2f, rect.y + 1f, 20f, 20f);
                    GUI.DrawTexture(iconRect, VoiceIcon);

                    Rect labelRect = new Rect(iconRect.xMax + 5f, rect.y, textWidth, rect.height);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(labelRect, voiceLabelText);
                    Text.Anchor = TextAnchor.UpperLeft;

                    if (Widgets.ButtonInvisible(rect))
                    {
                        Find.WindowStack.Add(new VoiceSelectionWindow(pawn));
                    }
                }
            });
        }

        private static bool ShouldShowVoiceUI(Pawn pawn)
        {
            if (pawn == null) return false;

            var def = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_PersonaData");
            if (def == null) return false;

            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) != null;
        }

        [HarmonyPatch(typeof(CharacterCardUtility), "DoTopStack")]
        public static class DoTopStack_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var anchorMethod = AccessTools.Method(
                    typeof(QuestUtility),
                    nameof(QuestUtility.AppendInspectStringsFromQuestParts),
                    new Type[]
                    {
                        typeof(Action<string, Quest>),
                        typeof(ISelectable),
                        typeof(int).MakeByRefType()
                    });

                foreach (var instruction in instructions)
                {
                    yield return instruction;

                    if (instruction.Calls(anchorMethod))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BioTabVoicePatch), nameof(AddVoiceElement)));
                    }
                }
            }
        }
    }
}
