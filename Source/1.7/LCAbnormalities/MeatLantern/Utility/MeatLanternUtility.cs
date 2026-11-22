using MeatLantern.Effect;
using MeatLantern.Setting;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MeatLantern.Utility
{
    public static class MeatLanternUtility
    {
        #region 字段

        public static readonly FloatRange SearchForTargetCooldownRangeDays = new(1f, 2f);

        private static readonly HashSet<Pawn> tmpTargets = [];

        #endregion 字段

        #region 方法

        #region 肉食提灯

        /// <summary>
        /// 在Pawn周围扫描目标
        /// </summary>
        /// <param name="pawn">pawn</param>
        /// <param name="radius">范围</param>
        /// <returns>符合条件的受害者List</returns>
        public static List<Pawn> ScanForTarget_NewTemp(Pawn pawn, float radius)
        {
            tmpTargets.Clear();
            if (pawn == null || pawn.Map == null || !pawn.Position.InBounds(pawn.Map))
            {
                return null;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(pawn.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn2
                        && ValidTarget(pawn2)
                        && pawn.Position.InHorDistOf(pawn2.Position, radius)
                        && GenSight.LineOfSightToThing(pawn.Position, pawn2, pawn.Map))
                    {
                        tmpTargets.Add(pawn2);
                    }
                }
            }

            return tmpTargets.Count > 0 ? [.. tmpTargets] : null;
        }

        /// <summary>
        /// 验证目标是否合法
        /// </summary>
        /// <param name="pawn">目标</param>
        /// <returns>是否合法</returns>
        public static bool ValidTarget(Pawn pawn)
        {
            //不吃同类、已倒地/死亡或未在地图中的单位
            return pawn != null
                && !pawn.Dead
                && pawn.Spawned
                && !pawn.Downed
                && pawn.kindDef != Def.PawnKindDefOf.MeatLanternEscaped
                && pawn.kindDef != Def.PawnKindDefOf.MeatLanternContained;
        }

        /// <summary>
        /// 验证附近是否有合法目标单位
        /// </summary>
        /// <param name="pos">位置</param>
        /// <param name="map">地图</param>
        /// <param name="radius">范围</param>
        /// <returns></returns>
        public static int NearbyHumanlikePawnCount(IntVec3 pos, Map map, float radius)
        {
            if (map == null || !pos.InBounds(map))
            {
                return 0;
            }

            int num = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pos, radius, useCenter: true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn item && ValidTarget(item) && pos.InHorDistOf(item.Position, radius))
                    {
                        num++;
                    }
                }
            }

            return num;
        }

        /// <summary>
        /// 获取最近的目标
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <param name="radius">范围</param>
        /// <returns>符合条件的目标</returns>
        public static Pawn GetClosestTargetInRadius(Pawn pawn, float radius)
        {
            if (pawn == null || pawn.Map == null || !pawn.Position.InBounds(pawn.Map))
            {
                return null;
            }

            float minDistSq = float.MaxValue;
            Pawn result = null;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(pawn.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn item
                        && ValidTarget(item)
                        && pawn.Position.InHorDistOf(item.Position, radius))
                    {
                        float distSq = item.Position.DistanceToSquared(pawn.Position);
                        if (distSq < minDistSq && GenSight.LineOfSightToThing(pawn.Position, item, pawn.Map))
                        {
                            minDistSq = distSq;
                            result = item;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 肉食提灯死后操作
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <param name="map">地图</param>
        public static void OnMeatLanternDeath(Pawn pawn, Map map)
        {
            Map targetMap = map ?? pawn?.MapHeld;
            IntVec3 targetPos = pawn?.PositionHeld ?? IntVec3.Invalid;
            if (pawn == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                Log.Warning($"Unable to create Meat Lantern death effect for {pawn}: no valid map position.");
                return;
            }

            Find.LetterStack.ReceiveLetter("LetterLabelMeatLanternKilled".Translate()
                , "LetterMeatLanternKilled".Translate()
                , LetterDefOf.PositiveEvent
                , new LookTargets(targetPos, targetMap));

            var dying = GenSpawn.Spawn(Def.ThingDefOf.DyingMeatLantern, targetPos, targetMap) as DyingMeatLantern;
            dying?.InitWith(pawn);
        }

        /// <summary>
        /// 肉食提灯逃脱操作
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <param name="map">地图</param>
        public static void OnMeatLanternEscape(Pawn pawn, Map map)
        {
            Map targetMap = map ?? pawn?.MapHeld;
            IntVec3 targetPos = pawn?.PositionHeld ?? IntVec3.Invalid;
            if (pawn == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                return;
            }

            var escaping = GenSpawn.Spawn(Def.ThingDefOf.EscapingMeatLantern, targetPos, targetMap) as EscapingMeatLantern;
            escaping?.InitWithEscaping(pawn);
        }

        /// <summary>
        /// 肉食提灯吞噬操作
        /// </summary>
        /// <param name="instigator">加害者</param>
        /// <param name="victims">受害者List</param>
        public static void OnMeatLanternEat(Pawn instigator, List<Pawn> victims)
        {
            if (instigator == null || instigator.MapHeld == null || !instigator.PositionHeld.InBounds(instigator.MapHeld))
            {
                return;
            }

            var eating = GenSpawn.Spawn(Def.ThingDefOf.EatingMeatLantern, instigator.PositionHeld, instigator.MapHeld) as EatingMeatLantern;
            eating?.InitWith(instigator, victims);
        }

        /// <summary>
        /// 应用吞噬伤害（对齐四色红伤）
        /// </summary>
        /// <param name="victims">受害者List</param>
        /// <param name="instigator">加害者</param>
        public static void DoDamageByEat(List<Pawn> victims, Pawn instigator)
        {
            if (victims == null || victims.Count == 0)
            {
                return;
            }

            DamageInfo dinfo = new(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 45f, 0.25f, -1f, instigator);

            foreach (Pawn p in victims)
            {
                if (p == null || p.Dead || p.Destroyed || !p.Spawned)
                {
                    continue;
                }

                if (instigator != null
                    && instigator.Spawned
                    && instigator.MapHeld == p.MapHeld
                    && p.Position.DistanceToSquared(instigator.Position) > 9f)
                {
                    continue;
                }

                p.TakeDamage(dinfo);
            }
        }

        #endregion 肉食提灯

        #region EGO

        /// <summary>
        /// 嚼嚼旋风技能（黑伤噬咬与非机械族吸血/眩晕）
        /// </summary>
        /// <param name="victim">受害者</param>
        /// <param name="instigator">加害者</param>
        /// <param name="damageRange">伤害范围</param>
        /// <param name="armorPenetration">破甲率</param>
        /// <param name="suckPercent">治愈比率</param>
        /// <param name="stunPercent">击晕比率</param>
        public static void DoBiteOnPawn(Pawn victim, Pawn instigator, FloatRange damageRange, float armorPenetration, float suckPercent, float stunPercent)
        {
            if (victim == null || victim.Dead || victim.Destroyed)
            {
                return;
            }

            Map victimMap = victim.MapHeld;
            IntVec3 victimPos = victim.PositionHeld;
            Vector3 victimDrawPos = victim.DrawPos;
            bool isMechanoid = victim.def?.race?.IsMechanoid == true;

            //造成黑伤
            float damage = damageRange.RandomInRange;
            DamageInfo dInfo = new(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, damage, armorPenetration, -1f, instigator, null, Def.ThingDefOf.EgoWeapon_MeatLantern);
            victim.TakeDamage(dInfo);

            //机械族不可被击晕和吸血
            if (!isMechanoid)
            {
                DoHeal(instigator, damage, suckPercent);

                if (!victim.Dead && !victim.Destroyed)
                {
                    DamageInfo dInfo_Stun = new(DamageDefOf.Stun, damage * stunPercent, 0f, -1f, instigator);
                    victim.TakeDamage(dInfo_Stun);
                }

                if (victimMap != null && victimPos.InBounds(victimMap))
                {
                    LCAnomalyCore.Util.AbnormalityUtility.Burst(victimPos, victimMap);
                }
            }
            else if (victimMap != null && victimPos.InBounds(victimMap))
            {
                FleckMaker.ThrowMicroSparks(victimDrawPos, victimMap);
            }

            if (victimMap != null && victimPos.InBounds(victimMap))
            {
                Def.SoundDefOf.MeatLantern_Eat.PlayOneShot(new TargetInfo(victimPos, victimMap));
            }
        }

        /// <summary>
        /// 吸血恢复
        /// </summary>
        /// <param name="selfPawn">单位</param>
        /// <param name="healAmount">恢复量</param>
        /// <param name="percent">恢复比率</param>
        private static void DoHeal(Pawn selfPawn, float healAmount, float percent)
        {
            if (selfPawn == null || selfPawn.Dead || selfPawn.health?.hediffSet == null)
            {
                return;
            }

            List<Hediff_Injury> list = [];
            selfPawn.health.hediffSet.GetHediffs(ref list);

            if (list.Count > 0)
            {
                Hediff_Injury injury = list.RandomElement();
                float healed = healAmount * percent;
                injury.Heal(healed);

                if (selfPawn.Map == null || !selfPawn.Position.InBounds(selfPawn.Map))
                {
                    return;
                }

                //是否允许显示吸血字样
                if (Setting_MeatLantern_Main.Settings?.If_ShowVampireText == true)
                {
                    StringBuilder sb = new();
                    sb.Append(Translator.Translate("LC_MeatLantern_VampireAmountThrowText"));
                    sb.Append(healed.ToString("0.#"));

                    //是否允许显示恢复部位
                    if (Setting_MeatLantern_Main.Settings.If_ShowVampireHealPartText && injury.Part != null)
                    {
                        sb.Append(Translator.Translate("LC_MeatLantern_VampireBodypartThrowText"));
                        sb.Append(injury.Part.Label);
                    }

                    MoteMaker.ThrowText(selfPawn.Position.ToVector3(), selfPawn.Map, sb.ToString(), Color.green);
                }

                //是否允许显示治疗特效
                if (Setting_MeatLantern_Main.Settings?.If_ShowVampireHealVFX == true)
                    FleckMaker.ThrowMetaIcon(selfPawn.Position, selfPawn.Map, FleckDefOf.HealingCross, 0.42f);
            }
        }

        #endregion EGO

        #endregion 方法
    }
}
