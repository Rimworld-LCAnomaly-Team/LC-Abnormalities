using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.BigBird
{
    [DefOf]
    public static class BigBirdDefOf
    {
        public static HediffDef LC_BigBirdEnchanted;
        public static HediffDef LC_LampDazed;
        public static JobDef LC_BigBirdApproach;
        public static SoundDef LC_BigBird_Attract;
        public static SoundDef LC_BigBird_Dead1;
        public static SoundDef LC_BigBird_Dead2;
        public static SoundDef LC_BigBird_Walk;
        public static SoundDef LC_Lamp_Hammer;

        public static AnimationDef BigBird_Idle;
        public static AnimationDef BigBird_Work;
        public static AnimationDef BigBird_MindAttack;
        public static AnimationDef BigBird_Walk;
        public static AnimationDef BigBird_Attract;
        public static AnimationDef BigBird_Deadshot;

        public static ThingDef LC_BigBirdBlackoutAuraFX;
        public static ThingDef LC_BigBirdEnchantWaveFX;
        public static ThingDef LC_BigBirdEyeMarkFX;
        public static ThingDef LC_BigBirdBiteBurstFX;
        public static ThingDef LC_LampImpactFX;
        public static ThingDef LC_LampSkillBeaconFX;
        public static ThingDef LC_LampSkillRunnerFX;
        public static DamageDef LC_Lamp_SkillBlack;

        static BigBirdDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BigBirdDefOf));
        }
    }

    public class CompProperties_BigBird : CompProperties_LCAbnormality
    {
        public int deathsPerCounterDrop = 5;
        public int enchantIntervalTicks = 240;
        public float enchantRadius = 14f;
        public float biteRange = 2.4f;
        public float biteDamage = 85f;

        public CompProperties_BigBird()
        {
            compClass = typeof(CompBigBird);
        }
    }

    /// <summary>
    /// O-02-40 大鸟核心逻辑：
    /// 1) 每 5 名人类死亡或差结果降低计数，优结果恢复计数；
    /// 2) 收容期间驱动待机/工作/精神冲击动画与眨眼；
    /// 3) 出逃后展开黑森林停电暗域，周期性摇灯释放金光波魅惑附近人类（附带黄→橙→红头顶眼眸标记），
    ///    贴近后发动与 BigBird_Deadshot 动画严格同步的致命黑伤咬头处决与猩红群瞳裂斩特效。
    /// </summary>
    public class CompBigBird : CompLCAbnormality, ILCMapHumanlikeDeathListener
    {
        private const int AttractAnimDuration = 120;
        private const int AttractWaveTriggerRemaining = 74; // tick 46 of 120 (peak lantern swing)
        private const int DeadshotAnimDuration = 120;
        private const int DeadshotImpactRemaining = 58;     // tick 62 of 120 (exact jaw slam)
        private const int MindAttackAnimDuration = 90;

        private int observedDeathCount;
        private int enchantCooldownTicks = 120;
        private int biteCooldownTicks;

        private int studyingGraceTicks;
        private int mindAttackAnimTicks;
        private int attractAnimTicks;
        private bool attractWavePending;

        private int deadshotAnimTicks;
        private bool deadshotImpactPending;
        private Pawn deadshotTarget;

        private int walkSoundTickCounter;
        private Thing_BigBirdBlackoutAura activeBlackoutAura;

        public CompProperties_BigBird PropsBigBird => (CompProperties_BigBird)props;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public bool IsPerformingMindAttack => mindAttackAnimTicks > 0 && !Escaped;

        public bool IsPerformingAttract => attractAnimTicks > 0 && Escaped;

        public bool IsPerformingDeadshot => deadshotAnimTicks > 0 && Escaped;

        /// <summary>兼容旧接口，供外部判断大鸟是否处于咬头姿态。</summary>
        public bool IsBitingPose => IsPerformingDeadshot;

        /// <summary>咬头动画中巨喙大幅张开、显露深红獠牙血腔的窗口（tick 12..62）。</summary>
        public bool IsMouthOpenForDeadshot => deadshotAnimTicks > 56 && deadshotAnimTicks < 108;

        public int ObservedDeathCount => observedDeathCount;

        private void ClearTransientCombatState()
        {
            attractAnimTicks = 0;
            attractWavePending = false;
            deadshotAnimTicks = 0;
            deadshotImpactPending = false;
            deadshotTarget = null;
            mindAttackAnimTicks = 0;
            walkSoundTickCounter = 0;
            DespawnBlackoutAura();
        }

        private void EnsureBlackoutAura(Pawn self)
        {
            if (self == null || !self.Spawned || self.Map == null || !Escaped)
            {
                return;
            }

            if (activeBlackoutAura == null || activeBlackoutAura.Destroyed || activeBlackoutAura.Map != self.Map)
            {
                activeBlackoutAura = BigBirdVfxUtility.SpawnBlackoutAura(self, Mathf.Min(13.5f, PropsBigBird.enchantRadius * 0.85f));
            }
        }

        private void DespawnBlackoutAura()
        {
            if (activeBlackoutAura != null && !activeBlackoutAura.Destroyed)
            {
                activeBlackoutAura.Destroy();
            }
            activeBlackoutAura = null;
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            studyingGraceTicks = 90;
            bool result = base.Notify_StudyInterval(studier, workType);
            if (!result && !Escaped && mindAttackAnimTicks <= 0 && parent is Pawn self)
            {
                mindAttackAnimTicks = MindAttackAnimDuration;
                (self as LC_BigBirdPawn)?.PlayMindAttackAnimation();
                if (self.MapHeld != null)
                {
                    BigBirdVfxUtility.SpawnEnchantWave(
                        self.MapHeld,
                        self.DrawPos,
                        3.4f,
                        duration: 38,
                        color: new Color(1.0f, 0.72f, 0.18f, 0.90f),
                        intensity: 1.2f);
                }
            }

            return result;
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            if (QliphothCountCurrent < QliphothCountMax)
            {
                QliphothCountCurrent++;
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (!Escaped)
            {
                mindAttackAnimTicks = MindAttackAnimDuration;
                (parent as LC_BigBirdPawn)?.PlayMindAttackAnimation();
            }
            QliphothCountCurrent--;
        }

        public void NotifyHumanlikeDied(Pawn victim, Map map)
        {
            if (Escaped || parent == null || parent.Destroyed)
            {
                return;
            }

            observedDeathCount++;
            if (observedDeathCount >= PropsBigBird.deathsPerCounterDrop)
            {
                observedDeathCount = 0;
                QliphothCountCurrent--;
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            studyingGraceTicks = 0;
            ClearTransientCombatState();
            enchantCooldownTicks = 75;
            biteCooldownTicks = 0;
            LCAbnormalitiesHelper.PlaySoundAt(BigBirdDefOf.LC_BigBird_Attract, parent);

            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                EnsureBlackoutAura(self);
                BigBirdVfxUtility.SpawnEnchantWave(self.Map, self.DrawPos, 7.5f, duration: 48, intensity: 1.4f);
            }

            (parent as LC_BigBirdPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            studyingGraceTicks = 0;
            ClearTransientCombatState();
            ClearAllEnchantments(parent?.MapHeld);
            (parent as LC_BigBirdPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            ClearTransientCombatState();
            ClearAllEnchantments(prevMap);
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }
            if (mindAttackAnimTicks > 0)
            {
                mindAttackAnimTicks--;
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || !(parent is Pawn pawn) || pawn.Destroyed || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            LC_BigBirdPawn birdPawn = pawn as LC_BigBirdPawn;
            EnsureBlackoutAura(pawn);

            if (biteCooldownTicks > 0)
            {
                biteCooldownTicks--;
            }

            // 1. Active Deadshot decapitation bite sequence (highest priority)
            if (deadshotAnimTicks > 0)
            {
                deadshotAnimTicks--;
                if (deadshotTarget != null && deadshotTarget.Spawned && deadshotTarget.Map == pawn.Map)
                {
                    birdPawn?.FaceCellHorizontal(deadshotTarget.Position);
                }

                if (deadshotImpactPending && deadshotAnimTicks <= DeadshotImpactRemaining)
                {
                    deadshotImpactPending = false;
                    ApplyDeadshotImpact(pawn, deadshotTarget);
                }

                if (deadshotAnimTicks <= 0)
                {
                    deadshotTarget = null;
                }
                return;
            }

            // 2. Active Attract lantern-lure sequence
            if (attractAnimTicks > 0)
            {
                attractAnimTicks--;
                if (attractWavePending && attractAnimTicks <= AttractWaveTriggerRemaining)
                {
                    attractWavePending = false;
                    EmitLanternEnchantment(pawn);
                }
                return;
            }

            // 3. Heavy lumbering footstep audio synced with BigBird_Walk (96t cycle -> step every 48t)
            if (pawn.pather?.Moving == true)
            {
                walkSoundTickCounter++;
                if (walkSoundTickCounter >= 48)
                {
                    walkSoundTickCounter = 0;
                    LCAbnormalitiesHelper.PlaySoundAt(BigBirdDefOf.LC_BigBird_Walk, pawn);
                }
            }
            else
            {
                walkSoundTickCounter = 24;
            }

            // 4. Check for biteable prey in jaws range
            if (biteCooldownTicks <= 0)
            {
                Pawn prey = FindBiteablePrey(pawn);
                if (prey != null)
                {
                    StartDeadshotSequence(pawn, prey);
                    return;
                }
            }

            // 5. Periodic lantern enchantment wave
            enchantCooldownTicks--;
            if (enchantCooldownTicks <= 0)
            {
                enchantCooldownTicks = PropsBigBird.enchantIntervalTicks;
                StartAttractSequence(pawn);
            }
        }

        private void StartAttractSequence(Pawn self)
        {
            attractAnimTicks = AttractAnimDuration;
            attractWavePending = true;
            self.pather?.StopDead();
            LCAbnormalitiesHelper.PlaySoundAt(BigBirdDefOf.LC_BigBird_Attract, self);
            (self as LC_BigBirdPawn)?.PlayAttractAnimation();
        }

        private void StartDeadshotSequence(Pawn self, Pawn victim)
        {
            deadshotAnimTicks = DeadshotAnimDuration;
            deadshotImpactPending = true;
            deadshotTarget = victim;
            biteCooldownTicks = 150;
            self.pather?.StopDead();

            LC_BigBirdPawn birdPawn = self as LC_BigBirdPawn;
            if (birdPawn != null)
            {
                birdPawn.FaceCellHorizontal(victim.Position);
                birdPawn.PlayDeadshotAnimation();
            }

            // Ominous dark gaze windup aura gathering around the target before the jaws slam shut
            if (victim.Spawned && victim.Map != null)
            {
                BigBirdVfxUtility.SpawnBiteBurst(victim.Map, victim.DrawPos, scale: 2.6f, duration: 52, windup: true);
            }
        }

        private Pawn FindBiteablePrey(Pawn self)
        {
            if (self?.Map == null)
            {
                return null;
            }

            Pawn best = null;
            float bestDist = PropsBigBird.biteRange;
            IReadOnlyList<Pawn> pawns = self.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p == self || p.Dead || p.Destroyed || !(p.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                float d = p.Position.DistanceTo(self.Position);
                if (d <= bestDist)
                {
                    // 优先咬杀已被魅惑的目标
                    bool enchanted = BigBirdDefOf.LC_BigBirdEnchanted != null
                        && (p.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_BigBirdEnchanted) ?? false);
                    if (enchanted || best == null)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
            }

            return best;
        }

        public int EmitLanternEnchantment(Pawn self)
        {
            if (self == null || !self.Spawned || self.Map == null || BigBirdDefOf.LC_BigBirdEnchanted == null)
            {
                return 0;
            }

            Map map = self.Map;
            IntVec3 pos = self.Position;
            float radiusSq = PropsBigBird.enchantRadius * PropsBigBird.enchantRadius;

            // Custom GLSL expanding golden-amber hypnotic lantern pulse wave!
            BigBirdVfxUtility.SpawnEnchantWave(map, self.DrawPos, PropsBigBird.enchantRadius, duration: 60, intensity: 1.55f);

            int affected = 0;
            List<Pawn> pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn target = pawns[i];
                if (target == null || target.Dead || target.Downed || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if ((target.Position - pos).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }

                if (target.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_BigBirdEnchanted) == true)
                {
                    continue;
                }

                Hediff h = LCAbnormalitiesHelper.ApplyOrRefreshHediff(target, BigBirdDefOf.LC_BigBirdEnchanted, 1f);
                if (h is HediffWithComps hwc)
                {
                    HediffComp_BigBirdEnchanted comp = hwc.TryGetComp<HediffComp_BigBirdEnchanted>();
                    if (comp != null)
                    {
                        comp.bigBird = self;
                        comp.EnsureEyeMark();
                    }
                }

                if (BigBirdDefOf.LC_BigBirdApproach != null && target.jobs != null)
                {
                    Verse.AI.Job approachJob = JobMaker.MakeJob(BigBirdDefOf.LC_BigBirdApproach, self);
                    target.jobs.TryTakeOrderedJob(approachJob, JobTag.Misc);
                }

                Messages.Message(
                    "LC_BigBird_EnchantedMsg".Translate(target.LabelShortCap),
                    target,
                    MessageTypeDefOf.ThreatSmall,
                    historical: false);
                affected++;
            }

            return affected;
        }

        private void ApplyDeadshotImpact(Pawn self, Pawn victim)
        {
            if (self == null || !self.Spawned || self.Map == null)
            {
                return;
            }

            // Re-acquire closest prey if original stepped slightly away during windup
            if (victim == null || victim.Dead || victim.Destroyed || !victim.Spawned || victim.Map != self.Map
                || victim.Position.DistanceTo(self.Position) > PropsBigBird.biteRange + 1.4f)
            {
                victim = FindBiteablePrey(self);
            }

            if (victim == null)
            {
                return;
            }

            ExecuteDecapitation(self, victim);
        }

        public void ExecuteDecapitation(Pawn self, Pawn victim)
        {
            if (self == null || victim == null || victim.Dead || victim.Destroyed)
            {
                return;
            }

            if (deadshotAnimTicks <= 0)
            {
                deadshotAnimTicks = 58;
            }

            SoundDef s = Rand.Bool ? BigBirdDefOf.LC_BigBird_Dead1 : BigBirdDefOf.LC_BigBird_Dead2;
            LCAbnormalitiesHelper.PlaySoundAt(s, victim);

            // Violent crimson-eye obsidian decapitation burst VFX + camera shake
            if (victim.Spawned && victim.Map != null)
            {
                BigBirdVfxUtility.SpawnBiteBurst(victim.Map, victim.DrawPos, scale: 3.4f, duration: 40, windup: false);
            }

            BodyPartRecord headPart = null;
            if (victim.health?.hediffSet != null)
            {
                foreach (BodyPartRecord part in victim.health.hediffSet.GetNotMissingParts())
                {
                    if (part.def == BodyPartDefOf.Head || part.def == BodyPartDefOf.Neck)
                    {
                        headPart = part;
                        break;
                    }
                }
            }

            DamageInfo dinfo = new DamageInfo(
                LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                PropsBigBird.biteDamage,
                0.90f,
                instigator: self,
                hitPart: headPart);
            victim.TakeDamage(dinfo);

            Messages.Message(
                "LC_BigBird_BiteMsg".Translate(victim.LabelShortCap),
                new LookTargets(victim.PositionHeld, victim.MapHeld),
                MessageTypeDefOf.ThreatBig,
                historical: false);
        }

        private static void ClearAllEnchantments(Map map)
        {
            if (map?.mapPawns == null || BigBirdDefOf.LC_BigBirdEnchanted == null)
            {
                return;
            }

            List<Pawn> pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                Hediff h = p?.health?.hediffSet?.GetFirstHediffOfDef(BigBirdDefOf.LC_BigBirdEnchanted);
                if (h != null)
                {
                    p.health.RemoveHediff(h);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref observedDeathCount, "bbObservedDeathCount", 0);
            Scribe_Values.Look(ref enchantCooldownTicks, "bbEnchantCooldownTicks", 120);
            Scribe_Values.Look(ref biteCooldownTicks, "bbBiteCooldownTicks", 0);
            Scribe_Values.Look(ref studyingGraceTicks, "bbStudyingGraceTicks", 0);
            Scribe_Values.Look(ref mindAttackAnimTicks, "bbMindAttackAnimTicks", 0);
            Scribe_Values.Look(ref attractAnimTicks, "bbAttractAnimTicks", 0);
            Scribe_Values.Look(ref attractWavePending, "bbAttractWavePending", false);
            Scribe_Values.Look(ref deadshotAnimTicks, "bbDeadshotAnimTicks", 0);
            Scribe_Values.Look(ref deadshotImpactPending, "bbDeadshotImpactPending", false);
            Scribe_References.Look(ref deadshotTarget, "bbDeadshotTarget");
        }
    }

    public class LC_BigBirdPawn : LC_StandardEntityPawn
    {
        private bool facingWest;

        public CompBigBird BigBirdComp => EntityComp as CompBigBird;

        public bool IsFacingLeft => facingWest;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public override string CurrentBodyTexPath =>
            BigBirdComp?.IsBitingPose == true
                ? "Things/Pawn/BigBird/BigBird_Bite"
                : "Things/Pawn/BigBird/BigBird";

        public override Vector2 CurrentBodyDrawSize => new Vector2(2.85f, 2.85f);

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

        public void PlayMindAttackAnimation()
        {
            if (BigBirdDefOf.BigBird_MindAttack != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(BigBirdDefOf.BigBird_MindAttack);
            }
        }

        public void PlayAttractAnimation()
        {
            if (BigBirdDefOf.BigBird_Attract != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(BigBirdDefOf.BigBird_Attract);
            }
        }

        public void PlayDeadshotAnimation()
        {
            if (BigBirdDefOf.BigBird_Deadshot != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(BigBirdDefOf.BigBird_Deadshot);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompBigBird comp = BigBirdComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                if (comp != null && comp.IsPerformingMindAttack)
                {
                    desired = BigBirdDefOf.BigBird_MindAttack;
                }
                else if (comp != null && comp.IsBeingStudiedNow)
                {
                    desired = BigBirdDefOf.BigBird_Work;
                }
                else
                {
                    desired = BigBirdDefOf.BigBird_Idle;
                }
            }
            else if (comp != null && comp.IsPerformingDeadshot)
            {
                desired = BigBirdDefOf.BigBird_Deadshot;
            }
            else if (comp != null && comp.IsPerformingAttract)
            {
                desired = BigBirdDefOf.BigBird_Attract;
            }
            else
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? BigBirdDefOf.BigBird_Walk
                    : BigBirdDefOf.BigBird_Idle;
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
            CompBigBird comp = BigBirdComp;
            if (comp != null)
            {
                if ((comp.IsPerformingDeadshot || comp.IsPerformingAttract) && pather?.Moving == true)
                {
                    pather.StopDead();
                }

                if (comp.Escaped && Spawned && !Dead)
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref facingWest, "bbFacingWest", false);
        }
    }

    public class JobGiver_BigBirdStalk : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompBigBird comp = pawn.TryGetComp<CompBigBird>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsPerformingDeadshot || comp.IsPerformingAttract)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(target.Position) <= comp.PropsBigBird.biteRange)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 30);
            }

            Verse.AI.Job stalk = JobMaker.MakeJob(JobDefOf.Goto, target);
            stalk.expiryInterval = 90;
            stalk.checkOverrideOnExpire = true;
            stalk.canBashDoors = true;
            return stalk;
        }
    }

    public class HediffCompProperties_BigBirdEnchanted : HediffCompProperties
    {
        public HediffCompProperties_BigBirdEnchanted()
        {
            compClass = typeof(HediffComp_BigBirdEnchanted);
        }
    }

    /// <summary>大鸟提灯魅惑状态：持续引导宿主走向大鸟、维持头顶黄→橙→红眼眸标记，并提供手动打破恍惚的微操按钮（消耗少量白伤挣脱）。</summary>
    public class HediffComp_BigBirdEnchanted : HediffComp
    {
        public Pawn bigBird;
        private Thing_BigBirdEyeMark activeMark;

        public void EnsureEyeMark()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null || Pawn.Dead)
            {
                return;
            }

            if (activeMark == null || activeMark.Destroyed || activeMark.Map != Pawn.Map)
            {
                activeMark = BigBirdVfxUtility.SpawnEnchantEyeMark(Pawn, bigBird);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (bigBird == null || bigBird.Dead || bigBird.Destroyed || !bigBird.Spawned || bigBird.Map != Pawn.Map)
            {
                severityAdjustment = -1f;
                return;
            }

            EnsureEyeMark();

            if (Pawn.IsHashIntervalTick(60) && !Pawn.Downed && !Pawn.Dead && BigBirdDefOf.LC_BigBirdApproach != null
                && Pawn.CurJobDef != BigBirdDefOf.LC_BigBirdApproach)
            {
                Pawn.jobs?.StartJob(JobMaker.MakeJob(BigBirdDefOf.LC_BigBirdApproach, bigBird), JobCondition.InterruptForced);
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (activeMark != null && !activeMark.Destroyed)
            {
                activeMark.Destroy();
            }
            activeMark = null;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn != null && Pawn.IsColonistPlayerControlled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "LC_BigBird_SnapOutGizmoLabel".Translate(),
                    defaultDesc = "LC_BigBird_SnapOutGizmoDesc".Translate(),
                    icon = TexCommand.Draft,
                    action = delegate
                    {
                        Pawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 10f));
                        Pawn.health?.RemoveHediff(parent);
                        Pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    }
                };
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref bigBird, "bbEnchantSource");
        }
    }

    /// <summary>受魅惑者不由自主走向大鸟的工作驱动。</summary>
    public class JobDriver_BigBirdApproach : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => BigBirdDefOf.LC_BigBirdEnchanted == null || !(pawn.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_BigBirdEnchanted) ?? false));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(45);
        }
    }

    /// <summary>
    /// 目灯（WAW 黑伤重锤）伤害处理器：
    /// 重击目标并造成 2 格范围 55% 溅射黑伤，伴随原作 hammer 锤击音效与黑金冲击波 VFX；
    /// 命中时有 25% 概率施加“目灯凝视”（迟缓 + 侵蚀易伤，再次命中额外增伤 25% 并浮现大鸟金眸印记）。
    /// </summary>
    public class DamageWorker_Lamp : DamageWorker_FourColor
    {
        private const float VulnerabilityProcChance = 0.25f;

        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Map victimMap = victim?.MapHeld;
            IntVec3 center = victim?.PositionHeld ?? IntVec3.Invalid;
            Vector3 hitPos = victim != null
                ? (victim.Spawned ? victim.DrawPos : center.ToVector3Shifted())
                : Vector3.zero;

            Pawn vp = victim as Pawn;
            bool alreadyMarked = vp != null && !vp.Dead && !vp.Destroyed && BigBirdDefOf.LC_LampDazed != null
                && (vp.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_LampDazed) ?? false);

            // 侵蚀易伤：对已被目灯凝视标记的目标造成 +25% 额外黑伤
            if (alreadyMarked)
            {
                dinfo.SetAmount(dinfo.Amount * 1.25f);
            }

            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);

            bool proc = false;
            if (vp != null && !vp.Dead && !vp.Destroyed && BigBirdDefOf.LC_LampDazed != null)
            {
                if (alreadyMarked || Rand.Chance(VulnerabilityProcChance))
                {
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(vp, BigBirdDefOf.LC_LampDazed, 1f);
                    proc = true;
                }
            }

            if (victim != null && victimMap != null && center.InBounds(victimMap))
            {
                Map map = victimMap;

                // Play authentic Lobotomy Corp hammer.wav + spawn custom GLSL golden-black smash impact VFX!
                if (BigBirdDefOf.LC_Lamp_Hammer != null)
                {
                    BigBirdDefOf.LC_Lamp_Hammer.PlayOneShot(new TargetInfo(center, map));
                }
                BigBirdVfxUtility.SpawnLampImpact(map, hitPos, radius: 2.2f, proc: proc);
                if (proc && vp != null && vp.Spawned)
                {
                    BigBirdVfxUtility.SpawnLampEyeMark(vp, duration: 75);
                }

                if (dinfo.Instigator is Pawn attacker)
                {
                    if (!EgoWeaponAnimationManager.IsPlaying(attacker, LampWeaponAnimHandler.ClipSkillSlam)
                        && !EgoWeaponAnimationManager.IsPlaying(attacker, LampWeaponAnimHandler.ClipSkillWarmup))
                    {
                        ThingWithComps heldWeapon = attacker.equipment?.Primary;
                        if (heldWeapon != null && heldWeapon.def?.defName == LampWeaponAnimHandler.WeaponDefName)
                        {
                            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, hitPos);
                            EgoWeaponAnimationManager.Play(attacker, heldWeapon, LampWeaponAnimHandler.ClipNormalSmash, 64, aimAngle);
                        }
                    }

                    float splashAmt = Mathf.Max(5f, dinfo.Amount * 0.55f);
                    List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                    for (int i = 0; i < nearby.Count; i++)
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

                        if (other.Position.DistanceTo(center) <= 2.1f)
                        {
                            bool otherMarked = BigBirdDefOf.LC_LampDazed != null
                                && (other.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_LampDazed) ?? false);
                            float finalSplash = otherMarked ? splashAmt * 1.25f : splashAmt;

                            other.TakeDamage(new DamageInfo(
                                LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                                finalSplash,
                                0.35f,
                                dinfo.Angle,
                                attacker,
                                weapon: dinfo.Weapon));
                            if (!other.Dead && BigBirdDefOf.LC_LampDazed != null && (otherMarked || Rand.Chance(VulnerabilityProcChance)))
                            {
                                LCAbnormalitiesHelper.ApplyOrRefreshHediff(other, BigBirdDefOf.LC_LampDazed, 0.8f);
                                BigBirdVfxUtility.SpawnLampEyeMark(other, duration: 60);
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
