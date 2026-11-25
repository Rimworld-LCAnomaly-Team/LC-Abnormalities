using System.Collections.Generic;
using Laetitia.Effect;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Laetitia.Comp
{
    public class HediffComp_LaetitiaGiftAggresive : HediffComp
    {
        public HediffCompProperties_LaetitiaGiftAggresive Props => (HediffCompProperties_LaetitiaGiftAggresive)props;

        private const float GiftBurstDamage = 45f;
        private const float GiftBurstArmorPen = 0.35f;
        private const float EgoHeartbreakMainDamage = 42f;
        private const float EgoHeartbreakAoeDamage = 30f;
        private const float EgoHeartbreakRadius = 3.6f;

        private bool hasSpawned = false;
        private bool fromEgoWeapon = false;
        private Pawn casterPawn = null;

        public void MarkFromEgoWeapon(Pawn caster)
        {
            fromEgoWeapon = true;
            casterPawn = caster;
        }

        /// <summary>蕾蒂希娅心蕾步枪命中携礼目标时裂解礼赞并加速引爆「碎心黑蕾丝」。</summary>
        public void Notify_LaetitiaRifleHit(bool forceDetonate = false)
        {
            if (hasSpawned || Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                return;
            }

            parent.Severity -= 0.34f;
            Map map = Pawn.MapHeld;
            if (map != null && Pawn.PositionHeld.InBounds(map))
            {
                Vector3 hitPos = Pawn.Spawned ? Pawn.DrawPos.Yto0() : Pawn.PositionHeld.ToVector3Shifted().Yto0();
                LaetitiaVfxUtility.SpawnHeartBloom(map, hitPos, scale: 1.50f, lifeTicks: 14);
            }

            if (forceDetonate || parent.Severity <= 0.02f)
            {
                TriggerDetonation();
            }
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

            Vector3 burstPos = Pawn.Spawned ? Pawn.DrawPos.Yto0() : pos.ToVector3Shifted().Yto0();
            LCAnomalyCore.Util.AbnormalityUtility.Burst(pos, map, 1.5f);

            if (fromEgoWeapon)
            {
                // E.G.O 武器「送出礼物」引爆改为「碎心黑蕾丝」友军安全范围黑伤震爆（不再孵化攻击友军的蜘蛛）
                Laetitia.Def.SoundDefOf.LaetitiaMonster_Spawn?.PlayOneShot(new TargetInfo(pos, map));
                LaetitiaVfxUtility.SpawnHeartbreakBurst(map, burstPos, scale: 4.4f, lifeTicks: 34);
                ThingDef weaponDef = casterPawn?.equipment?.Primary?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(LaetitiaWeaponAnimHandler.WeaponDefName);

                if (!Pawn.Dead)
                {
                    Pawn.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                        EgoHeartbreakMainDamage,
                        0.45f,
                        -1f,
                        casterPawn,
                        weapon: weaponDef));
                }

                List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn foe = candidates[i];
                    if (foe == null || foe == Pawn || foe == casterPawn || foe.Dead || foe.Destroyed || !foe.Spawned)
                    {
                        continue;
                    }
                    if (casterPawn != null && !foe.HostileTo(casterPawn))
                    {
                        continue;
                    }

                    if ((foe.DrawPos.Yto0() - burstPos).sqrMagnitude <= EgoHeartbreakRadius * EgoHeartbreakRadius)
                    {
                        foe.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                            EgoHeartbreakAoeDamage,
                            0.40f,
                            -1f,
                            casterPawn,
                            weapon: weaponDef));
                        if (!foe.Dead)
                        {
                            foe.stances?.stunner?.StunFor(60, casterPawn, addBattleLog: false);
                        }
                    }
                }

                if (Pawn.Dead || Pawn.Downed)
                {
                    Messages.Message(
                        "Laetitia_AggressiveGiftBurst".Translate(Pawn.LabelShortCap),
                        new LookTargets(pos, map),
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
                if (!Pawn.Dead && Pawn.health?.hediffSet?.hediffs.Contains(parent) == true)
                {
                    Pawn.health.RemoveHediff(parent);
                }
                return;
            }

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
                for (int i = 0; i < amount; i++)
                {
                    var thing = GenSpawn.Spawn(Laetitia.Def.ThingDefOf.SpawningLaetitiaMonster, pos, map) as SpawningLaetitiaMonster;
                    thing?.Init(-1);
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
