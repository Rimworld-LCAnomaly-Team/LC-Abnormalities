using LCAbnormalities.Shared;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Util;
using NothingThere.Effect;
using NothingThere.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NothingThere.Combat
{
    /// <summary>
    /// 拟态（Mimicry）E.G.O 武器四色红伤、10% 概率特殊重劈与 25% 命中吸血伤害处理器。
    /// </summary>
    public class DamageWorker_MimicryLifesteal : DamageWorker_FourColor
    {
        private const float LifestealRatio = 0.25f;
        private const float SpecialCleaveChance = 0.10f;

        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Map victimMap = victim?.MapHeld;
            IntVec3 victimCell = victim?.PositionHeld ?? IntVec3.Invalid;
            Vector3 vicPos = victim != null
                ? (victim.Spawned ? victim.DrawPos.Yto0() : victimCell.ToVector3Shifted().Yto0())
                : Vector3.zero;

            bool triggeredSpecialCleave = false;
            if (dinfo.Def == Def.DamageDefOf.LC_Mimicry_RedLifesteal && Rand.Chance(SpecialCleaveChance))
            {
                triggeredSpecialCleave = true;
                dinfo.SetAmount(Rand.Range(45f, 85f));
                if (victimMap != null && victimCell.InBounds(victimMap))
                {
                    AbnormalityUtility.Burst(victimCell, victimMap, 1.0f);
                    Vector3 textPos = vicPos + new Vector3(0f, 0f, 0.4f);
                    if (textPos.ShouldSpawnMotesAt(victimMap))
                    {
                        MoteMaker.ThrowText(textPos, victimMap, "LC_NothingThere_Goodbye".Translate(), new Color(0.95f, 0.15f, 0.15f), 2.0f);
                    }
                }
            }

            DamageResult result = base.Apply(dinfo, victim);

            if (dinfo.Instigator is Pawn wielder && !wielder.Dead && wielder.Spawned &&
                victim != null && victimMap != null && victimCell.InBounds(victimMap) &&
                dinfo.Weapon != null && dinfo.Weapon.defName == MimicryWeaponAnimHandler.WeaponDefName)
            {
                Vector3 attPos = wielder.DrawPos.Yto0();
                Vector3 dir = (vicPos - attPos).Yto0();
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }
                dir.Normalize();

                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(wielder, vicPos);

                ThingWithComps primary = wielder.equipment?.Primary;
                if (primary != null && primary.def?.defName == MimicryWeaponAnimHandler.WeaponDefName)
                {
                    if (triggeredSpecialCleave)
                    {
                        EgoWeaponAnimationManager.Play(wielder, primary, MimicryWeaponAnimHandler.ClipGoodbyeSlam, 78, aimAngle);
                    }
                    else if (!MimicryWeaponAnimHandler.IsLockedInGoodbyeSlam(wielder))
                    {
                        EgoWeaponAnimationManager.Play(wielder, primary, MimicryWeaponAnimHandler.ClipNormalCleave, 54, aimAngle);
                    }
                }

                MimicryVfxUtility.SpawnSlash(
                    victimMap,
                    Vector3.Lerp(attPos, vicPos, 0.55f),
                    yaw,
                    scale: triggeredSpecialCleave ? 2.65f : 2.15f,
                    flipSweep: Rand.Bool,
                    heavy: triggeredSpecialCleave,
                    lifeTicks: triggeredSpecialCleave ? 20 : 16,
                    spanRad: triggeredSpecialCleave ? 3.0f : 2.65f);

                SoundDef sfx = triggeredSpecialCleave ? Def.SoundDefOf.NothingThere_GoodbyeFinish : Def.SoundDefOf.NothingThere_Attack;
                sfx?.PlayOneShot(new TargetInfo(victimCell, victimMap));

                if (triggeredSpecialCleave)
                {
                    MimicryVfxUtility.SpawnGroundRupture(victimMap, attPos + dir * 0.80f, yaw, scale: 3.0f, lifeTicks: 24);
                }
            }

            if (result.totalDamageDealt > 0f && dinfo.Instigator is Pawn healWielder && !healWielder.Dead &&
                victim is Pawn targetPawn && targetPawn.def?.race?.IsMechanoid != true)
            {
                EffectUtil.HealPawnInjuries(healWielder, result.totalDamageDealt * LifestealRatio, showFeedback: true);
            }

            return result;
        }
    }
}
