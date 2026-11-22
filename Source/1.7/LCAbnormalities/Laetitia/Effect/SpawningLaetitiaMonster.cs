using Laetitia.Things;
using LCAnomalyCore.Misc;
using RimWorld;
using Verse;

namespace Laetitia.Effect
{
    public class SpawningLaetitiaMonster : LC_FX_Standard
    {
        private int monsterLifespanTicks = -1;

        public void Init(int lifespanTicks = -1)
        {
            monsterLifespanTicks = lifespanTicks;
            DoDefaultFX();
        }

        private void DoDefaultFX()
        {
            // 播放血肉特效，记录动画播放完的时间
            if (Map != null && Position.InBounds(Map))
            {
                LCAnomalyCore.Util.AbnormalityUtility.Burst(Position, Map, 2f);
            }
            completeTick = base.TickSpawned + 120;

            hasInited = true;
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            // 生成蕾蒂希娅的朋友
            if (MapHeld != null && Position.InBounds(MapHeld))
            {
                Pawn monster = PawnGenerator.GeneratePawn(Def.PawnKindDefOf.LaetitiaMonster, LCAnomalyCore.Util.LCContainmentUtility.AbnormalityFaction);
                GenSpawn.Spawn(monster, Position, MapHeld);
                if (monster is LC_LaetitiaMonsterPawn laetitiaMonster && monsterLifespanTicks > 0)
                {
                    laetitiaMonster.SetTemporaryLifespan(monsterLifespanTicks);
                }
            }

            // 销毁自己
            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref monsterLifespanTicks, "monsterLifespanTicks", -1);
        }
    }
}
