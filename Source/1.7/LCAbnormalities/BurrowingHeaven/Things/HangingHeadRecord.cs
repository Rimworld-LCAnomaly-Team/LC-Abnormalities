using System;
using System.Collections.Generic;
using BurrowingHeaven.Render;
using RimWorld;
using UnityEngine;
using Verse;

namespace BurrowingHeaven.Things
{
    /// <summary>
    /// 受害者头部在 PawnRenderTree 中的单个可见图层快照（头部相对坐标系，兼容 HAR 等扩展图层）。
    /// </summary>
    public class HangingHeadLayerRecord : IExposable
    {
        public string texPath;
        public string maskPath;
        public bool isMulti = true;
        public string shaderTypeDefName;
        public string shaderPath;
        public Color color = Color.white;
        public Color colorTwo = Color.white;
        public bool isSkinLayer;
        public Vector2 localOffsetXZ = Vector2.zero;
        public float localAngleY;
        public Vector2 localScale = Vector2.one;
        public Vector2 meshSize = Vector2.one;
        public bool flipMesh;
        public float relativeY;
        public int captureOrder;

        public void ExposeData()
        {
            Scribe_Values.Look(ref texPath, "texPath");
            Scribe_Values.Look(ref maskPath, "maskPath");
            Scribe_Values.Look(ref isMulti, "isMulti", true);
            Scribe_Values.Look(ref shaderTypeDefName, "shaderTypeDefName");
            Scribe_Values.Look(ref shaderPath, "shaderPath");
            Scribe_Values.Look(ref color, "color", Color.white);
            Scribe_Values.Look(ref colorTwo, "colorTwo", Color.white);
            Scribe_Values.Look(ref isSkinLayer, "isSkinLayer", false);
            Scribe_Values.Look(ref localOffsetXZ, "localOffsetXZ", Vector2.zero);
            Scribe_Values.Look(ref localAngleY, "localAngleY", 0f);
            Scribe_Values.Look(ref localScale, "localScale", Vector2.one);
            Scribe_Values.Look(ref meshSize, "meshSize", Vector2.one);
            Scribe_Values.Look(ref flipMesh, "flipMesh", false);
            Scribe_Values.Look(ref relativeY, "relativeY", 0f);
            Scribe_Values.Look(ref captureOrder, "captureOrder", 0);
        }
    }

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
        public List<HangingHeadLayerRecord> capturedLayers = new List<HangingHeadLayerRecord>();

        [Unsaved(false)]
        private List<ResolvedTrophyLayer> cachedCompositeLayers;

        [Unsaved(false)]
        private bool compositeCacheBuilt;

        public bool HasCapturedLayers => capturedLayers != null && capturedLayers.Count > 0;

        public bool HasUsableCapturedLayers
        {
            get
            {
                List<ResolvedTrophyLayer> layers = GetCompositeLayers();
                return layers != null && layers.Count > 0;
            }
        }

        public HangingHeadRecord()
        {
        }

        public void InvalidateRenderCache()
        {
            cachedCompositeLayers = null;
            compositeCacheBuilt = false;
        }

        public List<ResolvedTrophyLayer> GetCompositeLayers()
        {
            if (!compositeCacheBuilt)
            {
                compositeCacheBuilt = true;
                cachedCompositeLayers = HasCapturedLayers
                    ? TrophyHeadRenderHelper.BuildCompositeLayers(this)
                    : null;
            }
            return cachedCompositeLayers;
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

            try
            {
                record.capturedLayers = TrophyHeadRenderHelper.CaptureHeadLayers(victim)
                    ?? new List<HangingHeadLayerRecord>();
            }
            catch (Exception ex)
            {
                Log.Warning($"[LCAbnormalities] Failed to capture trophy head render layers for {record.victimName}: {ex}");
                record.capturedLayers = new List<HangingHeadLayerRecord>();
            }

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
            Scribe_Collections.Look(ref capturedLayers, "capturedLayers", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (capturedLayers == null)
                {
                    capturedLayers = new List<HangingHeadLayerRecord>();
                }
                InvalidateRenderCache();
            }
        }
    }
}
