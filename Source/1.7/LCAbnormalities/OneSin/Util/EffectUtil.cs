using OneSin.Effect;
using RimWorld;
using Verse;

namespace OneSin.Util
{
    /// <summary>
    /// 提供“一罪与百善”相关的效果处理方法。
    /// </summary>
    public static class EffectUtil
    {
        /// <summary>
        /// 一罪与百善死后操作
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <param name="map">地图</param>
        public static void OnOneSinDeath(Pawn pawn, Map map)
        {
            Map targetMap = map ?? pawn?.MapHeld;
            IntVec3 targetPos = pawn?.PositionHeld ?? IntVec3.Invalid;
            if (pawn == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                Log.Warning($"Unable to create OneSin death effect for {pawn}: no valid map position.");
                return;
            }

            Find.LetterStack.ReceiveLetter("LetterLabelOneSinKilled".Translate()
                , "LetterOneSinKilled".Translate()
                , LetterDefOf.PositiveEvent
                , new LookTargets(targetPos, targetMap));

            var dying = GenSpawn.Spawn(Def.ThingDefOf.DyingOneSin, targetPos, targetMap) as DyingOneSin;
            dying?.InitWith(pawn);
        }
    }
}
