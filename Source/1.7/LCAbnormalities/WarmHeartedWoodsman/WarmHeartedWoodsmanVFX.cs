using LCAbnormalities.Shared;
using UnityEngine;
using Verse;

namespace LCAbnormalities.WarmHeartedWoodsman
{
    [StaticConstructorOnStartup]
    public static class WarmHeartedWoodsmanMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropEmberColor = Shader.PropertyToID("_EmberColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        public static readonly Material FurnaceMat;
        public static readonly Material ShutterMat;

        static WarmHeartedWoodsmanMaterials()
        {
            Shader furnaceShader = ShaderDatabase.LoadShader("Map/LC_WoodsmanFurnace") ?? ShaderDatabase.MoteGlow;
            FurnaceMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = furnaceShader,
                color = new Color(0.96f, 0.14f, 0.12f, 1.0f)
            });

            Texture2D shutterTex = ContentFinder<Texture2D>.Get("Things/VFX/WarmHeartedWoodsman/Woodsman_IronShutter", reportFailure: false)
                ?? BaseContent.WhiteTex;
            ShutterMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = shutterTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });
        }
    }

    public class DyingWarmHeartedWoodsman : DyingAbnormality
    {
        public override void InitWith(Pawn targetPawn)
        {
            base.InitWith(targetPawn);
            LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Dead, targetPawn);
            if (Map != null && Position.InBounds(Map))
            {
                WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(Map, targetPawn.DrawPos, 2.9f, 40);
            }
        }
    }

    public class Thing_WoodsmanShutterFX : Thing
    {
        private int startTick;
        private int lifeTicks = 34;
        private float scale = 2.6f;

        public void Init(float s, int ticks)
        {
            scale = s;
            lifeTicks = Mathf.Max(10, ticks);
            startTick = Find.TickManager?.TicksGame ?? 0;
        }

        protected override void Tick()
        {
            if (Find.TickManager != null && Find.TickManager.TicksGame - startTick >= lifeTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (WarmHeartedWoodsmanMaterials.ShutterMat == null || Find.TickManager == null) return;
            float progress = Mathf.Clamp01((float)(Find.TickManager.TicksGame - startTick) / lifeTicks);
            float h = scale * (progress < 0.25f ? Mathf.Lerp(0.2f, 1.0f, progress / 0.25f) : 1.0f);
            Color col = new Color(1f, 1f, 1f, 1f - Mathf.Max(0f, (progress - 0.55f) / 0.45f));
            WarmHeartedWoodsmanMaterials.ShutterMat.color = col;
            Matrix4x4 mat = Matrix4x4.TRS(drawLoc.Yto0() + Vector3.up * AltitudeLayer.MoteOverhead.AltitudeFor(), Quaternion.identity, new Vector3(scale, 1f, h));
            Graphics.DrawMesh(MeshPool.plane10, mat, WarmHeartedWoodsmanMaterials.ShutterMat, 0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lifeTicks, "lifeTicks", 34);
            Scribe_Values.Look(ref scale, "scale", 2.6f);
        }
    }

    public class Thing_WoodsmanFurnaceAuraFX : Thing
    {
        private int startTick;
        private int lifeTicks = 32;
        private float scale = 2.4f;
        private MaterialPropertyBlock mpb;

        public void Init(float s, int ticks)
        {
            scale = s;
            lifeTicks = Mathf.Max(10, ticks);
            startTick = Find.TickManager?.TicksGame ?? 0;
        }

        protected override void Tick()
        {
            if (Find.TickManager != null && Find.TickManager.TicksGame - startTick >= lifeTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (WarmHeartedWoodsmanMaterials.FurnaceMat == null || Find.TickManager == null) return;
            int age = Find.TickManager.TicksGame - startTick;
            float progress = Mathf.Clamp01((float)age / lifeTicks);
            if (mpb == null) mpb = new MaterialPropertyBlock();

            mpb.SetColor(WarmHeartedWoodsmanMaterials.PropColor, new Color(0.96f, 0.14f, 0.12f, 1f));
            mpb.SetColor(WarmHeartedWoodsmanMaterials.PropEmberColor, new Color(1f, 0.58f, 0.18f, 1f));
            mpb.SetFloat(WarmHeartedWoodsmanMaterials.PropProgress, progress);
            mpb.SetFloat(WarmHeartedWoodsmanMaterials.PropAgeSecs, age / 60f);
            mpb.SetFloat(WarmHeartedWoodsmanMaterials.PropAlpha, 1f - progress * 0.65f);

            Matrix4x4 mat = Matrix4x4.TRS(drawLoc.Yto0() + Vector3.up * AltitudeLayer.MoteOverhead.AltitudeFor(), Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(MeshPool.plane10, mat, WarmHeartedWoodsmanMaterials.FurnaceMat, 0, null, 0, mpb);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lifeTicks, "lifeTicks", 32);
            Scribe_Values.Look(ref scale, "scale", 2.4f);
        }
    }

    public static class WarmHeartedWoodsmanVfxUtility
    {
        public static void SpawnShutterExecution(Map map, Vector3 pos, float scale = 2.6f, int lifeTicks = 34)
        {
            if (map == null || WarmHeartedWoodsmanDefOf.LC_WoodsmanShutterFX == null) return;
            IntVec3 cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return;
            if (GenSpawn.Spawn(WarmHeartedWoodsmanDefOf.LC_WoodsmanShutterFX, cell, map) is Thing_WoodsmanShutterFX fx)
            {
                fx.Position = cell;
                fx.Init(scale, lifeTicks);
            }
        }

        public static void SpawnFurnaceAura(Map map, Vector3 pos, float scale = 2.4f, int lifeTicks = 32)
        {
            if (map == null || WarmHeartedWoodsmanDefOf.LC_WoodsmanFurnaceAuraFX == null) return;
            IntVec3 cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return;
            if (GenSpawn.Spawn(WarmHeartedWoodsmanDefOf.LC_WoodsmanFurnaceAuraFX, cell, map) is Thing_WoodsmanFurnaceAuraFX fx)
            {
                fx.Position = cell;
                fx.Init(scale, lifeTicks);
            }
        }
    }
}
