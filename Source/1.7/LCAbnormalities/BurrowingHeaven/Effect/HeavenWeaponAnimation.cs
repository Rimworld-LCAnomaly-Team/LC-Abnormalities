using System.Collections.Generic;
using BurrowingHeaven.Def;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BurrowingHeaven.Effect
{
    /// <summary>
    /// F-04-42 穿刺乐园 E.G.O. 武器（天堂）定制 GLSL 着色器与材质缓存。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HeavenMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material ThornsMat;

        static HeavenMaterials()
        {
            Shader thornsShader = ShaderDatabase.LoadShader("Map/LC_HeavenThorns") ?? ShaderDatabase.Transparent;
            ThornsMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = thornsShader,
                color = new Color(0.88f, 0.12f, 0.16f, 1.0f)
            });
        }
    }

    /// <summary>
    /// 天堂（EgoWeapon_BurrowingHeaven）专属武器动画处理器：
    /// 贴图原图呈 0° 水平朝右（左为细长暗红枪柄，右为双叉荆棘枪尖）。
    /// 支持锐利前刺突进（ClipNormalThrust）、蓄力背弓蓄势（ClipSkillWarmup）以及主动技能「地棘贯狱」的双拍贯穿与根刺爆发。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class HeavenWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_BurrowingHeaven";

        public const string ClipNormalThrust = "Heaven_NormalThrust";
        public const string ClipSkillWarmup = "Heaven_SkillWarmup";
        public const string ClipSkillSkewer = "Heaven_SkillSkewer";

        private static readonly Vector2 SpearDrawSize = new Vector2(2.15f, 2.15f);
        private static readonly Color CrimsonAfterimageTint = new Color(0.92f, 0.16f, 0.22f, 0.85f);

        // === Normal Thrust (54 ticks): windup pullback -> lightning spear lunge -> crisp recovery ===
        private static readonly BodyKeyframe[] NormalThrustBody =
        {
            new BodyKeyframe(0f, -0.05f, 0f, -5f),
            new BodyKeyframe(10f, -0.14f, 0.01f, -12f),
            new BodyKeyframe(17f, 0.24f, 0f, 18f),
            new BodyKeyframe(30f, 0.16f, 0f, 12f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalThrustSpear =
        {
            new WeaponKeyframe(0f, 0.24f, -0.08f, -12f, 1.00f),
            new WeaponKeyframe(10f, -0.10f, -0.04f, 8f, 1.05f),
            new WeaponKeyframe(17f, 0.88f, -0.02f, -2f, 1.14f),
            new WeaponKeyframe(29f, 0.72f, -0.04f, 2f, 1.06f),
            new WeaponKeyframe(54f, 0.24f, -0.08f, -14f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): coil low, pull twin prongs back behind hip ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.35f, -0.10f, -0.01f, -10f),
            new BodyKeyframe(1.00f, -0.18f, -0.02f, -16f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupSpear =
        {
            new WeaponKeyframe(0.00f, 0.24f, -0.08f, -14f, 1.00f),
            new WeaponKeyframe(0.45f, -0.08f, -0.02f, 8f, 1.06f),
            new WeaponKeyframe(1.00f, -0.22f, 0.02f, 14f, 1.12f)
        };

        // === Skill Skewer (90 ticks): long lance surge -> plant spear upright -> root spire detonation @ tick 20 ===
        private static readonly BodyKeyframe[] SkillSkewerBody =
        {
            new BodyKeyframe(0f, -0.18f, -0.02f, -16f),
            new BodyKeyframe(7f, 0.34f, 0.01f, 24f),
            new BodyKeyframe(17f, 0.26f, 0.02f, 16f),
            new BodyKeyframe(22f, 0.14f, 0.04f, -6f),
            new BodyKeyframe(48f, 0.10f, 0.02f, -3f),
            new BodyKeyframe(90f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillSkewerSpear =
        {
            new WeaponKeyframe(0f, -0.22f, 0.02f, 14f, 1.12f),
            new WeaponKeyframe(7f, 1.02f, -0.02f, -2f, 1.24f),
            new WeaponKeyframe(16f, 0.86f, 0.02f, -18f, 1.18f),
            new WeaponKeyframe(22f, 0.44f, 0.16f, -72f, 1.14f),
            new WeaponKeyframe(48f, 0.40f, 0.10f, -58f, 1.08f),
            new WeaponKeyframe(90f, 0.24f, -0.08f, -14f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillSkewer))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalThrust, 54, aimAngle);
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
                case ClipNormalThrust:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalThrustBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillSkewer:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillSkewerBody, elapsedTicks);
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
                bool moving = ctx.Pawn.pather != null && ctx.Pawn.pather.Moving;
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.11f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.5f : 2.2f);
                float posX = moving ? 0.28f : 0.24f;
                float posZ = (moving ? -0.04f : -0.08f) + Mathf.Sin(tickPhase * 0.8f) * 0.012f;
                float angle = (moving ? -6f : -14f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, SpearDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalThrust:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalThrustSpear, t);
                    if (t >= 10f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalThrustSpear, t, ctx.BaseDrawPos, alt, SpearDrawSize, ctx.FacingLeft, CrimsonAfterimageTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupSpear, prog);
                    float tremble = Mathf.Sin(t * 1.4f) * (0.012f + 0.015f * prog);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + tremble, kf.PosZ, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillSkewer:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillSkewerSpear, t);
                    if (t >= 1f && t <= 24f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillSkewerSpear, t, ctx.BaseDrawPos, alt, SpearDrawSize, ctx.FacingLeft, CrimsonAfterimageTint);
                    }
                    float rumble = (t >= 20f && t <= 32f) ? Mathf.Sin(t * 2.8f) * 0.024f * (1f - (t - 20f) / 12f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + rumble, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    public static class HeavenVfxUtility
    {
        public static void SpawnSpearThrust(Map map, Vector3 origin, float yawDeg, float lengthScale = 3.2f, float widthScale = 1.15f, int lifeTicks = 18)
        {
            if (map == null || Def.ThingDefOf.LC_HeavenThrustFX == null)
            {
                return;
            }

            IntVec3 cell = origin.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Def.ThingDefOf.LC_HeavenThrustFX, cell, map) is Thing_HeavenSpearThrust fx)
            {
                fx.Initialize(origin, yawDeg, shaderMode: 0f, sx: lengthScale, sz: widthScale, lifeTicks: lifeTicks);
            }
        }

        public static void SpawnRootSpire(Map map, Vector3 center, float yawDeg, float widthScale = 1.65f, float heightScale = 2.4f, int lifeTicks = 36)
        {
            if (map == null || Def.ThingDefOf.LC_HeavenThrustFX == null)
            {
                return;
            }

            IntVec3 cell = center.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Def.ThingDefOf.LC_HeavenThrustFX, cell, map) is Thing_HeavenSpearThrust fx)
            {
                fx.Initialize(center, yawDeg, shaderMode: 1f, sx: widthScale, sz: heightScale, lifeTicks: lifeTicks);
            }
        }
    }

    /// <summary>穿刺乐园矛刺射线（Mode 0）与血色荆棘地刺爆发（Mode 1）渲染载体。</summary>
    public class Thing_HeavenSpearThrust : Thing
    {
        private Vector3 worldOrigin;
        private float yawDeg;
        private float mode;
        private float scaleX = 2.4f;
        private float scaleZ = 1.1f;
        private int ageTicks;
        private int maxTicks = 20;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 origin, float yaw, float shaderMode, float sx, float sz, int lifeTicks)
        {
            worldOrigin = origin.Yto0();
            yawDeg = yaw;
            mode = shaderMode;
            scaleX = Mathf.Max(0.4f, sx);
            scaleZ = Mathf.Max(0.4f, sz);
            ageTicks = 0;
            maxTicks = Mathf.Max(8, lifeTicks);
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            if (++ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || HeavenMaterials.ThornsMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            props ??= new MaterialPropertyBlock();
            props.SetColor(HeavenMaterials.PropColor, mode < 0.5f ? new Color(0.95f, 0.15f, 0.20f, 1.0f) : new Color(0.84f, 0.08f, 0.14f, 1.0f));
            props.SetColor(HeavenMaterials.PropCoreColor, new Color(0.14f, 0.02f, 0.04f, 1.0f));
            props.SetFloat(HeavenMaterials.PropProgress, prog);
            props.SetFloat(HeavenMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(HeavenMaterials.PropAlpha, 1.0f);
            props.SetFloat(HeavenMaterials.PropMode, mode);

            Vector3 pos = worldOrigin;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + (mode < 0.5f ? 0.06f : 0.03f);
            Quaternion rot = Quaternion.AngleAxis(yawDeg, Vector3.up);
            if (mode < 0.5f)
            {
                pos += rot * new Vector3(scaleX * 0.42f, 0f, 0f);
            }
            else
            {
                pos.z += scaleZ * 0.25f;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, new Vector3(scaleX, 1f, scaleZ));
            Graphics.DrawMesh(MeshPool.plane10, matrix, HeavenMaterials.ThornsMat, 0, null, 0, props);
        }
    }

    /// <summary>「地棘贯狱」蓄力阶段脚下渗血与猩红棘脉聚能光环。</summary>
    public class Thing_HeavenChargeAura : Thing
    {
        private Pawn caster;
        private Verb warmupVerb;
        private int ageTicks;
        private int maxTicks = 60;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn casterPawn, Verb verb, int expectedTicks)
        {
            caster = casterPawn;
            warmupVerb = verb;
            ageTicks = 0;
            maxTicks = Mathf.Max(12, expectedTicks);
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
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
            if (++ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned || HeavenMaterials.ThornsMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            float alpha = Mathf.Clamp01(ageTicks / 5f) * 0.85f;
            props ??= new MaterialPropertyBlock();
            props.SetColor(HeavenMaterials.PropColor, new Color(0.90f, 0.10f, 0.18f, 1.0f));
            props.SetColor(HeavenMaterials.PropCoreColor, new Color(0.18f, 0.02f, 0.05f, 1.0f));
            props.SetFloat(HeavenMaterials.PropProgress, 0.15f + 0.65f * ((ageTicks % 20) / 20f));
            props.SetFloat(HeavenMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(HeavenMaterials.PropAlpha, alpha);
            props.SetFloat(HeavenMaterials.PropMode, 1.0f);

            Vector3 cPos = caster.DrawPos.Yto0();
            cPos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            float sz = Mathf.Lerp(1.25f, 2.05f, prog);
            Matrix4x4 mat = Matrix4x4.TRS(cPos, Quaternion.identity, new Vector3(sz, 1f, sz * 0.75f));
            Graphics.DrawMesh(MeshPool.plane10, mat, HeavenMaterials.ThornsMat, 0, null, 0, props);
        }
    }

    /// <summary>穿刺乐园长矛近战伤害工人：爆发猩红突刺光矛 VFX 并确保触发普攻前刺动画。</summary>
    public class DamageWorker_HeavenSpear : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Map victimMap = victim?.MapHeld;
            IntVec3 center = victim?.PositionHeld ?? IntVec3.Invalid;
            Vector3 hitPos = victim != null
                ? (victim.Spawned ? victim.DrawPos : center.ToVector3Shifted())
                : Vector3.zero;

            DamageResult result = base.Apply(dinfo, victim);

            if (victimMap != null && center.InBounds(victimMap))
            {
                Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(new TargetInfo(center, victimMap));

                Vector3 origin = dinfo.Instigator != null && dinfo.Instigator.Spawned
                    ? dinfo.Instigator.DrawPos.Yto0()
                    : hitPos.Yto0();
                Vector3 dir = (hitPos.Yto0() - origin).Yto0();
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }
                dir.Normalize();

                float yawDeg = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                HeavenVfxUtility.SpawnSpearThrust(victimMap, origin + dir * 0.35f, yawDeg, lengthScale: 2.55f, widthScale: 1.05f, lifeTicks: 16);

                if (dinfo.Instigator is Pawn attacker)
                {
                    if (!EgoWeaponAnimationManager.IsPlaying(attacker, HeavenWeaponAnimHandler.ClipSkillWarmup)
                        && !EgoWeaponAnimationManager.IsPlaying(attacker, HeavenWeaponAnimHandler.ClipSkillSkewer))
                    {
                        ThingWithComps heldWeapon = attacker.equipment?.Primary;
                        if (heldWeapon != null && heldWeapon.def?.defName == HeavenWeaponAnimHandler.WeaponDefName)
                        {
                            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, hitPos);
                            EgoWeaponAnimationManager.Play(attacker, heldWeapon, HeavenWeaponAnimHandler.ClipNormalThrust, 54, aimAngle);
                        }
                    }
                }
            }

            return result;
        }
    }

    /// <summary>「地棘贯狱」蓄力动词：背弓压低持矛蓄力并在脚下生成猩红荆棘光环。</summary>
    public class Verb_CastAbility_HeavenImpale : Verb_CastAbility
    {
        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && Caster is Pawn casterPawn && casterPawn.equipment?.Primary != null &&
                casterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (casterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    HeavenWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null && Def.ThingDefOf.LC_HeavenChargeAuraFX != null && casterPawn.PositionHeld.InBounds(map))
                {
                    if (GenSpawn.Spawn(Def.ThingDefOf.LC_HeavenChargeAuraFX, casterPawn.PositionHeld, map) is Thing_HeavenChargeAura aura)
                    {
                        aura.Initialize(casterPawn, this, warmupTicks);
                    }
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityHeavenImpale : CompProperties_AbilityEffect
    {
        public float pierceRange = 6.8f;
        public float eruptionRadius = 3.2f;
        public FloatRange lanceDamageRange = new FloatRange(20f, 28f);
        public FloatRange spireDamageRange = new FloatRange(34f, 44f);
        public float armorPenetration = 0.50f;

        public CompProperties_AbilityHeavenImpale()
        {
            compClass = typeof(CompAbilityEffect_HeavenImpale);
        }
    }

    /// <summary>
    /// 天堂主动技能「地棘贯狱」效果处理器：
    /// 第 1 拍以 6.8 格穿透腥红长枪沿直线贯穿路径敌军并压制晕眩，
    /// 第 2 拍（20 ticks 后）于目标脚下 3.2 格范围内破土喷涌毁灭性荆棘根刺群。
    /// </summary>
    public class CompAbilityEffect_HeavenImpale : CompAbilityEffect
    {
        public new CompProperties_AbilityHeavenImpale Props => (CompProperties_AbilityHeavenImpale)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || caster.Dead || !caster.Spawned)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 casterPos = caster.DrawPos.Yto0();
            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            Vector3 dir = (anchorPos - casterPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == HeavenWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, HeavenWeaponAnimHandler.ClipSkillSkewer, 90, aimAngle);
            }

            float yawDeg = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            float lineLen = Mathf.Max(3.2f, Mathf.Min(Props.pierceRange, (anchorPos - casterPos).magnitude + 1.5f));
            HeavenVfxUtility.SpawnSpearThrust(map, casterPos + dir * 0.45f, yawDeg, lengthScale: lineLen, widthScale: 1.55f, lifeTicks: 22);
            Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(new TargetInfo(caster.Position, map));

            DamageDef skillDmg = Def.DamageDefOf.LC_Heaven_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(HeavenWeaponAnimHandler.WeaponDefName);

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned)
                {
                    continue;
                }
                if (foe != victim && !foe.HostileTo(caster))
                {
                    continue;
                }

                Vector3 rel = foe.DrawPos.Yto0() - casterPos;
                float proj = Vector3.Dot(rel, dir);
                if (proj < -0.4f || proj > Props.pierceRange)
                {
                    continue;
                }

                float perpSq = (rel - dir * proj).sqrMagnitude;
                if (perpSq <= 1.25f * 1.25f)
                {
                    foe.stances?.stunner?.StunFor(55, caster, addBattleLog: false);
                    foe.TakeDamage(new DamageInfo(skillDmg, Props.lanceDamageRange.RandomInRange, Props.armorPenetration, yawDeg, caster, weapon: weaponDef));
                }
            }

            if (Def.ThingDefOf.LC_HeavenSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(Def.ThingDefOf.LC_HeavenSkillRunnerFX, caster.PositionHeld, map) is Thing_HeavenSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.eruptionRadius, Props.spireDamageRange, Props.armorPenetration, weaponDef);
                }
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("LC_EgoSkill_TargetMustBeAlive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (parent?.pawn != null && pawn.Faction != null && pawn.Faction == parent.pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("LC_EgoSkill_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && parent?.pawn != null && target.Pawn.HostileTo(parent.pawn);
        }
    }

    /// <summary>驱动「地棘贯狱」第 2 拍（第 20 tick）破土荆棘爆发的纯逻辑实体。</summary>
    public class Thing_HeavenSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float eruptionRadius;
        private FloatRange spireDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool erupted;
        private bool initialized;

        private const int EruptTriggerTick = 20;
        private const int TotalDurationTicks = 86;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange damageRange,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            eruptionRadius = Mathf.Max(2.0f, radius);
            spireDmgRange = damageRange;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            erupted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != HeavenWeaponAnimHandler.WeaponDefName)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed && primaryTarget.Spawned && primaryTarget.Map == Map)
            {
                anchorPos = primaryTarget.DrawPos.Yto0();
                EgoWeaponAnimationManager.UpdateAimAngle(caster, EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos));
            }

            if (!erupted && ageTicks >= EruptTriggerTick)
            {
                erupted = true;
                TriggerEruption();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void TriggerEruption()
        {
            if (Map == null)
            {
                return;
            }

            IntVec3 cell = anchorPos.ToIntVec3().ClampInsideMap(Map);
            if (!cell.InBounds(Map))
            {
                return;
            }

            Def.SoundDefOf.BurrowingHeaven_scream?.PlayOneShot(new TargetInfo(cell, Map));
            HeavenVfxUtility.SpawnRootSpire(Map, anchorPos, 0f, widthScale: 2.35f, heightScale: 3.10f, lifeTicks: 42);
            for (int i = 0; i < 4; i++)
            {
                float ang = i * (Mathf.PI * 0.5f) + 0.4f;
                Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.15f;
                HeavenVfxUtility.SpawnRootSpire(Map, anchorPos + offset, Rand.Range(-10f, 10f), widthScale: 1.55f, heightScale: 2.20f, lifeTicks: 34);
            }

            DamageDef skillDmg = Def.DamageDefOf.LC_Heaven_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            List<Pawn> candidates = new List<Pawn>(Map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned)
                {
                    continue;
                }
                if (foe != primaryTarget && !foe.HostileTo(caster))
                {
                    continue;
                }

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= eruptionRadius * eruptionRadius)
                {
                    foe.TakeDamage(new DamageInfo(skillDmg, spireDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead)
                    {
                        foe.stances?.stunner?.StunFor(65, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
