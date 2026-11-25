using LCAbnormalities.Shared;
using UnityEngine;
using Verse;

namespace LCAbnormalities.ScarecrowSearchingForWisdom
{
    [StaticConstructorOnStartup]
    public static class ScarecrowMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropStrawColor = Shader.PropertyToID("_StrawColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        public static readonly Material DrainMat;

        static ScarecrowMaterials()
        {
            Shader shader = ShaderDatabase.LoadShader("Map/LC_ScarecrowDrainBeam") ?? ShaderDatabase.MoteGlow;
            DrainMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = shader,
                color = new Color(0.84f, 0.12f, 0.16f, 1.0f)
            });
        }
    }

    public class DyingScarecrowSearchingForWisdom : DyingAbnormality
    {
        public override void InitWith(Pawn targetPawn)
        {
            base.InitWith(targetPawn);
            LCAbnormalitiesHelper.PlaySoundAt(ScarecrowDefOf.LC_Scarecrow_Dead, targetPawn);
            if (Map != null && Position.InBounds(Map))
            {
                ScarecrowVfxUtility.SpawnDrainAura(Map, targetPawn.DrawPos, 2.8f, 40);
            }
        }
    }

    public class Thing_ScarecrowDrainBeamFX : Thing
    {
        private int startTick;
        private int lifeTicks = 210;
        private float scale = 2.6f;
        private MaterialPropertyBlock mpb;

        public void Init(float s, int ticks)
        {
            scale = s;
            lifeTicks = Mathf.Max(12, ticks);
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
            if (ScarecrowMaterials.DrainMat == null || Find.TickManager == null) return;
            int age = Find.TickManager.TicksGame - startTick;
            float progress = Mathf.Clamp01((float)age / lifeTicks);
            if (mpb == null) mpb = new MaterialPropertyBlock();

            mpb.SetColor(ScarecrowMaterials.PropColor, new Color(0.84f, 0.12f, 0.16f, 1f));
            mpb.SetColor(ScarecrowMaterials.PropStrawColor, new Color(0.92f, 0.72f, 0.32f, 1f));
            mpb.SetFloat(ScarecrowMaterials.PropProgress, progress);
            mpb.SetFloat(ScarecrowMaterials.PropAgeSecs, age / 60f);
            mpb.SetFloat(ScarecrowMaterials.PropAlpha, 1f - Mathf.Max(0f, (progress - 0.75f) / 0.25f));

            Matrix4x4 mat = Matrix4x4.TRS(drawLoc.Yto0() + Vector3.up * AltitudeLayer.MoteOverhead.AltitudeFor(), Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(MeshPool.plane10, mat, ScarecrowMaterials.DrainMat, 0, null, 0, mpb);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lifeTicks, "lifeTicks", 210);
            Scribe_Values.Look(ref scale, "scale", 2.6f);
        }
    }

    public static class ScarecrowVfxUtility
    {
        public static void SpawnDrainAura(Map map, Vector3 pos, float scale = 2.6f, int lifeTicks = 210)
        {
            if (map == null || ScarecrowDefOf.LC_ScarecrowDrainBeamFX == null) return;
            IntVec3 cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return;
            if (GenSpawn.Spawn(ScarecrowDefOf.LC_ScarecrowDrainBeamFX, cell, map) is Thing_ScarecrowDrainBeamFX fx)
            {
                fx.Position = cell;
                fx.Init(scale, lifeTicks);
            }
        }
    }
}
