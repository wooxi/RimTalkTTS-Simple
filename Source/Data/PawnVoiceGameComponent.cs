using Verse;

namespace RimTalkTTS.Simple.Data
{
    public class PawnVoiceGameComponent : GameComponent
    {
        public PawnVoiceGameComponent(Game game) { }

        public override void ExposeData()
        {
            PawnVoiceManager.ExposeData();
        }
    }
}
