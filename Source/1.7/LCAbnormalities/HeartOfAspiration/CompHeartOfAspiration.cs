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
    /// T-09-77 渴望之心（可佩戴工具型）：大幅强化攻速与移速，但若佩戴后未造成有效战斗伤害即归还或空转超过 60 秒，将引发狂暴反噬。
    /// </summary>
    public class CompHeartOfAspiration : CompAbnormalityTool
    {
        private int idleSeconds;
        private int beatSeconds;
        private bool hasDealtCombatDamage;
        private bool warnedRestless;

        public CompProperties_HeartOfAspiration PropsHeart => (CompProperties_HeartOfAspiration)props;

        public bool HasDealtCombatDamage => hasDealtCombatDamage;

        public int IdleSeconds => idleSeconds;

        public override void OnEquipped(Pawn user)
        {
            base.OnEquipped(user);
            idleSeconds = 0;
            beatSeconds = 0;
            hasDealtCombatDamage = false;
            warnedRestless = false;
            LinkEquipHediff(user);

            if (HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat != null && user != null && user.Spawned)
            {
                HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat.PlayOneShot(user);
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

            int secStep = Mathf.Max(1, intervalTicks / 60);
            beatSeconds += secStep;
            if (beatSeconds >= Mathf.Max(1, PropsHeart.heartbeatIntervalSeconds))
            {
                beatSeconds = 0;
                if (HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat != null && user.Spawned)
                {
                    HeartOfAspirationDefOf.LC_HeartOfAspiration_Beat.PlayOneShot(user);
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
                hasDealtCombatDamage = true;
                idleSeconds = 0;
                warnedRestless = false;
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
