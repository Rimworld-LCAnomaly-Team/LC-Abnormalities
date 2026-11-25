using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Damage;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.Ppodae
{
    [DefOf]
    public static class PpodaeDefOf
    {
        public static ThingDef PpodaeEgg;
        public static ThingDef LC_PpodaeSlamFX;
        public static ThingDef LC_SoCuteSkillRunnerFX;

        public static SoundDef LC_Ppodae_Bark;
        public static SoundDef LC_Ppodae_Transform;
        public static SoundDef LC_Ppodae_Atk;
        public static SoundDef LC_Ppodae_Dead;
        public static SoundDef LC_Ppodae_Fist;

        public static AnimationDef Ppodae_Puppy_Idle;
        public static AnimationDef Ppodae_Puppy_Bark;
        public static AnimationDef Ppodae_Puppy_Eat;
        public static AnimationDef Ppodae_Frenzy_Idle;
        public static AnimationDef Ppodae_Frenzy_Walk;
        public static AnimationDef Ppodae_Frenzy_Attack;
        public static AnimationDef Ppodae_Frenzy_Dead;

        static PpodaeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PpodaeDefOf));
        }
    }

    public class CompProperties_Ppodae : CompProperties_LCAbnormality
    {
        public float nonInstinctDropChance = 0.50f;
        public float frenzyAttackMinDmg = 16f;
        public float frenzyAttackMaxDmg = 24f;
        public int attackAnimTicks = 44;

        public CompProperties_Ppodae()
        {
            compClass = typeof(CompPpodae);
        }
    }

    /// <summary>
    /// D-02-107 波迪核心逻辑：
    /// 1. 仅喜爱本能工作；指派除本能外任何工作，进入收容室时有 50% 概率立即降低 1 点逆卡巴拉计数器（上限 2）；
    /// 2. 工作结果为差时计数器 -1，结果为优不恢复计数器；
    /// 3. 计数器归 0 出逃后狂暴化为纹身恶犬（R 1.2 / W 0.8 / B 1.0 / P 2.0）猛烈扑击人类；一旦击杀任意人类目标即心满意足地自行化卵返回收容状态。
    /// </summary>
    public class CompPpodae : CompLCAbnormality
    {
        private bool checkedCurrentStudy;
        private bool skipInterruptedStudy;
        private bool hasKilledHumanlikeOnBreach;
        private int barkTicksLeft;
        private int eatTicksLeft;
        private int attackTicksLeft;
        private int returnToEggDelayTicks;

        public CompProperties_Ppodae PropsPpodae => (CompProperties_Ppodae)props;

        public bool IsBarking => !Escaped && barkTicksLeft > 0;
        public bool IsEating => !Escaped && eatTicksLeft > 0;
        public bool IsFrenzyAttacking => Escaped && attackTicksLeft > 0;
        public bool IsFrenzyIdle => Escaped && !IsFrenzyAttacking;
        public bool ShouldReturnToEggSoon => returnToEggDelayTicks > 0;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref checkedCurrentStudy, "ppodaeCheckedCurrentStudy", false);
            Scribe_Values.Look(ref skipInterruptedStudy, "ppodaeSkipInterruptedStudy", false);
            Scribe_Values.Look(ref hasKilledHumanlikeOnBreach, "ppodaeHasKilledHumanlike", false);
            Scribe_Values.Look(ref barkTicksLeft, "ppodaeBarkTicksLeft", 0);
            Scribe_Values.Look(ref eatTicksLeft, "ppodaeEatTicksLeft", 0);
            Scribe_Values.Look(ref attackTicksLeft, "ppodaeAttackTicksLeft", 0);
            Scribe_Values.Look(ref returnToEggDelayTicks, "ppodaeReturnToEggDelayTicks", 0);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            checkedCurrentStudy = false;
            skipInterruptedStudy = false;
            hasKilledHumanlikeOnBreach = false;
            barkTicksLeft = 0;
            eatTicksLeft = 0;
            attackTicksLeft = 0;
            returnToEggDelayTicks = 0;
            (parent as LC_PpodaePawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            checkedCurrentStudy = false;
            skipInterruptedStudy = false;
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            if (!checkedCurrentStudy && studier != null && !Escaped)
            {
                checkedCurrentStudy = true;
                IAbnormalityContainmentUnit unit = HoldingPlaformTargetComp?.HeldUnit;
                EAnomalyWorkType curWork = unit != null ? unit.CurWorkType : EAnomalyWorkType.Instinct;
                if (curWork != EAnomalyWorkType.Instinct)
                {
                    if (Rand.Chance(PropsPpodae.nonInstinctDropChance))
                    {
                        LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Bark, parent);
                        barkTicksLeft = 60;
                        if (parent?.DrawPos.ShouldSpawnMotesAt(parent.MapHeld) == true)
                        {
                            MoteMaker.ThrowText(parent.DrawPos + new Vector3(0f, 0f, 0.55f), parent.MapHeld,
                                "LC_Ppodae_NonInstinctGrowl".Translate(), new Color(0.96f, 0.42f, 0.42f), 2.0f);
                        }

                        if (QliphothCountCurrent == 1)
                        {
                            skipInterruptedStudy = true;
                        }
                        QliphothCountCurrent--;
                    }
                    else
                    {
                        barkTicksLeft = 45;
                        LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Bark, parent);
                    }
                }
            }

            (parent as LC_PpodaePawn)?.UpdateAnimation();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            bool success = base.Notify_StudyInterval(studier, workType);
            if (success && workType == EAnomalyWorkType.Instinct && eatTicksLeft <= 0)
            {
                eatTicksLeft = 36;
            }
            return success;
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            checkedCurrentStudy = false;
            if (skipInterruptedStudy)
            {
                skipInterruptedStudy = false;
                return;
            }

            if (!interrupted && barkTicksLeft <= 0)
            {
                barkTicksLeft = 30;
            }
            base.Notify_Studied(studier, interrupted);
            (parent as LC_PpodaePawn)?.UpdateAnimation(force: true);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            int prev = QliphothCountCurrent;
            base.StudyEvent_Good(studier);
            // Good result does NOT raise Ppodae's Qliphoth counter
            if (prev > 0 && QliphothCountCurrent > prev)
            {
                QliphothCountCurrent = prev;
            }
            eatTicksLeft = 48;
        }

        public override void Notify_Escaped()
        {
            checkedCurrentStudy = false;
            hasKilledHumanlikeOnBreach = false;
            barkTicksLeft = 0;
            eatTicksLeft = 0;
            attackTicksLeft = 0;
            returnToEggDelayTicks = 0;
            base.Notify_Escaped();
            LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Transform, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                PpodaeVfxUtility.SpawnSlamBurst(self.Map, self.DrawPos, 2.8f, 26);
            }
            (parent as LC_PpodaePawn)?.UpdateAnimation(force: true);
        }

        public void TriggerFrenzyAttack(Pawn victim)
        {
            attackTicksLeft = PropsPpodae.attackAnimTicks;
            LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Atk, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null && victim != null)
            {
                Vector3 hitPos = victim.Spawned ? victim.DrawPos : victim.PositionHeld.ToVector3Shifted();
                PpodaeVfxUtility.SpawnSlamBurst(self.Map, Vector3.Lerp(self.DrawPos, hitPos, 0.55f), 2.3f, 18);
            }
            (parent as LC_PpodaePawn)?.UpdateAnimation(force: true);
        }

        public void Notify_SlainHumanlike(Pawn victim)
        {
            if (!Escaped || hasKilledHumanlikeOnBreach || parent == null || parent.Destroyed)
            {
                return;
            }

            if (victim == null || victim == parent || !(victim.RaceProps?.Humanlike ?? false))
            {
                return;
            }

            hasKilledHumanlikeOnBreach = true;
            returnToEggDelayTicks = 45;
            LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Bark, parent);
            if (parent.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
            {
                MoteMaker.ThrowText(parent.DrawPos + new Vector3(0f, 0f, 0.65f), parent.MapHeld,
                    "LC_Ppodae_SatedReturn".Translate(), new Color(1.0f, 0.85f, 0.85f), 2.4f);
                Messages.Message("LC_Ppodae_SatedMessage".Translate(parent.LabelCap, victim.LabelShortCap),
                    new LookTargets(parent.PositionHeld, parent.MapHeld), MessageTypeDefOf.PositiveEvent);
            }
        }

        public void ExecuteSelfReturnToEgg()
        {
            if (parent is Pawn self && !self.Destroyed)
            {
                LCAbnormalitiesHelper.ReturnEntityToEgg(self, PpodaeDefOf.PpodaeEgg, biosignature);
            }
        }

        public override void TickContained()
        {
            base.TickContained();
            if (barkTicksLeft > 0) barkTicksLeft--;
            if (eatTicksLeft > 0) eatTicksLeft--;
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (attackTicksLeft > 0) attackTicksLeft--;
            if (returnToEggDelayTicks > 0)
            {
                returnToEggDelayTicks--;
                if (returnToEggDelayTicks == 0)
                {
                    ExecuteSelfReturnToEgg();
                }
            }
        }
    }

    public class LC_PpodaePawn : LC_StandardEntityPawn
    {
        public CompPpodae PpodaeComp => EntityComp as CompPpodae;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null) return;
            CompPpodae comp = PpodaeComp;
            AnimationDef desired;
            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                if (comp != null && comp.IsEating) desired = PpodaeDefOf.Ppodae_Puppy_Eat;
                else if (comp != null && comp.IsBarking) desired = PpodaeDefOf.Ppodae_Puppy_Bark;
                else desired = PpodaeDefOf.Ppodae_Puppy_Idle;
            }
            else if (comp != null && comp.IsFrenzyAttacking)
            {
                desired = PpodaeDefOf.Ppodae_Frenzy_Attack;
            }
            else if (pather != null && pather.Moving)
            {
                desired = PpodaeDefOf.Ppodae_Frenzy_Walk;
            }
            else
            {
                desired = PpodaeDefOf.Ppodae_Frenzy_Idle;
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
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }
    }

    public class JobGiver_PpodaeFrenzyHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return null;
            CompPpodae comp = pawn.TryGetComp<CompPpodae>();
            if (comp != null && comp.ShouldReturnToEggSoon)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 65f, p => p != pawn);
            if (target == null) return null;

            if (pawn.Position.AdjacentTo8WayOrInside(target.Position))
            {
                return JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            }

            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.expiryInterval = 60;
            attack.checkOverrideOnExpire = true;
            attack.canBashDoors = true;
            return attack;
        }
    }

    /// <summary>D-02-107 狂暴爪击：触发冲击波与特效，并在击杀人类后使波迪满足地自我化卵。</summary>
    public class DamageWorker_PpodaeClaw : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn attacker = dinfo.Instigator as Pawn;
            CompPpodae comp = attacker?.TryGetComp<CompPpodae>();
            if (comp != null)
            {
                dinfo.SetAmount(Rand.Range(comp.PropsPpodae.frenzyAttackMinDmg, comp.PropsPpodae.frenzyAttackMaxDmg));
                comp.TriggerFrenzyAttack(victim as Pawn);
            }

            DamageResult res = base.Apply(dinfo, victim);
            if (comp != null && victim is Pawn pv && (pv.Dead || pv.Destroyed))
            {
                comp.Notify_SlainHumanlike(pv);
            }
            return res;
        }
    }
}
