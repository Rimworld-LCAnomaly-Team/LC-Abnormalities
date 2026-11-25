using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.PunishingBird
{
    [DefOf]
    public static class PunishingBirdDefOf
    {
        public static ThingDef PunishingBirdEgg;
        public static HediffDef LC_PunishingBirdEnragedBuff;
        public static SoundDef LC_PunishingBird_Hit;
        public static SoundDef LC_PunishingBird_Open;
        public static SoundDef LC_PunishingBird_WingFlap;
        public static SoundDef LC_PunishingBird_MawBite;
        public static SoundDef LC_PunishingBird_BeakShot;

        public static ThingDef LC_BeakTracerFX;
        public static ThingDef LC_BeakImpactFX;
        public static ThingDef LC_BeakSkillRunnerFX;

        public static AnimationDef PunishingBird_Idle;
        public static AnimationDef PunishingBird_Work;
        public static AnimationDef PunishingBird_Fly;
        public static AnimationDef PunishingBird_FlyIdle;
        public static AnimationDef PunishingBird_Peck;
        public static AnimationDef PunishingBird_EnragedFly;
        public static AnimationDef PunishingBird_EnragedAttack;

        static PunishingBirdDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PunishingBirdDefOf));
        }
    }

    public class CompProperties_PunishingBird : CompProperties_LCAbnormality
    {
        public int calmRoamDurationTicks = 2700;
        public int peckIntervalTicks = 90;

        public CompProperties_PunishingBird()
        {
            compClass = typeof(CompPunishingBird);
        }
    }

    /// <summary>O-02-56 惩戒鸟核心逻辑：突破后振翅温和啄击四周，若敢反击则立刻浑身赤红暴怒裂口追猎并用 75~100 红伤巨口咬噬处决后化蛋。</summary>
    public class CompPunishingBird : CompLCAbnormality
    {
        private const int CalmPeckAnimDuration = 46;
        private const int CalmPeckHitRemaining = 23;
        private const int MawAttackAnimDuration = 42;
        private const int MawAttackHitRemaining = 24;

        private bool isEnraged;
        private int breachTicks;
        private int lastPeckTick = -9999;
        private int postLossReturnTicks;
        private Pawn rageTarget;

        private int studyingGraceTicks;
        private int calmPeckAnimTicks;
        private bool calmPeckHitPending;
        private Pawn calmPeckTarget;

        private int mawAttackTicks;
        private bool mawHitPending;
        private Pawn mawAttackTarget;

        public CompProperties_PunishingBird PropsBird => (CompProperties_PunishingBird)props;

        public bool IsEnraged => isEnraged;

        public Pawn RageTarget => rageTarget;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public bool IsPecking => calmPeckAnimTicks > 0;

        public bool IsPerformingMawAttack => mawAttackTicks > 0;

        private void ClearTransientCombatState()
        {
            isEnraged = false;
            breachTicks = 0;
            postLossReturnTicks = 0;
            rageTarget = null;
            calmPeckAnimTicks = 0;
            calmPeckHitPending = false;
            calmPeckTarget = null;
            mawAttackTicks = 0;
            mawHitPending = false;
            mawAttackTarget = null;
            RemoveEnrageBuff();
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            studyingGraceTicks = 0;
            ClearTransientCombatState();
            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_WingFlap, parent);
            (parent as LC_PunishingBirdPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            studyingGraceTicks = 0;
            ClearTransientCombatState();
            (parent as LC_PunishingBirdPawn)?.UpdateAnimation(force: true);
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            studyingGraceTicks = 90;
            return base.Notify_StudyInterval(studier, workType);
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }
        }

        public void TriggerEnrage(Pawn attacker)
        {
            if (parent == null || parent.Destroyed || !Escaped)
            {
                return;
            }

            if (!isEnraged)
            {
                isEnraged = true;
                postLossReturnTicks = 0;
                calmPeckAnimTicks = 0;
                calmPeckHitPending = false;
                calmPeckTarget = null;
                LCAbnormalitiesHelper.ApplyOrRefreshHediff(parent as Pawn, PunishingBirdDefOf.LC_PunishingBirdEnragedBuff, 1f);
                LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Open, parent);
                Messages.Message(
                    "LC_PunishingBird_EnragedMsg".Translate(),
                    parent,
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }

            if (attacker != null && !attacker.Dead && attacker.Spawned && attacker.Map == parent.Map)
            {
                rageTarget = attacker;
            }
            else if (rageTarget == null && parent.Map != null)
            {
                rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position);
            }

            if (parent is LC_PunishingBirdPawn pb)
            {
                if (rageTarget != null)
                {
                    pb.FaceCellHorizontal(rageTarget.Position);
                }
                pb.UpdateAnimation(force: true);
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || parent == null || parent.Destroyed || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            breachTicks++;
            LC_PunishingBirdPawn birdPawn = parent as LC_PunishingBirdPawn;

            // Rhythmic soft wingbeat audio while flying across the map
            int flapInterval = isEnraged ? 18 : 28;
            if (birdPawn != null && birdPawn.pather?.Moving == true && calmPeckAnimTicks <= 0 && mawAttackTicks <= 0 && breachTicks % flapInterval == 0)
            {
                LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_WingFlap, parent);
            }

            if (!isEnraged)
            {
                if (calmPeckAnimTicks > 0)
                {
                    calmPeckAnimTicks--;
                    if (calmPeckTarget != null && calmPeckTarget.Spawned && calmPeckTarget.Map == parent.Map)
                    {
                        birdPawn?.FaceCellHorizontal(calmPeckTarget.Position);
                    }

                    if (calmPeckHitPending && calmPeckAnimTicks <= CalmPeckHitRemaining)
                    {
                        calmPeckHitPending = false;
                        ApplyCalmPeckImpact(calmPeckTarget);
                    }

                    if (calmPeckAnimTicks <= 0)
                    {
                        calmPeckTarget = null;
                    }
                }
                else
                {
                    Pawn calmTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position, 1.6f);
                    if (calmTarget != null && CanCalmPeckNow())
                    {
                        ExecuteCalmPeck(calmTarget);
                    }
                }

                if (breachTicks >= PropsBird.calmRoamDurationTicks && calmPeckAnimTicks <= 0)
                {
                    ReturnQuietlyToEgg();
                }
                return;
            }

            // Enraged smoke puffs matching SmallBirdDef.fbx AngrySmoke emitter
            if (breachTicks % 18 == 0 && parent.Map != null)
            {
                Vector3 puffPos = parent.DrawPos + new Vector3(Rand.Range(-0.18f, 0.18f), 0f, Rand.Range(0.18f, 0.32f));
                FleckMaker.ThrowSmoke(puffPos, parent.Map, 0.45f);
            }

            // Active enraged maw bite sequence (42 ticks: rip open -> slam shut at tick 18 -> retract -> return to egg)
            if (mawAttackTicks > 0)
            {
                mawAttackTicks--;
                if (mawAttackTarget != null && mawAttackTarget.Spawned && mawAttackTarget.Map == parent.Map)
                {
                    birdPawn?.FaceCellHorizontal(mawAttackTarget.Position);
                }

                if (mawHitPending && mawAttackTicks <= MawAttackHitRemaining)
                {
                    mawHitPending = false;
                    ApplyMawPunishmentImpact(mawAttackTarget);
                }

                if (mawAttackTicks <= 0)
                {
                    mawAttackTarget = null;
                    ReturnQuietlyToEgg();
                }
                return;
            }

            if (rageTarget == null || rageTarget.Dead || !rageTarget.Spawned || rageTarget.Map != parent.Map)
            {
                rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position);
                if (rageTarget == null)
                {
                    postLossReturnTicks++;
                    if (postLossReturnTicks >= 300)
                    {
                        ReturnQuietlyToEgg();
                    }
                    return;
                }
            }

            postLossReturnTicks = 0;
            if (parent.Position.DistanceTo(rageTarget.Position) <= 1.9f)
            {
                StartMawPunishment(rageTarget);
            }
        }

        public bool CanCalmPeckNow()
        {
            if (calmPeckAnimTicks > 0)
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            return now - lastPeckTick >= PropsBird.peckIntervalTicks;
        }

        public void ExecuteCalmPeck(Pawn target)
        {
            if (target == null || target.Dead || parent == null)
            {
                return;
            }

            lastPeckTick = Find.TickManager?.TicksGame ?? 0;
            calmPeckTarget = target;
            calmPeckAnimTicks = CalmPeckAnimDuration;
            calmPeckHitPending = true;
            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_WingFlap, parent);

            if (parent is LC_PunishingBirdPawn pb)
            {
                pb.pather?.StopDead();
                pb.FaceCellHorizontal(target.Position);
                pb.PlayPeckAnimation();
            }
        }

        private void ApplyCalmPeckImpact(Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || parent == null || target.Map != parent.Map)
            {
                return;
            }

            if (parent.Position.DistanceTo(target.Position) > 3.2f)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Hit, parent);
            target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 2f, instigator: parent));
        }

        public void StartMawPunishment(Pawn target)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            mawAttackTarget = target;
            mawAttackTicks = MawAttackAnimDuration;
            mawHitPending = true;
            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Open, parent);

            if (parent is LC_PunishingBirdPawn pb)
            {
                pb.pather?.StopDead();
                if (target != null && target.Spawned)
                {
                    pb.FaceCellHorizontal(target.Position);
                }
                pb.PlayEnragedAttackAnimation();
            }
        }

        /// <summary>保留原公开入口以兼容外部调用；立即结算巨口伤害并化蛋。</summary>
        public void ExecuteMawPunishment(Pawn target)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Open, parent);
            ApplyMawPunishmentImpact(target);
            ReturnQuietlyToEgg();
        }

        private void ApplyMawPunishmentImpact(Pawn target)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_MawBite ?? PunishingBirdDefOf.LC_PunishingBird_Hit, parent);
            IntVec3 burstPos = (target != null && target.Spawned && target.Map == parent.Map)
                ? target.Position
                : parent.Position;
            if (parent.Map != null && burstPos.InBounds(parent.Map))
            {
                AbnormalityUtility.Burst(burstPos, parent.Map, 1.8f);
            }

            if (target != null && !target.Dead && target.Spawned && target.Map == parent.Map && parent.Position.DistanceTo(target.Position) <= 4.0f)
            {
                int dmg = Rand.RangeInclusive(75, 100);
                target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, dmg, 0.9f, instigator: parent));
                Messages.Message(
                    "LC_PunishingBird_PunishMsg".Translate(target.LabelShortCap),
                    new LookTargets(target.PositionHeld, target.MapHeld),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }
        }

        public void ReturnQuietlyToEgg()
        {
            if (parent is Pawn p && !p.Destroyed)
            {
                if (p.MapHeld != null)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_WingFlap, p);
                    Messages.Message(
                        "LC_PunishingBird_ReturnMsg".Translate(),
                        new LookTargets(p.PositionHeld, p.MapHeld),
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
                LCAbnormalitiesHelper.ReturnEntityToEgg(p, PunishingBirdDefOf.PunishingBirdEgg, biosignature);
            }
        }

        private void RemoveEnrageBuff()
        {
            if (parent is Pawn p && PunishingBirdDefOf.LC_PunishingBirdEnragedBuff != null)
            {
                Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(PunishingBirdDefOf.LC_PunishingBirdEnragedBuff);
                if (h != null)
                {
                    p.health.RemoveHediff(h);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isEnraged, "pbIsEnraged", false);
            Scribe_Values.Look(ref breachTicks, "pbBreachTicks", 0);
            Scribe_Values.Look(ref lastPeckTick, "pbLastPeckTick", -9999);
            Scribe_Values.Look(ref postLossReturnTicks, "pbPostLossReturnTicks", 0);
            Scribe_References.Look(ref rageTarget, "pbRageTarget");
            Scribe_Values.Look(ref studyingGraceTicks, "pbStudyingGraceTicks", 0);
            Scribe_Values.Look(ref calmPeckAnimTicks, "pbCalmPeckAnimTicks", 0);
            Scribe_Values.Look(ref calmPeckHitPending, "pbCalmPeckHitPending", false);
            Scribe_References.Look(ref calmPeckTarget, "pbCalmPeckTarget");
            Scribe_Values.Look(ref mawAttackTicks, "pbMawAttackTicks", 0);
            Scribe_Values.Look(ref mawHitPending, "pbMawHitPending", false);
            Scribe_References.Look(ref mawAttackTarget, "pbMawAttackTarget");
        }
    }

    public class LC_PunishingBirdPawn : LC_StandardEntityPawn, ILCFourColorDynamicResistance
    {
        private bool facingWest = false;

        public CompPunishingBird BirdComp => EntityComp as CompPunishingBird;

        public bool IsFacingLeft => facingWest;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public override string CurrentBodyTexPath =>
            BirdComp?.IsEnraged == true
                ? "Things/Pawn/PunishingBird/PunishingBird_Enraged"
                : "Things/Pawn/PunishingBird/PunishingBird";

        public override Vector2 CurrentBodyDrawSize =>
            BirdComp?.IsEnraged == true
                ? new Vector2(2.2f, 2.2f)
                : new Vector2(1.25f, 1.25f);

        public void FaceCellHorizontal(IntVec3 cell)
        {
            rotationTracker?.FaceCell(cell);
            if (Spawned && cell.IsValid)
            {
                if (cell.x < Position.x)
                {
                    facingWest = true;
                }
                else if (cell.x > Position.x)
                {
                    facingWest = false;
                }
            }
        }

        public void PlayPeckAnimation()
        {
            if (PunishingBirdDefOf.PunishingBird_Peck != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(PunishingBirdDefOf.PunishingBird_Peck);
            }
        }

        public void PlayEnragedAttackAnimation()
        {
            if (PunishingBirdDefOf.PunishingBird_EnragedAttack != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(PunishingBirdDefOf.PunishingBird_EnragedAttack);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompPunishingBird comp = BirdComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                if (comp != null && comp.IsBeingStudiedNow)
                {
                    desired = PunishingBirdDefOf.PunishingBird_Work;
                }
                else if (comp?.HoldingPlaformTargetComp?.CurrentlyContainedInRoom == true && Spawned && pather?.Moving == true)
                {
                    desired = PunishingBirdDefOf.PunishingBird_Fly;
                }
                else
                {
                    desired = PunishingBirdDefOf.PunishingBird_Idle;
                }
            }
            else if (comp != null && comp.IsEnraged)
            {
                desired = comp.IsPerformingMawAttack
                    ? PunishingBirdDefOf.PunishingBird_EnragedAttack
                    : PunishingBirdDefOf.PunishingBird_EnragedFly;
            }
            else if (comp != null && comp.IsPecking)
            {
                desired = PunishingBirdDefOf.PunishingBird_Peck;
            }
            else
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? PunishingBirdDefOf.PunishingBird_Fly
                    : PunishingBirdDefOf.PunishingBird_FlyIdle;
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
            facingWest = false;
            UpdateAnimation();
        }

        protected override void Tick()
        {
            CompPunishingBird comp = BirdComp;
            if (comp != null)
            {
                if ((comp.IsPerformingMawAttack || comp.IsPecking) && pather?.Moving == true)
                {
                    pather.StopDead();
                }

                if ((comp.Escaped || comp.HoldingPlaformTargetComp?.CurrentlyContainedInRoom == true) && Spawned && !Dead)
                {
                    UpdateHorizontalFacing();
                }
                else if (!comp.Escaped)
                {
                    facingWest = false;
                }
            }

            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        private void UpdateHorizontalFacing()
        {
            if (pather != null && pather.Moving && pather.nextCell.IsValid)
            {
                if (pather.nextCell.x < Position.x)
                {
                    facingWest = true;
                    return;
                }
                if (pather.nextCell.x > Position.x)
                {
                    facingWest = false;
                    return;
                }
            }

            if (Rotation == Rot4.West)
            {
                facingWest = true;
            }
            else if (Rotation == Rot4.East)
            {
                facingWest = false;
            }
        }

        public bool TryGetDynamicFourColorResistance(FourColorDamageType damageType, out float resistance)
        {
            if (BirdComp?.IsEnraged == true)
            {
                resistance = 0.1f;
                return true;
            }

            resistance = 2.0f;
            return false;
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (!Dead && totalDamageDealt > 0f && BirdComp?.Escaped == true)
            {
                BirdComp.TriggerEnrage(dinfo.Instigator as Pawn);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref facingWest, "pbFacingWest", false);
        }
    }

    /// <summary>惩戒鸟突破后的行为节点：暴怒时全速追逐目标并在贴身处发动巨口处决；温和时随机轻啄四周。</summary>
    public class JobGiver_PunishingBirdPeck : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompPunishingBird comp = (pawn as LC_PunishingBirdPawn)?.BirdComp ?? pawn.TryGetComp<CompPunishingBird>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsPerformingMawAttack || comp.IsPecking)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 15);
            }

            if (comp.IsEnraged)
            {
                Pawn rageTarget = comp.RageTarget;
                if (rageTarget == null || rageTarget.Dead || !rageTarget.Spawned || rageTarget.Map != pawn.Map)
                {
                    rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
                }

                if (rageTarget == null)
                {
                    return null;
                }

                if (pawn.Position.DistanceTo(rageTarget.Position) <= 1.9f)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait, 15);
                }

                Verse.AI.Job chase = JobMaker.MakeJob(JobDefOf.Goto, rageTarget);
                chase.expiryInterval = 30;
                chase.checkOverrideOnExpire = true;
                chase.canBashDoors = true;
                return chase;
            }

            Pawn calmTarget = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 18f);
            if (calmTarget == null)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(calmTarget.Position) <= 1.6f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 45);
            }

            Verse.AI.Job hop = JobMaker.MakeJob(JobDefOf.Goto, calmTarget);
            hop.expiryInterval = 90;
            hop.checkOverrideOnExpire = true;
            return hop;
        }
    }
}
