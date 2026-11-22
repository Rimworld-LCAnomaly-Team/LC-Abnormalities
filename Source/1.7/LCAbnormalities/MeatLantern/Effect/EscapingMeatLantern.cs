using LCAnomalyCore.Misc;
using MeatLantern.Comp;
using LCAnomalyCore.Util;
using MeatLantern.Job;
using RimWorld;
using Verse;
using Verse.Sound;

namespace MeatLantern.Effect
{
    /// <summary>
    /// 肉食提灯逃脱收容特效对象
    /// </summary>
    public class EscapingMeatLantern : LC_FX_Standard
    {
        private Effecter drillEffecter;

        public override void InitWith(Pawn targetPawn)
        {
            DoDefaultFX();
        }

        public void InitWithEscaping(Pawn targetPawn)
        {
            this.isEscaping = true;
            var comp = targetPawn?.TryGetComp<CompMeatLantern>();
            if (comp != null)
            {
                //传递生物特征
                bioSignature = comp.biosignature;
            }

            //销毁未出逃提灯
            if (targetPawn != null && !targetPawn.Destroyed)
            {
                targetPawn.Destroy();
            }

            DoDefaultFX();
        }

        private void DoDefaultFX()
        {
            completeTick = base.TickSpawned + 120;

            if (base.Map != null && base.Position.InBounds(base.Map))
            {
                drillEffecter = EffecterDefOf.Drill.Spawn(base.Position, base.Map);
                AbnormalityUtility.Burst(base.Position, base.Map, 1.5f);
                Def.SoundDefOf.MeatLantern_Escape.PlayOneShot(new TargetInfo(base.Position, base.Map));
            }

            hasInited = true;
        }

        protected override void Tick()
        {
            if (hasInited && Spawned && base.Map != null)
            {
                drillEffecter ??= EffecterDefOf.Drill.Spawn(base.Position, base.Map);
                drillEffecter?.EffectTick(this, this);
            }

            base.Tick();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            drillEffecter?.Cleanup();
            drillEffecter = null;
            base.DeSpawn(mode);
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            //生成逃脱收容的肉食提灯
            if (base.MapHeld != null)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(Def.PawnKindDefOf.MeatLanternEscaped, LCContainmentUtility.AbnormalityFaction);
                GenSpawn.Spawn(pawn, TryFindTargetCell(50), base.MapHeld);
                CompMeatLantern compMeatLantern = pawn.TryGetComp<CompMeatLantern>();
                if (compMeatLantern != null)
                {
                    compMeatLantern.SetState(MeatLanternState.Wait);
                    if (isEscaping)
                    {
                        //传递生物特征
                        compMeatLantern.biosignature = bioSignature;
                    }
                }
            }

            //销毁自己
            Destroy();
        }

        /// <summary>
        /// 瞬移逃到某个位置
        /// </summary>
        /// <param name="radius">范围</param>
        /// <returns>目标位置</returns>
        private IntVec3 TryFindTargetCell(int radius)
        {
            IntVec3 pos = base.Position;
            if (base.MapHeld == null)
            {
                return pos;
            }

            bool found = CellFinder.TryFindRandomReachableNearbyCell(
                base.Position, base.MapHeld
                , radius, TraverseParms.For(TraverseMode.PassDoors)
                , null, null, out pos);

            return found && pos.InBounds(MapHeld) ? pos : Position;
        }
    }
}
