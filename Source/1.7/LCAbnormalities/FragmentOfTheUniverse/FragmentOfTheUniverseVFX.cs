using LCAbnormalities.Shared;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FragmentOfTheUniverse
{
    [StaticConstructorOnStartup]
    public static class FragmentOfTheUniverseMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropAccentColor = Shader.PropertyToID("_AccentColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        public static readonly Material SongWaveMat;

        static FragmentOfTheUniverseMaterials()
        {
            Shader shader = ShaderDatabase.LoadShader("Map/LC_CosmosSongWave") ?? ShaderDatabase.MoteGlow;
            SongWaveMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = shader,
                color = new Color(0.66f, 0.24f, 0.98f, 1.0f)
            });
        }
    }

    public class DyingFragmentOfTheUniverse : DyingAbnormality
    {
        public override void InitWith(Pawn targetPawn)
        {
            base.InitWith(targetPawn);
            LCAbnormalitiesHelper.PlaySoundAt(FragmentOfTheUniverseDefOf.LC_Cosmos_Default, targetPawn);
            if (Map != null && Position.InBounds(Map))
            {
                FragmentOfTheUniverseVfxUtility.SpawnSongWave(Map, targetPawn.DrawPos, 3.2f, 44);
            }
        }
    }

    public class Thing_CosmosSongWaveFX : Thing
    {
        private int startTick;
        private int lifeTicks = 42;
        private float maxScale = 6.0f;
        private MaterialPropertyBlock mpb;

        public void Init(float s, int ticks)
        {
            maxScale = s;
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
            if (FragmentOfTheUniverseMaterials.SongWaveMat == null || Find.TickManager == null) return;
            int age = Find.TickManager.TicksGame - startTick;
            float progress = Mathf.Clamp01((float)age / lifeTicks);
            if (mpb == null) mpb = new MaterialPropertyBlock();

            mpb.SetColor(FragmentOfTheUniverseMaterials.PropColor, new Color(0.66f, 0.24f, 0.98f, 1f));
            mpb.SetColor(FragmentOfTheUniverseMaterials.PropAccentColor, new Color(0.18f, 0.88f, 0.96f, 1f));
            mpb.SetFloat(FragmentOfTheUniverseMaterials.PropProgress, progress);
            mpb.SetFloat(FragmentOfTheUniverseMaterials.PropAgeSecs, age / 60f);
            mpb.SetFloat(FragmentOfTheUniverseMaterials.PropAlpha, 1f - progress * 0.65f);

            float s = maxScale * (0.55f + 0.5f * progress);
            Matrix4x4 mat = Matrix4x4.TRS(drawLoc.Yto0() + Vector3.up * AltitudeLayer.MoteOverhead.AltitudeFor(), Quaternion.identity, new Vector3(s, 1f, s));
            Graphics.DrawMesh(MeshPool.plane10, mat, FragmentOfTheUniverseMaterials.SongWaveMat, 0, null, 0, mpb);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lifeTicks, "lifeTicks", 42);
            Scribe_Values.Look(ref maxScale, "maxScale", 6.0f);
        }
    }

    public static class FragmentOfTheUniverseVfxUtility
    {
        public static void SpawnSongWave(Map map, Vector3 pos, float scale = 6.0f, int lifeTicks = 42)
        {
            if (map == null || FragmentOfTheUniverseDefOf.LC_CosmosSongWaveFX == null) return;
            IntVec3 cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return;
            if (GenSpawn.Spawn(FragmentOfTheUniverseDefOf.LC_CosmosSongWaveFX, cell, map) is Thing_CosmosSongWaveFX fx)
            {
                fx.Position = cell;
                fx.Init(scale, lifeTicks);
            }
        }
    }
}
