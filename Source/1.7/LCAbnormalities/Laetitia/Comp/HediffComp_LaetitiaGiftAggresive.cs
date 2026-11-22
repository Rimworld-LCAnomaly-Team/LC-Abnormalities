using Laetitia.Effect;
using RimWorld;
using Verse;

namespace Laetitia.Comp
{
    public class HediffComp_LaetitiaGiftAggresive : HediffComp
    {
        public HediffCompProperties_LaetitiaGiftAggresive Props => (HediffCompProperties_LaetitiaGiftAggresive)props;

        private const int EgoMonsterLifespanTicks = 4500;
        private const float GiftBurstDamage = 45f;
        private const float GiftBurstArmorPen = 0.35f;

        private bool hasSpawned = false;
        private bool fromEgoWeapon = false;
        private Pawn casterPawn = null;

        public void MarkFromEgoWeapon(Pawn caster)
        {
            fromEgoWeapon = true;
            casterPawn = caster;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref hasSpawned, "hasSpawned", false);
            Scribe_Values.Look(ref fromEgoWeapon, "fromEgoWeapon", false);
            Scribe_References.Look(ref casterPawn, "casterPawn");
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn != null && Pawn.Downed && !hasSpawned)
            {
                TriggerDetonation();
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            if (!hasSpawned && Pawn != null && (Pawn.Dead || Pawn.Downed || parent.Severity <= 0.01f))
            {
                TriggerDetonation();
            }
        }

        private void TriggerDetonation()
        {
            if (hasSpawned)
                return;
            hasSpawned = true;

            if (Pawn == null)
                return;

            Map map = Pawn.MapHeld;
            IntVec3 pos = Pawn.PositionHeld;
            if (map == null || !pos.InBounds(map))
                return;

            LCAnomalyCore.Util.AbnormalityUtility.Burst(pos, map, 1.5f);

            if (!Pawn.Dead)
            {
                DamageInfo burstDamage = new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                    GiftBurstDamage,
                    GiftBurstArmorPen,
                    -1f,
                    casterPawn);
                Pawn.TakeDamage(burstDamage);
            }

            if (Pawn.Dead || Pawn.Downed)
            {
                int amount = Props.spawnAmount.RandomInRange;
                int lifespan = fromEgoWeapon ? EgoMonsterLifespanTicks : -1;
                for (int i = 0; i < amount; i++)
                {
                    var thing = GenSpawn.Spawn(Laetitia.Def.ThingDefOf.SpawningLaetitiaMonster, pos, map) as SpawningLaetitiaMonster;
                    thing?.Init(lifespan);
                }

                Messages.Message(
                    "Laetitia_AggressiveGiftBurst".Translate(Pawn.LabelShortCap),
                    new LookTargets(pos, map),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            }
        }
    }
}
