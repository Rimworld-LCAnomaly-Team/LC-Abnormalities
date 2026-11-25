using MeatLantern.Utility;
using RimWorld;
using UnityEngine;
using Verse;

namespace MeatLantern.Comp
{
    public class CompAbilityEffect_WhirlwindOmNomNom : CompAbilityEffect
    {
        public new CompProperties_AbilityWhirlwindOmNomNom Props => (CompProperties_AbilityWhirlwindOmNomNom)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            Pawn pawn = target.Pawn;
            if (caster != null && caster.Spawned && caster.MapHeld != null)
            {
                Vector3 anchorPos = pawn != null && pawn.Spawned ? pawn.DrawPos.Yto0() : target.CenterVector3.Yto0();
                float aimAngle = LCAnomalyCore.Render.Weapon.EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
                ThingWithComps weapon = caster.equipment?.Primary;
                if (weapon != null && weapon.def?.defName == MeatLantern.Effect.MeatLanternWeaponAnimHandler.WeaponDefName)
                {
                    LCAnomalyCore.Render.Weapon.EgoWeaponAnimationManager.Play(
                        caster,
                        weapon,
                        MeatLantern.Effect.MeatLanternWeaponAnimHandler.ClipSkillChomp,
                        72,
                        aimAngle);
                }

                if (MeatLantern.Def.ThingDefOf.LC_MeatLanternChompFX != null && caster.PositionHeld.InBounds(caster.MapHeld))
                {
                    IntVec3 spawnCell = pawn != null && pawn.Spawned ? pawn.PositionHeld : caster.PositionHeld;
                    if (GenSpawn.Spawn(MeatLantern.Def.ThingDefOf.LC_MeatLanternChompFX, spawnCell, caster.MapHeld) is MeatLantern.Effect.Thing_MeatLanternChompFX chompFx)
                    {
                        chompFx.Initialize(anchorPos, 2.75f, 30);
                    }
                }
            }

            if (pawn != null)
            {
                MeatLanternUtility.DoBiteOnPawn(pawn, parent.pawn, Props.damageRange, Props.armorPenetration, Props.suckPercent, Props.stunPercent);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead || pawn.Downed)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("LC_MeatLantern_TargetMustBeAlive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (parent?.pawn != null && pawn.Faction != null && pawn.Faction == parent.pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("LC_MeatLantern_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && parent?.pawn != null && target.Pawn.HostileTo(parent.pawn);
        }
    }
}