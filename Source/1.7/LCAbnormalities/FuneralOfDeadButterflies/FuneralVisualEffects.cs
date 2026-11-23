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

        public static readonly Material DeadCoffinBackMat;
        public static readonly Material DeadCoffinFrontMat;
        public static readonly Material DeadCoffinLidMat;
        public static readonly Material DeadBodyMat;

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

            DeadCoffinBackMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinBack", ShaderDatabase.Transparent);
            DeadCoffinFrontMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinFront", ShaderDatabase.Transparent);
            DeadCoffinLidMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_CoffinLid", ShaderDatabase.Transparent);
            DeadBodyMat = MaterialPool.MatFrom("Things/Pawn/FuneralOfDeadButterflies/Parts/Dead_Body", ShaderDatabase.Transparent);
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

        /// <summary>开棺爆发期间释放向四周/前方飞舞的亡蝶群。</summary>
        public static void SpawnCoffinSwarmWave(Map map, Vector3 origin, bool facingLeft, int count = 5)
        {
            if (map == null || FuneralDefOf.LC_SolemnLamentButterflyFlutter == null)
            {
                return;
            }

            float baseDirX = facingLeft ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                float spreadAng = Rand.Range(-1.35f, 1.35f);
                float spd = Rand.Range(0.05f, 0.11f);
                Vector3 vel = new Vector3(Mathf.Cos(spreadAng) * baseDirX * spd, 0f, Mathf.Sin(spreadAng) * spd + 0.012f);
                Vector3 start = origin + new Vector3(baseDirX * Rand.Range(0.15f, 0.45f), 0f, Rand.Range(-0.25f, 0.35f));
                bool isBlack = Rand.Value < 0.30f;
                SpawnFlutterButterfly(map, start, vel, isBlack, Rand.Range(0.40f, 0.62f), Rand.Range(36, 56));
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
    /// 使用解包 ButterflyAttackEffect 三帧贴图与 MaterialPropertyBlock 实现无 GC 透明淡出。
    /// </summary>
    public class SolemnLamentButterflyBloom : Thing
    {
        private Vector3 exactPos;
        private bool isBlack;
        private float baseScale = 1.0f;
        private float angleDeg;
        private int ageTicks;
        private int lifetimeTicks = 22;

        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

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

            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(finalScale, 1f, finalScale));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, propBlock);
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

        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

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

            bool faceRight = velocity.x >= 0f;
            Mesh mesh = faceRight ? MeshPool.plane10Flip : MeshPool.plane10;
            float tilt = Mathf.Clamp(velocity.z * 220f, -22f, 22f);

            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(tilt, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(mesh, matrix, mat, 0, null, 0, propBlock);
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
        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

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

            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, propBlock);
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
