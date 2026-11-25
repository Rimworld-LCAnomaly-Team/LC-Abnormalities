using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.DamageWorker;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.ScarecrowSearchingForWisdom
{
    [DefOf]
    public static class ScarecrowDefOf
    {
        public static ThingDef ScarecrowSearchingForWisdomEgg;
        public static ThingDef LC_ScarecrowDrainBeamFX;
        public static ThingDef LC_HarvestSkillRunnerFX;

        public static SoundDef LC_Scarecrow_Start;
        public static SoundDef LC_Scarecrow_Atk;
        public static SoundDef LC_Scarecrow_Drink;
        public static SoundDef LC_Scarecrow_Dead;
        public static SoundDef LC_Scarecrow_SpearHit;

        public static AnimationDef Scarecrow_Contained_Idle;
        public static AnimationDef Scarecrow_Breached_Idle;
        public static AnimationDef Scarecrow_Walk;
        public static AnimationDef Scarecrow_Attack1;
        public static AnimationDef Scarecrow_Attack2;
        public static AnimationDef Scarecrow_Drink;
        public static AnimationDef Scarecrow_Dead;

        static ScarecrowDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ScarecrowDefOf));
        }
    }

    public class CompProperties_ScarecrowSearchingForWisdom : CompProperties_LCAbnormality
    {
        public int drainDurationTicks = 210;
        public int drainPulseIntervalTicks = 30;
        public float drainHealPerPulse = 22f;
        public float attackMinDmg = 14f;
        public float attackMaxDmg = 20f;
        public int attackAnimTicks = 42;

        public CompProperties_ScarecrowSearchingForWisdom()
        {
            compClass = typeof(CompScarecrowSearchingForWisdom);
        }
    }

    /// <summary>
    /// F-01-87 索求智慧的稻草人核心逻辑：
    /// 1. 慎重 ≥ III 级的员工完成工作或工作结果为差时，逆卡巴拉计数器（上限 1）归 0 并立即突破（优不恢复）；
    /// 2. 出逃后摘下草帽追杀最后作业者及高慎重目标，造成 14~20 点黑伤；
    /// 3. 击杀人类后伸出血染稻草长舌吸食脑浆（Scarecrow_Drink，210 ticks）：期间完全免疫所有伤害（absorbed=true），并分 7 次各回复 22 点生命值。
    /// </summary>
    public class CompScarecrowSearchingForWisdom : CompLCAbnormality
    {
        private Pawn lastStudier;
        private int attackTicksLeft;
        private bool alternateAttack2;
        private int drainTicksLeft;
        private int drainPulseTimer;

        public CompProperties_ScarecrowSearchingForWisdom PropsScarecrow => (CompProperties_ScarecrowSearchingForWisdom)props;

        public Pawn PreferredTarget => lastStudier;
        public bool IsAttacking => Escaped && attackTicksLeft > 0;
        public bool UseAttack2Anim => alternateAttack2;
        public bool IsDrainingBrain => Escaped && drainTicksLeft > 0;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref lastStudier, "scarecrowLastStudier");
            Scribe_Values.Look(ref attackTicksLeft, "scarecrowAttackTicks", 0);
            Scribe_Values.Look(ref alternateAttack2, "scarecrowAltAtk2", false);
            Scribe_Values.Look(ref drainTicksLeft, "scarecrowDrainTicks", 0);
            Scribe_Values.Look(ref drainPulseTimer, "scarecrowDrainPulseTimer", 0);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            attackTicksLeft = 0;
            drainTicksLeft = 0;
            drainPulseTimer = 0;
            (parent as LC_ScarecrowPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            if (studier != null) lastStudier = studier;
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            if (studier != null) lastStudier = studier;
            base.Notify_Studied(studier, interrupted);
            if (!interrupted && !Escaped && studier != null && QliphothCountCurrent > 0)
            {
                CompPawnStatus status = studier.GetComp<CompPawnStatus>();
                if (status != null && status.GetPawnStatusELevel(EPawnStatus.Prudence) >= EPawnLevel.III)
                {
                    if (parent?.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                    {
                        MoteMaker.ThrowText(parent.DrawPos + new Vector3(0f, 0f, 0.55f), parent.MapHeld,
                            "LC_Scarecrow_PrudenceEnvyFloat".Translate(), new Color(0.85f, 0.25f, 0.28f), 2.2f);
                    }
                    QliphothCountCurrent--;
                }
            }

            (parent as LC_ScarecrowPawn)?.UpdateAnimation(force: true);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            int prev = QliphothCountCurrent;
            base.StudyEvent_Good(studier);
            if (prev > 0 && QliphothCountCurrent > prev)
            {
                QliphothCountCurrent = prev;
            }
        }

        public override void Notify_Escaped()
        {
            attackTicksLeft = 0;
            drainTicksLeft = 0;
            drainPulseTimer = 0;
            base.Notify_Escaped();
            LCAbnormalitiesHelper.PlaySoundAt(ScarecrowDefOf.LC_Scarecrow_Start, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                ScarecrowVfxUtility.SpawnDrainAura(self.Map, self.DrawPos, 2.6f, 34);
            }
            (parent as LC_ScarecrowPawn)?.UpdateAnimation(force: true);
        }

        public void TriggerAttack(Pawn victim)
        {
            attackTicksLeft = PropsScarecrow.attackAnimTicks;
            alternateAttack2 = !alternateAttack2;
            LCAbnormalitiesHelper.PlaySoundAt(ScarecrowDefOf.LC_Scarecrow_Atk, parent);
            (parent as LC_ScarecrowPawn)?.UpdateAnimation(force: true);
        }

        public void StartBrainDrain(Pawn slainVictim)
        {
            if (!Escaped || parent == null || parent.Destroyed) return;
            drainTicksLeft = PropsScarecrow.drainDurationTicks;
            drainPulseTimer = PropsScarecrow.drainPulseIntervalTicks;
            attackTicksLeft = 0;
            LCAbnormalitiesHelper.PlaySoundAt(ScarecrowDefOf.LC_Scarecrow_Drink, parent);

            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                ScarecrowVfxUtility.SpawnDrainAura(self.Map, self.DrawPos, 2.8f, PropsScarecrow.drainDurationTicks);
                MoteMaker.ThrowText(self.DrawPos + new Vector3(0f, 0f, 0.60f), self.Map,
                    "LC_Scarecrow_BrainDrainFloat".Translate(), new Color(0.95f, 0.22f, 0.26f), 2.3f);
            }

            (parent as LC_ScarecrowPawn)?.UpdateAnimation(force: true);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (IsDrainingBrain)
            {
                absorbed = true;
                if (parent is Pawn self && self.Spawned && self.Map != null && self.DrawPos.ShouldSpawnMotesAt(self.Map) && Rand.Chance(0.35f))
                {
                    MoteMaker.ThrowText(self.DrawPos + new Vector3(0f, 0f, 0.55f), self.Map,
                        "LC_Scarecrow_InvulnFloat".Translate(), new Color(0.95f, 0.72f, 0.30f), 1.4f);
                }
                return;
            }

            base.PostPreApplyDamage(ref dinfo, out absorbed);
        }

        public Pawn SelectHuntTarget(Map map, IntVec3 origin, Pawn self)
        {
            if (lastStudier != null && !lastStudier.Dead && !lastStudier.Downed && lastStudier.Spawned
                && lastStudier.Map == map && (lastStudier.RaceProps?.Humanlike ?? false))
            {
                return lastStudier;
            }

            if (map?.mapPawns?.AllPawnsSpawned == null) return null;
            Pawn best = null;
            int bestPrudence = -1;
            float bestDistSq = float.MaxValue;
            IReadOnlyList<Pawn> list = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < list.Count; i++)
            {
                Pawn c = list[i];
                if (c == null || c == self || c.Dead || c.Downed || !c.Spawned || !(c.RaceProps?.Humanlike ?? false)) continue;
                CompPawnStatus st = c.GetComp<CompPawnStatus>();
                int prud = st != null ? (int)st.GetPawnStatusELevel(EPawnStatus.Prudence) : 0;
                float dSq = (c.Position - origin).LengthHorizontalSquared;
                if (prud > bestPrudence || (prud == bestPrudence && dSq < bestDistSq))
                {
                    bestPrudence = prud;
                    bestDistSq = dSq;
                    best = c;
                }
            }
            return best;
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (attackTicksLeft > 0) attackTicksLeft--;
            if (drainTicksLeft > 0)
            {
                drainTicksLeft--;
                drainPulseTimer--;
                if (drainPulseTimer <= 0)
                {
                    drainPulseTimer = PropsScarecrow.drainPulseIntervalTicks;
                    if (parent is Pawn self && !self.Dead)
                    {
                        LCAbnormalitiesHelper.HealInjuries(self, PropsScarecrow.drainHealPerPulse);
                    }
                }
            }
        }
    }

    public class LC_ScarecrowPawn : LC_StandardEntityPawn
    {
        public CompScarecrowSearchingForWisdom ScarecrowComp => EntityComp as CompScarecrowSearchingForWisdom;

        public bool IsHeldOnPlatform => ParentHolder is Building_AbnormalityHoldingPlatform;

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null) return;
            CompScarecrowSearchingForWisdom comp = ScarecrowComp;
            AnimationDef desired;
            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = ScarecrowDefOf.Scarecrow_Contained_Idle;
            }
            else if (comp != null && comp.IsDrainingBrain)
            {
                desired = ScarecrowDefOf.Scarecrow_Drink;
            }
            else if (comp != null && comp.IsAttacking)
            {
                desired = comp.UseAttack2Anim ? ScarecrowDefOf.Scarecrow_Attack2 : ScarecrowDefOf.Scarecrow_Attack1;
            }
            else if (pather != null && pather.Moving)
            {
                desired = ScarecrowDefOf.Scarecrow_Walk;
            }
            else
            {
                desired = ScarecrowDefOf.Scarecrow_Breached_Idle;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public override void TickHolded()
        {
            base.TickHolded();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            CompScarecrowSearchingForWisdom comp = ScarecrowComp;
            if (comp != null && comp.IsDrainingBrain && pather?.Moving == true)
            {
                pather.StopDead();
            }
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }
    }

    public class JobGiver_ScarecrowHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return null;
            CompScarecrowSearchingForWisdom comp = pawn.TryGetComp<CompScarecrowSearchingForWisdom>();
            if (comp != null && comp.IsDrainingBrain)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 30);
            }

            Pawn target = comp?.SelectHuntTarget(pawn.Map, pawn.Position, pawn)
                ?? LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 65f, p => p != pawn);
            if (target == null) return null;

            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.expiryInterval = 60;
            attack.checkOverrideOnExpire = true;
            attack.canBashDoors = true;
            return attack;
        }
    }

    public class DamageWorker_ScarecrowHarvest : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn attacker = dinfo.Instigator as Pawn;
            CompScarecrowSearchingForWisdom comp = attacker?.TryGetComp<CompScarecrowSearchingForWisdom>();
            if (comp != null)
            {
                dinfo.SetAmount(Rand.Range(comp.PropsScarecrow.attackMinDmg, comp.PropsScarecrow.attackMaxDmg));
                comp.TriggerAttack(victim as Pawn);
            }

            DamageResult res = base.Apply(dinfo, victim);
            if (comp != null && victim is Pawn pv && (pv.Dead || pv.Destroyed) && (pv.RaceProps?.Humanlike ?? false))
            {
                comp.StartBrainDrain(pv);
            }
            return res;
        }
    }
}
