using RimWorld;
using UnityEngine;
using Verse;

namespace BurrowingHeaven.Things
{
    /// <summary>
    /// 记录被穿刺乐园击杀并悬挂在枝干上的受害者头颅外观快照。
    /// </summary>
    public class HangingHeadRecord : IExposable
    {
        public int slotIndex;
        public string victimName;
        public HeadTypeDef headTypeDef;
        public Color skinColor = Color.white;
        public HairDef hairDef;
        public Color hairColor = Color.black;
        public BeardDef beardDef;
        public float tiltAngle;
        public int killedTick;

        public HangingHeadRecord()
        {
        }

        public static HangingHeadRecord CreateFrom(Pawn victim, int slotIndex, float tiltAngle)
        {
            var record = new HangingHeadRecord
            {
                slotIndex = slotIndex,
                victimName = victim?.Name?.ToStringShort ?? victim?.LabelShortCap ?? "Unknown",
                headTypeDef = victim?.story?.headType ?? HeadTypeDefOf.Skull,
                skinColor = victim?.story?.SkinColor ?? new Color(0.85f, 0.78f, 0.72f),
                hairDef = victim?.story?.hairDef,
                hairColor = victim?.story?.HairColor ?? Color.black,
                beardDef = victim?.style?.beardDef,
                tiltAngle = tiltAngle,
                killedTick = Find.TickManager?.TicksGame ?? 0
            };
            return record;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref slotIndex, "slotIndex", 0);
            Scribe_Values.Look(ref victimName, "victimName");
            Scribe_Defs.Look(ref headTypeDef, "headTypeDef");
            Scribe_Values.Look(ref skinColor, "skinColor", Color.white);
            Scribe_Defs.Look(ref hairDef, "hairDef");
            Scribe_Values.Look(ref hairColor, "hairColor", Color.black);
            Scribe_Defs.Look(ref beardDef, "beardDef");
            Scribe_Values.Look(ref tiltAngle, "tiltAngle", 0f);
            Scribe_Values.Look(ref killedTick, "killedTick", 0);
        }
    }
}
