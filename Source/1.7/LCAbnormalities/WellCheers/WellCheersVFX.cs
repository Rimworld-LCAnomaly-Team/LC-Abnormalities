using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.WellCheers
{
    /// <summary>韦尔奇乐牌汽水“苏打（Soda）”E.G.O. 武器专属 GLSL 450 着色器与解包气泡贴图缓存。</summary>
    [StaticConstructorOnStartup]
    public static class WellCheersMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropAccentCol = Shader.PropertyToID("_AccentCol");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material MuzzleFizzMat;
        public static readonly Material SodaBoltMat;
        public static readonly Material SodaSplashMat;

        public static readonly Material BubblePurpleMat;
        public static readonly Material BubbleRedMat;
        public static readonly Material BubbleBlueMat;

        private const int AlphaBucketCount = 24;
        private static readonly MaterialPropertyBlock[] AlphaPropBlocks;

        static WellCheersMaterials()
        {
            Shader muzzleShader = ShaderDatabase.LoadShader("Map/LC_WellCheersMuzzleFizz") ?? ShaderDatabase.MoteGlow;
            Shader boltShader = ShaderDatabase.LoadShader("Map/LC_WellCheersSodaBolt") ?? ShaderDatabase.MoteGlow;
            Shader splashShader = ShaderDatabase.LoadShader("Map/LC_WellCheersSodaSplash") ?? ShaderDatabase.MoteGlow;

            MuzzleFizzMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = muzzleShader,
                color = new Color(0.68f, 0.34f, 1.0f, 1.0f)
            });

            SodaBoltMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = boltShader,
                color = new Color(0.68f, 0.34f, 1.0f, 1.0f)
            });

            SodaSplashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = splashShader,
                color = new Color(0.68f, 0.34f, 1.0f, 1.0f)
            });

            BubblePurpleMat = MaterialPool.MatFrom("Things/VFX/WellCheers/WellCheers_Bubble_Purple", ShaderDatabase.Transparent);
            BubbleRedMat = MaterialPool.MatFrom("Things/VFX/WellCheers/WellCheers_Bubble_Red", ShaderDatabase.Transparent);
            BubbleBlueMat = MaterialPool.MatFrom("Things/VFX/WellCheers/WellCheers_Bubble_Blue", ShaderDatabase.Transparent);

            AlphaPropBlocks = new MaterialPropertyBlock[AlphaBucketCount];
            for (int i = 0; i < AlphaBucketCount; i++)
            {
                float a = i / (float)(AlphaBucketCount - 1);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, a));
                AlphaPropBlocks[i] = block;
            }
        }

        public static Material GetBubbleMaterial(WellCheersFlavor flavor)
        {
            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    return BubbleRedMat;
                case WellCheersFlavor.Blue:
                    return BubbleBlueMat;
                default:
                    return BubblePurpleMat;
            }
        }

        /// <summary>返回预分配的量化透明度 PropertyBlock，避免单帧多气泡互相覆盖且零 GC 分配。</summary>
        public static MaterialPropertyBlock GetAlphaPropBlock(float alpha)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alpha) * (AlphaBucketCount - 1)), 0, AlphaBucketCount - 1);
            return AlphaPropBlocks[idx];
        }
    }

    internal struct SodaFizzBubble
    {
        public Vector3 pos;
        public Vector3 vel;
        public float baseSize;
        public float phase;
        public float swayAmp;
        public WellCheersFlavor flavor;
    }

    /// <summary>苏打手枪枪口碳酸雾气与葡萄香气气泡特效（LC_WellCheersMuzzleFizz）。</summary>
    public class Thing_WellCheersMuzzleFizz : Thing
    {
        private const int DurationTicks = 14;

        private Vector3 muzzlePos;
        private Vector3 dir = Vector3.right;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock coneProps;
        private readonly List<SodaFizzBubble> bubbles = new List<SodaFizzBubble>(4);

        public void Initialize(Vector3 start, Vector3 aimDir)
        {
            muzzlePos = start.Yto0();
            dir = aimDir.Yto0().sqrMagnitude > 0.001f ? aimDir.Yto0().normalized : Vector3.right;
            ageTicks = 0;
            initialized = true;

            bubbles.Clear();
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            for (int i = 0; i < 4; i++)
            {
                WellCheersFlavor f = i < 2 ? WellCheersFlavor.Purple : (i == 2 ? WellCheersFlavor.Red : WellCheersFlavor.Blue);
                float lat = Rand.Range(-0.22f, 0.22f);
                float fwd = Rand.Range(0.035f, 0.085f);
                bubbles.Add(new SodaFizzBubble
                {
                    pos = muzzlePos + dir * Rand.Range(0.04f, 0.16f) + perp * lat,
                    vel = dir * fwd + perp * Rand.Range(-0.015f, 0.015f) + new Vector3(0f, 0f, Rand.Range(0.010f, 0.024f)),
                    baseSize = Rand.Range(0.16f, 0.26f),
                    phase = Rand.Range(0f, Mathf.PI * 2f),
                    swayAmp = 0.008f,
                    flavor = f
                });
            }
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
                return;
            }

            WellCheersVfxUtility.StepBubbles(bubbles, ageTicks, damping: 0.90f);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float coneAlpha = Mathf.Clamp01(1f - ageTicks / 11f);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            if (coneAlpha > 0.01f && WellCheersMaterials.MuzzleFizzMat != null)
            {
                const float coneLength = 1.55f;
                const float coneWidth = 0.95f;
                Vector3 center = muzzlePos + dir * (coneLength * 0.5f);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                coneProps ??= new MaterialPropertyBlock();
                coneProps.SetColor(WellCheersMaterials.PropColor, new Color(0.68f, 0.34f, 1.0f, 1.0f));
                coneProps.SetColor(WellCheersMaterials.PropAccentCol, new Color(1.0f, 0.26f, 0.52f, 1.0f));
                coneProps.SetColor(WellCheersMaterials.PropCoreColor, new Color(1.0f, 0.94f, 1.0f, 1.0f));
                coneProps.SetFloat(WellCheersMaterials.PropProgress, prog);
                coneProps.SetFloat(WellCheersMaterials.PropAgeSecs, ageTicks / 60f);
                coneProps.SetFloat(WellCheersMaterials.PropAlpha, coneAlpha);

                Matrix4x4 mat = Matrix4x4.TRS(new Vector3(center.x, alt, center.z), rot, new Vector3(coneWidth, 1f, coneLength));
                Graphics.DrawMesh(MeshPool.plane10, mat, WellCheersMaterials.MuzzleFizzMat, 0, null, 0, coneProps);
            }

            WellCheersVfxUtility.DrawBubbles(bubbles, prog, ageTicks, alt + 0.01f);
        }
    }

    /// <summary>苏打 E.G.O. 碳酸弹命中飞溅波纹与四散气泡特效（LC_WellCheersSodaSplash，Mode 0）。</summary>
    public class Thing_WellCheersSodaSplash : Thing
    {
        private const int DurationTicks = 22;

        private Vector3 exactPos;
        private float maxDiameter = 1.85f;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock splashProps;
        private readonly List<SodaFizzBubble> bubbles = new List<SodaFizzBubble>(7);

        public void Initialize(Vector3 pos, bool hitLiving)
        {
            exactPos = pos.Yto0();
            maxDiameter = hitLiving ? 1.95f : 1.65f;
            ageTicks = 0;
            initialized = true;

            bubbles.Clear();
            int count = hitLiving ? 7 : 5;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + Rand.Range(-0.35f, 0.35f);
                float spd = Rand.Range(0.042f, 0.088f);
                WellCheersFlavor f = i < 4 ? WellCheersFlavor.Purple : (i < 6 ? WellCheersFlavor.Red : WellCheersFlavor.Blue);
                bubbles.Add(new SodaFizzBubble
                {
                    pos = exactPos + new Vector3(Mathf.Cos(ang) * 0.08f, 0f, Mathf.Sin(ang) * 0.08f),
                    vel = new Vector3(Mathf.Cos(ang) * spd, 0f, Mathf.Sin(ang) * spd + 0.015f),
                    baseSize = Rand.Range(0.20f, 0.34f),
                    phase = Rand.Range(0f, Mathf.PI * 2f),
                    swayAmp = 0.010f,
                    flavor = f
                });
            }
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
                return;
            }

            WellCheersVfxUtility.StepBubbles(bubbles, ageTicks, damping: 0.89f);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float alpha = Mathf.Pow(1f - prog, 0.90f);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            if (alpha > 0.01f && WellCheersMaterials.SodaSplashMat != null)
            {
                splashProps ??= new MaterialPropertyBlock();
                splashProps.SetColor(WellCheersMaterials.PropColor, new Color(0.68f, 0.34f, 1.0f, 1.0f));
                splashProps.SetColor(WellCheersMaterials.PropAccentCol, new Color(1.0f, 0.24f, 0.48f, 1.0f));
                splashProps.SetColor(WellCheersMaterials.PropCoreColor, new Color(1.0f, 0.96f, 1.0f, 1.0f));
                splashProps.SetFloat(WellCheersMaterials.PropProgress, 1f - Mathf.Pow(1f - prog, 2.0f));
                splashProps.SetFloat(WellCheersMaterials.PropAgeSecs, ageTicks / 60f);
                splashProps.SetFloat(WellCheersMaterials.PropAlpha, alpha);
                splashProps.SetFloat(WellCheersMaterials.PropMode, 0.0f);

                Matrix4x4 mat = Matrix4x4.TRS(new Vector3(exactPos.x, alt, exactPos.z), Quaternion.identity, new Vector3(maxDiameter, 1f, maxDiameter));
                Graphics.DrawMesh(MeshPool.plane10, mat, WellCheersMaterials.SodaSplashMat, 0, null, 0, splashProps);
            }

            WellCheersVfxUtility.DrawBubbles(bubbles, prog, ageTicks, alt + 0.015f);
        }
    }

    /// <summary>苏打 E.G.O. 命中射手触发疗愈时的葡萄汽水舒缓雾气反馈（LC_WellCheersSodaSplash，Mode 1）。</summary>
    public class Thing_WellCheersHealMist : Thing
    {
        private const int DurationTicks = 34;

        private Pawn recipient;
        private Vector3 lastPos;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock mistProps;
        private readonly List<SodaFizzBubble> bubbles = new List<SodaFizzBubble>(5);

        public void Initialize(Pawn targetPawn)
        {
            recipient = targetPawn;
            lastPos = (targetPawn != null && targetPawn.Spawned) ? targetPawn.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            ageTicks = 0;
            initialized = true;

            bubbles.Clear();
            for (int i = 0; i < 5; i++)
            {
                WellCheersFlavor f = i < 2 ? WellCheersFlavor.Purple : (i < 4 ? WellCheersFlavor.Blue : WellCheersFlavor.Red);
                bubbles.Add(new SodaFizzBubble
                {
                    pos = new Vector3(Rand.Range(-0.36f, 0.36f), 0f, Rand.Range(-0.10f, 0.30f)),
                    vel = new Vector3(Rand.Range(-0.008f, 0.008f), 0f, Rand.Range(0.018f, 0.030f)),
                    baseSize = Rand.Range(0.22f, 0.34f),
                    phase = Rand.Range(0f, Mathf.PI * 2f),
                    swayAmp = 0.012f,
                    flavor = f
                });
            }
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
                return;
            }

            if (recipient != null && recipient.Spawned && !recipient.Dead && recipient.Map == Map)
            {
                lastPos = recipient.DrawPos.Yto0();
                if (recipient.Position.InBounds(Map) && Position != recipient.Position)
                {
                    Position = recipient.Position;
                }
            }

            WellCheersVfxUtility.StepBubbles(bubbles, ageTicks, damping: 0.96f);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float fadeIn = Mathf.Clamp01(ageTicks / 6f);
            float fadeOut = Mathf.Pow(1f - prog, 1.15f);
            float alpha = fadeIn * fadeOut * 0.90f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            if (alpha > 0.01f && WellCheersMaterials.SodaSplashMat != null)
            {
                mistProps ??= new MaterialPropertyBlock();
                mistProps.SetColor(WellCheersMaterials.PropColor, new Color(0.72f, 0.44f, 1.0f, 1.0f));
                mistProps.SetColor(WellCheersMaterials.PropAccentCol, new Color(0.32f, 0.88f, 0.98f, 1.0f));
                mistProps.SetColor(WellCheersMaterials.PropCoreColor, new Color(0.98f, 0.96f, 1.0f, 1.0f));
                mistProps.SetFloat(WellCheersMaterials.PropProgress, prog);
                mistProps.SetFloat(WellCheersMaterials.PropAgeSecs, ageTicks / 60f);
                mistProps.SetFloat(WellCheersMaterials.PropAlpha, alpha);
                mistProps.SetFloat(WellCheersMaterials.PropMode, 1.0f);

                const float diameter = 1.90f;
                Matrix4x4 mat = Matrix4x4.TRS(new Vector3(lastPos.x, alt, lastPos.z), Quaternion.identity, new Vector3(diameter, 1f, diameter));
                Graphics.DrawMesh(MeshPool.plane10, mat, WellCheersMaterials.SodaSplashMat, 0, null, 0, mistProps);
            }

            WellCheersVfxUtility.DrawBubbles(bubbles, prog, ageTicks, alt + 0.015f, worldOriginOffset: lastPos);
        }
    }

    /// <summary>苏打 E.G.O. 武器射击动词：在枪口喷发葡萄紫/樱桃红碳酸雾锥与飘散苏打气泡。</summary>
    public class Verb_ShootWellCheersEgo : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && Caster != null && Caster.Spawned && Caster.MapHeld != null)
            {
                Vector3 origin = Caster.DrawPos.Yto0();
                Vector3 targetPos = currentTarget.IsValid ? currentTarget.CenterVector3.Yto0() : (origin + Vector3.right);
                if (Caster is Pawn shooterPawn &&
                    !EgoWeaponAnimationManager.IsPlaying(shooterPawn, SodaWeaponAnimHandler.ClipSkillWarmup) &&
                    !EgoWeaponAnimationManager.IsPlaying(shooterPawn, SodaWeaponAnimHandler.ClipSkillSplash))
                {
                    float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(shooterPawn, targetPos);
                    EgoWeaponAnimationManager.Play(shooterPawn, shooterPawn.equipment?.Primary, SodaWeaponAnimHandler.ClipRangeShot, 22, aimAngle);
                }
                Vector3 dir = (targetPos - origin).Yto0();
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }
                dir.Normalize();

                Vector3 muzzlePos = origin + dir * 0.46f;
                WellCheersVfxUtility.SpawnMuzzleFizz(Caster.MapHeld, muzzlePos, dir);
                FleckMaker.ThrowDustPuffThick(muzzlePos, Caster.MapHeld, 0.34f, new Color(0.76f, 0.44f, 0.96f));
            }

            return fired;
        }
    }

    /// <summary>苏打 E.G.O. 碳酸能量弹：飞行沿途拖拽葡萄/樱桃微光气泡，命中时绽放碳酸飞溅特效。</summary>
    public class Projectile_WellCheersSoda : Bullet
    {
        private MaterialPropertyBlock haloProps;
        private int flightTicks;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (Destroyed || !Spawned || Map == null)
            {
                return;
            }

            flightTicks += delta;
            if (flightTicks % 2 == 0)
            {
                Color puffColor = (flightTicks % 4 == 0)
                    ? new Color(0.74f, 0.42f, 0.98f)
                    : new Color(1.0f, 0.48f, 0.72f);
                FleckMaker.ThrowDustPuffThick(ExactPosition, Map, 0.28f, puffColor);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (WellCheersMaterials.SodaBoltMat != null)
            {
                haloProps ??= new MaterialPropertyBlock();
                haloProps.SetColor(WellCheersMaterials.PropColor, new Color(0.70f, 0.36f, 1.0f, 0.90f));
                haloProps.SetColor(WellCheersMaterials.PropAccentCol, new Color(1.0f, 0.28f, 0.54f, 0.85f));
                haloProps.SetColor(WellCheersMaterials.PropCoreColor, new Color(1.0f, 0.95f, 1.0f, 1.0f));
                haloProps.SetFloat(WellCheersMaterials.PropAgeSecs, flightTicks / 60f);
                haloProps.SetFloat(WellCheersMaterials.PropAlpha, 0.88f);
                haloProps.SetFloat(WellCheersMaterials.PropIntensity, 1.35f);

                Vector3 p = drawLoc.Yto0();
                float alt = AltitudeLayer.Projectile.AltitudeFor() - 0.01f;
                Matrix4x4 mat = Matrix4x4.TRS(new Vector3(p.x, alt, p.z), Quaternion.identity, new Vector3(0.58f, 1f, 0.58f));
                Graphics.DrawMesh(MeshPool.plane10, mat, WellCheersMaterials.SodaBoltMat, 0, null, 0, haloProps);
            }

            base.DrawAt(drawLoc, flip);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = (hitThing != null && hitThing.Spawned) ? hitThing.DrawPos.Yto0() : ExactPosition.Yto0();
            bool hitLiving = hitThing is Pawn;

            base.Impact(hitThing, blockedByShield);

            if (map != null)
            {
                WellCheersVfxUtility.SpawnImpactSplash(map, impactPos, hitLiving);
            }
        }
    }

    /// <summary>苏打 E.G.O. 武器 VFX 生成与轻量气泡粒子驱动工具。</summary>
    public static class WellCheersVfxUtility
    {
        internal static void StepBubbles(List<SodaFizzBubble> bubbles, int ageTicks, float damping)
        {
            for (int i = 0; i < bubbles.Count; i++)
            {
                SodaFizzBubble b = bubbles[i];
                b.pos += b.vel;
                b.pos.x += Mathf.Sin(ageTicks * 0.35f + b.phase) * b.swayAmp;
                b.vel.x *= damping;
                b.vel.z *= damping;
                bubbles[i] = b;
            }
        }

        internal static void DrawBubbles(List<SodaFizzBubble> bubbles, float progress, int ageTicks, float altitude, Vector3? worldOriginOffset = null)
        {
            float alpha = progress < 0.15f
                ? (progress / 0.15f)
                : Mathf.Clamp01(1f - (progress - 0.35f) / 0.65f);
            if (alpha <= 0.02f)
            {
                return;
            }

            Vector3 origin = worldOriginOffset ?? Vector3.zero;
            MaterialPropertyBlock propBlock = WellCheersMaterials.GetAlphaPropBlock(alpha * 0.92f);

            for (int i = 0; i < bubbles.Count; i++)
            {
                SodaFizzBubble b = bubbles[i];
                Material mat = WellCheersMaterials.GetBubbleMaterial(b.flavor);
                if (mat == null)
                {
                    continue;
                }

                float pulse = 1f + 0.10f * Mathf.Sin(ageTicks * 0.45f + b.phase);
                float sz = b.baseSize * Mathf.Lerp(0.85f, 1.20f, progress) * pulse;
                Vector3 drawPos = new Vector3(origin.x + b.pos.x, altitude + i * 0.002f, origin.z + b.pos.z);
                Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(sz, 1f, sz));
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, propBlock);
            }
        }

        public static void SpawnMuzzleFizz(Map map, Vector3 muzzlePos, Vector3 dir)
        {
            if (map == null || WellCheersDefOf.LC_WellCheersMuzzleFizzFX == null)
            {
                return;
            }

            IntVec3 cell = muzzlePos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(WellCheersDefOf.LC_WellCheersMuzzleFizzFX, cell, map) is Thing_WellCheersMuzzleFizz fx)
            {
                fx.Initialize(muzzlePos, dir);
            }
        }

        public static void SpawnImpactSplash(Map map, Vector3 pos, bool hitLiving)
        {
            if (map == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (WellCheersDefOf.LC_WellCheersSodaSplashFX != null
                && GenSpawn.Spawn(WellCheersDefOf.LC_WellCheersSodaSplashFX, cell, map) is Thing_WellCheersSodaSplash fx)
            {
                fx.Initialize(pos, hitLiving);
            }

            FleckMaker.ThrowDustPuffThick(pos, map, 0.52f, new Color(0.76f, 0.44f, 0.98f));
        }

        public static void SpawnHealMist(Pawn recipient)
        {
            if (recipient == null || !recipient.Spawned || recipient.Map == null)
            {
                return;
            }

            if (WellCheersDefOf.LC_WellCheersHealMistFX != null
                && GenSpawn.Spawn(WellCheersDefOf.LC_WellCheersHealMistFX, recipient.Position, recipient.Map) is Thing_WellCheersHealMist fx)
            {
                fx.Initialize(recipient);
            }

            FleckMaker.ThrowDustPuffThick(recipient.DrawPos, recipient.Map, 0.65f, new Color(0.80f, 0.66f, 1.0f));
        }
    }
}
