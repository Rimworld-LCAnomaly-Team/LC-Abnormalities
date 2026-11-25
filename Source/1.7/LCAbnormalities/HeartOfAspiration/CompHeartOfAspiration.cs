using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.HeartOfAspiration
{
    [DefOf]
    public static class HeartOfAspirationDefOf
    {
        public static SoundDef LC_HeartOfAspiration_Beat;
        public static SoundDef LC_HeartOfAspiration_Equip;

        static HeartOfAspirationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HeartOfAspirationDefOf));
        }
    }

    public class CompProperties_HeartOfAspiration : CompProperties_AbnormalityTool
    {
        public int restlessSeconds = 45;
        public int berserkSeconds = 60;
        public int heartbeatIntervalSeconds = 5;

        public CompProperties_HeartOfAspiration()
        {
            compClass = typeof(CompHeartOfAspiration);
        }
    }

    /// <summary>
    /// T-09-77 渴望之心（可佩戴工具型）：
    /// 收容时在静滞容器内悬浮并按 Lub-Dub 心率跳动；佩戴后心率加倍并在佩戴者身周呈现活体心脏与猩红心律脉冲；
    /// 若佩戴后未造成有效战斗伤害即归还或空转超过 60 秒，将引发剧烈猩红狂乱反噬。
    /// </summary>
    public class CompHeartOfAspiration : CompAbnormalityTool
    {
        private int idleSeconds;
        private int beatSeconds;
        private bool hasDealtCombatDamage;
        private bool warnedRestless;

        // Transient visual fields
        private int lastCombatVfxTick = -9999;
        private int visualTickCounter;
        private MaterialPropertyBlock containerProps;
        private MaterialPropertyBlock heartProps;
        private MaterialPropertyBlock auraProps;

        public CompProperties_HeartOfAspiration PropsHeart => (CompProperties_HeartOfAspiration)props;

        public bool HasDealtCombatDamage => hasDealtCombatDamage;

        public int IdleSeconds => idleSeconds;

        /// <summary>返回躁动紧迫度（0=平静，45s~60s 线性攀升至 1）。</summary>
        public float RestlessFraction
        {
            get
            {
                if (hasDealtCombatDamage || !IsEquipped)
                {
                    return 0f;
                }

                int start = Mathf.Max(0, PropsHeart.restlessSeconds);
                int end = Mathf.Max(start + 1, PropsHeart.berserkSeconds);
                if (idleSeconds < start)
                {
                    return 0f;
                }

                return Mathf.Clamp01((idleSeconds - start) / (float)(end - start));
            }
        }

        public override void TickWhileHeld()
        {
            base.TickWhileHeld();
            TickVisualMaintenance();
        }

        public override void CompTick()
        {
            base.CompTick();
            TickVisualMaintenance();
        }

        private void TickVisualMaintenance()
        {
            visualTickCounter++;
            if (IsEquipped && ActiveEquipper != null && ActiveEquipper.Spawned && visualTickCounter % 25 == 0)
            {
                HeartOfAspirationVfxUtility.EnsureBearerAura(ActiveEquipper, parent);
            }
        }

        public override void OnEquipped(Pawn user)
        {
            base.OnEquipped(user);
            idleSeconds = 0;
            beatSeconds = 0;
            hasDealtCombatDamage = false;
            warnedRestless = false;
            LinkEquipHediff(user);

            if (user != null && user.Spawned && user.Map != null)
            {
                HeartOfAspirationDefOf.LC_HeartOfAspiration_Equip?.PlayOneShot(user);
                HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat?.PlayOneShot(user);
                HeartOfAspirationVfxUtility.EnsureBearerAura(user, parent);
                HeartOfAspirationVfxUtility.SpawnPulseWave(user.Map, user.DrawPos, radius: 2.1f, duration: 28, satisfied: false, restless: false);
            }

            if (user != null)
            {
                Messages.Message("LC_HeartOfAspiration_EquipMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void OnEquippableTick(Pawn user, int intervalTicks, int equippedTicks)
        {
            base.OnEquippableTick(user, intervalTicks, equippedTicks);
            if (user == null || user.Dead)
            {
                return;
            }

            LinkEquipHediff(user);
            if (user.Spawned)
            {
                HeartOfAspirationVfxUtility.EnsureBearerAura(user, parent);
            }

            int secStep = Mathf.Max(1, intervalTicks / 60);
            beatSeconds += secStep;
            bool isRestless = !hasDealtCombatDamage && idleSeconds >= PropsHeart.restlessSeconds;
            int targetBeatInterval = isRestless
                ? 2
                : Mathf.Max(1, PropsHeart.heartbeatIntervalSeconds);

            if (beatSeconds >= targetBeatInterval)
            {
                beatSeconds = 0;
                if (user.Spawned && user.Map != null)
                {
                    HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat?.PlayOneShot(user);
                    HeartOfAspirationVfxUtility.SpawnPulseWave(
                        user.Map,
                        user.DrawPos,
                        radius: isRestless ? 2.1f : 1.55f,
                        duration: isRestless ? 22 : 28,
                        satisfied: hasDealtCombatDamage,
                        restless: isRestless);
                }
            }

            if (hasDealtCombatDamage)
            {
                return;
            }

            idleSeconds += secStep;
            if (!warnedRestless && idleSeconds >= PropsHeart.restlessSeconds)
            {
                warnedRestless = true;
                if (user.Spawned && user.Map != null)
                {
                    HeartOfAspirationVfxUtility.SpawnPulseWave(user.Map, user.DrawPos, radius: 2.5f, duration: 28, satisfied: false, restless: true);
                }
                Messages.Message("LC_HeartOfAspiration_RestlessMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.CautionInput, historical: false);
            }

            if (idleSeconds >= PropsHeart.berserkSeconds)
            {
                TriggerAspirationBacklash(user, equippedTicks, "LC_HeartOfAspiration_BerserkMsg", 25f, 25f);
            }
        }

        public void NotifyCombatDamageDealt(Pawn attacker, Thing target, float dealt)
        {
            if (attacker != null && attacker == ActiveEquipper && dealt > 0f && target != attacker)
            {
                bool firstSatisfaction = !hasDealtCombatDamage;
                hasDealtCombatDamage = true;
                idleSeconds = 0;
                warnedRestless = false;

                int now = Find.TickManager?.TicksGame ?? 0;
                if (attacker.Spawned && attacker.Map != null && (firstSatisfaction || now - lastCombatVfxTick >= 18))
                {
                    lastCombatVfxTick = now;
                    HeartOfAspirationVfxUtility.SpawnPulseWave(
                        attacker.Map,
                        attacker.DrawPos,
                        radius: firstSatisfaction ? 2.4f : 1.6f,
                        duration: 24,
                        satisfied: true,
                        restless: false);
                    if (target != null && target.Spawned && target.Map == attacker.Map)
                    {
                        FleckMaker.ThrowDustPuffThick(target.DrawPos, attacker.Map, 0.85f, new Color(0.95f, 0.16f, 0.24f));
                    }
                }
            }
        }

        public override void OnUnequipped(Pawn user, int equippedTicks, bool returnedNormally)
        {
            bool satisfied = hasDealtCombatDamage;
            ResetTracking();

            if (returnedNormally)
            {
                if (!satisfied && user != null && !user.Dead)
                {
                    base.OnUnequipped(user, equippedTicks, returnedNormally: false);
                    ApplyBacklashHarm(user, "LC_HeartOfAspiration_PrematureReturnMsg", 18f, 22f);
                    return;
                }

                if (IsOperational && ActiveMap != null && ActivePosition.IsValid)
                {
                    HeartOfAspirationDefOf.LC_HeartOfAspiration_Equip?.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
                    HeartOfAspirationVfxUtility.SpawnPulseWave(ActiveMap, ActiveDrawPos, radius: 1.9f, duration: 28, satisfied: true, restless: false);
                }

                if (user != null)
                {
                    Messages.Message("LC_HeartOfAspiration_SafeReturnMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            }

            base.OnUnequipped(user, equippedTicks, returnedNormally);
        }

        public override void OnEquippableCarrierLost(Pawn lastUser, int equippedTicks)
        {
            ResetTracking();
            base.OnEquippableCarrierLost(lastUser, equippedTicks);
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
            Vector2 drawSize = bodyGraphic?.drawSize ?? new Vector2(1.8f, 1.8f);
            CompAbnormalityHoldingPlatformTarget targetComp = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (targetComp != null && targetComp.Props.heldDrawScale > 0f && Mathf.Abs(targetComp.Props.heldDrawScale - 1f) > 0.0001f)
            {
                drawSize *= targetComp.Props.heldDrawScale;
            }

            float baseY = HeldPlatform != null ? HeldPlatform.HeldPawnDrawPos_Y : drawLoc.y;
            float ageSecs = ((Find.TickManager?.TicksGame ?? 0) + (parent?.thingIDNumber ?? 0) * 19) / 60f;
            bool equipped = IsEquipped;

            // 1. Stasis container brackets (KitCreatureContainer.png from Lobotomy Corp)
            if (HeartOfAspirationMaterials.ContainerMat != null)
            {
                containerProps ??= new MaterialPropertyBlock();
                Color contTint = equipped
                    ? new Color(0.72f, 0.80f, 0.90f, 0.86f)
                    : Color.white;
                containerProps.SetColor(HeartOfAspirationMaterials.PropColor, contTint);

                float contSize = drawSize.y * 1.12f;
                Matrix4x4 contMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x, baseY, drawLoc.z + 0.02f),
                    Quaternion.identity,
                    new Vector3(contSize, 1f, contSize));
                Graphics.DrawMesh(MeshPool.plane10, contMat, HeartOfAspirationMaterials.ContainerMat, 0, null, 0, containerProps);
            }

            // If equipped by a colonist, show empty stasis pedestal with faint blue idle shimmer
            if (equipped)
            {
                if (HeartOfAspirationMaterials.PulseAuraMat != null)
                {
                    float idleFlicker = 0.24f + 0.12f * Mathf.Sin(ageSecs * 2.8f);
                    auraProps ??= new MaterialPropertyBlock();
                    auraProps.SetColor(HeartOfAspirationMaterials.PropColor, new Color(0.22f, 0.72f, 1.0f, 0.55f));
                    auraProps.SetColor(HeartOfAspirationMaterials.PropCoreColor, new Color(0.76f, 0.94f, 1.0f, 0.70f));
                    auraProps.SetFloat(HeartOfAspirationMaterials.PropProgress, 0.25f);
                    auraProps.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageSecs);
                    auraProps.SetFloat(HeartOfAspirationMaterials.PropAlpha, idleFlicker);
                    auraProps.SetFloat(HeartOfAspirationMaterials.PropIntensity, 0.75f);
                    auraProps.SetFloat(HeartOfAspirationMaterials.PropMode, 0.0f);

                    float beamSz = drawSize.y * 0.72f;
                    Matrix4x4 bMat = Matrix4x4.TRS(
                        new Vector3(drawLoc.x, baseY + 0.002f, drawLoc.z + 0.02f),
                        Quaternion.identity,
                        new Vector3(beamSz, 1f, beamSz));
                    Graphics.DrawMesh(MeshPool.plane10, bMat, HeartOfAspirationMaterials.PulseAuraMat, 0, null, 0, auraProps);
                }

                return true;
            }

            // 2. Contained Heart of Aspiration: gentle floating + Lub-Dub rhythmic heartbeat pulse
            float beat = HeartOfAspirationMaterials.EvaluateLubDub(ageSecs, beatsPerSecond: 0.95f);
            float floatZ = 0.045f * Mathf.Sin(ageSecs * 2.1f);
            float heartSize = drawSize.y * 0.62f * (1f + 0.13f * beat);

            // Soft crimson pulse aura inside the stasis chamber
            if (HeartOfAspirationMaterials.PulseAuraMat != null)
            {
                auraProps ??= new MaterialPropertyBlock();
                auraProps.SetColor(HeartOfAspirationMaterials.PropColor, new Color(0.96f, 0.16f, 0.26f, 0.80f));
                auraProps.SetColor(HeartOfAspirationMaterials.PropCoreColor, new Color(1.0f, 0.80f, 0.82f, 0.90f));
                auraProps.SetFloat(HeartOfAspirationMaterials.PropProgress, beat);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageSecs);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropAlpha, 0.42f + 0.36f * beat);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropIntensity, 1.05f + 0.35f * beat);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropMode, 0.0f);

                float glowSz = drawSize.y * (0.78f + 0.16f * beat);
                Matrix4x4 gMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x, baseY + 0.003f, drawLoc.z + 0.02f + floatZ),
                    Quaternion.identity,
                    new Vector3(glowSz, 1f, glowSz));
                Graphics.DrawMesh(MeshPool.plane10, gMat, HeartOfAspirationMaterials.PulseAuraMat, 0, null, 0, auraProps);
            }

            if (HeartOfAspirationMaterials.HeartCoreMat != null)
            {
                heartProps ??= new MaterialPropertyBlock();
                heartProps.SetColor(HeartOfAspirationMaterials.PropColor, Color.white);
                heartProps.SetColor(HeartOfAspirationMaterials.PropGlowColor, new Color(1.0f, 0.18f, 0.24f, 1.0f));
                heartProps.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageSecs);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropBeatAmt, beat);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropRestlessAmt, 0.0f);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropSatisfiedAmt, 0.0f);

                Matrix4x4 hMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x, baseY + 0.006f, drawLoc.z + 0.02f + floatZ),
                    Quaternion.AngleAxis(Mathf.Sin(ageSecs * 1.4f) * 3.5f, Vector3.up),
                    new Vector3(heartSize, 1f, heartSize));
                Graphics.DrawMesh(MeshPool.plane10, hMat, HeartOfAspirationMaterials.HeartCoreMat, 0, null, 0, heartProps);
            }

            return true;
        }

        public override string GetToolStatusInspectString()
        {
            if (!IsEquipped || ActiveEquipper == null)
            {
                return null;
            }

            if (hasDealtCombatDamage)
            {
                return "LC_HeartOfAspiration_StatusSatisfied".Translate();
            }

            return "LC_HeartOfAspiration_StatusRestless".Translate(idleSeconds, PropsHeart.berserkSeconds);
        }

        private void TriggerAspirationBacklash(Pawn user, int equippedTicks, string messageKey, float redDmg, float whiteDmg)
        {
            ResetTracking();
            base.OnUnequipped(user, equippedTicks, returnedNormally: false);
            ApplyBacklashHarm(user, messageKey, redDmg, whiteDmg);
        }

        private void ApplyBacklashHarm(Pawn user, string messageKey, float redDmg, float whiteDmg)
        {
            if (user == null || user.Dead)
            {
                return;
            }

            if (user.Spawned && user.Map != null)
            {
                HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat?.PlayOneShot(user);
                HeartOfAspirationVfxUtility.SpawnFrenzyBurst(user.Map, user.DrawPos);
            }

            user.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, redDmg, instigator: parent));
            if (!user.Dead)
            {
                user.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, whiteDmg, instigator: parent));
            }
            if (!user.Dead && MentalStateDefOf.Berserk != null)
            {
                user.mindState?.mentalStateHandler?.TryStartMentalState(
                    MentalStateDefOf.Berserk,
                    "LC_HeartOfAspiration_MentalReason".Translate(),
                    forced: true);
            }

            Messages.Message(messageKey.Translate(user.LabelShortCap), user, MessageTypeDefOf.ThreatBig, historical: false);
        }

        private void LinkEquipHediff(Pawn user)
        {
            if (user == null || Props.equipHediffDef == null || user.health?.hediffSet == null)
            {
                return;
            }

            Hediff h = user.health.hediffSet.GetFirstHediffOfDef(Props.equipHediffDef);
            HediffComp_HeartOfAspiration comp = h?.TryGetComp<HediffComp_HeartOfAspiration>();
            if (comp != null && comp.toolThing != parent)
            {
                comp.toolThing = parent;
            }
        }

        private void ResetTracking()
        {
            idleSeconds = 0;
            beatSeconds = 0;
            hasDealtCombatDamage = false;
            warnedRestless = false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref idleSeconds, "heartIdleSeconds", 0);
            Scribe_Values.Look(ref beatSeconds, "heartBeatSeconds", 0);
            Scribe_Values.Look(ref hasDealtCombatDamage, "heartDealtCombatDamage", false);
            Scribe_Values.Look(ref warnedRestless, "heartWarnedRestless", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && ActiveEquipper != null)
            {
                LinkEquipHediff(ActiveEquipper);
            }
        }
    }

    public class HediffCompProperties_HeartOfAspiration : HediffCompProperties
    {
        public HediffCompProperties_HeartOfAspiration()
        {
            compClass = typeof(HediffComp_HeartOfAspiration);
        }
    }

    public class HediffComp_HeartOfAspiration : HediffComp, ILCPawnCombatDamageListener
    {
        public Thing toolThing;

        public void OnDealtDamage(Pawn attacker, Thing target, DamageInfo dinfo, float dealt)
        {
            if (attacker != Pawn || dealt <= 0f)
            {
                return;
            }

            (toolThing as ThingWithComps)?.TryGetComp<CompHeartOfAspiration>()?.NotifyCombatDamageDealt(attacker, target, dealt);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref toolThing, "heartToolThing");
        }
    }
}
