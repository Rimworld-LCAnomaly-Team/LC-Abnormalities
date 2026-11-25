using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    [DefOf]
    public static class FairyFestivalDefOf
    {
        public static HediffDef LC_FairyCare;
        public static SoundDef LC_FairyFestival_Special;
        public static SoundDef LC_FairyFestival_Dead;
        public static SoundDef LC_Wingbeat_Hit;

        public static AnimationDef FairyFestival_Idle;
        public static AnimationDef FairyFestival_Work;
        public static AnimationDef FairyFestival_GiveFairy;
        public static AnimationDef FairyFestival_Feast;

        public static ThingDef LC_FairyCareSwarmFX;
        public static ThingDef LC_FairyBlessingBurstFX;
        public static ThingDef LC_FairyDevourExecutionFX;
        public static ThingDef LC_WingbeatSlashFX;
        public static ThingDef LC_WingbeatLifestealFX;
        public static ThingDef LC_WingbeatSkillRunnerFX;

        static FairyFestivalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FairyFestivalDefOf));
        }
    }

    public class CompProperties_FairyFestival : CompProperties_LCAbnormality
    {
        public HediffDef careHediffDef;
        public float goodHealAmount = 14f;
        public float goodMentalRestore = 0.25f;
        public float normalHealAmount = 8f;
        public float normalMentalRestore = 0.15f;
        public float badNibbleRedDamage = 6f;

        public CompProperties_FairyFestival()
        {
            compClass = typeof(CompFairyFestival);
        }
    }

    /// <summary>
    /// F-04-83 精灵盛宴核心逻辑：
    /// 驱动多部件振翅悬停、工作迎候、祝福派遣（Give_fairy）与嫉妒分食（Feast）动画及定制 GLSL 演出特效；
    /// 研究后赋予受试者温和疗愈与护卫小精灵随行标记，一旦受试者去照料其他异想体即刻触发猩红分食处决。
    /// </summary>
    public class CompFairyFestival : CompLCAbnormality
    {
        public CompProperties_FairyFestival PropsFairy => (CompProperties_FairyFestival)props;

        public LC_FairyFestivalPawn FairyPawn => parent as LC_FairyFestivalPawn;

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            FairyPawn?.NotifyWorkingTick();
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            FairyPawn?.NotifyWorkingTick();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            FairyPawn?.NotifyWorkingTick();
            return base.Notify_StudyInterval(studier, workType);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            ApplyCare(studier, PropsFairy.goodHealAmount, PropsFairy.goodMentalRestore, showGoodMessage: true);
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            ApplyCare(studier, PropsFairy.normalHealAmount, PropsFairy.normalMentalRestore, showGoodMessage: false);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (studier != null && !studier.Dead && PropsFairy.badNibbleRedDamage > 0f)
            {
                if (studier.MapHeld != null && studier.PositionHeld.InBounds(studier.MapHeld))
                {
                    Vector3 victimPos = studier.Spawned ? studier.DrawPos.Yto0() : studier.PositionHeld.ToVector3Shifted().Yto0();
                    Vector3 sourcePos = parent != null && parent.Spawned ? parent.DrawPos.Yto0() : (parent?.PositionHeld.ToVector3Shifted().Yto0() ?? victimPos + Vector3.left);
                    Vector3 dir = (victimPos - sourcePos).Yto0();
                    float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg : 0f;
                    FairyFestivalVfxUtility.SpawnWingbeatSlash(studier.MapHeld, victimPos, yaw, scale: 1.55f, flipSweep: Rand.Bool, crimsonFeastMode: true, durationTicks: 16);
                }

                studier.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, PropsFairy.badNibbleRedDamage, instigator: parent));
            }
            ApplyCare(studier, 0f, 0f, showGoodMessage: false);
        }

        private void ApplyCare(Pawn studier, float healAmount, float mentalRestore, bool showGoodMessage)
        {
            if (studier == null || studier.Dead)
            {
                return;
            }

            if (healAmount > 0f)
            {
                LCAbnormalitiesHelper.HealInjuries(studier, healAmount);
            }

            if (mentalRestore > 0f)
            {
                LCAbnormalitiesHelper.RestoreMentalHealth(studier, mentalRestore, 0.08f);
            }

            HediffDef careDef = PropsFairy.careHediffDef ?? FairyFestivalDefOf.LC_FairyCare;
            LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, careDef, 1f);
            LCAbnormalitiesHelper.PlaySoundAt(FairyFestivalDefOf.LC_FairyFestival_Special, parent);

            // Trigger authentic Spine Give_fairy animation + dispatch glowing guardian fairies to the studier
            FairyPawn?.PlayGiveFairyAnimation();

            Map map = studier.MapHeld ?? parent?.MapHeld;
            if (map != null)
            {
                Vector3 fromPos = parent != null && parent.Spawned
                    ? parent.DrawPos.Yto0()
                    : (parent?.PositionHeld.ToVector3Shifted().Yto0() ?? studier.PositionHeld.ToVector3Shifted().Yto0());
                Vector3 toPos = studier.Spawned
                    ? studier.DrawPos.Yto0()
                    : studier.PositionHeld.ToVector3Shifted().Yto0();
                FairyFestivalVfxUtility.SpawnBlessingDispatch(map, fromPos, toPos);
            }

            Hediff careHediff = studier.health?.hediffSet?.GetFirstHediffOfDef(careDef);
            careHediff?.TryGetComp<HediffComp_FairyCare>()?.EnsureSwarmActive();

            if (showGoodMessage)
            {
                Messages.Message(
                    "LC_FairyFestival_CareMsg".Translate(studier.LabelShortCap),
                    studier,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }
    }

    /// <summary>
    /// 精灵盛宴异想体 Pawn：
    /// 驱动连续亚刻插值 Spine 时钟，精准同步 Idle（Default 3.0s）、Work（1.15x）、GiveFairy（Give_fairy 2.167s）与 Feast（1.25x 猩红狂喜）状态机。
    /// </summary>
    public class LC_FairyFestivalPawn : LC_StandardEntityPawn
    {
        private const float DefaultClipDuration = 3.0f;
        private const float GiveFairyClipDuration = 2.1667f;

        private int oneShotAnimStartTick = -1;
        private int oneShotAnimEndTick = -1;
        private bool oneShotIsFeast;
        private int lastWorkingTick = -999999;

        private bool idlePhaseInitialized;
        private float continuousIdlePhase;
        private int lastClockTick = -1;
        private float lastTickRealtime = -1f;

        public CompFairyFestival FairyComp => EntityComp as CompFairyFestival;

        public bool OneShotPlaying => Find.TickManager != null && oneShotAnimEndTick > Find.TickManager.TicksGame;

        public bool IsGivingFairyNow => OneShotPlaying && !oneShotIsFeast;

        public bool IsFeastingNow => OneShotPlaying && oneShotIsFeast;

        public bool IsWorkingNow => Find.TickManager != null && (Find.TickManager.TicksGame - lastWorkingTick) <= 12;

        /// <summary>还原 Spine Give_fairy 口型切换：前 28t 与末尾 12t 显示微笑猫嘴 mouth2，中段张口 mouth1。</summary>
        public bool ShouldShowSmileMouth
        {
            get
            {
                if (!IsGivingFairyNow || Find.TickManager == null)
                {
                    return false;
                }

                int elapsed = Find.TickManager.TicksGame - oneShotAnimStartTick;
                return elapsed < 28 || elapsed >= 118;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref oneShotAnimStartTick, "ffOneShotStartTick", -1);
            Scribe_Values.Look(ref oneShotAnimEndTick, "ffOneShotEndTick", -1);
            Scribe_Values.Look(ref oneShotIsFeast, "ffOneShotIsFeast", false);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            EnsureClockInitialized();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed)
            {
                AdvanceSpineClock();
                UpdateAnimation();
                EmitAmbientShimmer();
            }
        }

        public override void TickHolded()
        {
            base.TickHolded();
            AdvanceSpineClock();
            UpdateAnimation();
            EmitAmbientShimmer();
        }

        private void EnsureClockInitialized()
        {
            if (!idlePhaseInitialized)
            {
                idlePhaseInitialized = true;
                continuousIdlePhase = ((Mathf.Abs(thingIDNumber) * 0.6180339f) % 1f) * DefaultClipDuration;
            }
        }

        private void AdvanceSpineClock()
        {
            EnsureClockInitialized();
            if (Find.TickManager == null)
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame == lastClockTick)
            {
                return;
            }

            int deltaTicks = lastClockTick >= 0 ? Mathf.Clamp(ticksGame - lastClockTick, 1, 12) : 1;
            lastClockTick = ticksGame;
            lastTickRealtime = Time.realtimeSinceStartup;

            if (!OneShotPlaying)
            {
                float speed = IsWorkingNow ? 1.15f : 1.0f;
                continuousIdlePhase = Mathf.Repeat(continuousIdlePhase + (deltaTicks / 60f) * speed, DefaultClipDuration);
            }
        }

        public void GetSpinePlaybackState(out FairySpineAnimKind animKind, out float animTime, out bool feastMode)
        {
            EnsureClockInitialized();

            float subTick = 0f;
            if (Find.TickManager != null && !Find.TickManager.Paused && lastTickRealtime > 0f)
            {
                float elapsedReal = Time.realtimeSinceStartup - lastTickRealtime;
                float rate = Find.TickManager.TickRateMultiplier;
                subTick = Mathf.Clamp(elapsedReal * rate, 0f, 1f / 60f);
            }

            if (OneShotPlaying && Find.TickManager != null)
            {
                float elapsed = ((Find.TickManager.TicksGame - oneShotAnimStartTick) / 60f) + subTick;
                if (oneShotIsFeast)
                {
                    animKind = FairySpineAnimKind.GiveFairy;
                    animTime = Mathf.Repeat(Mathf.Max(0f, elapsed) * 1.25f, GiveFairyClipDuration);
                    feastMode = true;
                    return;
                }

                if (elapsed < GiveFairyClipDuration)
                {
                    animKind = FairySpineAnimKind.GiveFairy;
                    animTime = Mathf.Clamp(elapsed, 0f, GiveFairyClipDuration);
                    feastMode = false;
                    return;
                }
            }

            float idleSpeed = IsWorkingNow ? 1.15f : 1.0f;
            animKind = FairySpineAnimKind.Default;
            animTime = Mathf.Repeat(continuousIdlePhase + subTick * idleSpeed, DefaultClipDuration);
            feastMode = false;
        }

        public void NotifyWorkingTick()
        {
            if (Find.TickManager != null)
            {
                lastWorkingTick = Find.TickManager.TicksGame;
            }

            if (!OneShotPlaying && FairyFestivalDefOf.FairyFestival_Work != null)
            {
                SetAnimationIfNeeded(FairyFestivalDefOf.FairyFestival_Work);
            }
        }

        public void PlayGiveFairyAnimation()
        {
            AnimationDef anim = FairyFestivalDefOf.FairyFestival_GiveFairy;
            if (anim == null || Find.TickManager == null)
            {
                return;
            }

            oneShotIsFeast = false;
            oneShotAnimStartTick = Find.TickManager.TicksGame;
            oneShotAnimEndTick = oneShotAnimStartTick + anim.durationTicks;
            lastTickRealtime = Time.realtimeSinceStartup;
            SetAnimationIfNeeded(anim);
        }

        public void PlayFeastAnimation()
        {
            AnimationDef anim = FairyFestivalDefOf.FairyFestival_Feast;
            if (anim == null || Find.TickManager == null)
            {
                return;
            }

            oneShotIsFeast = true;
            oneShotAnimStartTick = Find.TickManager.TicksGame;
            oneShotAnimEndTick = oneShotAnimStartTick + anim.durationTicks;
            lastTickRealtime = Time.realtimeSinceStartup;
            SetAnimationIfNeeded(anim);
        }

        private void EmitAmbientShimmer()
        {
            if (!IsFeastingNow)
            {
                return;
            }

            Map map = MapHeld;
            if (map == null || Find.TickManager == null)
            {
                return;
            }

            if (((Find.TickManager.TicksGame + thingIDNumber * 17) % 14) != 0)
            {
                return;
            }

            IntVec3 pos = PositionHeld;
            if (!pos.InBounds(map))
            {
                return;
            }

            Vector3 drawPos = Spawned ? DrawPos.Yto0() : pos.ToVector3Shifted().Yto0();
            Vector3 offset = new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(-0.20f, 0.65f));
            FleckMaker.ThrowDustPuffThick(drawPos + offset, map, 0.68f, new Color(0.92f, 0.16f, 0.20f));
        }

        private void UpdateAnimation()
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            if (oneShotAnimEndTick >= 0)
            {
                if (OneShotPlaying)
                {
                    AnimationDef target = oneShotIsFeast
                        ? FairyFestivalDefOf.FairyFestival_Feast
                        : FairyFestivalDefOf.FairyFestival_GiveFairy;
                    if (target != null)
                    {
                        SetAnimationIfNeeded(target);
                    }
                    return;
                }

                if (!oneShotIsFeast)
                {
                    // Seamless transition: Give_fairy ends in closed-wing pose matching Default t=0
                    continuousIdlePhase = 0f;
                }
                oneShotAnimStartTick = -1;
                oneShotAnimEndTick = -1;
                oneShotIsFeast = false;
            }

            if (IsWorkingNow && FairyFestivalDefOf.FairyFestival_Work != null)
            {
                SetAnimationIfNeeded(FairyFestivalDefOf.FairyFestival_Work);
                return;
            }

            if (FairyFestivalDefOf.FairyFestival_Idle != null)
            {
                SetAnimationIfNeeded(FairyFestivalDefOf.FairyFestival_Idle);
            }
        }

        private void SetAnimationIfNeeded(AnimationDef animation)
        {
            if (animation != null && Drawer?.renderer != null && Drawer.renderer.CurAnimation != animation)
            {
                Drawer.renderer.SetAnimation(animation);
            }
        }
    }

    public class HediffCompProperties_FairyCare : HediffCompProperties
    {
        public float healPerPulse = 2f;
        public float mentalRestorePerPulse = 0.03f;
        public int pulseIntervalTicks = 250;

        public HediffCompProperties_FairyCare()
        {
            compClass = typeof(HediffComp_FairyCare);
        }
    }

    /// <summary>精灵的照料：周期性回复伤口与精神并维持护卫小精灵环绕特效；一旦持有者开始研究任何其他异想体，立刻惨遭猩红群精灵分食。</summary>
    public class HediffComp_FairyCare : HediffComp, ILCPawnStudyStartListener
    {
        private bool triggered;
        private Thing_FairyCareSwarm activeSwarm;

        public HediffCompProperties_FairyCare Props => (HediffCompProperties_FairyCare)props;

        public void EnsureSwarmActive()
        {
            if (triggered || Pawn == null || Pawn.Dead || Pawn.Destroyed || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            if (activeSwarm == null || activeSwarm.Destroyed || !activeSwarm.Spawned || activeSwarm.Map != Pawn.Map)
            {
                activeSwarm = FairyFestivalVfxUtility.SpawnCareSwarm(Pawn);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                CleanupSwarm();
                return;
            }

            if (!triggered && Pawn.Spawned && Pawn.IsHashIntervalTick(30))
            {
                EnsureSwarmActive();
            }

            int interval = Mathf.Max(60, Props.pulseIntervalTicks);
            if (Pawn.IsHashIntervalTick(interval))
            {
                LCAbnormalitiesHelper.HealInjuries(Pawn, Props.healPerPulse);
                LCAbnormalitiesHelper.RestoreMentalHealth(Pawn, Props.mentalRestorePerPulse);

                if (Pawn.Spawned && Pawn.Map != null)
                {
                    FairyFestivalVfxUtility.SpawnHealPulse(Pawn.Map, Pawn.DrawPos.Yto0(), radius: 1.15f);
                }
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            CleanupSwarm();
        }

        private void CleanupSwarm()
        {
            if (activeSwarm != null && !activeSwarm.Destroyed)
            {
                activeSwarm.Destroy();
            }
            activeSwarm = null;
        }

        public void OnStudyStarted(Pawn studier, CompAbnormality abnormality)
        {
            if (triggered || studier == null || studier != Pawn || studier.Dead || abnormality?.parent == null)
            {
                return;
            }

            if (abnormality.parent.def?.defName == "FairyFestival")
            {
                return;
            }

            triggered = true;
            CleanupSwarm();

            Map map = studier.MapHeld;
            IntVec3 pos = studier.PositionHeld;
            Vector3 exactPos = studier.Spawned ? studier.DrawPos.Yto0() : pos.ToVector3Shifted().Yto0();

            LCAbnormalitiesHelper.PlaySoundAt(FairyFestivalDefOf.LC_FairyFestival_Dead, studier);
            if (map != null && pos.InBounds(map))
            {
                // Authentic Lobotomy Corp jealous devouring VFX (crimson swarm + GLSL blood vortex + gore orbs)
                FairyFestivalVfxUtility.SpawnDevourExecution(map, exactPos);
                AbnormalityUtility.Burst(pos, map, 1.5f);

                // Echo the predatory feast animation on any Fairy Festival held on this map
                NotifyFairyFestivalsOnMap(map);

                Find.LetterStack.ReceiveLetter(
                    "LC_FairyFestival_DevouredLetterLabel".Translate(),
                    "LC_FairyFestival_DevouredLetterText".Translate(studier.LabelShortCap, abnormality.parent.LabelCap),
                    LetterDefOf.ThreatBig,
                    new LookTargets(pos, map));
            }

            DamageInfo dinfo = new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 200f, 999f, instigator: abnormality.parent);
            studier.TakeDamage(dinfo);
            if (!studier.Dead)
            {
                studier.Kill(dinfo);
            }

            if (!studier.Dead && studier.health?.hediffSet != null && parent != null)
            {
                studier.health.RemoveHediff(parent);
            }
        }

        private static void NotifyFairyFestivalsOnMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawns;
            for (int i = 0; i < allPawns.Count; i++)
            {
                if (allPawns[i] is LC_FairyFestivalPawn fairy && !fairy.Destroyed && !fairy.Dead)
                {
                    fairy.PlayFeastAnimation();
                }
            }

            List<Building> buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i] is Building_AbnormalityHoldingPlatform platform
                        && platform.HeldPawn is LC_FairyFestivalPawn heldFairy
                        && !heldFairy.Destroyed
                        && !heldFairy.Dead)
                    {
                        heldFairy.PlayFeastAnimation();
                    }
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref triggered, "fairyCareTriggered", false);
        }
    }

    /// <summary>
    /// 振翅（Wingbeat）E.G.O. 武器专属伤害处理器：
    /// 1) 还原脑叶公司原作双段交替近战挥击（左右交替的荧光精灵羽翼红伤弧斩特效）；
    /// 2) 命中血肉目标时为挥舞者吸收 15% 造成的伤害，并生成猩红→翡翠吸血丝带与疗愈光环特效。
    /// </summary>
    public class DamageWorker_WingbeatLifesteal : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        private static readonly Dictionary<int, int> SwingParityByAttacker = new Dictionary<int, int>();

        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);

            Map map = victim?.MapHeld ?? dinfo.Instigator?.MapHeld;
            if (map != null && victim != null)
            {
                Vector3 victimVec = victim.Spawned
                    ? victim.DrawPos.Yto0()
                    : victim.PositionHeld.ToVector3Shifted().Yto0();
                Vector3 attackerVec = dinfo.Instigator != null && dinfo.Instigator.Spawned
                    ? dinfo.Instigator.DrawPos.Yto0()
                    : victimVec + Vector3.left;
                Vector3 dir = (victimVec - attackerVec).Yto0();
                float yaw = dir.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg
                    : dinfo.Angle;

                bool flipSweep = false;
                if (dinfo.Instigator != null)
                {
                    int id = dinfo.Instigator.thingIDNumber;
                    SwingParityByAttacker.TryGetValue(id, out int count);
                    flipSweep = (count % 2) == 1;
                    SwingParityByAttacker[id] = count + 1;
                    if (SwingParityByAttacker.Count > 128)
                    {
                        SwingParityByAttacker.Clear();
                    }
                }

                // Alternating Wingbeat gossamer-crimson slash VFX (2-step sequential strike feel)
                FairyFestivalVfxUtility.SpawnWingbeatSlash(
                    map,
                    victimVec,
                    yaw,
                    scale: 1.65f,
                    flipSweep: flipSweep,
                    crimsonFeastMode: false,
                    durationTicks: 16);

                if (result.totalDamageDealt > 0f
                    && dinfo.Instigator is Pawn attacker
                    && !attacker.Dead
                    && victim is Pawn targetPawn
                    && targetPawn.RaceProps?.IsFlesh == true)
                {
                    float heal = Mathf.Min(6f, result.totalDamageDealt * 0.15f);
                    LCAbnormalitiesHelper.HealInjuries(attacker, heal);
                    FairyFestivalVfxUtility.SpawnWingbeatLifesteal(map, victimVec, attacker);
                }
            }

            return result;
        }
    }
}
