using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Hediffs;
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
    /// T-09-79 血肉偶像核心逻辑：
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

        public CompProperties_FleshIdol PropsFlesh => (CompProperties_FleshIdol)props;

        public int HealPulsesTriggered => healPulsesTriggered;

        public int DetonationsTriggered => detonationsTriggered;

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

        public override void OnContinuousStart(Pawn user)
        {
            base.OnContinuousStart(user);
            announcedMiracle = false;
            announcedWarning = false;
            explodedThisSession = false;
            lastHealPulseSecond = -999f;
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
            List<Pawn> candidates = map.mapPawns.FreeColonistsAndPrisonersSpawned
                .Where(p => p != null && !p.Dead && !p.IsPrisonerOfColony)
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
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
                }
            }

            if (anyHealed && IsOperational && ActivePosition.IsValid && map != null)
            {
                FleshIdolSoundDefOf.LC_FleshIdol_Heal?.PlayOneShot(new TargetInfo(ActivePosition, map));
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

        protected override Color GetHeldOverrideColor()
        {
            if (IsActiveContinuous)
            {
                switch (CurrentPhase)
                {
                    case 2:
                        return new Color(1.0f, 0.92f, 0.68f);
                    case 3:
                        return new Color(1.0f, 0.38f, 0.38f);
                    default:
                        return new Color(0.95f, 0.78f, 0.75f);
                }
            }

            return Color.white;
        }

        public override void DrawExtraSelectionOverlaysForPlatform(Building_AbnormalityHoldingPlatform platform)
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

        static FleshIdolSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FleshIdolSoundDefOf));
        }
    }
}
