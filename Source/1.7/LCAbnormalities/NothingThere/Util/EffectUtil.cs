using System.Collections.Generic;
using System.Text;
using MeatLantern.Setting;
using NothingThere.Effect;
using RimWorld;
using UnityEngine;
using Verse;

namespace NothingThere.Util
{
    public static class EffectUtil
    {
        /// <summary>一带死亡效应与收容蛋生成，并发送镇压提示。</summary>
        public static void OnNothingThereDeath(Pawn pawn, Map map)
        {
            Map targetMap = map ?? pawn?.MapHeld;
            IntVec3 targetPos = pawn?.PositionHeld ?? IntVec3.Invalid;
            if (pawn == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                Log.Warning($"Unable to create Nothing There death effect for {pawn}: no valid map position.");
                return;
            }

            Find.LetterStack.ReceiveLetter(
                "LC_NothingThere_KilledLabel".Translate(),
                "LC_NothingThere_KilledText".Translate(),
                LetterDefOf.PositiveEvent,
                new LookTargets(targetPos, targetMap));

            if (GenSpawn.Spawn(Def.ThingDefOf.DyingNothingThere, targetPos, targetMap) is DyingNothingThere dying)
            {
                dying.InitWith(pawn);
            }
        }

        /// <summary>恢复全部缺失部位并治愈所有非永久性外伤（用于一无所有形态蜕变重塑躯体）。</summary>
        public static void RestoreAllInjuriesAndMissingParts(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            List<Hediff_MissingPart> missingParts = new List<Hediff_MissingPart>();
            pawn.health.hediffSet.GetHediffs(ref missingParts);
            List<BodyPartRecord> partsToRestore = new List<BodyPartRecord>(missingParts.Count);
            for (int i = 0; i < missingParts.Count; i++)
            {
                BodyPartRecord part = missingParts[i]?.Part;
                if (part != null && !partsToRestore.Contains(part))
                {
                    partsToRestore.Add(part);
                }
            }

            for (int i = 0; i < partsToRestore.Count; i++)
            {
                BodyPartRecord part = partsToRestore[i];
                if (pawn.health.hediffSet.PartIsMissing(part))
                {
                    pawn.health.RestorePart(part, checkStateChange: false);
                }
            }

            HealPawnInjuries(pawn, 99999f, showFeedback: false);
        }

        /// <summary>按指定治疗量治愈单位的非永久性外伤，返回实际恢复总量。</summary>
        public static float HealPawnInjuries(Pawn pawn, float amount, bool showFeedback = false)
        {
            if (pawn == null || pawn.Dead || amount <= 0f || pawn.health?.hediffSet == null)
            {
                return 0f;
            }

            List<Hediff_Injury> injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries);

            float remaining = amount;
            float healedTotal = 0f;
            Hediff_Injury lastHealed = null;
            for (int i = injuries.Count - 1; i >= 0 && remaining > 0f; i--)
            {
                Hediff_Injury injury = injuries[i];
                if (injury == null || injury.IsPermanent() || injury.Severity <= 0f)
                {
                    continue;
                }

                float heal = Mathf.Min(remaining, injury.Severity);
                injury.Heal(heal);
                remaining -= heal;
                healedTotal += heal;
                lastHealed = injury;
            }

            if (showFeedback && healedTotal > 0f && pawn.Spawned && pawn.Map != null && pawn.Position.InBounds(pawn.Map))
            {
                if (Setting_MeatLantern_Main.Settings?.If_ShowVampireText != false)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("LC_NothingThere_LifestealText".Translate());
                    sb.Append(healedTotal.ToString("0.#"));
                    if (Setting_MeatLantern_Main.Settings?.If_ShowVampireHealPartText == true && lastHealed?.Part != null)
                    {
                        sb.Append(" (").Append(lastHealed.Part.Label).Append(")");
                    }

                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, sb.ToString(), Color.green);
                }

                if (Setting_MeatLantern_Main.Settings?.If_ShowVampireHealVFX != false)
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, RimWorld.FleckDefOf.HealingCross, 0.45f);
                }
            }

            return healedTotal;
        }
    }
}
