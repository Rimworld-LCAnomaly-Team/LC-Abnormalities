using BurrowingHeaven.Effect;
using RimWorld;
using Verse;

namespace BurrowingHeaven.Util
{
    public static class EffectUtil
    {
        /// <summary>
        /// 穿刺乐园死后操作
        /// </summary>
        /// <param name="pawn">单位</param>
        /// <param name="map">地图</param>
        public static void OnBurrowingHeavenDeath(Pawn pawn, Map map)
        {
            Map targetMap = map ?? pawn?.MapHeld;
            IntVec3 targetPos = pawn?.PositionHeld ?? IntVec3.Invalid;
            if (pawn == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                Log.Warning($"Unable to create BurrowingHeaven death effect for {pawn}: no valid map position.");
                return;
            }

            Find.LetterStack.ReceiveLetter("LetterLabelBurrowingHeavenKilled".Translate()
                , "LetterBurrowingHeavenKilled".Translate()
                , LetterDefOf.PositiveEvent
                , new LookTargets(targetPos, targetMap));

            var dying = GenSpawn.Spawn(Def.ThingDefOf.DyingBurrowingHeaven, targetPos, targetMap) as DyingBurrowingHeaven;
            dying?.InitWith(pawn);
        }
    }
}
