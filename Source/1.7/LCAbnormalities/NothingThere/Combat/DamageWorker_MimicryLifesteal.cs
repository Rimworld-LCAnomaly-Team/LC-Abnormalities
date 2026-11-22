using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Util;
using NothingThere.Util;
using RimWorld;
using UnityEngine;
using Verse;

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
            if (dinfo.Def == Def.DamageDefOf.LC_Mimicry_RedLifesteal && Rand.Chance(SpecialCleaveChance))
            {
                dinfo.SetAmount(Rand.Range(45f, 85f));
                if (victim?.MapHeld != null && victim.PositionHeld.InBounds(victim.MapHeld))
                {
                    AbnormalityUtility.Burst(victim.PositionHeld, victim.MapHeld, 1.0f);
                    if (victim.DrawPos.ShouldSpawnMotesAt(victim.MapHeld))
                    {
                        MoteMaker.ThrowText(victim.DrawPos + new Vector3(0f, 0f, 0.4f), victim.MapHeld, "LC_NothingThere_Goodbye".Translate(), new Color(0.95f, 0.15f, 0.15f), 2.0f);
                    }
                }
            }

            DamageResult result = base.Apply(dinfo, victim);
            if (result.totalDamageDealt <= 0f)
            {
                return result;
            }

            if (dinfo.Instigator is Pawn wielder && !wielder.Dead && victim is Pawn targetPawn && targetPawn.def?.race?.IsMechanoid != true)
            {
                EffectUtil.HealPawnInjuries(wielder, result.totalDamageDealt * LifestealRatio, showFeedback: true);
            }

            return result;
        }
    }
}
