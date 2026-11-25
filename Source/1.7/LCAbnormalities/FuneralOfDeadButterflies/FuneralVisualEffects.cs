using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.FuneralOfDeadButterflies
{
    /// <summary>亡蝶葬仪与圣宣 E.G.O. 共用的蝴蝶绽放、群蝶飞散及棺材入葬材质缓存与特效工具。</summary>
    [StaticConstructorOnStartup]
    public static class FuneralVfxUtility
    {
        public static readonly Material[] BloomWhiteMats;
        public static readonly Material[] BloomBlackMats;
        public static readonly Material[] FlutterWhiteMats;
        public static readonly Material[] FlutterBlackMats;

        public static readonly Material StreamRibbonMat;
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropDarkColor = Shader.PropertyToID("_DarkColor");
        public static readonly int PropHeadProgress = Shader.PropertyToID("_HeadProgress");
        public static readonly int PropTailProgress = Shader.PropertyToID("_TailProgress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        public static readonly Material DeadCoffinBackMat;
        public static readonly Material DeadCoffinFrontMat;
        public static readonly Material DeadCoffinLidMat;
        public static readonly Material DeadBodyMat;

        private const int AlphaBucketCount = 24;
        private static readonly MaterialPropertyBlock[] AlphaPropBlocks;

        private static readonly Dictionary<int, bool> LauncherBlackToggle = new Dictionary<int, bool>();

        static FuneralVfxUtility()
        {
            BloomWhiteMats = new[]
            {
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_White_0", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_White_1", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_White_2", ShaderDatabase.Transparent),
            };
            BloomBlackMats = new[]
            {
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_Black_0", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_Black_1", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyBloom_Black_2", ShaderDatabase.Transparent),
            };
            FlutterWhiteMats = new[]
            {
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_White_0", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_White_1", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_White_2", ShaderDatabase.Transparent),
            };
            FlutterBlackMats = new[]
            {
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_Black_0", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_Black_1", ShaderDatabase.Transparent),
                MaterialPool.MatFrom("Effects/FuneralOfDeadButterflies/ButterflyFlutter_Black_2", ShaderDatabase.Transparent),
            };

            Shader streamShader = ShaderDatabase.LoadShader("Map/LC_FuneralButterflyStream") ?? ShaderDatabase.Transparent;
            StreamRibbonMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = streamShader,
                color = new Color(0.92f, 0.94f, 0.98f, 0.85f)
            });

            DeadCoffinBackMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinBack", ShaderDatabase.Transparent);
            DeadCoffinFrontMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinFront", ShaderDatabase.Transparent);
            DeadCoffinLidMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinLid", ShaderDatabase.Transparent);
            DeadBodyMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_Body", ShaderDatabase.Transparent);

            AlphaPropBlocks = new MaterialPropertyBlock[AlphaBucketCount];
            for (int i = 0; i < AlphaBucketCount; i++)
            {
                float a = i / (float)(AlphaBucketCount - 1);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, a));
                AlphaPropBlocks[i] = block;
            }
        }

        /// <summary>按量化透明度返回预分配的只读 MaterialPropertyBlock，避免多实例同帧绘制相互覆写。</summary>
        public static MaterialPropertyBlock GetAlphaPropBlock(float alpha)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alpha) * (AlphaBucketCount - 1)), 0, AlphaBucketCount - 1);
            return AlphaPropBlocks[idx];
        }

        /// <summary>根据水平飞行方向计算蝴蝶贴图（原图触角朝右）对应的网格与 Y 轴旋转角，确保任意 360° 方向均头朝前且翅膀朝上。</summary>
        public static void ComputeButterflyOrientation(Vector3 moveDir, out Mesh mesh, out float angleDeg)
        {
            Vector3 d = moveDir.Yto0();
            if (d.sqrMagnitude < 0.0001f)
            {
                mesh = MeshPool.plane10;
                angleDeg = 0f;
                return;
            }

            d.Normalize();
            if (d.x >= 0f)
            {
                mesh = MeshPool.plane10;
                angleDeg = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            }
            else
            {
                mesh = MeshPool.plane10Flip;
                angleDeg = Mathf.Atan2(d.z, -d.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>按发射者交替返回黑白双枪属性，实现圣宣白/黑双蝶交替绽放。</summary>
        public static bool NextShotIsBlack(Thing launcher)
        {
            if (launcher == null)
            {
                return Rand.Bool;
            }

            if (LauncherBlackToggle.Count > 256)
            {
                LauncherBlackToggle.Clear();
            }

            int id = launcher.thingIDNumber;
            bool next = !LauncherBlackToggle.TryGetValue(id, out bool prev) ? false : !prev;
            LauncherBlackToggle[id] = next;
            return next;
        }

        /// <summary>在目标点生成原版脑叶公司风格的 3 帧蝴蝶绽放特效，并伴随数只飞散小蝶。</summary>
        public static void SpawnTargetButterflyBloom(Map map, Vector3 worldPos, bool isBlack, float scale = 1.0f, int flutterCount = 2)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyBloom == null)
            {
                return;
            }

            IntVec3 cell = worldPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            Vector3 jitter = new Vector3(Rand.Range(-0.16f, 0.16f), 0f, Rand.Range(-0.14f, 0.18f));
            if (GenSpawn.Spawn(FuneralDefOf.LC_SolemnLamentButterflyBloom, cell, map) is SolemnLamentButterflyBloom bloom)
            {
                bloom.Init(worldPos + jitter, isBlack, scale * Rand.Range(0.92f, 1.12f), Rand.Range(-20f, 20f));
            }

            if (FuneralDefOf.LC_SolemnLamentButterflyFlutter != null && flutterCount > 0)
            {
                for (int i = 0; i < flutterCount; i++)
                {
                    float ang = Rand.Range(0f, Mathf.PI * 2f);
                    float spd = Rand.Range(0.022f, 0.052f);
                    Vector3 vel = new Vector3(Mathf.Cos(ang) * spd, 0f, Mathf.Sin(ang) * spd + 0.015f);
                    bool flutterBlack = i == 0 ? isBlack : (Rand.Value < 0.45f ? !isBlack : isBlack);
                    SpawnFlutterButterfly(map, worldPos + jitter * 0.5f, vel, flutterBlack, Rand.Range(0.32f, 0.48f), Rand.Range(26, 40));
                }
            }
        }

        /// <summary>在枪口位置生成一缕短暂黑白蝶影，替代原版火药枪口火光。</summary>
        public static void SpawnMuzzleFlutter(Map map, Vector3 origin, Vector3 direction, bool isBlack)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyFlutter == null)
            {
                return;
            }

            Vector3 dir = direction.Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            Vector3 spawnPos = origin + dir * 0.42f;
            Vector3 vel = dir * Rand.Range(0.03f, 0.055f) + new Vector3(Rand.Range(-0.012f, 0.012f), 0f, Rand.Range(0.005f, 0.020f));
            SpawnFlutterButterfly(map, spawnPos, vel, isBlack, Rand.Range(0.28f, 0.38f), Rand.Range(14, 22));
        }

        /// <summary>开棺爆发期间释放沿直线席卷前方的密集黑白蝴蝶洪流（底带哀悼气流 + 高密度振翅亡蝶群）。</summary>
        public static SolemnLamentButterflyStream SpawnCoffinButterflyStream(
            Map map,
            Vector3 startPos,
            Vector3 direction,
            float length = 13f,
            float halfWidth = 1.65f,
            int emitDurationTicks = 80)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyStream == null)
            {
                return null;
            }

            IntVec3 cell = startPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return null;
            }

            if (GenSpawn.Spawn(FuneralDefOf.LC_SolemnLamentButterflyStream, cell, map) is SolemnLamentButterflyStream stream)
            {
                stream.Init(startPos, direction, length, halfWidth, emitDurationTicks);
                return stream;
            }

            return null;
        }

        /// <summary>开棺爆发 / 死亡入棺期间释放向四周飞舞的零散亡蝶群。</summary>
        public static void SpawnCoffinSwarmWave(Map map, Vector3 origin, bool facingLeft, int count = 5)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyFlutter == null)
            {
                return;
            }

            float baseDirX = facingLeft ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                float spreadAng = Rand.Range(-1.15f, 1.15f);
                float spd = Rand.Range(0.06f, 0.12f);
                Vector3 vel = new Vector3(Mathf.Cos(spreadAng) * baseDirX * spd, 0f, Mathf.Sin(spreadAng) * spd + 0.012f);
                Vector3 start = origin + new Vector3(baseDirX * Rand.Range(0.15f, 0.45f), 0f, Rand.Range(-0.25f, 0.35f));
                bool isBlack = Rand.Value < 0.30f;
                SpawnFlutterButterfly(map, start, vel, isBlack, Rand.Range(0.45f, 0.68f), Rand.Range(36, 56));
            }
        }

        public static void SpawnFlutterButterfly(Map map, Vector3 worldPos, Vector3 velocityPerTick, bool isBlack, float scale, int lifetimeTicks)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyFlutter == null)
            {
                return;
            }

            IntVec3 cell = worldPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FuneralDefOf.LC_SolemnLamentButterflyFlutter, cell, map) is SolemnLamentButterflyFlutter fx)
            {
                fx.Init(worldPos, velocityPerTick, isBlack, scale, lifetimeTicks);
            }
        }
    }

    /// <summary>
    /// 圣宣 / 亡蝶葬仪目标点 3 帧蝴蝶绽放特效（合翼 -> 展翼 -> 柔光盛放消散）。
    /// 使用解包 ButterflyAttackEffect 三帧贴图与预分配 MaterialPropertyBlock 实现无 GC 透明淡出。
    /// </summary>
    public class SolemnLamentButterflyBloom : Thing
    {
        private Vector3 exactPos;
        private bool isBlack;
        private float baseScale = 1.0f;
        private float angleDeg;
        private int ageTicks;
        private int lifetimeTicks = 22;

        public void Init(Vector3 pos, bool black, float scale, float angle)
        {
            exactPos = pos;
            isBlack = black;
            baseScale = scale;
            angleDeg = angle;
            ageTicks = 0;
            lifetimeTicks = 22;
        }

        protected override void Tick()
        {
            ageTicks++;
            if (ageTicks >= lifetimeTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, lifetimeTicks));
            Material[] frames = isBlack ? FuneralVfxUtility.BloomBlackMats : FuneralVfxUtility.BloomWhiteMats;
            if (frames == null || frames.Length < 3)
            {
                return;
            }

            // 帧 0（收拢竖翼 0..0.22）-> 帧 1（清晰展翼 0.22..0.58）-> 帧 2（盛放柔光消散 0.58..1.0）
            int frameIdx = progress < 0.22f ? 0 : (progress < 0.58f ? 1 : 2);
            Material mat = frames[frameIdx];
            if (mat == null)
            {
                return;
            }

            float scaleCurve = frameIdx == 0
                ? Mathf.Lerp(0.70f, 1.02f, progress / 0.22f)
                : (frameIdx == 1
                    ? Mathf.Lerp(1.02f, 1.24f, (progress - 0.22f) / 0.36f)
                    : Mathf.Lerp(1.24f, 1.50f, (progress - 0.58f) / 0.42f));

            float alpha = progress < 0.12f
                ? Mathf.Lerp(0.45f, 1f, progress / 0.12f)
                : (progress < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.55f) / 0.45f));

            float finalScale = baseScale * 1.15f * scaleCurve;
            Vector3 pos = exactPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + (isBlack ? 0.015f : 0.025f);
            pos.z += progress * 0.14f;

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(finalScale, 1f, finalScale));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, FuneralVfxUtility.GetAlphaPropBlock(alpha));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref exactPos, "slBloomPos");
            Scribe_Values.Look(ref isBlack, "slBloomBlack");
            Scribe_Values.Look(ref baseScale, "slBloomScale", 1f);
            Scribe_Values.Look(ref angleDeg, "slBloomAngle", 0f);
            Scribe_Values.Look(ref ageTicks, "slBloomAge", 0);
            Scribe_Values.Look(ref lifetimeTicks, "slBloomLife", 22);
        }
    }

    /// <summary>3 帧振翅飞散的小型黑白亡蝶（使用解包 ButterflyEffect 三帧贴图）。</summary>
    public class SolemnLamentButterflyFlutter : Thing
    {
        private Vector3 exactPos;
        private Vector3 velocity;
        private bool isBlack;
        private float baseScale = 0.4f;
        private int ageTicks;
        private int lifetimeTicks = 34;
        private float phaseSeed;

        public void Init(Vector3 pos, Vector3 vel, bool black, float scale, int life)
        {
            exactPos = pos;
            velocity = vel;
            isBlack = black;
            baseScale = scale;
            lifetimeTicks = Mathf.Max(10, life);
            ageTicks = 0;
            phaseSeed = Rand.Range(0f, 6.28f);
        }

        protected override void Tick()
        {
            ageTicks++;
            exactPos += velocity;
            // 轻微正弦飘摇感，模拟蝴蝶振翅飞舞
            float wave = Mathf.Sin(ageTicks * 0.35f + phaseSeed) * 0.014f;
            exactPos.z += wave;
            velocity *= 0.965f;

            if (Map != null)
            {
                IntVec3 cell = exactPos.ToIntVec3();
                if (cell.InBounds(Map) && cell != Position)
                {
                    Position = cell;
                }
            }

            if (ageTicks >= lifetimeTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, lifetimeTicks));
            Material[] frames = isBlack ? FuneralVfxUtility.FlutterBlackMats : FuneralVfxUtility.FlutterWhiteMats;
            if (frames == null || frames.Length < 3)
            {
                return;
            }

            // 0 -> 1 -> 2 -> 1 循环振翅
            int cycle = (ageTicks / 3) % 4;
            int frameIdx = cycle == 3 ? 1 : cycle;
            Material mat = frames[frameIdx];
            if (mat == null)
            {
                return;
            }

            float alpha = progress < 0.15f
                ? progress / 0.15f
                : (progress > 0.65f ? Mathf.Lerp(1f, 0f, (progress - 0.65f) / 0.35f) : 1f);

            float size = baseScale * Mathf.Lerp(0.85f, 1.15f, progress);
            Vector3 pos = exactPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;

            FuneralVfxUtility.ComputeButterflyOrientation(velocity, out Mesh mesh, out float angleDeg);

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(mesh, matrix, mat, 0, null, 0, FuneralVfxUtility.GetAlphaPropBlock(alpha));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref exactPos, "slFlutPos");
            Scribe_Values.Look(ref velocity, "slFlutVel");
            Scribe_Values.Look(ref isBlack, "slFlutBlack");
            Scribe_Values.Look(ref baseScale, "slFlutScale", 0.4f);
            Scribe_Values.Look(ref ageTicks, "slFlutAge", 0);
            Scribe_Values.Look(ref lifetimeTicks, "slFlutLife", 34);
            Scribe_Values.Look(ref phaseSeed, "slFlutSeed", 0f);
        }
    }

    /// <summary>
    /// 亡蝶葬仪开棺范围攻击的“直线黑白蝴蝶洪流”专属特效实体：
    /// 1) 底层使用 GLSL 450 着色器 LC_FuneralButterflyStream 绘制沿直线奔涌的素白/墨黑哀悼气流薄纱带；
    /// 2) 上层内置轻量级粒子池（单实体统一渲染、零额外 Thing 污染），驱动 50+ 只 3 帧振翅黑白蝴蝶从棺材口笔直横扫整条直线走廊。
    /// </summary>
    public class SolemnLamentButterflyStream : Thing
    {
        private struct StreamButterfly
        {
            public float along;          // 沿直线方向已前进距离（格）
            public float maxAlong;       // 飞达终点距离（格）
            public float lateralBase;    // 横向基准偏移（格）
            public float lateralAmp;     // 横向正弦摆幅
            public float lateralFreq;    // 横向正弦频率
            public float speedPerTick;   // 沿直线前进速度（格/tick）
            public float scale;          // 蝴蝶尺寸
            public float phaseSeed;      // 振翅与摆动随机初相
            public int ageTicks;         // 已存活 tick
            public int flapDivisor;      // 振翅周期除数（2~3）
            public bool isBlack;         // 黑蝶 / 白蝶
            public float altOffset;      // 微小高度层次避免 Z-fighting
        }

        private const int MaxStreamButterflies = 96;

        private Vector3 startPos;
        private Vector3 streamDir = Vector3.right;
        private Vector3 streamRight = Vector3.back;
        private float streamLength = 13f;
        private float streamHalfWidth = 1.65f;
        private int emitDurationTicks = 80;
        private int ageTicks;
        private int emitStopTick = -1;
        private bool initialized;

        private readonly List<StreamButterfly> butterflies = new List<StreamButterfly>(MaxStreamButterflies);
        private MaterialPropertyBlock ribbonProps;

        public void Init(Vector3 origin, Vector3 direction, float length, float halfWidth, int emitTicks)
        {
            startPos = origin.Yto0();
            Vector3 d = direction.Yto0();
            if (d.sqrMagnitude < 0.0001f)
            {
                d = Vector3.right;
            }
            streamDir = d.normalized;
            streamRight = new Vector3(-streamDir.z, 0f, streamDir.x);

            streamLength = Mathf.Clamp(length, 2.5f, 24f);
            streamHalfWidth = Mathf.Clamp(halfWidth, 0.8f, 3.0f);
            emitDurationTicks = Mathf.Max(20, emitTicks);
            ageTicks = 0;
            emitStopTick = -1;
            butterflies.Clear();
            initialized = true;

            // 开棺瞬间先放出一批紧密错落的先锋蝶群，立刻勾勒出一条直线蝶潮轮廓
            for (int i = 0; i < 18; i++)
            {
                float initialAlong = Rand.Range(0f, Mathf.Min(2.4f, streamLength * 0.28f));
                SpawnInternalButterfly(initialAlong, initialBurst: true);
            }
        }

        public void StopEmitting()
        {
            if (emitStopTick < 0)
            {
                emitStopTick = ageTicks;
            }
        }

        private bool IsEmitting => emitStopTick < 0 && ageTicks < emitDurationTicks;

        private void SpawnInternalButterfly(float initialAlong = 0f, bool initialBurst = false)
        {
            if (butterflies.Count >= MaxStreamButterflies)
            {
                return;
            }

            // 使用三角分布让中心线更密集、边缘略轻盈，形成鲜明的“一条线”视觉聚焦
            float u = (Rand.Value + Rand.Value - 1f);
            float lateralLimit = streamHalfWidth * 0.62f;
            float lateral = u * lateralLimit;

            // 前进速度保证 34~50 tick 内笔直贯穿整条直线，绝不在棺口停滞
            float baseSpeed = Mathf.Max(0.24f, streamLength / 42f);
            float speed = baseSpeed * Rand.Range(0.84f, 1.24f);
            if (initialBurst)
            {
                speed *= 1.12f;
            }

            StreamButterfly b = new StreamButterfly
            {
                along = initialAlong,
                maxAlong = streamLength * Rand.Range(0.88f, 1.04f),
                lateralBase = lateral,
                lateralAmp = Rand.Range(0.08f, 0.24f),
                lateralFreq = Rand.Range(0.20f, 0.34f),
                speedPerTick = speed,
                scale = Rand.Range(0.56f, 0.92f),
                phaseSeed = Rand.Range(0f, Mathf.PI * 2f),
                ageTicks = Rand.Range(0, 6),
                flapDivisor = Rand.Bool ? 2 : 3,
                isBlack = Rand.Value < 0.32f,
                altOffset = Rand.Range(0.025f, 0.075f)
            };
            butterflies.Add(b);
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (IsEmitting)
            {
                // 每 tick 持续喷射 1~2 只，使整条直线内常驻 50~70 只高频振翅的黑白蝴蝶
                int emitCount = (ageTicks % 2 == 0) ? 2 : 1;
                for (int i = 0; i < emitCount; i++)
                {
                    SpawnInternalButterfly(Rand.Range(-0.15f, 0.35f));
                }

                // 定期在直线中远端补一小簇绽放微光，强调蝶潮冲击感
                if (ageTicks > 14 && ageTicks % 24 == 0 && streamLength >= 4f)
                {
                    float dist = Rand.Range(streamLength * 0.45f, streamLength * 0.92f);
                    Vector3 bloomPos = startPos + streamDir * dist + streamRight * Rand.Range(-0.45f, 0.45f);
                    FuneralVfxUtility.SpawnTargetButterflyBloom(Map, bloomPos, isBlack: Rand.Value < 0.38f, scale: 0.72f, flutterCount: 0);
                }
            }
            else if (emitStopTick < 0)
            {
                emitStopTick = emitDurationTicks;
            }

            for (int i = butterflies.Count - 1; i >= 0; i--)
            {
                StreamButterfly b = butterflies[i];
                b.ageTicks++;
                b.along += b.speedPerTick;
                if (b.along >= b.maxAlong)
                {
                    butterflies.RemoveAt(i);
                }
                else
                {
                    butterflies[i] = b;
                }
            }

            int ticksSinceStop = emitStopTick < 0 ? 0 : (ageTicks - emitStopTick);
            if (!IsEmitting && butterflies.Count == 0 && ticksSinceStop >= 26)
            {
                Destroy();
            }
            else if (ageTicks > emitDurationTicks + 95)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor();
            float ageSecs = ageTicks / 60f;

            // 1. 底层 GLSL 450 直线哀悼气流薄纱带（先快速推进至全长，合棺后由棺口向远端自然收尾散去）
            if (FuneralVfxUtility.StreamRibbonMat != null)
            {
                float headProgress = Mathf.Clamp01(ageTicks / 20f);
                float tailProgress = emitStopTick < 0
                    ? 0f
                    : Mathf.Clamp01((ageTicks - emitStopTick) / 34f);
                float ribbonAlpha = emitStopTick < 0
                    ? Mathf.Clamp01(ageTicks / 8f)
                    : Mathf.Clamp01(1f - (ageTicks - emitStopTick) / 38f);

                if (ribbonAlpha > 0.01f && tailProgress < 0.99f)
                {
                    ribbonProps ??= new MaterialPropertyBlock();
                    ribbonProps.SetColor(FuneralVfxUtility.PropColor, new Color(0.93f, 0.95f, 1.0f, 0.85f));
                    ribbonProps.SetColor(FuneralVfxUtility.PropDarkColor, new Color(0.04f, 0.04f, 0.07f, 0.72f));
                    ribbonProps.SetFloat(FuneralVfxUtility.PropHeadProgress, headProgress);
                    ribbonProps.SetFloat(FuneralVfxUtility.PropTailProgress, tailProgress);
                    ribbonProps.SetFloat(FuneralVfxUtility.PropAgeSecs, ageSecs);
                    ribbonProps.SetFloat(FuneralVfxUtility.PropAlpha, ribbonAlpha);

                    Vector3 mid = startPos + streamDir * (streamLength * 0.5f);
                    Quaternion ribbonRot = Quaternion.LookRotation(streamDir, Vector3.up);
                    Matrix4x4 ribbonMatrix = Matrix4x4.TRS(
                        new Vector3(mid.x, baseAlt + 0.005f, mid.z),
                        ribbonRot,
                        new Vector3(streamHalfWidth * 1.65f, 1f, streamLength));
                    Graphics.DrawMesh(MeshPool.plane10, ribbonMatrix, FuneralVfxUtility.StreamRibbonMat, 0, null, 0, ribbonProps);
                }
            }

            // 2. 上层密集直线飞驰的 3 帧振翅黑白蝴蝶群
            Material[] whiteFrames = FuneralVfxUtility.FlutterWhiteMats;
            Material[] blackFrames = FuneralVfxUtility.FlutterBlackMats;
            if (whiteFrames == null || whiteFrames.Length < 3 || blackFrames == null || blackFrames.Length < 3)
            {
                return;
            }

            FuneralVfxUtility.ComputeButterflyOrientation(streamDir, out Mesh baseMesh, out float baseAngleDeg);
            bool flipSign = streamDir.x < 0f;

            for (int i = 0; i < butterflies.Count; i++)
            {
                StreamButterfly b = butterflies[i];
                float t = Mathf.Clamp01(b.along / Mathf.Max(0.5f, b.maxAlong));

                // 离开棺口平滑淡入、抵达远端平滑消散
                float alpha = t < 0.09f
                    ? Mathf.Lerp(0.15f, 1f, t / 0.09f)
                    : (t > 0.78f ? Mathf.Lerp(1f, 0f, (t - 0.78f) / 0.22f) : 1f);
                if (alpha <= 0.02f)
                {
                    continue;
                }

                // 0 -> 1 -> 2 -> 1 振翅循环（每只独立初相，群蝶交错扑翼）
                int cycle = ((b.ageTicks + (int)(b.phaseSeed * 5f)) / b.flapDivisor) % 4;
                int frameIdx = cycle == 3 ? 1 : cycle;
                Material mat = b.isBlack ? blackFrames[frameIdx] : whiteFrames[frameIdx];
                if (mat == null)
                {
                    continue;
                }

                // 沿直线行进并伴随轻柔横向正弦起伏
                float waveOffset = Mathf.Sin(b.ageTicks * b.lateralFreq + b.phaseSeed) * b.lateralAmp;
                float waveSlope = Mathf.Cos(b.ageTicks * b.lateralFreq + b.phaseSeed) * b.lateralAmp * b.lateralFreq;
                Vector3 worldPos = startPos + streamDir * b.along + streamRight * (b.lateralBase + waveOffset);
                worldPos.y = baseAlt + b.altOffset;

                float pitchJitter = Mathf.Clamp(waveSlope * 90f, -14f, 14f) * (flipSign ? -1f : 1f);
                float size = b.scale * Mathf.Lerp(0.88f, 1.12f, t);

                Matrix4x4 matrix = Matrix4x4.TRS(
                    worldPos,
                    Quaternion.AngleAxis(baseAngleDeg + pitchJitter, Vector3.up),
                    new Vector3(size, 1f, size));
                Graphics.DrawMesh(baseMesh, matrix, mat, 0, null, 0, FuneralVfxUtility.GetAlphaPropBlock(alpha));
            }
        }
    }

    /// <summary>
    /// 圣宣（Solemn Lament）E.G.O. 专属弹射体：
    /// 不绘制任何原版实弹贴图与扬尘火花，开火时在枪口释放一缕黑白蝶影，抵达目标点后交替绽放白/黑蝴蝶特效并结算双色伤害。
    /// </summary>
    public class Projectile_SolemnLament : Bullet
    {
        private bool isBlackShot;

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            isBlackShot = FuneralVfxUtility.NextShotIsBlack(launcher);
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            if (Map != null)
            {
                Vector3 dir = (destination - origin).Yto0();
                FuneralVfxUtility.SpawnMuzzleFlutter(Map, origin, dir, isBlackShot);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 故意不渲染原版飞弹：圣宣的核心视觉表现为目标点蝴蝶绽放
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            Vector3 impactPos = hitThing != null && hitThing.Spawned ? hitThing.DrawPos : ExactPosition;

            if (map != null && pos.InBounds(map))
            {
                FuneralVfxUtility.SpawnTargetButterflyBloom(map, impactPos, isBlackShot, scale: 1.05f, flutterCount: 3);
                SoundDef bloomSound = isBlackShot ? FuneralDefOf.LC_SolemnLament_BloomBlack : FuneralDefOf.LC_SolemnLament_BloomWhite;
                if (bloomSound != null)
                {
                    bloomSound.PlayOneShot(new TargetInfo(pos, map));
                }
            }

            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
            if (!blockedByShield && def.projectile.landedEffecter != null && map != null)
            {
                def.projectile.landedEffecter.Spawn(ExactPosition.ToIntVec3(), map).Cleanup();
            }

            BattleLogEntry_RangedImpact battleLog = new BattleLogEntry_RangedImpact(
                launcher,
                hitThing,
                intendedTarget.Thing,
                equipmentDef,
                def,
                targetCoverDef);
            Find.BattleLog.Add(battleLog);

            if (hitThing != null && !blockedByShield)
            {
                bool instigatorGuilty = !(launcher is Pawn p) || !p.Drafted;
                DamageInfo dinfo = new DamageInfo(
                    DamageDef,
                    DamageAmount,
                    ArmorPenetration,
                    ExactRotation.eulerAngles.y,
                    launcher,
                    null,
                    equipmentDef,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    intendedTarget.Thing,
                    instigatorGuilty);
                dinfo.SetWeaponQuality(equipmentQuality);
                hitThing.TakeDamage(dinfo).AssociateWithLog(battleLog);
                (hitThing as Pawn)?.stances?.stagger?.Notify_BulletImpact(this);
            }

            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isBlackShot, "slIsBlackShot", false);
        }
    }

    /// <summary>
    /// 亡蝶葬仪镇压死亡演出（还原原作 Butterfly.txt dead 动画名场面）：
    /// 水平棺材置于地面开盖 -> 身体缓缓垂直浮起 -> 空中放平平移至棺材正上方 -> 缓缓沉入棺材内部（前后棺壁遮挡）-> 盖上棺盖化作异想体蛋。
    /// </summary>
    public class DyingFuneralOfDeadButterflies : LC_FX_Standard
    {
        private const int TotalDurationTicks = 210; // 3.5 秒
        private int startTick;
        private bool closedSoundPlayed;

        public override void InitWith(Pawn targetPawn)
        {
            CompAbnormality comp = targetPawn?.TryGetComp<CompAbnormality>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            startTick = Find.TickManager?.TicksGame ?? TickSpawned;
            completeTick = startTick + TotalDurationTicks;
            closedSoundPlayed = false;
            hasInited = true;

            if (Map != null && Position.InBounds(Map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Dead, this);
                FuneralVfxUtility.SpawnCoffinSwarmWave(Map, Position.ToVector3Shifted(), facingLeft: false, count: 6);
            }
        }

        protected override void Tick()
        {
            if (hasInited && Map != null && Position.InBounds(Map))
            {
                int elapsed = (Find.TickManager?.TicksGame ?? startTick) - startTick;
                if (elapsed > 0 && elapsed < 170 && elapsed % 28 == 0)
                {
                    FuneralVfxUtility.SpawnFlutterButterfly(
                        Map,
                        Position.ToVector3Shifted() + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(-0.1f, 0.5f)),
                        new Vector3(Rand.Range(-0.018f, 0.018f), 0f, Rand.Range(0.024f, 0.045f)),
                        isBlack: Rand.Value < 0.25f,
                        scale: Rand.Range(0.36f, 0.52f),
                        lifetimeTicks: 42);
                }

                if (!closedSoundPlayed && elapsed >= 175)
                {
                    closedSoundPlayed = true;
                    LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Close, this);
                    FuneralVfxUtility.SpawnTargetButterflyBloom(Map, Position.ToVector3Shifted() + new Vector3(0f, 0f, 0.05f), isBlack: false, scale: 1.25f, flutterCount: 5);
                }
            }

            base.Tick();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!hasInited)
            {
                return;
            }

            int elapsed = Mathf.Clamp((Find.TickManager?.TicksGame ?? startTick) - startTick, 0, TotalDurationTicks);
            float t = elapsed / (float)TotalDurationTicks; // 0..1

            float baseY = AltitudeLayer.Pawn.AltitudeFor();
            Vector3 center = drawLoc;
            float drawSize = 2.15f;
            float fadeOut = t > 0.88f ? Mathf.Lerp(1f, 0f, (t - 0.88f) / 0.12f) : 1f;

            // 1. 棺材底座背板（在身体后方）
            DrawLayer(FuneralVfxUtility.DeadCoffinBackMat, new Vector3(center.x, baseY, center.z), 0f, drawSize, fadeOut);

            // 2. 身体动画轨迹（起立 -> 垂直浮空 -> 90°放平平移 -> 缓缓沉入棺材内）
            bool drawBody = t < 0.80f;
            if (drawBody)
            {
                float bodyOffX = 0f;
                float bodyOffZ = 0f;
                float bodyAngle = 0f;
                float bodyAlpha = 1f;

                if (t < 0.16f)
                {
                    // 阶段 1：立于棺材旁
                    float u = t / 0.16f;
                    bodyOffX = -0.55f;
                    bodyOffZ = Mathf.SmoothStep(0.18f, 0.24f, u);
                }
                else if (t < 0.40f)
                {
                    // 阶段 2：直挺挺缓缓浮起
                    float u = (t - 0.16f) / 0.24f;
                    bodyOffX = -0.55f;
                    bodyOffZ = Mathf.SmoothStep(0.24f, 0.92f, u);
                }
                else if (t < 0.58f)
                {
                    // 阶段 3：在空中放平并平移到棺材正上方
                    float u = (t - 0.40f) / 0.18f;
                    float s = Mathf.SmoothStep(0f, 1f, u);
                    bodyOffX = Mathf.Lerp(-0.55f, 0.02f, s);
                    bodyOffZ = Mathf.Lerp(0.92f, 0.84f, s);
                    bodyAngle = Mathf.Lerp(0f, 90f, s);
                }
                else
                {
                    // 阶段 4：水平缓缓降入棺材内部并被棺壁遮挡
                    float u = (t - 0.58f) / 0.22f;
                    float s = Mathf.SmoothStep(0f, 1f, u);
                    bodyOffX = 0.02f;
                    bodyOffZ = Mathf.Lerp(0.84f, -0.36f, s);
                    bodyAngle = 90f;
                    bodyAlpha = u > 0.72f ? Mathf.Lerp(1f, 0f, (u - 0.72f) / 0.28f) : 1f;
                }

                DrawLayer(
                    FuneralVfxUtility.DeadBodyMat,
                    new Vector3(center.x + bodyOffX, baseY + 0.02f, center.z + bodyOffZ),
                    bodyAngle,
                    drawSize * 0.92f,
                    bodyAlpha * fadeOut);
            }

            // 3. 棺材前壁（在身体前方，营造身体沉入棺材内部的真实遮挡感）
            DrawLayer(FuneralVfxUtility.DeadCoffinFrontMat, new Vector3(center.x, baseY + 0.04f, center.z), 0f, drawSize, fadeOut);

            // 4. 棺材盖（阶段 1 滑落在一旁；阶段 5 从下方缓缓合回棺口封棺）
            float lidOffZ;
            if (t < 0.16f)
            {
                lidOffZ = Mathf.SmoothStep(0.35f, 0.0f, t / 0.16f);
            }
            else if (t < 0.74f)
            {
                lidOffZ = 0.0f;
            }
            else if (t < 0.86f)
            {
                lidOffZ = Mathf.SmoothStep(0.0f, 0.35f, (t - 0.74f) / 0.12f);
            }
            else
            {
                lidOffZ = 0.35f;
            }

            DrawLayer(FuneralVfxUtility.DeadCoffinLidMat, new Vector3(center.x, baseY + 0.06f, center.z + lidOffZ), 0f, drawSize, fadeOut);
        }

        private void DrawLayer(Material mat, Vector3 pos, float angleDeg, float size, float alpha)
        {
            if (mat == null || alpha <= 0.005f)
            {
                return;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, FuneralVfxUtility.GetAlphaPropBlock(alpha));
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            EggReturnExtension ext = def.GetModExtension<EggReturnExtension>();
            if (ext?.eggDef != null && MapHeld != null && PositionHeld.InBounds(MapHeld))
            {
                Thing egg = ThingMaker.MakeThing(ext.eggDef);
                AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, PositionHeld, MapHeld);
            }

            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "fdbDeadStartTick", 0);
            Scribe_Values.Look(ref closedSoundPlayed, "fdbDeadClosedSound", false);
        }
    }
}
