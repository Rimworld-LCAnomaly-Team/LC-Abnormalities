using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Damage;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.Schadenfreude
{
    [DefOf]
    public static class SchadenfreudeDefOf
    {
        public static HediffDef LC_GazeRending;

        public static SoundDef LC_Schadenfreude_Attack;
        public static SoundDef LC_Schadenfreude_Blood;
        public static SoundDef LC_Schadenfreude_Dead1;
        public static SoundDef LC_Schadenfreude_Dead2;
        public static SoundDef LC_Schadenfreude_Skill;
        public static SoundDef LC_Schadenfreude_SkillStart;
        public static SoundDef LC_Schadenfreude_SkillEnd;
        public static SoundDef LC_Schadenfreude_Transform;
        public static SoundDef LC_Gaze_Saw;
        public static SoundDef LC_Gaze_SawLoop;

        public static AnimationDef Schadenfreude_Box_Idle;
        public static AnimationDef Schadenfreude_Box_WorkWatched;
        public static AnimationDef Schadenfreude_Transform;
        public static AnimationDef Schadenfreude_Active_Idle;
        public static AnimationDef Schadenfreude_Walk;
        public static AnimationDef Schadenfreude_DrillAttack;
        public static AnimationDef Schadenfreude_SawWindup;
        public static AnimationDef Schadenfreude_SawSpin;
        public static AnimationDef Schadenfreude_SawRetract;

        public static ThingDef LC_SchadenfreudeWatchAuraFX;
        public static ThingDef LC_SchadenfreudeKeyholePulseFX;
        public static ThingDef LC_SchadenfreudeSawVortexFX;
        public static ThingDef LC_SchadenfreudeDrillThrustFX;
        public static ThingDef LC_SchadenfreudeBloodBurstFX;
        public static ThingDef LC_GazeSawSlashFX;
        public static ThingDef LC_GazeFlurryFX;
        public static ThingDef LC_GazeSkillRunnerFX;
        public static DamageDef LC_Gaze_SkillRed;

        static SchadenfreudeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SchadenfreudeDefOf));
        }
    }

    public class CompProperties_Schadenfreude : CompProperties_LCAbnormality
    {
        public int watchedWorkDropTicks = 210;
        public int attackCooldownTicks = 65;
        public int spinCooldownTicks = 260;
        public int transformDurationTicks = 38;
        public int drillAttackDurationTicks = 48;
        public int sawWindupTicks = 50;
        public int sawSpinDurationTicks = 180;
        public int sawRetractTicks = 36;
        public float spinDamage = 22f;
        public float spinRadius = 3.2f;

        public CompProperties_Schadenfreude()
        {
            compClass = typeof(CompSchadenfreude);
        }
    }

    /// <summary>
    /// O-05-76 幸灾乐祸核心逻辑（对齐解包 LookAtMe.txt 动画序列与脑叶 Wiki）：
    /// 1. 作业期间玩家摄像机不可注视它，持续注视约 3.5 秒即伴随锁孔警戒光圈扣除逆卡巴拉计数器（上限 2）；
    /// 2. 突破收容后，处于玩家视野内时会弹开机械钻腿与血肉高速追杀目标（R 1.2 / W 0.5 / B 1.0 / P 1.5），
    ///    具备机械钻腿穿刺与三段式双电锯狂暴切割（蓄力预警 -> 3 秒高频范围切割 -> 收锯）；
    ///    移出玩家视野后会立即中断一切攻击动作、缩回铁盒静止并切换高防御抗性（R 0.6 / W 0.2 / B 0.5 / P 0.7）。
    /// </summary>
    public class CompSchadenfreude : CompLCAbnormality
    {
        private int lastStudyingTick = -999;
        private int watchedWorkTicks;
        private bool workWarningSent;
        private bool droppedThisWork;
        private bool isWatchedActive;
        private int attackCooldown;
        private int spinCooldown;
        private int transformTicksLeft;
        private int drillAttackTicksLeft;
        private int sawWindupTicksLeft;
        private int sawSpinTicksLeft;
        private int sawRetractTicksLeft;
        private bool drillStrikeDelivered;
        private Pawn pendingDrillTarget;
        private bool? testWatchOverride;

        private Thing_SchadenfreudeWatchAura activeWatchAura;
        private Thing_SchadenfreudeSawVortex activeSawVortex;

        public CompProperties_Schadenfreude PropsSchaden => (CompProperties_Schadenfreude)props;

        public bool IsWatchedActive => isWatchedActive;

        public int WatchedWorkTicks => watchedWorkTicks;

        public float WatchedWorkProgress => PropsSchaden.watchedWorkDropTicks > 0
            ? Mathf.Clamp01(watchedWorkTicks / (float)PropsSchaden.watchedWorkDropTicks)
            : 0f;

        public float SawWindupProgress => PropsSchaden.sawWindupTicks > 0 && sawWindupTicksLeft > 0
            ? Mathf.Clamp01(1f - sawWindupTicksLeft / (float)PropsSchaden.sawWindupTicks)
            : 0f;

        public bool IsWatchedDuringStudy => !Escaped && watchedWorkTicks > 0;

        public bool IsTransforming => Escaped && isWatchedActive && transformTicksLeft > 0;

        public bool IsDrillAttacking => Escaped && isWatchedActive && drillAttackTicksLeft > 0;

        public bool IsSawWindingUp => Escaped && isWatchedActive && sawWindupTicksLeft > 0;

        public bool IsSawSpinning => Escaped && isWatchedActive && sawSpinTicksLeft > 0;

        public bool IsSawRetracting => Escaped && isWatchedActive && sawRetractTicksLeft > 0;

        public bool IsSawAttacking => IsSawWindingUp || IsSawSpinning;

        public bool AreSawsVisible => IsSawWindingUp || IsSawSpinning || IsSawRetracting;

        public bool IsActionLocked => IsTransforming || IsDrillAttacking || AreSawsVisible;

        /// <summary>仅供自动化验证显式模拟“视野内/视野外”状态。</summary>
        public void SetTestWatchOverride(bool? forced)
        {
            testWatchOverride = forced;
        }

        public bool IsWatchedByCamera()
        {
            if (testWatchOverride.HasValue)
            {
                return testWatchOverride.Value;
            }

            if (parent == null || parent.MapHeld == null || Find.CameraDriver == null || !parent.PositionHeld.IsValid)
            {
                return false;
            }

            if (WorldRendererUtility.WorldSelected || Find.CurrentMap != parent.MapHeld)
            {
                return false;
            }

            return Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(parent.PositionHeld);
        }

        private void ClearTransientFx()
        {
            if (activeWatchAura != null && !activeWatchAura.Destroyed)
            {
                activeWatchAura.Destroy();
            }
            activeWatchAura = null;

            if (activeSawVortex != null && !activeSawVortex.Destroyed)
            {
                activeSawVortex.Destroy();
            }
            activeSawVortex = null;
        }

        private void ClearCombatStages()
        {
            transformTicksLeft = 0;
            drillAttackTicksLeft = 0;
            sawWindupTicksLeft = 0;
            sawSpinTicksLeft = 0;
            sawRetractTicksLeft = 0;
            drillStrikeDelivered = false;
            pendingDrillTarget = null;
            ClearTransientFx();
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            isWatchedActive = false;
            watchedWorkTicks = 0;
            workWarningSent = false;
            droppedThisWork = false;
            lastStudyingTick = -999;
            ClearCombatStages();
            (parent as LC_SchadenfreudePawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            if (Find.TickManager != null)
            {
                lastStudyingTick = Find.TickManager.TicksGame;
            }

            if (!workWarningSent && parent?.MapHeld != null && parent.PositionHeld.IsValid)
            {
                workWarningSent = true;
                Messages.Message(
                    "LC_Schadenfreude_WorkWarning".Translate(),
                    new LookTargets(parent.PositionHeld, parent.MapHeld),
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            watchedWorkTicks = 0;
            workWarningSent = false;
            droppedThisWork = false;
            lastStudyingTick = -999;
            ClearTransientFx();
            base.Notify_Studied(studier, interrupted);
            (parent as LC_SchadenfreudePawn)?.UpdateAnimation(force: true);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            QliphothCountCurrent--;
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            ClearCombatStages();
            isWatchedActive = IsWatchedByCamera();
            attackCooldown = 45;
            spinCooldown = 180;
            watchedWorkTicks = 0;
            workWarningSent = false;
            droppedThisWork = false;

            if (isWatchedActive)
            {
                transformTicksLeft = PropsSchaden.transformDurationTicks;
            }

            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Transform, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                SchadenfreudeVfxUtility.SpawnKeyholePulse(self.Map, self.DrawPos, scale: 3.5f, lifeTicks: 28, withEyeMedallion: true);
            }

            (parent as LC_SchadenfreudePawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            isWatchedActive = false;
            ClearCombatStages();
            // DyingSchadenfreude handles authentic Dead1/Dead2 audio & dissolution VFX
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void TickContained()
        {
            base.TickContained();
            if (parent == null || parent.Destroyed || Escaped || Find.TickManager == null)
            {
                return;
            }

            bool studyingNow = Find.TickManager.TicksGame - lastStudyingTick <= 5;
            if (!studyingNow)
            {
                if (watchedWorkTicks > 0)
                {
                    watchedWorkTicks = 0;
                    ClearTransientFx();
                }
                workWarningSent = false;
                droppedThisWork = false;
                return;
            }

            if (IsWatchedByCamera())
            {
                watchedWorkTicks++;
                if (parent is Pawn pawn && (activeWatchAura == null || activeWatchAura.Destroyed || activeWatchAura.Map != pawn.MapHeld))
                {
                    activeWatchAura = SchadenfreudeVfxUtility.SpawnWatchAura(pawn, sawTelegraphMode: false);
                }

                if (!droppedThisWork && watchedWorkTicks >= PropsSchaden.watchedWorkDropTicks)
                {
                    droppedThisWork = true;
                    watchedWorkTicks = 0;
                    ClearTransientFx();
                    LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Transform, parent);
                    if (parent.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                    {
                        SchadenfreudeVfxUtility.SpawnKeyholePulse(
                            parent.MapHeld,
                            parent.PositionHeld.ToVector3Shifted(),
                            scale: 3.4f,
                            lifeTicks: 26,
                            withEyeMedallion: true);
                    }
                    Messages.Message(
                        "LC_Schadenfreude_WatchedWorkDropMsg".Translate(parent.LabelCap),
                        new LookTargets(parent.PositionHeld, parent.MapHeld),
                        MessageTypeDefOf.NegativeEvent,
                        historical: false);
                    QliphothCountCurrent--;
                }
            }
            else if (watchedWorkTicks > 0)
            {
                watchedWorkTicks = Mathf.Max(0, watchedWorkTicks - 2);
                if (watchedWorkTicks == 0)
                {
                    ClearTransientFx();
                }
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!(parent is Pawn pawn) || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            bool watched = IsWatchedByCamera();
            if (watched != isWatchedActive)
            {
                isWatchedActive = watched;
                if (watched)
                {
                    ClearCombatStages();
                    transformTicksLeft = PropsSchaden.transformDurationTicks;
                    LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Transform, pawn);
                    SchadenfreudeVfxUtility.SpawnKeyholePulse(pawn.Map, pawn.DrawPos, scale: 3.4f, lifeTicks: 24, withEyeMedallion: true);
                    (pawn as LC_SchadenfreudePawn)?.PlayTransformAnimation();
                }
                else
                {
                    ClearCombatStages();
                    pawn.pather?.StopDead();
                    pawn.jobs?.StopAll(ifLayingKeepLaying: false);
                    LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_SkillEnd, pawn);
                    (pawn as LC_SchadenfreudePawn)?.UpdateAnimation(force: true);
                }
            }

            if (!isWatchedActive)
            {
                return;
            }

            if (attackCooldown > 0) attackCooldown--;
            if (spinCooldown > 0) spinCooldown--;

            // Stage 0: Spring-open Transform
            if (transformTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                transformTicksLeft--;
                return;
            }

            // Stage 1: Twin Chainsaw Windup (special1)
            if (sawWindupTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                sawWindupTicksLeft--;
                if (activeWatchAura == null || activeWatchAura.Destroyed || activeWatchAura.Map != pawn.Map)
                {
                    activeWatchAura = SchadenfreudeVfxUtility.SpawnWatchAura(pawn, sawTelegraphMode: true);
                }
                if (sawWindupTicksLeft % 10 == 0)
                {
                    FleckMaker.ThrowMicroSparks(pawn.DrawPos + new Vector3(Rand.Range(-0.9f, 0.9f), 0f, Rand.Range(-0.5f, 0.5f)), pawn.Map);
                }
                if (sawWindupTicksLeft == 0)
                {
                    StartSawSpinRampage(pawn);
                }
                return;
            }

            // Stage 2: Twin Chainsaw High-Frequency Shredding Rampage (special2, ~3.0s)
            if (sawSpinTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                StepSawSpinRampage(pawn);
                return;
            }

            // Stage 3: Twin Chainsaw Retract (special3)
            if (sawRetractTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                sawRetractTicksLeft--;
                return;
            }

            // Stage 4: Mechanical Drill-Leg Thrust Swing (attack1/attack2)
            if (drillAttackTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                drillAttackTicksLeft--;
                if (!drillStrikeDelivered && drillAttackTicksLeft <= PropsSchaden.drillAttackDurationTicks - 24)
                {
                    drillStrikeDelivered = true;
                    DeliverDrillStrikeImpact(pawn);
                }
                return;
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 60f, p => p != pawn);
            if (target == null)
            {
                return;
            }

            float dist = pawn.Position.DistanceTo(target.Position);
            if (dist <= PropsSchaden.spinRadius && spinCooldown <= 0)
            {
                BeginSawSpinWindup(pawn);
            }
            else if (dist <= 2.6f && attackCooldown <= 0)
            {
                BeginDrillStrike(pawn, target);
            }
        }

        private void BeginDrillStrike(Pawn self, Pawn target)
        {
            attackCooldown = PropsSchaden.attackCooldownTicks;
            drillAttackTicksLeft = PropsSchaden.drillAttackDurationTicks;
            drillStrikeDelivered = false;
            pendingDrillTarget = target;
            self.pather?.StopDead();
            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Attack, self);
            (self as LC_SchadenfreudePawn)?.PlayDrillAttackAnimation();
        }

        private void DeliverDrillStrikeImpact(Pawn self)
        {
            if (self == null || !self.Spawned || self.Map == null)
            {
                return;
            }

            Pawn target = pendingDrillTarget;
            if (target == null || target.Dead || target.Destroyed || !target.Spawned || target.Map != self.Map
                || target.Position.DistanceTo(self.Position) > 3.2f)
            {
                target = LCAbnormalitiesHelper.FindNearestHumanlike(self.Map, self.Position, 3.0f, p => p != self);
            }

            pendingDrillTarget = null;
            if (target == null)
            {
                return;
            }

            Vector3 fromPos = self.DrawPos;
            Vector3 toPos = target.DrawPos;
            float yaw = (toPos - fromPos).AngleFlat();

            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Blood, target);
            SchadenfreudeVfxUtility.SpawnDrillThrust(self.Map, fromPos, toPos, lifeTicks: 18);
            SchadenfreudeVfxUtility.SpawnBloodBurst(self.Map, toPos, yaw, scale: 1.15f, lifeTicks: 18);
            FleckMaker.ThrowMicroSparks(toPos, self.Map);

            target.TakeDamage(new DamageInfo(
                LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                Rand.Range(15f, 20f),
                0.45f,
                yaw,
                instigator: self));
        }

        private void BeginSawSpinWindup(Pawn self)
        {
            spinCooldown = PropsSchaden.spinCooldownTicks;
            attackCooldown = PropsSchaden.attackCooldownTicks;
            sawWindupTicksLeft = PropsSchaden.sawWindupTicks;
            self.pather?.StopDead();
            ClearTransientFx();
            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_SkillStart, self);
            activeWatchAura = SchadenfreudeVfxUtility.SpawnWatchAura(self, sawTelegraphMode: true);
            (self as LC_SchadenfreudePawn)?.PlaySawWindupAnimation();
        }

        private void StartSawSpinRampage(Pawn self)
        {
            if (activeWatchAura != null && !activeWatchAura.Destroyed)
            {
                activeWatchAura.Destroy();
            }
            activeWatchAura = null;

            sawSpinTicksLeft = PropsSchaden.sawSpinDurationTicks;
            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Skill, self);
            SchadenfreudeVfxUtility.SpawnKeyholePulse(self.Map, self.DrawPos, scale: PropsSchaden.spinRadius * 2.1f, lifeTicks: 24, withEyeMedallion: true);
            activeSawVortex = SchadenfreudeVfxUtility.SpawnSawVortex(self);
            (self as LC_SchadenfreudePawn)?.PlaySawSpinAnimation();

            if (Find.CurrentMap == self.Map && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(0.32f);
            }
        }

        private void StepSawSpinRampage(Pawn self)
        {
            sawSpinTicksLeft--;
            Map map = self.Map;
            if (map == null)
            {
                sawSpinTicksLeft = 0;
                return;
            }

            if (activeSawVortex == null || activeSawVortex.Destroyed || activeSawVortex.Map != map)
            {
                activeSawVortex = SchadenfreudeVfxUtility.SpawnSawVortex(self);
            }

            // Re-trigger chainsaw roar periodically across the 3-second rampage
            if (sawSpinTicksLeft > 0 && sawSpinTicksLeft % 58 == 0)
            {
                LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Skill, self);
            }

            // High-frequency serrated Red shredding every 18 ticks (~0.3s -> 10 rapid cuts over 3.0s)
            if (sawSpinTicksLeft % 18 == 0)
            {
                float pulseDmg = Mathf.Max(3.5f, PropsSchaden.spinDamage * 0.24f);
                List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn victim = candidates[i];
                    if (victim == null || victim == self || victim.Dead || victim.Destroyed || !victim.Spawned)
                    {
                        continue;
                    }

                    if (!(victim.RaceProps?.Humanlike ?? false))
                    {
                        continue;
                    }

                    if (victim.Position.DistanceTo(self.Position) <= PropsSchaden.spinRadius)
                    {
                        Vector3 vPos = victim.DrawPos;
                        float yaw = (vPos - self.DrawPos).AngleFlat();
                        victim.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                            pulseDmg,
                            0.50f,
                            yaw,
                            instigator: self));
                        SchadenfreudeVfxUtility.SpawnGazeSlash(map, vPos, yaw + Rand.Range(-45f, 45f), scale: 1.55f, flipSweep: Rand.Bool, lifeTicks: 11);
                        SchadenfreudeVfxUtility.SpawnBloodBurst(map, vPos, yaw, scale: 0.90f, lifeTicks: 14);
                        FleckMaker.ThrowMicroSparks(vPos, map);
                    }
                }
            }

            if (sawSpinTicksLeft <= 0)
            {
                sawRetractTicksLeft = PropsSchaden.sawRetractTicks;
                LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_SkillEnd, self);
                (self as LC_SchadenfreudePawn)?.PlaySawRetractAnimation();
            }
        }

        public override string GetPlatformInspectString()
        {
            return IsWatchedByCamera()
                ? "LC_Schadenfreude_InspectWatched".Translate()
                : "LC_Schadenfreude_InspectUnwatched".Translate();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref watchedWorkTicks, "sfWatchedWorkTicks", 0);
            Scribe_Values.Look(ref isWatchedActive, "sfIsWatchedActive", false);
            Scribe_Values.Look(ref attackCooldown, "sfAttackCooldown", 0);
            Scribe_Values.Look(ref spinCooldown, "sfSpinCooldown", 0);
            Scribe_Values.Look(ref transformTicksLeft, "sfTransformTicksLeft", 0);
            Scribe_Values.Look(ref drillAttackTicksLeft, "sfDrillAttackTicksLeft", 0);
            Scribe_Values.Look(ref sawWindupTicksLeft, "sfSawWindupTicksLeft", 0);
            Scribe_Values.Look(ref sawSpinTicksLeft, "sfSawSpinTicksLeft", 0);
            Scribe_Values.Look(ref sawRetractTicksLeft, "sfSawRetractTicksLeft", 0);
            Scribe_Values.Look(ref drillStrikeDelivered, "sfDrillStrikeDelivered", false);
        }
    }

    /// <summary>幸灾乐祸实体 Pawn：驱动全套解包 Spine 多部件骨骼动画、形态切换及动态四色抗性切换。</summary>
    public class LC_SchadenfreudePawn : LC_StandardEntityPawn, ILCFourColorDynamicResistance
    {
        public CompSchadenfreude SchadenComp => EntityComp as CompSchadenfreude;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public override string CurrentBodyTexPath => SchadenComp != null && SchadenComp.IsWatchedActive
            ? "Things/Pawn/Schadenfreude/Schadenfreude_Active"
            : "Things/Pawn/Schadenfreude/Schadenfreude";

        public override Vector2 CurrentBodyDrawSize => SchadenComp != null && SchadenComp.IsWatchedActive
            ? new Vector2(2.3f, 2.3f)
            : new Vector2(1.75f, 1.75f);

        public void PlayTransformAnimation()
        {
            if (SchadenfreudeDefOf.Schadenfreude_Transform != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(SchadenfreudeDefOf.Schadenfreude_Transform);
            }
        }

        public void PlayDrillAttackAnimation()
        {
            if (SchadenfreudeDefOf.Schadenfreude_DrillAttack != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(SchadenfreudeDefOf.Schadenfreude_DrillAttack);
            }
        }

        public void PlaySawWindupAnimation()
        {
            if (SchadenfreudeDefOf.Schadenfreude_SawWindup != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(SchadenfreudeDefOf.Schadenfreude_SawWindup);
            }
        }

        public void PlaySawSpinAnimation()
        {
            if (SchadenfreudeDefOf.Schadenfreude_SawSpin != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(SchadenfreudeDefOf.Schadenfreude_SawSpin);
            }
        }

        public void PlaySawRetractAnimation()
        {
            if (SchadenfreudeDefOf.Schadenfreude_SawRetract != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(SchadenfreudeDefOf.Schadenfreude_SawRetract);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompSchadenfreude comp = SchadenComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = (comp != null && comp.IsWatchedDuringStudy)
                    ? SchadenfreudeDefOf.Schadenfreude_Box_WorkWatched
                    : SchadenfreudeDefOf.Schadenfreude_Box_Idle;
            }
            else if (comp == null || !comp.IsWatchedActive)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_Box_Idle;
            }
            else if (comp.IsTransforming)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_Transform;
            }
            else if (comp.IsSawSpinning)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_SawSpin;
            }
            else if (comp.IsSawWindingUp)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_SawWindup;
            }
            else if (comp.IsSawRetracting)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_SawRetract;
            }
            else if (comp.IsDrillAttacking)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_DrillAttack;
            }
            else if (pather != null && pather.Moving)
            {
                desired = SchadenfreudeDefOf.Schadenfreude_Walk;
            }
            else
            {
                desired = SchadenfreudeDefOf.Schadenfreude_Active_Idle;
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
            CompSchadenfreude comp = SchadenComp;
            if (comp != null && (!comp.IsWatchedActive || comp.IsActionLocked) && pather?.Moving == true)
            {
                pather.StopDead();
            }

            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        public bool TryGetDynamicFourColorResistance(FourColorDamageType damageType, out float resistance)
        {
            bool watched = SchadenComp != null && SchadenComp.IsWatchedActive;
            switch (damageType)
            {
                case FourColorDamageType.Red:
                    resistance = watched ? 1.2f : 0.6f;
                    return true;
                case FourColorDamageType.White:
                    resistance = watched ? 0.5f : 0.2f;
                    return true;
                case FourColorDamageType.Black:
                    resistance = watched ? 1.0f : 0.5f;
                    return true;
                case FourColorDamageType.Pale:
                    resistance = watched ? 1.5f : 0.7f;
                    return true;
                default:
                    resistance = 1.0f;
                    return false;
            }
        }
    }

    /// <summary>幸灾乐祸突破 AI：视野外完全静止等待，视野内且非技能硬直期间高速追杀最近人类。</summary>
    public class JobGiver_SchadenfreudeHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }

            CompSchadenfreude comp = pawn.TryGetComp<CompSchadenfreude>();
            if (comp == null || !comp.IsWatchedActive || comp.IsActionLocked)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 60f, p => p != pawn);
            if (target == null)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 30);
            }

            if (pawn.Position.DistanceTo(target.Position) <= 1.8f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Job chase = JobMaker.MakeJob(JobDefOf.Goto, target);
            chase.expiryInterval = 45;
            chase.checkOverrideOnExpire = true;
            chase.canBashDoors = true;
            return chase;
        }
    }

    /// <summary>
    /// 凝视（HE 级猩红锯盘长柄武器）专属伤害处理器：
    /// 播放原作锯切音效（DontWatch / DontWatch_loop），触发旋转锯盘与 4 连黑铁猩红锯齿斩击 VFX，
    /// 叠加“凝视撕裂”破甲状态并附带额外锯齿撕扯红伤（当持握者处于镜头注视范围内时，追加锁孔注视增益）。
    /// </summary>
    public class DamageWorker_Gaze : DamageWorker_FourColor
    {
        public static bool IsInstigatorWatched(Thing instigator)
        {
            if (instigator == null || instigator.MapHeld == null || Find.CameraDriver == null || !instigator.PositionHeld.IsValid)
            {
                return false;
            }

            if (WorldRendererUtility.WorldSelected || Find.CurrentMap != instigator.MapHeld)
            {
                return false;
            }

            return Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(instigator.PositionHeld);
        }

        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Map victimMap = victim?.MapHeld;
            IntVec3 center = victim?.PositionHeld ?? IntVec3.Invalid;
            Vector3 hitPos = victim != null
                ? (victim.Spawned ? victim.DrawPos : center.ToVector3Shifted())
                : Vector3.zero;
            Vector3 instigatorPos = dinfo.Instigator != null && dinfo.Instigator.Spawned ? dinfo.Instigator.DrawPos : hitPos;

            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Gaze_Saw, victim);
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);

            if (victim == null)
            {
                return result;
            }

            bool watchedBoost = IsInstigatorWatched(dinfo.Instigator);

            if (victimMap != null && center.InBounds(victimMap))
            {
                float attackYaw = dinfo.Instigator != null && dinfo.Instigator.Spawned
                    ? (hitPos - instigatorPos).AngleFlat()
                    : dinfo.Angle;

                if (SchadenfreudeDefOf.LC_Gaze_SawLoop != null)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Gaze_SawLoop, victim);
                }

                // Spawn authentic Gaze spinning bloody saw disc + 4 staggered serrated crimson slashes!
                SchadenfreudeVfxUtility.SpawnGazeFlurry(victimMap, hitPos, attackYaw, watchedBoost);

                if (dinfo.Instigator is Pawn attacker)
                {
                    if (!EgoWeaponAnimationManager.IsPlaying(attacker, GazeWeaponAnimHandler.ClipSkillWarmup)
                        && !EgoWeaponAnimationManager.IsPlaying(attacker, GazeWeaponAnimHandler.ClipSkillStorm))
                    {
                        ThingWithComps heldWeapon = attacker.equipment?.Primary;
                        if (heldWeapon != null && heldWeapon.def?.defName == GazeWeaponAnimHandler.WeaponDefName)
                        {
                            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, hitPos);
                            EgoWeaponAnimationManager.Play(attacker, heldWeapon, GazeWeaponAnimHandler.ClipNormalGrind, 58, aimAngle);
                        }
                    }
                }
            }

            if (victim is Pawn victimPawn && !victimPawn.Dead && !victimPawn.Destroyed && victimPawn.health?.hediffSet != null)
            {
                float newSeverity = 0.25f;
                if (SchadenfreudeDefOf.LC_GazeRending != null)
                {
                    Hediff existing = victimPawn.health.hediffSet.GetFirstHediffOfDef(SchadenfreudeDefOf.LC_GazeRending);
                    if (existing != null)
                    {
                        existing.Severity = Mathf.Min(1f, existing.Severity + 0.25f);
                        newSeverity = existing.Severity;
                    }
                    else
                    {
                        LCAbnormalitiesHelper.ApplyOrRefreshHediff(victimPawn, SchadenfreudeDefOf.LC_GazeRending, 0.25f);
                        newSeverity = 0.25f;
                    }
                }

                // Trigger crimson Keyhole Gaze flash when Gaze Rending reaches >= 50% stack or on watched boost
                if (victimMap != null && center.InBounds(victimMap) && (newSeverity >= 0.50f || watchedBoost))
                {
                    Vector3 vPos = victimPawn.Spawned ? victimPawn.DrawPos : center.ToVector3Shifted();
                    SchadenfreudeVfxUtility.SpawnKeyholePulse(victimMap, vPos, scale: 2.0f, lifeTicks: 20, withEyeMedallion: newSeverity >= 0.50f);
                }

                // 旋转锯刃追加一段 25% 红伤撕扯（镜头注视下提升至 35%）
                float shredRatio = watchedBoost ? 0.35f : 0.25f;
                float extraShred = Mathf.Max(2f, dinfo.Amount * shredRatio);
                victimPawn.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    extraShred,
                    0.35f,
                    dinfo.Angle,
                    dinfo.Instigator,
                    weapon: dinfo.Weapon));
            }

            return result;
        }
    }
}
