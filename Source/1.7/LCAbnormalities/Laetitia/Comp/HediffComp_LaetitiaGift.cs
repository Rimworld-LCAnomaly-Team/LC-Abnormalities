using Laetitia.Effect;
using LCAnomalyCore.Jobs;
using RimWorld;
using Verse;

namespace Laetitia.Comp
{
    public class HediffComp_LaetitiaGift : HediffComp
    {
        public HediffCompProperties_LaetitiaGift Props => (HediffCompProperties_LaetitiaGift)props;

        private int lastWarnedJobId = -1;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Dead || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            if (Pawn.CurJob?.def == LCAnomalyCore.Defs.JobDefOf.LC_AbnormalityStudyInteract
                && Pawn.CurJob.GetCachedDriver(Pawn) is JobDriver_AbnormalityStudyInteract driver)
            {
                Pawn target = driver.ThingToStudy as Pawn;
                if (target != null
                    && target.kindDef != Laetitia.Def.PawnKindDefOf.Laetitia
                    && target.MapHeld == Pawn.MapHeld)
                {
                    float dis = Pawn.Position.DistanceTo(target.PositionHeld);
                    if (dis < 12f && lastWarnedJobId != Pawn.CurJob.loadID)
                    {
                        lastWarnedJobId = Pawn.CurJob.loadID;
                        Messages.Message(
                            "Laetitia_GiftApproachWarning".Translate(Pawn.LabelShortCap, target.LabelShortCap),
                            Pawn,
                            MessageTypeDefOf.ThreatSmall,
                            historical: false);
                    }

                    if (dis < Props.deathDistance)
                    {
                        TurnIntoMonster();
                    }
                }
            }
        }

        public void TurnIntoMonster()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            Map map = Pawn.MapHeld;
            IntVec3 pos = Pawn.PositionHeld;
            if (map != null && pos.InBounds(map))
            {
                var thing = GenSpawn.Spawn(Laetitia.Def.ThingDefOf.SpawningLaetitiaMonster, pos, map) as SpawningLaetitiaMonster;
                thing?.Init();

                Find.LetterStack.ReceiveLetter(
                    "Laetitia_GiftBurstLetterLabel".Translate(),
                    "Laetitia_GiftBurstLetterText".Translate(Pawn.LabelShortCap),
                    LetterDefOf.ThreatBig,
                    new LookTargets(pos, map));
            }

            Pawn.Kill(null);
        }
    }
}
