using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace NothingThere.Effect
{
    /// <summary>拟态 E.G.O. 武器专属 GLSL 450 裂地重劈、蓄力气旋与斩击材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class MimicryMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropBoneColor = Shader.PropertyToID("_BoneColor");
        public static readonly int PropDarkColor = Shader.PropertyToID("_DarkColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropArcSpan = Shader.PropertyToID("_ArcSpan");
        public static readonly int PropHeavy = Shader.PropertyToID("_Heavy");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material CleaveMat;
        public static readonly Material GroundRuptureMat;

        static MimicryMaterials()
        {
            Shader cleaveShader = ShaderDatabase.LoadShader("Map/LC_MimicryCleave") ?? ShaderDatabase.Transparent;
            Shader ruptureShader = ShaderDatabase.LoadShader("Map/LC_MimicryGroundRupture") ?? ShaderDatabase.MoteGlow;

            CleaveMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = cleaveShader,
                color = new Color(0.92f, 0.07f, 0.11f, 1.0f)
            });

            GroundRuptureMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ruptureShader,
                color = new Color(0.95f, 0.08f, 0.12f, 1.0f)
            });
        }
    }

    /// <summary>拟态大剑血肉锯齿月牙斩击特效（自定义 GLSL LC_MimicryCleave）。</summary>
    public class Thing_MimicrySlash : Thing
    {
        private Vector3 exactPos;
        private float yawDeg;
        private float scale = 2.1f;
        private float arcSpanRad = 2.75f;
        private bool flipSweep;
        private bool isHeavy;
        private int ageTicks;
        private int durationTicks = 18;
        private bool initialized;

        private MaterialPropertyBlock slashProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float slashScale, bool flip, bool heavy = false, int lifeTicks = 18, float spanRad = 2.75f)
        {
            exactPos = pos.Yto0();
            yawDeg = yawAngleDeg;
            scale = Mathf.Max(0.9f, slashScale);
            flipSweep = flip;
            isHeavy = heavy;
            durationTicks = Mathf.Max(10, lifeTicks);
            arcSpanRad = Mathf.Clamp(spanRad, 1.6f, 3.6f);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= durationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || MimicryMaterials.CleaveMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - prog, 0.68f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            float sz = scale * Mathf.Lerp(0.86f, 1.18f, 1f - Mathf.Pow(1f - prog, 2f));

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(MimicryMaterials.PropColor, isHeavy
                ? new Color(1.0f, 0.06f, 0.12f, 1.0f)
                : new Color(0.90f, 0.08f, 0.13f, 1.0f));
            slashProps.SetColor(MimicryMaterials.PropCoreColor, new Color(0.04f, 0.004f, 0.01f, 1.0f));
            slashProps.SetColor(MimicryMaterials.PropBoneColor, isHeavy
                ? new Color(1.0f, 0.92f, 0.78f, 1.0f)
                : new Color(0.95f, 0.84f, 0.70f, 1.0f));
            slashProps.SetFloat(MimicryMaterials.PropProgress, prog);
            slashProps.SetFloat(MimicryMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(MimicryMaterials.PropAlpha, alpha);
            slashProps.SetFloat(MimicryMaterials.PropArcSpan, arcSpanRad);
            slashProps.SetFloat(MimicryMaterials.PropHeavy, isHeavy ? 1.0f : 0.0f);

            Quaternion rot = Quaternion.AngleAxis(yawDeg, Vector3.up);
            Mesh mesh = flipSweep ? MeshPool.plane10Flip : MeshPool.plane10;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(exactPos.x, alt, exactPos.z), rot, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(mesh, matrix, MimicryMaterials.CleaveMat, 0, null, 0, slashProps);
        }
    }

    /// <summary>拟态「再见。」重劈砸地瞬间迸发的血红裂地冲击波与向前延伸裂隙（LC_MimicryGroundRupture _Mode=0）。</summary>
    public class Thing_MimicryGroundRupture : Thing
    {
        private Vector3 exactPos;
        private float yawDeg;
        private float scale = 3.2f;
        private int ageTicks;
        private int durationTicks = 28;
        private bool initialized;

        private MaterialPropertyBlock ruptureProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float ruptureScale = 3.2f, int lifeTicks = 28)
        {
            exactPos = pos.Yto0();
            yawDeg = yawAngleDeg;
            scale = Mathf.Max(1.4f, ruptureScale);
            durationTicks = Mathf.Max(14, lifeTicks);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= durationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || MimicryMaterials.GroundRuptureMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - prog, 0.85f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            float sz = scale * Mathf.Lerp(0.72f, 1.24f, 1f - Mathf.Pow(1f - prog, 2.2f));

            ruptureProps ??= new MaterialPropertyBlock();
            ruptureProps.SetColor(MimicryMaterials.PropColor, new Color(0.96f, 0.08f, 0.12f, 1.0f));
            ruptureProps.SetColor(MimicryMaterials.PropCoreColor, new Color(1.0f, 0.80f, 0.62f, 1.0f));
            ruptureProps.SetColor(MimicryMaterials.PropDarkColor, new Color(0.06f, 0.005f, 0.01f, 1.0f));
            ruptureProps.SetFloat(MimicryMaterials.PropProgress, prog);
            ruptureProps.SetFloat(MimicryMaterials.PropAgeSecs, ageTicks / 60f);
            ruptureProps.SetFloat(MimicryMaterials.PropAlpha, alpha);
            ruptureProps.SetFloat(MimicryMaterials.PropMode, 0f);

            Quaternion rot = Quaternion.AngleAxis(yawDeg, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(exactPos.x, alt, exactPos.z), rot, new Vector3(sz, 1f, sz * 0.85f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, MimicryMaterials.GroundRuptureMat, 0, null, 0, ruptureProps);
        }
    }

    /// <summary>拟态「再见。」蓄力高举阶段围绕施放者的收束血气旋涡（LC_MimicryGroundRupture _Mode=1）。</summary>
    public class Thing_MimicryChargeAura : Thing
    {
        private Pawn caster;
        private Verb warmupVerb;
        private int ageTicks;
        private int totalTicks = 72;
        private bool initialized;

        private MaterialPropertyBlock auraProps;

        public void Initialize(Pawn casterPawn, Verb requiredWarmupVerb, int warmupTicks)
        {
            caster = casterPawn;
            warmupVerb = requiredWarmupVerb;
            totalTicks = Mathf.Max(12, warmupTicks);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned)
            {
                Destroy();
                return;
            }

            if (warmupVerb != null)
            {
                if (!(caster.stances?.curStance is Stance_Warmup sw) || sw.verb != warmupVerb)
                {
                    Destroy();
                    return;
                }
            }

            Position = caster.Position.ClampInsideMap(Map);
            if (++ageTicks >= totalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned || MimicryMaterials.GroundRuptureMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, totalTicks));
            float alpha = Mathf.Clamp01(ageTicks / 10f) * 0.88f;
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 anchor = caster.DrawPos.Yto0();
            float alt = AltitudeLayer.Blueprint.AltitudeFor() + 0.04f;
            float sz = Mathf.Lerp(2.15f, 1.55f, prog);

            auraProps ??= new MaterialPropertyBlock();
            auraProps.SetColor(MimicryMaterials.PropColor, new Color(0.92f, 0.07f, 0.12f, 1.0f));
            auraProps.SetColor(MimicryMaterials.PropCoreColor, new Color(1.0f, 0.72f, 0.56f, 1.0f));
            auraProps.SetColor(MimicryMaterials.PropDarkColor, new Color(0.05f, 0.005f, 0.01f, 1.0f));
            auraProps.SetFloat(MimicryMaterials.PropProgress, prog);
            auraProps.SetFloat(MimicryMaterials.PropAgeSecs, ageTicks / 60f);
            auraProps.SetFloat(MimicryMaterials.PropAlpha, alpha);
            auraProps.SetFloat(MimicryMaterials.PropMode, 1f);

            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(anchor.x, alt, anchor.z), Quaternion.identity, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, MimicryMaterials.GroundRuptureMat, 0, null, 0, auraProps);
        }
    }

    /// <summary>拟态 E.G.O. 特效生成辅助类。</summary>
    public static class MimicryVfxUtility
    {
        public static void SpawnSlash(Map map, Vector3 pos, float yawAngleDeg, float scale = 2.15f, bool flipSweep = false, bool heavy = false, int lifeTicks = 18, float spanRad = 2.75f)
        {
            if (map == null || Def.ThingDefOf.LC_MimicrySlashFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Def.ThingDefOf.LC_MimicrySlashFX, cell, map) is Thing_MimicrySlash slash)
            {
                slash.Initialize(pos, yawAngleDeg, scale, flipSweep, heavy, lifeTicks, spanRad);
            }
        }

        public static void SpawnGroundRupture(Map map, Vector3 pos, float yawAngleDeg, float scale = 3.2f, int lifeTicks = 28)
        {
            if (map == null || Def.ThingDefOf.LC_MimicryGroundRuptureFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Def.ThingDefOf.LC_MimicryGroundRuptureFX, cell, map) is Thing_MimicryGroundRupture rupture)
            {
                rupture.Initialize(pos, yawAngleDeg, scale, lifeTicks);
            }
        }

        public static void SpawnChargeAura(Pawn caster, Verb warmupVerb, int warmupTicks)
        {
            if (caster == null || caster.MapHeld == null || Def.ThingDefOf.LC_MimicryChargeAuraFX == null)
            {
                return;
            }

            IntVec3 cell = caster.PositionHeld.ClampInsideMap(caster.MapHeld);
            if (!cell.InBounds(caster.MapHeld))
            {
                return;
            }

            if (GenSpawn.Spawn(Def.ThingDefOf.LC_MimicryChargeAuraFX, cell, caster.MapHeld) is Thing_MimicryChargeAura aura)
            {
                aura.Initialize(caster, warmupVerb, warmupTicks);
            }
        }
    }

    /// <summary>
    /// 拟态（EgoWeapon_NothingThere）E.G.O. 武器持握姿势、常规斜劈（Attack_02）与
    /// 「再见。」后仰蓄力高举→爆发前冲砸地全套动画处理器（还原参考视频与 NothingWeapon Spine 数据）。
    /// 注：EgoWeapon_NothingThere.png 刀柄在左下、刀尖指向右上 45°（angle=0° 右上、+45° 水平向前、-45° 垂直向上、-118° 脑后高举）。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MimicryWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_NothingThere";

        public const string ClipNormalCleave = "Mimicry_NormalCleave";
        public const string ClipGoodbyeWindup = "Mimicry_GoodbyeWindup";
        public const string ClipGoodbyeSlam = "Mimicry_GoodbyeSlam";

        private static readonly Vector2 BladeDrawSize = new Vector2(2.15f, 2.15f);
        private static readonly Color CrimsonGhostTint = new Color(0.98f, 0.14f, 0.18f, 0.85f);

        // === Normal Cleave (54 ticks, Attack_02): fast diagonal overhead-to-ground chop ===
        private static readonly BodyKeyframe[] NormalCleaveBody =
        {
            new BodyKeyframe(0f, -0.05f, 0f, -5f),
            new BodyKeyframe(8f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(14f, 0.22f, -0.02f, 18f),
            new BodyKeyframe(28f, 0.16f, -0.01f, 12f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalCleaveSword =
        {
            new WeaponKeyframe(0f, 0.34f, -0.16f, 54f, 1.0f),
            new WeaponKeyframe(8f, 0.06f, 0.56f, -82f, 1.06f),
            new WeaponKeyframe(14f, 0.66f, -0.18f, 52f, 1.12f),
            new WeaponKeyframe(28f, 0.62f, -0.22f, 58f, 1.04f),
            new WeaponKeyframe(54f, 0.34f, -0.16f, 56f, 1.0f)
        };

        // === "Goodbye." Windup (normalized 0..1, Attack_01 0..1.33s): dramatic backstep + backward lean + overhead hoist behind head ===
        private static readonly BodyKeyframe[] GoodbyeWindupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.25f, -0.08f, 0.01f, -10f),
            new BodyKeyframe(0.55f, -0.15f, 0.02f, -19f),
            new BodyKeyframe(1.00f, -0.18f, 0.02f, -22f)
        };

        private static readonly WeaponKeyframe[] GoodbyeWindupSword =
        {
            new WeaponKeyframe(0.00f, 0.34f, -0.16f, 56f, 1.00f),
            new WeaponKeyframe(0.28f, 0.22f, 0.34f, -48f, 1.04f),
            new WeaponKeyframe(0.60f, -0.08f, 0.56f, -96f, 1.09f),
            new WeaponKeyframe(1.00f, -0.18f, 0.58f, -118f, 1.12f)
        };

        // === "Goodbye." Slam (78 ticks, Attack_01 1.35..2.67s): explosive lunge pinning blade flat onto earth + weighty hold + drag-back ===
        private static readonly BodyKeyframe[] GoodbyeSlamBody =
        {
            new BodyKeyframe(0f, -0.18f, 0.02f, -22f),
            new BodyKeyframe(4f, 0.08f, 0.00f, 10f),
            new BodyKeyframe(8f, 0.30f, -0.05f, 36f),
            new BodyKeyframe(40f, 0.28f, -0.04f, 32f),
            new BodyKeyframe(56f, 0.14f, -0.02f, 16f),
            new BodyKeyframe(78f, 0.00f, 0.00f, 0f)
        };

        private static readonly WeaponKeyframe[] GoodbyeSlamSword =
        {
            new WeaponKeyframe(0f, -0.18f, 0.58f, -118f, 1.12f),
            new WeaponKeyframe(4f, 0.34f, 0.42f, -28f, 1.18f),
            new WeaponKeyframe(8f, 0.68f, -0.24f, 46f, 1.16f),
            new WeaponKeyframe(40f, 0.68f, -0.24f, 46f, 1.06f),
            new WeaponKeyframe(58f, 0.52f, -0.22f, 52f, 1.02f),
            new WeaponKeyframe(78f, 0.34f, -0.16f, 56f, 1.00f)
        };

        /// <summary>判断是否仍处于「再见。」蓄力或主砸地动作锁定窗内（恢复收尾后允许衔接下一次普攻）。</summary>
        public static bool IsLockedInGoodbyeSlam(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (EgoWeaponAnimationManager.IsPlaying(pawn, ClipGoodbyeWindup))
            {
                return true;
            }

            if (EgoWeaponAnimationManager.TryGetActive(pawn, out EgoWeaponAnimPlayback active) &&
                string.Equals(active.ClipId, ClipGoodbyeSlam, System.StringComparison.Ordinal))
            {
                int elapsed = (Find.TickManager?.TicksGame ?? active.StartTick) - active.StartTick;
                return elapsed < 52;
            }

            return false;
        }

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null || IsLockedInGoodbyeSlam(attacker))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalCleave, 54, aimAngle);
        }

        public EgoWeaponBodyPose EvaluateBodyPose(EgoWeaponAnimPlayback playback, float elapsedTicks)
        {
            if (playback == null)
            {
                return EgoWeaponBodyPose.None;
            }

            BodyKeyframe kf;
            switch (playback.ClipId)
            {
                case ClipNormalCleave:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalCleaveBody, elapsedTicks);
                    break;
                case ClipGoodbyeWindup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(GoodbyeWindupBody, prog);
                    break;
                }
                case ClipGoodbyeSlam:
                    kf = EgoWeaponAnimationUtility.SampleBody(GoodbyeSlamBody, elapsedTicks);
                    break;
                default:
                    return EgoWeaponBodyPose.None;
            }

            return EgoWeaponAnimationUtility.ToWorldBodyPose(kf.OffsetX, kf.OffsetZ, kf.LeanDeg, playback.FacingLeft);
        }

        public bool TryDrawWeapon(in EgoWeaponDrawContext ctx)
        {
            Material mat = ctx.Weapon.Graphic?.MatSingleFor(ctx.Weapon);
            if (mat == null)
            {
                return false;
            }

            float alt = ctx.BaseDrawPos.y + 0.015f;

            if (ctx.ActiveAnim == null)
            {
                bool isMoving = ctx.Pawn.pather != null && ctx.Pawn.pather.Moving;
                float carryX = isMoving ? 0.16f : 0.34f;
                float carryZ = isMoving ? 0.08f : -0.16f;
                float carryAngle = isMoving ? -16f : 56f;

                EgoWeaponAnimationUtility.DrawWeaponSprite(
                    mat,
                    ctx.BaseDrawPos,
                    alt,
                    carryX,
                    carryZ,
                    carryAngle,
                    BladeDrawSize,
                    ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalCleave:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalCleaveSword, t);
                    if (t >= 7f && t <= 19f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalCleaveSword, t, ctx.BaseDrawPos, alt, BladeDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipGoodbyeWindup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(GoodbyeWindupSword, prog);
                    float shake = prog > 0.55f ? Mathf.Sin(t * 1.9f) * 0.022f : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + shake, kf.PosZ + shake * 0.5f, kf.AngleDeg, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipGoodbyeSlam:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(GoodbyeSlamSword, t);
                    if (t >= 1f && t <= 14f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, GoodbyeSlamSword, t, ctx.BaseDrawPos, alt, BladeDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                    }
                    // Heavy impact tremor right as the giant greatsword slams into the earth
                    float impactShake = (t >= 8f && t <= 16f) ? Mathf.Sin(t * 2.6f) * 0.025f * (1f - (t - 8f) / 8f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + impactShake, kf.AngleDeg, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}

namespace NothingThere.Combat
{
    /// <summary>
    /// 拟态主动技能「再见。」起手动词：进入读条瞬间触发大幅后仰+脑后高举巨剑动画与血气聚拢特效。
    /// </summary>
    public class Verb_CastAbilityTouch_MimicryGoodbye : Verb_CastAbilityTouch
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && Caster is Pawn casterPawn && casterPawn.equipment?.Primary != null &&
                casterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (casterPawn.DrawPos + Vector3.right);
                float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                int warmupTicks = Mathf.Max(12, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    NothingThere.Effect.MimicryWeaponAnimHandler.ClipGoodbyeWindup,
                    warmupTicks,
                    aimAngle,
                    requiredWarmupVerb: this);

                NothingThere.Effect.MimicryVfxUtility.SpawnChargeAura(casterPawn, this, warmupTicks);
                LCAbnormalitiesHelper.PlaySoundAt(Def.SoundDefOf.NothingThere_GoodbyeCast, casterPawn);
            }

            return started;
        }
    }
}
