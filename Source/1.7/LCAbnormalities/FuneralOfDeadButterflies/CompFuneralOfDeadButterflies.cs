using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.FuneralOfDeadButterflies
{
    [DefOf]
    public static class FuneralDefOf
    {
        public static SoundDef LC_Funeral_Attack;
        public static SoundDef LC_Funeral_Open;
        public static SoundDef LC_Funeral_Skill;
        public static SoundDef LC_Funeral_Close;

        static FuneralDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FuneralDefOf));
        }
    }

    public class CompProperties_FuneralOfDeadButterflies : CompProperties_LCAbnormality
    {
        public int burstIntervalTicks = 600;
        public int salvoIntervalTicks = 110;
        public float burstRadius = 6.5f;
        public float salvoRange = 18f;

        public CompProperties_FuneralOfDeadButterflies()
        {
            compClass = typeof(CompFuneralOfDeadButterflies);
        }
    }

    /// <summary>
    /// T-01-68 亡蝶葬仪核心逻辑：勇气高于 III 或正义低于 III 工作必降计数；突破后定期开棺释放白黑混合群蝶冲击并持续远程射击。
    /// </summary>
    public class CompFuneralOfDeadButterflies : CompLCAbnormality
    {
        private int coffinOpenTicks;
        private int burstCooldownTicks = 240;
        private int salvoCooldownTicks = 60;

        public CompProperties_FuneralOfDeadButterflies PropsFuneral => (CompProperties_FuneralOfDeadButterflies)props;

        public bool IsCoffinOpen => coffinOpenTicks > 0;

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            if (studier == null || parent == null || parent.Destroyed || Escaped)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status != null)
            {
                bool badFortitude = status.GetPawnStatusELevel(EPawnStatus.Fortitude) > EPawnLevel.III;
                bool badJustice = status.GetPawnStatusELevel(EPawnStatus.Justice) < EPawnLevel.III;
                if (badFortitude || badJustice)
                {
                    QliphothCountCurrent--;
                    Messages.Message("LC_Funeral_BadVirtueMsg".Translate(studier.LabelShortCap), parent, MessageTypeDefOf.NegativeEvent, historical: false);
                }
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            coffinOpenTicks = 0;
            burstCooldownTicks = 240;
            salvoCooldownTicks = 60;
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            coffinOpenTicks = 0;
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || parent == null || parent.Destroyed || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            if (coffinOpenTicks > 0)
            {
                coffinOpenTicks--;
                if (coffinOpenTicks == 0)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Close, parent);
                }
            }

            burstCooldownTicks--;
            if (burstCooldownTicks <= 0)
            {
                burstCooldownTicks = PropsFuneral.burstIntervalTicks;
                TriggerCoffinBurst();
            }

            salvoCooldownTicks--;
            if (salvoCooldownTicks <= 0)
            {
                salvoCooldownTicks = PropsFuneral.salvoIntervalTicks;
                TryFireButterflySalvo();
            }
        }

        private void TriggerCoffinBurst()
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            coffinOpenTicks = 90;
            LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Open, parent);
            LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Skill, parent);
            FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(), map, 2.4f, Color.white);

            float radSq = PropsFuneral.burstRadius * PropsFuneral.burstRadius;
            List<Pawn> list = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < list.Count; i++)
            {
                Pawn target = list[i];
                if (target == null || target == parent || target.Dead || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if ((target.Position - pos).LengthHorizontalSquared <= radSq)
                {
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 14f, 0.35f, instigator: parent));
                    if (!target.Dead)
                    {
                        target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 14f, 0.35f, instigator: parent));
                    }
                }
            }
        }

        private void TryFireButterflySalvo()
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(map, pos, PropsFuneral.salvoRange);
            if (target == null || !GenSight.LineOfSight(pos, target.Position, map))
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Attack, parent);
            FleckMaker.ThrowMicroSparks(target.DrawPos, map);
            target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 9f, 0.30f, instigator: parent));
            if (!target.Dead)
            {
                target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 9f, 0.30f, instigator: parent));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref coffinOpenTicks, "fdbCoffinOpenTicks", 0);
            Scribe_Values.Look(ref burstCooldownTicks, "fdbBurstCooldownTicks", 240);
            Scribe_Values.Look(ref salvoCooldownTicks, "fdbSalvoCooldownTicks", 60);
        }
    }

    public class LC_FuneralOfDeadButterfliesPawn : LC_StandardEntityPawn
    {
        public CompFuneralOfDeadButterflies FuneralComp => EntityComp as CompFuneralOfDeadButterflies;

        public override string CurrentBodyTexPath =>
            FuneralComp?.IsCoffinOpen == true
                ? "Things/Pawn/FuneralOfDeadButterflies/FuneralOfDeadButterflies_Open"
                : "Things/Pawn/FuneralOfDeadButterflies/FuneralOfDeadButterflies";

        public override Vector2 CurrentBodyDrawSize => new Vector2(2.0f, 2.0f);
    }

    public class JobGiver_ButterflyCombat : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompFuneralOfDeadButterflies comp = pawn.TryGetComp<CompFuneralOfDeadButterflies>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            float dist = pawn.Position.DistanceTo(target.Position);
            if (dist <= 9f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 45);
            }

            Verse.AI.Job approach = JobMaker.MakeJob(JobDefOf.Goto, target);
            approach.expiryInterval = 60;
            approach.checkOverrideOnExpire = true;
            approach.canBashDoors = true;
            return approach;
        }
    }

    /// <summary>圣宣 WAW 级双枪专属伤害处理器：同时结算白伤与黑伤双重判定。</summary>
    public class DamageWorker_SolemnLament : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (victim != null && !victim.Destroyed && !(victim is Pawn vp && vp.Dead))
            {
                float secondary = Mathf.Max(4f, dinfo.Amount);
                DamageInfo blackHit = new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                    secondary,
                    dinfo.ArmorPenetrationInt,
                    dinfo.Angle,
                    dinfo.Instigator,
                    weapon: dinfo.Weapon);
                victim.TakeDamage(blackHit);
            }

            return result;
        }
    }
}
