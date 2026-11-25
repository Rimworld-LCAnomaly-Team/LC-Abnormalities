using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Ppodae
{
    [StaticConstructorOnStartup]
    public static class PpodaeMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        public static readonly Material SlamMat;

        static PpodaeMaterials()
        {
            Shader shader = ShaderDatabase.LoadShader("Map/LC_PpodaeSlam") ?? ShaderDatabase.MoteGlow;
            SlamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = shader,
                color = new Color(0.95f, 0.16f, 0.22f, 1.0f)
            });
        }
    }

    public class DyingPpodae : DyingAbnormality
    {
        public override void InitWith(Pawn targetPawn)
        {
            base.InitWith(targetPawn);
            LCAbnormalitiesHelper.PlaySoundAt(PpodaeDefOf.LC_Ppodae_Dead, targetPawn);
            if (Map != null && Position.InBounds(Map))
            {
                PpodaeVfxUtility.SpawnSlamBurst(Map, targetPawn.DrawPos, 2.6f, 34);
            }
        }
    }

    public class Thing_PpodaeSlamFX : Thing
    {
        private int startTick;
        private int lifeTicks = 24;
        private float maxScale = 2.4f;
        private MaterialPropertyBlock mpb;

        public void Init(float scale, int ticks)
        {
            maxScale = scale;
            lifeTicks = Mathf.Max(8, ticks);
            startTick = Find.TickManager?.TicksGame ?? 0;
        }

        protected override void Tick()
        {
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame - startTick >= lifeTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (PpodaeMaterials.SlamMat == null || Find.TickManager == null) return;
            int age = Find.TickManager.TicksGame - startTick;
            float progress = Mathf.Clamp01((float)age / lifeTicks);
            if (mpb == null) mpb = new MaterialPropertyBlock();

            mpb.SetColor(PpodaeMaterials.PropColor, new Color(0.95f, 0.16f, 0.22f, 1f));
            mpb.SetColor(PpodaeMaterials.PropCoreColor, new Color(1f, 0.86f, 0.80f, 1f));
            mpb.SetFloat(PpodaeMaterials.PropProgress, progress);
            mpb.SetFloat(PpodaeMaterials.PropAgeSecs, age / 60f);
            mpb.SetFloat(PpodaeMaterials.PropAlpha, 1f - progress * 0.7f);

            float s = maxScale * (0.7f + 0.55f * progress);
            Matrix4x4 mat = Matrix4x4.TRS(drawLoc.Yto0() + Vector3.up * AltitudeLayer.MoteOverhead.AltitudeFor(), Quaternion.identity, new Vector3(s, 1f, s));
            Graphics.DrawMesh(MeshPool.plane10, mat, PpodaeMaterials.SlamMat, 0, null, 0, mpb);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lifeTicks, "lifeTicks", 24);
            Scribe_Values.Look(ref maxScale, "maxScale", 2.4f);
        }
    }

    public static class PpodaeVfxUtility
    {
        public static void SpawnSlamBurst(Map map, Vector3 pos, float scale = 2.4f, int lifeTicks = 24)
        {
            if (map == null || PpodaeDefOf.LC_PpodaeSlamFX == null) return;
            IntVec3 cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return;
            if (GenSpawn.Spawn(PpodaeDefOf.LC_PpodaeSlamFX, cell, map) is Thing_PpodaeSlamFX fx)
            {
                fx.Position = cell;
                fx.Init(scale, lifeTicks);
            }
        }
    }
}
