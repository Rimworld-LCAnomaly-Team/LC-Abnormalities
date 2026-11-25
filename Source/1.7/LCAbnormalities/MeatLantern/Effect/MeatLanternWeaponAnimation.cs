using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MeatLantern.Effect
{
    [StaticConstructorOnStartup]
    public static class MeatLanternWeaponMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly Material MouthMat;

        static MeatLanternWeaponMaterials()
        {
            MouthMat = MaterialPool.MatFrom("Fleck/MeatLantern_Escaped_Mouth_Eat", ShaderDatabase.Transparent, Color.white);
        }
    }

    /// <summary>
    /// O-04-84 肉食提灯 E.G.O. 武器「灯笼」（EgoWeapon_MeatLantern）专属动画处理器：
    /// 贴图原图颚锤朝右（0°），支持血腥张颌重砸（ClipNormalChomp）与主动技能「旋风饕餮啃咬」（Whirlwind_OmNomNom）的狂咬演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MeatLanternWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_MeatLantern";

        public const string ClipNormalChomp = "MeatLantern_NormalChomp";
        public const string ClipSkillWarmup = "MeatLantern_SkillWarmup";
        public const string ClipSkillChomp = "MeatLantern_SkillChomp";

        private static readonly Vector2 LanternDrawSize = new Vector2(1.85f, 1.85f);
        private static readonly Color MawGhostTint = new Color(0.92f, 0.14f, 0.18f, 0.85f);

        // === Normal Chomp (54 ticks): heave heavy meat-maw up -> crushing bite slam -> recover ===
        private static readonly BodyKeyframe[] NormalChompBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(9f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(16f, 0.24f, -0.03f, 18f),
            new BodyKeyframe(32f, 0.14f, -0.01f, 10f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalChompMaw =
        {
            new WeaponKeyframe(0f, 0.30f, -0.08f, -12f, 1.00f),
            new WeaponKeyframe(9f, 0.04f, 0.38f, -54f, 1.06f),
            new WeaponKeyframe(16f, 0.64f, -0.14f, 34f, 1.15f),
            new WeaponKeyframe(32f, 0.50f, -0.16f, 24f, 1.04f),
            new WeaponKeyframe(54f, 0.30f, -0.08f, -12f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise gaping jagged maw high ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.08f, 0.02f, -8f),
            new BodyKeyframe(1.00f, -0.15f, 0.03f, -15f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupMaw =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.08f, -12f, 1.00f),
            new WeaponKeyframe(0.45f, 0.08f, 0.32f, -48f, 1.08f),
            new WeaponKeyframe(1.00f, -0.06f, 0.44f, -68f, 1.15f)
        };

        // === Skill Chomp (72 ticks): lunge forward and slam maw shut on prey ===
        private static readonly BodyKeyframe[] SkillChompBody =
        {
            new BodyKeyframe(0f, -0.15f, 0.03f, -15f),
            new BodyKeyframe(8f, 0.32f, -0.04f, 24f),
            new BodyKeyframe(22f, 0.24f, -0.02f, 16f),
            new BodyKeyframe(42f, 0.10f, 0f, 6f),
            new BodyKeyframe(72f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillChompMaw =
        {
            new WeaponKeyframe(0f, -0.06f, 0.44f, -68f, 1.15f),
            new WeaponKeyframe(8f, 0.72f, -0.14f, 42f, 1.24f),
            new WeaponKeyframe(20f, 0.64f, -0.10f, 26f, 1.16f),
            new WeaponKeyframe(40f, 0.48f, -0.12f, 18f, 1.08f),
            new WeaponKeyframe(72f, 0.30f, -0.08f, -12f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillChomp))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalChomp, 54, aimAngle);
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
                case ClipNormalChomp:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalChompBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillChomp:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillChompBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.09f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 5.0f : 2.4f);
                float posX = moving ? 0.28f : 0.30f;
                float posZ = (moving ? -0.02f : -0.08f) + Mathf.Sin(tickPhase * 0.8f) * 0.012f;
                float angle = (moving ? -22f : -12f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, LanternDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalChomp:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalChompMaw, t);
                    if (t >= 7f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalChompMaw, t, ctx.BaseDrawPos, alt, LanternDrawSize, ctx.FacingLeft, MawGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, LanternDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupMaw, prog);
                    float tremble = Mathf.Sin(t * 1.6f) * 0.014f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + tremble, kf.PosZ, kf.AngleDeg, LanternDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillChomp:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillChompMaw, t);
                    if (t >= 2f && t <= 26f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillChompMaw, t, ctx.BaseDrawPos, alt, LanternDrawSize, ctx.FacingLeft, MawGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, LanternDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「旋风饕餮啃咬」蓄力动词：高举灯笼巨颚准备扑咬。</summary>
    public class Verb_CastAbilityTouch_MeatLanternChomp : Verb_CastAbilityTouch
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
            if (started && CasterPawn != null && CasterPawn.equipment?.Primary != null &&
                CasterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (CasterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(CasterPawn, targetPos);
                int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    CasterPawn,
                    CasterPawn.equipment.Primary,
                    MeatLanternWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    MeatLantern.Def.SoundDefOf.MeatLantern_Escape?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    /// <summary>肉食提灯巨颚扑咬闭合瞬爆视觉实体。</summary>
    public class Thing_MeatLanternChompFX : Thing
    {
        private Vector3 exactPos;
        private float scale = 2.6f;
        private int ageTicks;
        private int durationTicks = 28;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float chompScale = 2.6f, int duration = 28)
        {
            exactPos = pos.Yto0();
            scale = Mathf.Max(1.2f, chompScale);
            durationTicks = Mathf.Max(14, duration);
            ageTicks = 0;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref exactPos, "exactPos");
            Scribe_Values.Look(ref scale, "scale", 2.6f);
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref durationTicks, "durationTicks", 28);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!initialized || ++ageTicks >= durationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || MeatLanternWeaponMaterials.MouthMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = t < 0.22f ? Mathf.Clamp01(t / 0.22f) : Mathf.Pow(1f - (t - 0.22f) / 0.78f, 1.3f);
            if (alpha <= 0.02f)
            {
                return;
            }

            float snap = t < 0.40f ? Mathf.Lerp(1.28f, 0.74f, t / 0.40f) : Mathf.Lerp(0.74f, 0.92f, (t - 0.40f) / 0.60f);
            float curScale = scale * snap;

            props ??= new MaterialPropertyBlock();
            props.SetColor(MeatLanternWeaponMaterials.PropColor, new Color(1.0f, 0.28f, 0.28f, alpha));

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis((t - 0.2f) * 8f, Vector3.up),
                new Vector3(curScale, 1f, curScale));
            Graphics.DrawMesh(MeshPool.plane10, mat, MeatLanternWeaponMaterials.MouthMat, 0, null, 0, props);
        }
    }
}
