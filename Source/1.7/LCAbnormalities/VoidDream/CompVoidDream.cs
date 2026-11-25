using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.VoidDream
{
    [DefOf]
    public static class VoidDreamDefOf
    {
        public static ThingDef VoidDreamEgg;
        public static HediffDef LC_VoidDreamSleep;
        public static JobDef LC_WakeVoidDreamSleeper;
        public static SoundDef LC_VoidDream_Change;
        public static SoundDef LC_VoidDream_Shout;
        public static SoundDef LC_VoidDream_Skill;
        public static SoundDef LC_VoidDream_Ball;
        public static SoundDef LC_VoidDream_Default;
        public static SoundDef LC_VoidDream_EgoShot;

        public static AnimationDef VoidDream_SheepIdle;
        public static AnimationDef VoidDream_SheepWalk;
        public static AnimationDef VoidDream_Transform;
        public static AnimationDef VoidDream_RoosterIdle;
        public static AnimationDef VoidDream_RoosterWalk;
        public static AnimationDef VoidDream_RoosterCrowStart;
        public static AnimationDef VoidDream_RoosterCrowLoop;
        public static AnimationDef VoidDream_RoosterCrowEnd;

        public static ThingDef LC_VoidDreamSleepOrbFX;
        public static ThingDef LC_VoidDreamCrowWaveFX;
        public static ThingDef LC_VoidDreamSleepAuraFX;
        public static ThingDef LC_VoidDreamNightmareBurstFX;
        public static ThingDef LC_VoidDreamSkillRunnerFX;

        static VoidDreamDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VoidDreamDefOf));
        }
    }

    public enum VoidDreamCrowPhase
    {
        None = 0,
        Start = 1,
        Loop = 2,
        End = 3
    }

    public class CompProperties_VoidDream : CompProperties_LCAbnormality
    {
        public int crowIntervalTicks = 420;
        public float crowRadius = 10f;
        public int orbIntervalTicks = 210;
        public float orbRange = 14f;
        public int orbVolleyCount = 4;
        public int sheepCalmReturnTicks = 3600;

        public CompProperties_VoidDream()
        {
            compClass = typeof(CompVoidDream);
        }
    }

    /// <summary>
    /// T-02-99 虚空之梦核心逻辑（严格对齐脑叶公司原作两阶段机制）：
    /// 1) 收容态：悬浮紫羊，播放轻柔呼吸/眨眼/飘浮 Zzz 动画；自律 I 级员工工作必降逆卡巴拉计数器；
    /// 2) 出逃一阶段（绵羊形态）：漂浮游荡并周期发射催眠梦境光球（BakuSheepSkill），命中后令员工在云朵上沉睡；
    ///    若 60 秒内未被攻击则安静化蛋返回；
    /// 3) 受击变身（0_Transform）：一旦受到伤害即剧烈震颤并爆发 5 帧羊毛云，蜕变为高大的三眼怪鸡；
    /// 4) 出逃二阶段（公鸡形态）：主动追击啄咬，并周期发动三阶段催眠啼鸣（Start → Loop 三连声波脉冲 → End），
    ///    对清醒人类造成精神冲击并催眠，对已沉睡者触发毁灭性白/黑双伤噩梦碎裂。
    /// </summary>
    public class CompVoidDream : CompLCAbnormality
    {
        private const int TransformDurationTicks = 150;
        private const int TransformCloudStartElapsed = 45;
        private const int TransformCloudEndElapsed = 135;
        private const int TransformRigSwapElapsed = 72;

        private const int CrowStartTicks = 60;
        private const int CrowLoopTicks = 180;
        private const int CrowEndTicks = 60;

        private bool isRoosterForm;
        private int transformAnimTicks;
        private int sheepBreachTicks;
        private int orbCooldownTicks = 90;
        private int crowCooldownTicks = 180;
        private VoidDreamCrowPhase crowPhase = VoidDreamCrowPhase.None;
        private int crowPhaseTicks;
        private int ambientSoundCooldown = 180;

        private readonly HashSet<int> shatteredIdsThisCrow = new HashSet<int>();

        public CompProperties_VoidDream PropsDream => (CompProperties_VoidDream)props;

        public bool IsRoosterForm => isRoosterForm;

        public bool IsTransforming => transformAnimTicks > 0;

        public bool IsCrowing => crowPhase != VoidDreamCrowPhase.None;

        public VoidDreamCrowPhase CurrentCrowPhase => crowPhase;

        /// <summary>当前渲染树是否显示公鸡骨骼（变身动画过半后无缝切换）。</summary>
        public bool ShowRoosterRig
        {
            get
            {
                if (!Escaped)
                {
                    return false;
                }
                if (transformAnimTicks > 0)
                {
                    int elapsed = TransformDurationTicks - transformAnimTicks;
                    return elapsed >= TransformRigSwapElapsed;
                }
                return isRoosterForm;
            }
        }

        /// <summary>变身动画羊毛云爆发窗口（elapsed 45..135）。</summary>
        public bool IsShowingTransformCloud
        {
            get
            {
                if (transformAnimTicks <= 0)
                {
                    return false;
                }
                int elapsed = TransformDurationTicks - transformAnimTicks;
                return elapsed >= TransformCloudStartElapsed && elapsed <= TransformCloudEndElapsed;
            }
        }

        public float TransformCloudProgress
        {
            get
            {
                if (!IsShowingTransformCloud)
                {
                    return 0f;
                }
                int elapsed = TransformDurationTicks - transformAnimTicks;
                return Mathf.Clamp01((elapsed - TransformCloudStartElapsed) / (float)(TransformCloudEndElapsed - TransformCloudStartElapsed));
            }
        }

        private void ResetTransientBreachState()
        {
            isRoosterForm = false;
            transformAnimTicks = 0;
            sheepBreachTicks = 0;
            orbCooldownTicks = 90;
            crowCooldownTicks = 180;
            crowPhase = VoidDreamCrowPhase.None;
            crowPhaseTicks = 0;
            shatteredIdsThisCrow.Clear();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            if (ambientSoundCooldown <= 0 && !Escaped && parent != null)
            {
                ambientSoundCooldown = 260;
                LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Default, parent);
            }
            return base.Notify_StudyInterval(studier, workType);
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            if (studier == null || parent == null || parent.Destroyed || Escaped)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status != null && status.GetPawnStatusELevel(EPawnStatus.Temperance) <= EPawnLevel.I)
            {
                QliphothCountCurrent--;
                Messages.Message(
                    "LC_VoidDream_LowTemperanceMsg".Translate(studier.LabelShortCap),
                    parent,
                    MessageTypeDefOf.NegativeEvent,
                    historical: false);
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            ResetTransientBreachState();
            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Default, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                VoidDreamVfxUtility.SpawnCrowWave(
                    self.Map,
                    self.DrawPos,
                    radius: 3.8f,
                    duration: 36,
                    color: new Color(0.76f, 0.48f, 1.0f, 0.90f),
                    accentColor: new Color(0.42f, 0.96f, 0.70f, 0.90f),
                    intensity: 1.25f);
            }
            (parent as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            ResetTransientBreachState();
            (parent as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0.01f && Escaped && !isRoosterForm && transformAnimTicks <= 0)
            {
                TriggerTransformToRooster();
            }
        }

        public void TriggerTransformToRooster()
        {
            if (parent == null || parent.Destroyed || !Escaped || isRoosterForm || transformAnimTicks > 0)
            {
                return;
            }

            transformAnimTicks = TransformDurationTicks;
            crowCooldownTicks = 150;
            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Change, parent);

            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                self.pather?.StopDead();
                VoidDreamVfxUtility.SpawnCrowWave(
                    self.Map,
                    self.DrawPos,
                    radius: 4.5f,
                    duration: 42,
                    color: new Color(0.84f, 0.34f, 0.96f, 1.0f),
                    accentColor: new Color(0.96f, 0.24f, 0.56f, 1.0f),
                    intensity: 1.35f);
            }

            Messages.Message("LC_VoidDream_TransformMsg".Translate(), parent, MessageTypeDefOf.ThreatBig, historical: false);
            (parent as LC_VoidDreamPawn)?.PlayTransformAnimation();
        }

        public override void TickContained()
        {
            base.TickContained();
            if (ambientSoundCooldown > 0)
            {
                ambientSoundCooldown--;
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || !(parent is Pawn self) || self.Destroyed || !self.Spawned || self.Map == null)
            {
                return;
            }

            // Stage transition: Sheep -> Rooster transformation sequence (0_Transform)
            if (transformAnimTicks > 0)
            {
                transformAnimTicks--;
                int elapsed = TransformDurationTicks - transformAnimTicks;
                if (elapsed == TransformRigSwapElapsed)
                {
                    VoidDreamVfxUtility.SpawnCrowWave(
                        self.Map,
                        self.DrawPos,
                        radius: 6.2f,
                        duration: 40,
                        color: new Color(0.76f, 0.26f, 0.96f, 1.0f),
                        accentColor: new Color(0.98f, 0.28f, 0.62f, 1.0f),
                        intensity: 1.55f);
                    FleckMaker.ThrowDustPuffThick(self.DrawPos, self.Map, 2.6f, new Color(0.92f, 0.88f, 0.98f));
                }

                if (transformAnimTicks <= 0)
                {
                    isRoosterForm = true;
                    (self as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
                }
                return;
            }

            // Stage 1: Floating Sheep form — fires hypnotic light orbs, peacefully returns after 60s if unharmed
            if (!isRoosterForm)
            {
                sheepBreachTicks++;
                if (PropsDream.sheepCalmReturnTicks > 0 && sheepBreachTicks >= PropsDream.sheepCalmReturnTicks)
                {
                    ReturnQuietlyToEgg();
                    return;
                }

                orbCooldownTicks--;
                if (orbCooldownTicks <= 0)
                {
                    orbCooldownTicks = Mathf.Max(90, PropsDream.orbIntervalTicks);
                    FireSheepDreamOrbVolley(self);
                }
                return;
            }

            // Stage 2: Enraged Rooster form — 3-phase hypnotic crowing sequence
            if (crowPhase != VoidDreamCrowPhase.None)
            {
                TickCrowSequence(self);
                return;
            }

            crowCooldownTicks--;
            if (crowCooldownTicks <= 0)
            {
                crowCooldownTicks = Mathf.Max(180, PropsDream.crowIntervalTicks);
                BeginCrowSequence(self);
            }
        }

        private void FireSheepDreamOrbVolley(Pawn self)
        {
            Map map = self.Map;
            if (map == null)
            {
                return;
            }

            List<Pawn> awakeHumans = new List<Pawn>();
            IReadOnlyList<Pawn> all = map.mapPawns?.AllPawnsSpawned;
            float rangeSq = PropsDream.orbRange * PropsDream.orbRange;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    Pawn p = all[i];
                    if (p == null || p == self || p.Dead || !p.Spawned || !(p.RaceProps?.Humanlike ?? false))
                    {
                        continue;
                    }
                    if (VoidDreamDefOf.LC_VoidDreamSleep != null
                        && (p.health?.hediffSet?.HasHediff(VoidDreamDefOf.LC_VoidDreamSleep) ?? false))
                    {
                        continue;
                    }
                    if ((p.Position - self.Position).LengthHorizontalSquared <= rangeSq)
                    {
                        awakeHumans.Add(p);
                    }
                }
            }

            Vector3 origin = self.DrawPos.Yto0() + new Vector3(0f, 0f, 0.25f);
            int count = Mathf.Clamp(PropsDream.orbVolleyCount, 2, 6);
            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Ball ?? VoidDreamDefOf.LC_VoidDream_Default, self);
            VoidDreamVfxUtility.SpawnCrowWave(
                map,
                origin,
                radius: 2.2f,
                duration: 24,
                color: new Color(0.76f, 0.44f, 1.0f, 0.90f),
                accentColor: new Color(0.38f, 0.96f, 0.66f, 0.90f),
                intensity: 1.25f);

            for (int i = 0; i < count; i++)
            {
                Pawn target = awakeHumans.Count > 0 ? awakeHumans[i % awakeHumans.Count] : null;
                Vector3 dir;
                if (target != null)
                {
                    Vector3 toTarget = (target.DrawPos.Yto0() - origin);
                    float baseAng = Mathf.Atan2(toTarget.z, toTarget.x);
                    float spread = (i - (count - 1) * 0.5f) * 0.34f + Rand.Range(-0.12f, 0.12f);
                    dir = new Vector3(Mathf.Cos(baseAng + spread), 0f, Mathf.Sin(baseAng + spread));
                }
                else
                {
                    float ang = (Mathf.PI * 2f * i / count) + Rand.Range(-0.25f, 0.25f);
                    dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                }

                VoidDreamVfxUtility.SpawnSleepOrb(self, target, origin + dir * 0.45f, dir, Rand.Range(0.13f, 0.16f));
            }
        }

        private void BeginCrowSequence(Pawn self)
        {
            crowPhase = VoidDreamCrowPhase.Start;
            crowPhaseTicks = CrowStartTicks;
            shatteredIdsThisCrow.Clear();
            self.pather?.StopDead();

            Pawn nearest = LCAbnormalitiesHelper.FindNearestHumanlike(self.Map, self.Position);
            if (nearest != null)
            {
                (self as LC_VoidDreamPawn)?.FaceCellHorizontal(nearest.Position);
            }

            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Shout, self);
            VoidDreamVfxUtility.SpawnCrowWave(
                self.Map,
                self.DrawPos,
                radius: 3.2f,
                duration: 36,
                color: new Color(0.74f, 0.30f, 0.98f, 0.85f),
                accentColor: new Color(0.96f, 0.28f, 0.62f, 0.85f),
                intensity: 1.15f);
            (self as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
        }

        private void TickCrowSequence(Pawn self)
        {
            self.pather?.StopDead();
            crowPhaseTicks--;

            if (crowPhase == VoidDreamCrowPhase.Start)
            {
                if (crowPhaseTicks <= 0)
                {
                    crowPhase = VoidDreamCrowPhase.Loop;
                    crowPhaseTicks = CrowLoopTicks;
                    (self as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
                }
                return;
            }

            if (crowPhase == VoidDreamCrowPhase.Loop)
            {
                int elapsed = CrowLoopTicks - crowPhaseTicks;
                // 3 rhythmic sonic pulses synchronized with VoidDream_RoosterCrowLoop beak peaks (ticks 30, 90, 150)
                if (elapsed == 30)
                {
                    EmitHypnoticCrowPulse(self, pulseIndex: 1);
                }
                else if (elapsed == 90)
                {
                    EmitHypnoticCrowPulse(self, pulseIndex: 2);
                }
                else if (elapsed == 150)
                {
                    EmitHypnoticCrowPulse(self, pulseIndex: 3);
                }

                if (crowPhaseTicks <= 0)
                {
                    crowPhase = VoidDreamCrowPhase.End;
                    crowPhaseTicks = CrowEndTicks;
                    (self as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
                }
                return;
            }

            if (crowPhase == VoidDreamCrowPhase.End && crowPhaseTicks <= 0)
            {
                crowPhase = VoidDreamCrowPhase.None;
                shatteredIdsThisCrow.Clear();
                (self as LC_VoidDreamPawn)?.UpdateAnimation(force: true);
            }
        }

        private void EmitHypnoticCrowPulse(Pawn self, int pulseIndex)
        {
            Map map = self.Map;
            IntVec3 pos = self.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Shout, self);

            // Dramatic expanding GLSL hypnotic sonic wave ring + mist
            bool finalPulse = pulseIndex >= 3;
            VoidDreamVfxUtility.SpawnCrowWave(
                map,
                self.DrawPos + new Vector3(0f, 0f, 0.35f),
                radius: PropsDream.crowRadius * (finalPulse ? 1.05f : 0.85f),
                duration: finalPulse ? 52 : 42,
                color: finalPulse ? new Color(0.84f, 0.24f, 0.82f, 1.0f) : new Color(0.64f, 0.34f, 0.98f, 1.0f),
                accentColor: finalPulse ? new Color(0.98f, 0.20f, 0.48f, 1.0f) : new Color(0.92f, 0.36f, 0.72f, 1.0f),
                intensity: finalPulse ? 1.65f : 1.38f);
            FleckMaker.ThrowDustPuffThick(self.DrawPos, map, 2.0f, new Color(0.68f, 0.38f, 0.94f));

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            float radiusSq = PropsDream.crowRadius * PropsDream.crowRadius;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                if (target == null || target.Dead || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                float distSq = (target.Position - pos).LengthHorizontalSquared;
                if (distSq > radiusSq)
                {
                    continue;
                }

                Hediff existing = target.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep);
                if (existing != null)
                {
                    if (shatteredIdsThisCrow.Add(target.thingIDNumber))
                    {
                        VoidDreamVfxUtility.SpawnNightmareBurst(map, target.DrawPos, 3.2f);
                        target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 35f, instigator: self));
                        target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 25f, instigator: self));
                        if (!target.Dead && target.health?.hediffSet != null)
                        {
                            target.health.RemoveHediff(existing);
                        }
                        Messages.Message("LC_VoidDream_SecondCrowMsg".Translate(target.LabelShortCap), target, MessageTypeDefOf.ThreatBig, historical: false);
                    }
                }
                else
                {
                    // Pulses 1 & 2 inflict eerie White sanity shock; final pulse (or close-range pulse 2) plunges victims into Void Hypnosis!
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 5f, instigator: self));
                    bool inflictSleep = finalPulse || (pulseIndex == 2 && distSq <= 25f);
                    if (inflictSleep && !target.Dead)
                    {
                        shatteredIdsThisCrow.Add(target.thingIDNumber);
                        LCAbnormalitiesHelper.ApplyOrRefreshHediff(target, VoidDreamDefOf.LC_VoidDreamSleep, 1f);
                        LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Skill, target);
                        VoidDreamVfxUtility.EnsureSleepAura(target);
                        Messages.Message("LC_VoidDream_SleepMsg".Translate(target.LabelShortCap), target, MessageTypeDefOf.ThreatSmall, historical: false);
                    }
                }
            }
        }

        public void ReturnQuietlyToEgg()
        {
            if (parent is Pawn p && !p.Destroyed)
            {
                if (p.MapHeld != null)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Default, p);
                    VoidDreamVfxUtility.SpawnCrowWave(
                        p.MapHeld,
                        p.DrawPos,
                        radius: 2.8f,
                        duration: 30,
                        color: new Color(0.82f, 0.72f, 1.0f, 0.85f),
                        accentColor: new Color(0.45f, 0.96f, 0.74f, 0.85f),
                        intensity: 1.1f);
                    Messages.Message(
                        "LC_VoidDream_ReturnMsg".Translate(),
                        new LookTargets(p.PositionHeld, p.MapHeld),
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }

                if (VoidDreamDefOf.VoidDreamEgg != null)
                {
                    LCAbnormalitiesHelper.ReturnEntityToEgg(p, VoidDreamDefOf.VoidDreamEgg, biosignature);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isRoosterForm, "vdIsRoosterForm", false);
            Scribe_Values.Look(ref transformAnimTicks, "vdTransformAnimTicks", 0);
            Scribe_Values.Look(ref sheepBreachTicks, "vdSheepBreachTicks", 0);
            Scribe_Values.Look(ref orbCooldownTicks, "vdOrbCooldownTicks", 90);
            Scribe_Values.Look(ref crowCooldownTicks, "vdCrowCooldownTicks", 180);
            Scribe_Values.Look(ref crowPhase, "vdCrowPhase", VoidDreamCrowPhase.None);
            Scribe_Values.Look(ref crowPhaseTicks, "vdCrowPhaseTicks", 0);
        }
    }

    /// <summary>虚空之梦实体类：驱动绵羊/变身/公鸡多部件动画与水平朝向追踪。</summary>
    public class LC_VoidDreamPawn : LC_StandardEntityPawn
    {
        private bool facingWest = false;

        public CompVoidDream DreamComp => EntityComp as CompVoidDream;

        public bool IsFacingLeft => facingWest;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public override string CurrentBodyTexPath =>
            DreamComp?.ShowRoosterRig == true
                ? "Things/Pawn/VoidDream/VoidDream_Rooster"
                : "Things/Pawn/VoidDream/VoidDream_Sheep";

        public override Vector2 CurrentBodyDrawSize =>
            DreamComp?.ShowRoosterRig == true
                ? new Vector2(2.4f, 2.4f)
                : new Vector2(1.75f, 1.75f);

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

        public void PlayTransformAnimation()
        {
            if (VoidDreamDefOf.VoidDream_Transform != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(VoidDreamDefOf.VoidDream_Transform);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompVoidDream comp = DreamComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = VoidDreamDefOf.VoidDream_SheepIdle;
            }
            else if (comp != null && comp.IsTransforming)
            {
                desired = VoidDreamDefOf.VoidDream_Transform;
            }
            else if (comp != null && comp.IsRoosterForm)
            {
                switch (comp.CurrentCrowPhase)
                {
                    case VoidDreamCrowPhase.Start:
                        desired = VoidDreamDefOf.VoidDream_RoosterCrowStart;
                        break;
                    case VoidDreamCrowPhase.Loop:
                        desired = VoidDreamDefOf.VoidDream_RoosterCrowLoop;
                        break;
                    case VoidDreamCrowPhase.End:
                        desired = VoidDreamDefOf.VoidDream_RoosterCrowEnd;
                        break;
                    default:
                        bool roosterMoving = Spawned && pather?.Moving == true;
                        desired = roosterMoving
                            ? VoidDreamDefOf.VoidDream_RoosterWalk
                            : VoidDreamDefOf.VoidDream_RoosterIdle;
                        break;
                }
            }
            else
            {
                bool sheepMoving = Spawned && pather?.Moving == true;
                desired = sheepMoving
                    ? VoidDreamDefOf.VoidDream_SheepWalk
                    : VoidDreamDefOf.VoidDream_SheepIdle;
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
            CompVoidDream comp = DreamComp;
            if (comp != null)
            {
                if ((comp.IsTransforming || comp.IsCrowing) && pather?.Moving == true)
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
            Scribe_Values.Look(ref facingWest, "vdFacingWest", false);
        }
    }

    /// <summary>行为树条件节点：仅当虚空之梦已变身为公鸡且未处于变身/啼鸣僵直时，才主动近战索敌追击。</summary>
    public class ThinkNode_ConditionalVoidDreamRooster : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            CompVoidDream comp = (pawn as LC_VoidDreamPawn)?.DreamComp ?? pawn?.TryGetComp<CompVoidDream>();
            return comp != null && comp.Escaped && comp.IsRoosterForm && !comp.IsTransforming && !comp.IsCrowing;
        }
    }

    public class HediffCompProperties_VoidDreamSleep : HediffCompProperties
    {
        public int nightmareTimeoutTicks = 3600;

        public HediffCompProperties_VoidDreamSleep()
        {
            compClass = typeof(HediffComp_VoidDreamSleep);
        }
    }

    /// <summary>虚空催眠：陷入深度昏睡并托举于梦境云上；超过 60 秒未被同事拍醒将承受致命白/黑双伤噩梦反噬。</summary>
    public class HediffComp_VoidDreamSleep : HediffComp
    {
        private int sleepTicks;

        public HediffCompProperties_VoidDreamSleep Props => (HediffCompProperties_VoidDreamSleep)props;

        public float DangerFraction => Mathf.Clamp01(sleepTicks / (float)Mathf.Max(1, Props.nightmareTimeoutTicks));

        public override string CompTipStringExtra =>
            "LC_VoidDream_SleepCountdownTip".Translate(Mathf.Max(0, (Props.nightmareTimeoutTicks - sleepTicks) / 60));

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            VoidDreamVfxUtility.EnsureSleepAura(Pawn);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                return;
            }

            if (sleepTicks % 60 == 0 && Pawn.Spawned)
            {
                VoidDreamVfxUtility.EnsureSleepAura(Pawn);
            }

            sleepTicks++;
            if (sleepTicks >= Props.nightmareTimeoutTicks)
            {
                Messages.Message("LC_VoidDream_NightmareMsg".Translate(Pawn.LabelShortCap), Pawn, MessageTypeDefOf.ThreatBig, historical: false);
                if (Pawn.MapHeld != null)
                {
                    VoidDreamVfxUtility.SpawnNightmareBurst(Pawn.MapHeld, Pawn.DrawPos, 3.2f);
                }
                Pawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 40f));
                Pawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 35f));
                if (!Pawn.Dead && Pawn.health?.hediffSet != null && parent != null)
                {
                    Pawn.health.RemoveHediff(parent);
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "LC_VoidDream_WakeGizmoLabel".Translate(),
                defaultDesc = "LC_VoidDream_WakeGizmoDesc".Translate(Pawn.LabelShortCap),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/VoidDream", reportFailure: false) ?? BaseContent.BadTex,
                action = OpenWakeMenu
            };
        }

        private void OpenWakeMenu()
        {
            Map map = Pawn?.MapHeld;
            if (map?.mapPawns?.FreeColonistsSpawned == null)
            {
                return;
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (c == null || c == Pawn || c.Dead || c.Downed || !c.CanReach(Pawn, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                opts.Add(new FloatMenuOption(c.LabelShortCap, () =>
                {
                    Verse.AI.Job job = JobMaker.MakeJob(VoidDreamDefOf.LC_WakeVoidDreamSleeper, Pawn);
                    c.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }

            if (opts.Count == 0)
            {
                opts.Add(new FloatMenuOption("NoColonistAbleToReachTarget".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref sleepTicks, "vdSleepTicks", 0);
        }
    }

    /// <summary>唤醒陷入虚空催眠的同伴并驱散其噩梦。</summary>
    public class JobDriver_WakeVoidDreamSleeper : JobDriver
    {
        private Pawn TargetSleeper => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetSleeper, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(60).WithProgressBarToilDelay(TargetIndex.A);
            Toil wake = ToilMaker.MakeToil("WakeVoidDreamSleeper");
            wake.initAction = delegate
            {
                Pawn target = TargetSleeper;
                if (target != null && !target.Dead)
                {
                    Hediff sleep = target.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep);
                    if (sleep != null)
                    {
                        target.health.RemoveHediff(sleep);
                    }
                    if (target.Map != null)
                    {
                        FleckMaker.ThrowDustPuffThick(target.DrawPos, target.Map, 1.1f, new Color(0.92f, 0.90f, 1.0f));
                    }
                    Messages.Message("LC_VoidDream_WokeMsg".Translate(target.LabelShortCap, pawn.LabelShortCap), target, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };
            wake.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return wake;
        }
    }
}
