using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Hediffs;
using LCAnomalyCore.Interface;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.FleshIdol
{
    /// <summary>T-09-79 血肉偶像组件属性。</summary>
    public class CompProperties_FleshIdol : CompProperties_AbnormalityTool
    {
        public int minSafeSeconds = 20;
        public int maxSafeSeconds = 80;
        public int explosionSeconds = 90;
        public int healPulseIntervalSeconds = 3;
        public float healAmountPerPulse = 4.5f;
        public float mentalHealPerPulse = 0.18f;
        public float earlyStopBlackDamage = 45f;
        public float earlyStopWhiteDamage = 30f;
        public float explosionMapWhiteDamage = 35f;
        public float explosionMapBlackDamage = 20f;
        public float explosionUserWhiteDamage = 120f;
        public float explosionUserBlackDamage = 80f;
        public int postExplosionCooldownTicks = 15000;

        public CompProperties_FleshIdol()
        {
            compClass = typeof(CompFleshIdol);
        }
    }

    /// <summary>
    /// T-09-79 血肉偶像核心逻辑与活体蠕动/神迹/灾变视觉演出：
    /// 持续祈祷满 20 秒且在 80 秒内停止可为全殖民地周期性回复生命与精神值；
    /// 未满 20 秒提前打断会遭受信仰反噬；超过 90 秒则引发全图惨烈爆炸并进入长冷却。
    /// </summary>
    public class CompFleshIdol : CompAbnormalityTool
    {
        private int healPulsesTriggered;
        private int detonationsTriggered;
        private int explosionCooldownUntilTick = -999999;

        private bool announcedMiracle;
        private bool announcedWarning;
        private bool explodedThisSession;
        private float lastHealPulseSecond = -999f;

        // Transient animation timers
        private int healPulseFlashTicks;
        private int visualTickCounter;
        private MaterialPropertyBlock crossProps;
        private MaterialPropertyBlock backGlowProps;

        public CompProperties_FleshIdol PropsFlesh => (CompProperties_FleshIdol)props;

        public int HealPulsesTriggered => healPulsesTriggered;

        public int DetonationsTriggered => detonationsTriggered;

        public float HealPulseFlash => Mathf.Clamp01(healPulseFlashTicks / 28f);

        public int ExplosionCooldownRemaining
        {
            get
            {
                if (Find.TickManager == null)
                {
                    return 0;
                }

                return Mathf.Max(0, explosionCooldownUntilTick - Find.TickManager.TicksGame);
            }
        }

        /// <summary>当前祈祷阶段：0=空闲，1=蓄能未满(0-19s)，2=神迹涌现(20-80s)，3=过载危险(>80s)。</summary>
        public int CurrentPhase
        {
            get
            {
                if (!IsActiveContinuous)
                {
                    return 0;
                }

                float sec = CurrentSessionSeconds;
                if (sec > PropsFlesh.maxSafeSeconds)
                {
                    return 3;
                }

                if (sec >= PropsFlesh.minSafeSeconds)
                {
                    return 2;
                }

                return 1;
            }
        }

        public override AcceptanceReport CanUseTool(Pawn pawn)
        {
            if (ExplosionCooldownRemaining > 0 && activeContinuousUser != pawn)
            {
                return "LC_Tool_OnCooldown".Translate(ExplosionCooldownRemaining.ToStringTicksToPeriod());
            }

            return base.CanUseTool(pawn);
        }

        public override void TickWhileHeld()
        {
            base.TickWhileHeld();
            TickVisualEffects();
        }

        public override void CompTick()
        {
            base.CompTick();
            TickVisualEffects();
        }

        private void TickVisualEffects()
        {
            visualTickCounter++;
            if (healPulseFlashTicks > 0)
            {
                healPulseFlashTicks--;
            }

            Map map = ActiveMap;
            if (map == null || !ActivePosition.IsValid)
            {
                return;
            }

            if (IsActiveContinuous && visualTickCounter % 20 == 0)
            {
                FleshIdolVfxUtility.EnsurePrayerAura(parent, map, ActivePosition);
            }
            else if (ExplosionCooldownRemaining > 0 && visualTickCounter % 45 == 0)
            {
                // Faint charred smoke wisps rising during post-explosion dormancy
                FleckMaker.ThrowSmoke(ActiveDrawPos + new Vector3(0f, 0f, 0.35f), map, 0.65f);
            }
        }

        public override void OnContinuousStart(Pawn user)
        {
            base.OnContinuousStart(user);
            announcedMiracle = false;
            announcedWarning = false;
            explodedThisSession = false;
            lastHealPulseSecond = -999f;

            Map map = ActiveMap;
            if (IsOperational && map != null && ActivePosition.IsValid)
            {
                FleshIdolVfxUtility.EnsurePrayerAura(parent, map, ActivePosition);
            }
        }

        public override void OnContinuousTick(Pawn user, int intervalTicks, int sessionTicks)
        {
            base.OnContinuousTick(user, intervalTicks, sessionTicks);

            Map map = ActiveMap;
            if (explodedThisSession || map == null)
            {
                return;
            }

            Thing focusTarget = InteractionTarget ?? parent;
            float sec = sessionTicks / 60f;
            if (sec >= PropsFlesh.explosionSeconds)
            {
                TriggerDetonation(user);
                return;
            }

            if (sec > PropsFlesh.maxSafeSeconds)
            {
                if (!announcedWarning)
                {
                    announcedWarning = true;
                    Messages.Message("LC_FleshIdol_OverloadWarning".Translate(parent.LabelCap), focusTarget, MessageTypeDefOf.ThreatSmall);
                }

                if (user != null && !user.Dead && !user.Destroyed)
                {
                    user.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                        6f,
                        armorPenetration: 1f,
                        instigator: parent));
                }
                return;
            }

            if (sec >= PropsFlesh.minSafeSeconds)
            {
                if (!announcedMiracle)
                {
                    announcedMiracle = true;
                    if (IsOperational && ActivePosition.IsValid)
                    {
                        FleshIdolSoundDefOf.LC_FleshIdol_Choir?.PlayOneShot(new TargetInfo(ActivePosition, map));
                    }
                    Messages.Message("LC_FleshIdol_MiracleStarted".Translate(parent.LabelCap), focusTarget, MessageTypeDefOf.PositiveEvent);
                }

                if (lastHealPulseSecond < 0f || (sec - lastHealPulseSecond) >= PropsFlesh.healPulseIntervalSeconds - 0.01f)
                {
                    lastHealPulseSecond = sec;
                    PulseColonyHeal(map);
                }
            }
        }

        public override void OnContinuousEnd(Pawn user, int sessionTicks, bool completedNaturally)
        {
            base.OnContinuousEnd(user, sessionTicks, completedNaturally);

            Thing focusTarget = InteractionTarget ?? parent;
            float sec = sessionTicks / 60f;
            if (!explodedThisSession
                && sessionTicks > 0
                && sec < PropsFlesh.minSafeSeconds
                && user != null
                && !user.Dead
                && !user.Destroyed)
            {
                Map map = ActiveMap ?? user.MapHeld;
                if (map != null)
                {
                    FleshIdolVfxUtility.SpawnRetributionBurst(map, user.Spawned ? user.DrawPos : ActiveDrawPos, catastrophic: false);
                }

                user.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                    PropsFlesh.earlyStopBlackDamage,
                    armorPenetration: 1.5f,
                    instigator: parent));

                if (!user.Dead && !user.Destroyed)
                {
                    user.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                        PropsFlesh.earlyStopWhiteDamage,
                        armorPenetration: 1.5f,
                        instigator: parent));
                }

                Messages.Message(
                    "LC_FleshIdol_EarlyStopPunish".Translate(user.LabelShortCap),
                    new LookTargets(user),
                    MessageTypeDefOf.NegativeEvent);
            }
            else if (!explodedThisSession && sec >= PropsFlesh.minSafeSeconds && sec <= PropsFlesh.maxSafeSeconds)
            {
                Messages.Message(
                    "LC_FleshIdol_MiracleEnded".Translate(parent.LabelCap),
                    focusTarget,
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        private void PulseColonyHeal(Map map)
        {
            if (map == null)
            {
                return;
            }

            bool anyHealed = false;
            int motesSpawned = 0;
            List<Pawn> candidates = map.mapPawns.FreeColonistsAndPrisonersSpawned
                .Where(p => p != null && !p.Dead && !p.IsPrisonerOfColony)
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                bool targetHealed = false;
                int healedInjuries = 0;
                List<Hediff> hediffs = target.health?.hediffSet?.hediffs;
                if (hediffs != null)
                {
                    for (int h = hediffs.Count - 1; h >= 0 && healedInjuries < 2; h--)
                    {
                        if (hediffs[h] is Hediff_Injury injury && !injury.IsPermanent())
                        {
                            injury.Heal(PropsFlesh.healAmountPerPulse);
                            healedInjuries++;
                            anyHealed = true;
                            targetHealed = true;
                        }
                    }
                }

                if (target.health?.hediffSet?.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage) is Hediff_LCMentalDamage mental)
                {
                    mental.Severity -= PropsFlesh.mentalHealPerPulse;
                    if (mental.Severity <= 0.0001f)
                    {
                        target.health.RemoveHediff(mental);
                        if (target.InMentalState && target.MentalState != null)
                        {
                            target.MentalState.RecoverFromState();
                        }
                    }
                    anyHealed = true;
                    targetHealed = true;
                }

                if (targetHealed && motesSpawned < 16)
                {
                    FleshIdolVfxUtility.SpawnHealMote(target);
                    motesSpawned++;
                }
            }

            healPulseFlashTicks = 28;
            if (IsOperational && ActivePosition.IsValid && map != null)
            {
                FleshIdolVfxUtility.SpawnMiracleWave(map, ActiveDrawPos + new Vector3(0f, 0f, 0.28f), radius: 8.0f, duration: 50);
                if (anyHealed)
                {
                    FleshIdolSoundDefOf.LC_FleshIdol_Heal?.PlayOneShot(new TargetInfo(ActivePosition, map));
                }
            }

            healPulsesTriggered++;
        }

        private void TriggerDetonation(Pawn user)
        {
            explodedThisSession = true;
            detonationsTriggered++;

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            explosionCooldownUntilTick = currentTick + Mathf.Max(0, PropsFlesh.postExplosionCooldownTicks);

            Map map = ActiveMap;
            IntVec3 pos = ActivePosition;
            if (IsOperational && map != null && pos.IsValid)
            {
                FleshIdolSoundDefOf.LC_FleshIdol_Explode?.PlayOneShot(new TargetInfo(pos, map));
                FleshIdolVfxUtility.SpawnRetributionBurst(map, ActiveDrawPos, catastrophic: true);
                if (RimWorld.ThingDefOf.Filth_Blood != null)
                {
                    FilthMaker.TryMakeFilth(pos, map, RimWorld.ThingDefOf.Filth_Blood, count: 3);
                }
            }

            if (user != null && !user.Dead && !user.Destroyed)
            {
                user.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                    PropsFlesh.explosionUserWhiteDamage,
                    armorPenetration: 2f,
                    instigator: parent));
                if (!user.Dead && !user.Destroyed)
                {
                    user.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                        PropsFlesh.explosionUserBlackDamage,
                        armorPenetration: 2f,
                        instigator: parent));
                }
            }

            if (map != null)
            {
                List<Pawn> victims = map.mapPawns.AllPawnsSpawned
                    .Where(p => p != null && p != user && !p.Dead && !p.Destroyed && p.RaceProps.Humanlike)
                    .ToList();

                for (int i = 0; i < victims.Count; i++)
                {
                    Pawn v = victims[i];
                    v.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                        PropsFlesh.explosionMapWhiteDamage,
                        armorPenetration: 1.5f,
                        instigator: parent));
                    if (!v.Dead && !v.Destroyed)
                    {
                        v.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                            PropsFlesh.explosionMapBlackDamage,
                            armorPenetration: 1.5f,
                            instigator: parent));
                    }
                }
            }

            Find.LetterStack?.ReceiveLetter(
                "LC_FleshIdol_ExplosionLetterLabel".Translate(),
                "LC_FleshIdol_ExplosionLetterDesc".Translate(parent.LabelCap),
                LetterDefOf.ThreatBig,
                InteractionTarget ?? parent);

            RequestStopContinuous();
        }

        public override bool TryDrawHeldOverride(DrawPhase phase, Vector3 drawLoc)
        {
            if (phase != DrawPhase.Draw)
            {
                return true;
            }

            Pawn pawn = parent as Pawn;
            List<PawnKindLifeStage> lifeStages = pawn?.kindDef?.lifeStages;
            GraphicData bodyGraphic = (lifeStages != null && lifeStages.Count > 0) ? lifeStages[0].bodyGraphicData : null;
            Vector2 drawSize = bodyGraphic?.drawSize ?? new Vector2(2.2f, 2.2f);
            CompAbnormalityHoldingPlatformTarget targetComp = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (targetComp != null && targetComp.Props.heldDrawScale > 0f && Mathf.Abs(targetComp.Props.heldDrawScale - 1f) > 0.0001f)
            {
                drawSize *= targetComp.Props.heldDrawScale;
            }

            float baseY = drawLoc.y;
            float ageSecs = ((Find.TickManager?.TicksGame ?? 0) + (parent?.thingIDNumber ?? 0) * 13) / 60f;
            int curPhase = CurrentPhase;
            bool charred = ExplosionCooldownRemaining > 0 && !IsActiveContinuous;
            float shaderPhase = charred ? 4.0f : (float)curPhase;
            float pulseFlash = HealPulseFlash;

            // Subtle living scale pulse & violent overload seismic tremble
            Vector3 offset = Vector3.zero;
            float scalePulse = 1f;
            if (curPhase == 2)
            {
                scalePulse = 1f + 0.032f * pulseFlash + 0.012f * Mathf.Sin(ageSecs * 4.5f);
            }
            else if (curPhase == 3)
            {
                offset.x = Mathf.Sin(ageSecs * 58.0f) * 0.032f;
                offset.z = Mathf.Cos(ageSecs * 67.0f) * 0.020f;
                scalePulse = 1f + 0.035f * Mathf.Abs(Mathf.Sin(ageSecs * 12.0f));
            }
            else if (curPhase == 1)
            {
                scalePulse = 1f + 0.014f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(ageSecs * 3.8f)), 2f);
            }

            // Optional radiant divine/crimson back-glow behind the cross junction during Miracle or Overload
            if ((curPhase == 2 || curPhase == 3) && FleshIdolMaterials.MiracleWaveMat != null)
            {
                backGlowProps ??= new MaterialPropertyBlock();
                Color backCol = curPhase == 2
                    ? new Color(1.0f, 0.86f, 0.44f, 0.72f)
                    : new Color(0.98f, 0.14f, 0.14f, 0.85f);
                Color backCore = curPhase == 2
                    ? new Color(1.0f, 0.99f, 0.90f, 0.90f)
                    : new Color(1.0f, 0.76f, 0.72f, 0.95f);

                backGlowProps.SetColor(FleshIdolMaterials.PropColor, backCol);
                backGlowProps.SetColor(FleshIdolMaterials.PropCoreColor, backCore);
                backGlowProps.SetFloat(FleshIdolMaterials.PropProgress, curPhase == 3 ? 1.0f : 0.0f);
                backGlowProps.SetFloat(FleshIdolMaterials.PropAgeSecs, ageSecs);
                backGlowProps.SetFloat(FleshIdolMaterials.PropAlpha, curPhase == 2 ? (0.55f + 0.35f * pulseFlash) : 0.82f);
                backGlowProps.SetFloat(FleshIdolMaterials.PropIntensity, curPhase == 2 ? (1.15f + 0.45f * pulseFlash) : 1.45f);
                backGlowProps.SetFloat(FleshIdolMaterials.PropMode, 1.0f);

                float glowSize = drawSize.y * (curPhase == 2 ? (1.05f + 0.18f * pulseFlash) : 1.18f);
                Matrix4x4 gMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x + offset.x, baseY - 0.004f, drawLoc.z + 0.34f + offset.z),
                    Quaternion.identity,
                    new Vector3(glowSize, 1f, glowSize));
                Graphics.DrawMesh(MeshPool.plane10, gMat, FleshIdolMaterials.MiracleWaveMat, 0, null, 0, backGlowProps);
            }

            if (FleshIdolMaterials.CrossMat != null)
            {
                crossProps ??= new MaterialPropertyBlock();
                crossProps.SetColor(FleshIdolMaterials.PropColor, Color.white);
                crossProps.SetColor(FleshIdolMaterials.PropGlowColor, curPhase == 2
                    ? new Color(1.0f, 0.88f, 0.46f, 1.0f)
                    : new Color(1.0f, 0.24f, 0.22f, 1.0f));
                crossProps.SetFloat(FleshIdolMaterials.PropAgeSecs, ageSecs);
                crossProps.SetFloat(FleshIdolMaterials.PropPhase, shaderPhase);
                crossProps.SetFloat(FleshIdolMaterials.PropPulseAmt, pulseFlash);

                Matrix4x4 cMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x + offset.x, baseY, drawLoc.z + offset.z),
                    Quaternion.identity,
                    new Vector3(drawSize.x * scalePulse, 1f, drawSize.y * scalePulse));
                Graphics.DrawMesh(MeshPool.plane10, cMat, FleshIdolMaterials.CrossMat, 0, null, 0, crossProps);
                return true;
            }

            return false;
        }

        public override void DrawExtraSelectionOverlaysForUnit(IAbnormalityContainmentUnit unit)
        {
            Map map = ActiveMap;
            if (!IsActiveContinuous || map == null)
            {
                return;
            }

            Pawn user = ActiveContinuousUser;
            if (user != null && user.Spawned && user.Map == map)
            {
                SimpleColor lineColor = CurrentPhase == 2
                    ? SimpleColor.Yellow
                    : (CurrentPhase == 3 ? SimpleColor.Red : SimpleColor.White);
                GenDraw.DrawLineBetween(ActiveDrawPos, user.DrawPos, lineColor);
            }
        }

        public override string GetToolStatusInspectString()
        {
            if (ExplosionCooldownRemaining > 0)
            {
                return "LC_FleshIdol_InspectCooldown".Translate(ExplosionCooldownRemaining.ToStringTicksToPeriod());
            }

            switch (CurrentPhase)
            {
                case 1:
                    return "LC_FleshIdol_PhaseWarmup".Translate(CurrentSessionSeconds.ToString("0.#"), PropsFlesh.minSafeSeconds);
                case 2:
                    return "LC_FleshIdol_PhaseMiracle".Translate(CurrentSessionSeconds.ToString("0.#"), PropsFlesh.maxSafeSeconds);
                case 3:
                    return "LC_FleshIdol_PhaseOverload".Translate(CurrentSessionSeconds.ToString("0.#"), PropsFlesh.explosionSeconds);
                default:
                    return "LC_FleshIdol_PhaseIdle".Translate(healPulsesTriggered);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref healPulsesTriggered, "fleshIdolHealPulses", 0);
            Scribe_Values.Look(ref detonationsTriggered, "fleshIdolDetonations", 0);
            Scribe_Values.Look(ref explosionCooldownUntilTick, "fleshIdolCooldownUntilTick", -999999);
        }
    }

    [DefOf]
    public static class FleshIdolSoundDefOf
    {
        public static SoundDef LC_FleshIdol_Heal;
        public static SoundDef LC_FleshIdol_Explode;
        public static SoundDef LC_FleshIdol_Choir;

        static FleshIdolSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FleshIdolSoundDefOf));
        }
    }
}
