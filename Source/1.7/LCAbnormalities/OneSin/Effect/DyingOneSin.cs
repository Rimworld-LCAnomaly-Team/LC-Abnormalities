using LCAnomalyCore.Misc;
using OneSin.Comp;
using RimWorld;
using Verse;

namespace OneSin.Effect
{
    /// <summary>
    /// 一罪与百善死亡特效对象
    /// </summary>
    public class DyingOneSin : LC_FX_Standard
    {
        /// <inheritdoc />
        public override void InitWith(Pawn targetPawn)
        {
            var comp = targetPawn?.TryGetComp<CompOneSin>();
            if (comp != null)
            {
                //传递生物特征
                bioSignature = comp.biosignature;
            }

            if (Map != null && Position.InBounds(Map))
            {
                LCAnomalyCore.Util.AbnormalityUtility.Burst(Position, Map, 2f);
            }
            completeTick = TickSpawned + 60;

            hasInited = true;
        }

        /// <inheritdoc />
        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            //生成扭曲血肉脏污
            ThingDef filthDef = RimWorld.ThingDefOf.Filth_Blood;
            if (filthDef != null
                && base.Map != null
                && base.PositionHeld.InBounds(base.Map)
                && FilthMaker.TryMakeFilth(base.PositionHeld, base.Map, filthDef))
            {
                //清除1格以内的植物
                foreach (IntVec3 item in CellRect.CenteredOn(base.PositionHeld, 1))
                {
                    if (!item.InBounds(base.Map))
                        continue;

                    Plant plant = item.GetPlant(base.Map);
                    if (plant != null && plant.MaxHitPoints < 100)
                    {
                        plant.Destroy();
                    }
                }
            }

            //生成蛋，销毁自己
            if (base.MapHeld != null && base.PositionHeld.InBounds(base.MapHeld))
            {
                Thing thing = ThingMaker.MakeThing(Def.ThingDefOf.OneSinEgg);
                LCAnomalyCore.Util.AbnormalityUtility.TransferSignature(thing, bioSignature);
                GenSpawn.Spawn(thing, base.PositionHeld, base.MapHeld);
            }
            Destroy();
        }
    }
}
