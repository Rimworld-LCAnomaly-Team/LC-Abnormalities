using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.AllAroundHelper
{
    [DefOf]
    public static class AllAroundHelperDefOf
    {
        public static SoundDef LC_AllAroundHelper_Gaging;
        public static SoundDef LC_AllAroundHelper_GageMax;
        public static SoundDef LC_AllAroundHelper_GageMaxSequence;
        public static SoundDef LC_AllAroundHelper_Rise;
        public static SoundDef LC_AllAroundHelper_SystemOff;
        public static SoundDef LC_AllAroundHelper_Spark;
        public static SoundDef LC_AllAroundHelper_Transform;
        public static SoundDef LC_GrinderMk4_Hit;

        public static AnimationDef AllAroundHelper_Lv1_Idle;
        public static AnimationDef AllAroundHelper_Lv2_Idle;
        public static AnimationDef AllAroundHelper_Work;
        public static AnimationDef AllAroundHelper_Breached_Move;
        public static AnimationDef AllAroundHelper_Charge;
        public static AnimationDef AllAroundHelper_Spin;
        public static AnimationDef AllAroundHelper_Shutdown;

        public static ThingDef LC_HelperChargeTelegraphFX;
        public static ThingDef LC_HelperSawVortexFX;
        public static ThingDef LC_HelperDashTrailFX;
        public static ThingDef LC_GrinderSawSlashFX;
        public static ThingDef LC_GrinderFlurryFX;
        public static ThingDef LC_HelperSparkBurstFX;
        public static ThingDef LC_GrinderMk4SkillRunnerFX;

        static AllAroundHelperDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AllAroundHelperDefOf));
        }
    }

    public class CompProperties_AllAroundHelper : CompProperties_LCAbnormality
    {
        public int chargeWindupTicks = 96;
        public int dashDurationTicks = 42;
        public int shutdownDurationTicks = 180;
        public int dashCooldownTicks = 60;
        public float dashRange = 14f;
        public float dashDamageMin = 18f;
        public float dashDamageMax = 28f;

        public CompProperties_AllAroundHelper()
        {
            compClass = typeof(CompAllAroundHelper);
        }
    }

    /// <summary>
    /// T-05-41 小帮手核心逻辑（对齐解包 Helper.fbx 与脑叶 Wiki）：
    /// 1) 收容期：计数=2 为纯白清洁机器人 Lv1；计数=1 时弹出上方血刃机械臂进入 Lv2 躁动态；
    /// 2) 出逃期：展开四条二段关节血镰进入 Lv3；索敌后原地蓄力（黄色→猩红警戒环 + 直线走廊预警），
    ///    满蓄力后高速旋转电锯冲锋撕裂路径目标，撞墙或冲刺结束后进入过热宕机冷却窗（SystemOff + 短路电弧），随后重启继续清扫。
    /// </summary>
    public class CompAllAroundHelper : CompLCAbnormality
    {
        private int studyingGraceTicks;
        private int chargeTicksLeft;
        private int dashTicksLeft;
        private int shutdownTicksLeft;
        private int dashCooldownTicks = 60;
        private IntVec3 dashTargetCell = IntVec3.Invalid;
        private readonly HashSet<int> hitThisDash = new HashSet<int>();

        private Thing_HelperChargeTelegraph activeTelegraph;
        private Thing_HelperSawVortex activeVortex;

        public CompProperties_AllAroundHelper PropsHelper => (CompProperties_AllAroundHelper)props;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public bool IsCharging => chargeTicksLeft > 0 && Escaped;

        public bool IsDashing => dashTicksLeft > 0 && Escaped;

        public bool IsShuttingDown => shutdownTicksLeft > 0 && Escaped;

        public bool IsSawingActive => IsCharging || IsDashing;

        public IntVec3 DashTargetCell => dashTargetCell;

        public float ChargeProgress
        {
            get
            {
                if (chargeTicksLeft <= 0 || PropsHelper.chargeWindupTicks <= 0)
                {
                    return 0f;
                }

                return Mathf.Clamp01(1f - chargeTicksLeft / (float)PropsHelper.chargeWindupTicks);
            }
        }

        private void ClearTransientFx()
        {
            if (activeTelegraph != null && !activeTelegraph.Destroyed)
            {
                activeTelegraph.Destroy();
            }
            activeTelegraph = null;

            if (activeVortex != null && !activeVortex.Destroyed)
            {
                activeVortex.Destroy();
            }
            activeVortex = null;
        }

        private void ClearCombatState()
        {
            chargeTicksLeft = 0;
            dashTicksLeft = 0;
            shutdownTicksLeft = 0;
            dashTargetCell = IntVec3.Invalid;
            hitThisDash.Clear();
            ClearTransientFx();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            studyingGraceTicks = 90;
            return base.Notify_StudyInterval(studier, workType);
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (Rand.Chance(0.50f))
            {
                int prev = QliphothCountCurrent;
                QliphothCountCurrent--;
                if (!Escaped && prev > 1 && QliphothCountCurrent == 1)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Transform, parent);
                }
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            int prev = QliphothCountCurrent;
            QliphothCountCurrent--;
            if (!Escaped && prev > 1 && QliphothCountCurrent == 1)
            {
                LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Transform, parent);
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            studyingGraceTicks = 0;
            ClearCombatState();
            dashCooldownTicks = 55;
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Rise, parent);
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Transform, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                AllAroundHelperVfxUtility.SpawnSparkBurst(self.Map, self.DrawPos, scale: 1.05f, lifeTicks: 18, isCrimson: true);
            }
            (parent as LC_AllAroundHelperPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            studyingGraceTicks = 0;
            ClearCombatState();
            (parent as LC_AllAroundHelperPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            ClearCombatState();
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || !(parent is Pawn pawn) || pawn.Destroyed || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            // 1. Active high-RPM rotary buzzsaw linear dash (Lv3_Spin)
            if (dashTicksLeft > 0)
            {
                StepLinearDash(pawn);
                return;
            }

            // 2. Active charge windup (Lv3_Attack_On)
            if (chargeTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                chargeTicksLeft--;
                if (activeTelegraph == null || activeTelegraph.Destroyed || activeTelegraph.Map != pawn.Map)
                {
                    activeTelegraph = AllAroundHelperVfxUtility.SpawnChargeTelegraph(pawn, dashTargetCell);
                }

                if (chargeTicksLeft == 0)
                {
                    StartLinearDash(pawn);
                }
                return;
            }

            // 3. Post-dash overheat shutdown downtime (Lv3_Attack_Off)
            if (shutdownTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                shutdownTicksLeft--;
                if (shutdownTicksLeft % 28 == 0 && shutdownTicksLeft > 24)
                {
                    Vector3 pos = pawn.DrawPos + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.25f, 0.45f));
                    AllAroundHelperVfxUtility.SpawnSparkBurst(pawn.Map, pos, scale: 0.68f, lifeTicks: 14, isCrimson: false);
                    FleckMaker.ThrowSmoke(pawn.DrawPos, pawn.Map, 0.75f);
                }
                if (shutdownTicksLeft == 0)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Rise, pawn);
                    dashCooldownTicks = PropsHelper.dashCooldownTicks;
                }
                return;
            }

            if (dashCooldownTicks > 0)
            {
                dashCooldownTicks--;
                return;
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, PropsHelper.dashRange);
            if (target != null)
            {
                BeginDashCharge(target.Position);
            }
        }

        public void BeginDashCharge(IntVec3 targetCell)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            dashTargetCell = targetCell;
            chargeTicksLeft = PropsHelper.chargeWindupTicks;
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Gaging, parent);
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Transform, parent);

            if (parent is Pawn pawn && pawn.Spawned && pawn.Map != null)
            {
                pawn.pather?.StopDead();
                ClearTransientFx();
                activeTelegraph = AllAroundHelperVfxUtility.SpawnChargeTelegraph(pawn, dashTargetCell);
                (pawn as LC_AllAroundHelperPawn)?.PlayChargeAnimation();
            }
        }

        public void StartLinearDash(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (activeTelegraph != null && !activeTelegraph.Destroyed)
            {
                activeTelegraph.Destroy();
            }
            activeTelegraph = null;

            dashTicksLeft = PropsHelper.dashDurationTicks;
            hitThisDash.Clear();
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_GageMax, pawn);
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_GageMaxSequence, pawn);

            if (!dashTargetCell.IsValid || !dashTargetCell.InBounds(pawn.Map))
            {
                Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, PropsHelper.dashRange);
                dashTargetCell = target != null ? target.Position : pawn.Position;
            }

            // Extend target slightly past the target pawn so Helper tears cleanly THROUGH the target like in Lobotomy Corp!
            Vector3 toTarget = (dashTargetCell - pawn.Position).ToVector3();
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.25f)
            {
                Vector3 dir = toTarget.normalized;
                float desiredDist = Mathf.Clamp(toTarget.magnitude + 3.0f, 5.0f, PropsHelper.dashRange);
                IntVec3 extended = (pawn.Position.ToVector3Shifted() + dir * desiredDist).ToIntVec3().ClampInsideMap(pawn.Map);
                dashTargetCell = extended;
            }

            activeVortex = AllAroundHelperVfxUtility.SpawnSawVortex(pawn);
            (pawn as LC_AllAroundHelperPawn)?.PlaySpinAnimation();
        }

        private void EnterShutdown(Pawn pawn)
        {
            dashTicksLeft = 0;
            shutdownTicksLeft = PropsHelper.shutdownDurationTicks;
            dashTargetCell = IntVec3.Invalid;
            LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_SystemOff, pawn);

            if (pawn != null && pawn.Spawned && pawn.Map != null)
            {
                FleckMaker.ThrowSmoke(pawn.DrawPos, pawn.Map, 1.4f);
                AllAroundHelperVfxUtility.SpawnSparkBurst(pawn.Map, pawn.DrawPos, scale: 0.95f, lifeTicks: 18, isCrimson: false);
                (pawn as LC_AllAroundHelperPawn)?.PlayShutdownAnimation();
            }
        }

        private void StepLinearDash(Pawn pawn)
        {
            dashTicksLeft--;
            Map map = pawn.Map;
            if (map == null)
            {
                dashTicksLeft = 0;
                return;
            }

            if (activeVortex == null || activeVortex.Destroyed || activeVortex.Map != map)
            {
                activeVortex = AllAroundHelperVfxUtility.SpawnSawVortex(pawn);
            }

            bool hitObstacle = false;
            // 每 3 Tick 沿直线推进 1 格并生成猩红钢刃尾流与火花，遇墙或到达终点后进入过热宕机
            if (dashTicksLeft % 3 == 0 && dashTargetCell.IsValid && pawn.Position != dashTargetCell)
            {
                Vector3 prevPos = pawn.DrawPos;
                Vector3 delta = (dashTargetCell - pawn.Position).ToVector3();
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.01f)
                {
                    delta.Normalize();
                    IntVec3 next = pawn.Position + new IntVec3(Mathf.RoundToInt(delta.x), 0, Mathf.RoundToInt(delta.z));
                    if (next.InBounds(map) && next.Standable(map) && next.GetEdifice(map) == null)
                    {
                        pawn.Position = next;
                        pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
                        Vector3 newPos = pawn.DrawPos;
                        AllAroundHelperVfxUtility.SpawnDashTrail(map, prevPos, newPos, width: 1.95f, lifeTicks: 24);
                        FleckMaker.ThrowMicroSparks(newPos, map);
                        if (dashTicksLeft % 6 == 0)
                        {
                            AllAroundHelperVfxUtility.SpawnSparkBurst(map, newPos, scale: 0.72f, lifeTicks: 12, isCrimson: true);
                        }
                    }
                    else
                    {
                        hitObstacle = true;
                        FleckMaker.ThrowMicroSparks(pawn.DrawPos, map);
                        LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Spark, pawn);
                    }
                }
            }

            // 对当前 1.9 格内的非友方活体目标施加高速电锯红伤连斩与锯齿刀光特效
            ApplyDashCleave(pawn);

            if (hitObstacle || dashTicksLeft <= 0 || pawn.Position == dashTargetCell)
            {
                EnterShutdown(pawn);
            }
        }

        public int ApplyDashCleave(Pawn self)
        {
            if (self == null || !self.Spawned || self.Map == null)
            {
                return 0;
            }

            int hits = 0;
            List<Pawn> candidates = new List<Pawn>(self.Map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn victim = candidates[i];
                if (victim == null || victim == self || victim.Dead || victim.Destroyed || !victim.Spawned)
                {
                    continue;
                }

                if (victim.Faction == self.Faction)
                {
                    continue;
                }

                if (hitThisDash.Contains(victim.thingIDNumber))
                {
                    continue;
                }

                if (victim.Position.DistanceTo(self.Position) <= 1.9f)
                {
                    hitThisDash.Add(victim.thingIDNumber);
                    float dmg = Rand.Range(PropsHelper.dashDamageMin, PropsHelper.dashDamageMax);
                    victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, dmg, 0.35f, instigator: self));
                    LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Spark, victim);

                    Vector3 vPos = victim.Spawned ? victim.DrawPos : victim.PositionHeld.ToVector3Shifted();
                    float yaw = (vPos - self.DrawPos).AngleFlat();
                    AllAroundHelperVfxUtility.SpawnSawSlash(self.Map, vPos, yaw + Rand.Range(-25f, 25f), scale: 2.15f, flipSweep: false, lifeTicks: 15, spanRad: 2.85f);
                    AllAroundHelperVfxUtility.SpawnSawSlash(self.Map, vPos, yaw + 150f + Rand.Range(-25f, 25f), scale: 1.85f, flipSweep: true, lifeTicks: 13, spanRad: 2.55f);
                    AllAroundHelperVfxUtility.SpawnSparkBurst(self.Map, vPos, scale: 1.05f, lifeTicks: 16, isCrimson: true);
                    FleckMaker.ThrowMicroSparks(vPos, self.Map);

                    if (Find.CurrentMap == self.Map && Find.CameraDriver != null)
                    {
                        Find.CameraDriver.shaker.DoShake(0.35f);
                    }

                    hits++;
                }
            }

            return hits;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref studyingGraceTicks, "aahStudyingGraceTicks", 0);
            Scribe_Values.Look(ref chargeTicksLeft, "aahChargeTicksLeft", 0);
            Scribe_Values.Look(ref dashTicksLeft, "aahDashTicksLeft", 0);
            Scribe_Values.Look(ref shutdownTicksLeft, "aahShutdownTicksLeft", 0);
            Scribe_Values.Look(ref dashCooldownTicks, "aahDashCooldownTicks", 60);
            Scribe_Values.Look(ref dashTargetCell, "aahDashTargetCell", IntVec3.Invalid);
        }
    }

    public class LC_AllAroundHelperPawn : LC_StandardEntityPawn
    {
        public CompAllAroundHelper HelperComp => EntityComp as CompAllAroundHelper;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public override string CurrentBodyTexPath =>
            HelperComp?.IsSawingActive == true
                ? "Things/Pawn/AllAroundHelper/AllAroundHelper_Sawing"
                : "Things/Pawn/AllAroundHelper/AllAroundHelper";

        public override Vector2 CurrentBodyDrawSize => new Vector2(1.95f, 1.95f);

        public void PlayChargeAnimation()
        {
            if (AllAroundHelperDefOf.AllAroundHelper_Charge != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(AllAroundHelperDefOf.AllAroundHelper_Charge);
            }
        }

        public void PlaySpinAnimation()
        {
            if (AllAroundHelperDefOf.AllAroundHelper_Spin != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(AllAroundHelperDefOf.AllAroundHelper_Spin);
            }
        }

        public void PlayShutdownAnimation()
        {
            if (AllAroundHelperDefOf.AllAroundHelper_Shutdown != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(AllAroundHelperDefOf.AllAroundHelper_Shutdown);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompAllAroundHelper comp = HelperComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                if (comp != null && comp.IsBeingStudiedNow)
                {
                    desired = AllAroundHelperDefOf.AllAroundHelper_Work;
                }
                else if (comp != null && comp.QliphothCountCurrent <= 1)
                {
                    desired = AllAroundHelperDefOf.AllAroundHelper_Lv2_Idle;
                }
                else
                {
                    desired = AllAroundHelperDefOf.AllAroundHelper_Lv1_Idle;
                }
            }
            else if (comp != null && comp.IsDashing)
            {
                desired = AllAroundHelperDefOf.AllAroundHelper_Spin;
            }
            else if (comp != null && comp.IsCharging)
            {
                desired = AllAroundHelperDefOf.AllAroundHelper_Charge;
            }
            else if (comp != null && comp.IsShuttingDown)
            {
                desired = AllAroundHelperDefOf.AllAroundHelper_Shutdown;
            }
            else
            {
                desired = AllAroundHelperDefOf.AllAroundHelper_Breached_Move;
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
            CompAllAroundHelper comp = HelperComp;
            if (comp != null && (comp.IsCharging || comp.IsDashing || comp.IsShuttingDown) && pather?.Moving == true)
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

    /// <summary>小帮手出逃 AI：蓄力/冲锋/过热宕机期间停止移动交由 TickBreached 推进，常态迅速逼近最近人类目标。</summary>
    public class JobGiver_AllAroundHelperHunt : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompAllAroundHelper comp = pawn.TryGetComp<CompAllAroundHelper>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsSawingActive || comp.IsShuttingDown)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            Verse.AI.Job approach = JobMaker.MakeJob(JobDefOf.Goto, target);
            approach.expiryInterval = 45;
            approach.checkOverrideOnExpire = true;
            approach.canBashDoors = true;
            approach.locomotionUrgency = LocomotionUrgency.Sprint;
            return approach;
        }
    }

    /// <summary>
    /// 粉碎机 Mk4（HE 级高频电锯长柄武器）伤害处理器：
    /// 每次命中播放原作锯切音效并触发旋转锯齿环 + 4 连锯齿撕裂刀光 VFX，对主目标施加 45% 额外红伤撕裂，
    /// 并顺势割伤主目标周围 1.65 格内最多 2 名敌对目标（40% 红伤溅射）。
    /// </summary>
    public class DamageWorker_GrinderMk4 : DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (victim == null)
            {
                return result;
            }

            Map map = victim.MapHeld;
            IntVec3 center = victim.PositionHeld;
            if (map != null && center.InBounds(map))
            {
                Vector3 hitPos = victim.Spawned ? victim.DrawPos : center.ToVector3Shifted();
                float attackYaw = dinfo.Instigator != null && dinfo.Instigator.Spawned
                    ? (hitPos - dinfo.Instigator.DrawPos).AngleFlat()
                    : dinfo.Angle;

                // Play authentic Lobotomy Corp Grinder Mk4 buzzsaw audio + spawn spinning Helper_Edge sawblade flurry VFX!
                LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_GrinderMk4_Hit, victim);
                AllAroundHelperVfxUtility.SpawnGrinderFlurry(map, hitPos, attackYaw);

                // Extra rotary shredding hit on the primary target
                if (!victim.Destroyed && !(victim is Pawn vp && vp.Dead))
                {
                    float extraDmg = Mathf.Max(3f, dinfo.Amount * 0.45f);
                    DamageInfo shred = new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        extraDmg,
                        dinfo.ArmorPenetrationInt,
                        dinfo.Angle,
                        dinfo.Instigator,
                        weapon: dinfo.Weapon);
                    victim.TakeDamage(shred);
                }

                // Short-range rotary buzzsaw cleave against up to 2 adjacent hostile foes
                if (dinfo.Instigator is Pawn attacker)
                {
                    float cleaveDmg = Mathf.Max(4f, dinfo.Amount * 0.40f);
                    int cleaved = 0;
                    List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                    for (int i = 0; i < nearby.Count && cleaved < 2; i++)
                    {
                        Pawn other = nearby[i];
                        if (other == null || other == victim || other == attacker || other.Dead || other.Destroyed || !other.Spawned)
                        {
                            continue;
                        }

                        if (!other.HostileTo(attacker))
                        {
                            continue;
                        }

                        if (other.Position.DistanceTo(center) <= 1.65f)
                        {
                            other.TakeDamage(new DamageInfo(
                                LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                                cleaveDmg,
                                0.25f,
                                attackYaw,
                                attacker,
                                weapon: dinfo.Weapon));
                            AllAroundHelperVfxUtility.SpawnSawSlash(map, other.DrawPos, attackYaw + Rand.Range(-35f, 35f), scale: 1.55f, flipSweep: (cleaved % 2) == 1, lifeTicks: 12);
                            AllAroundHelperVfxUtility.SpawnSparkBurst(map, other.DrawPos, scale: 0.65f, lifeTicks: 11, isCrimson: true);
                            cleaved++;
                        }
                    }
                }
            }

            return result;
        }
    }
}
