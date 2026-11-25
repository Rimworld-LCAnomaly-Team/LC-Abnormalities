using System;
using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.LittleRedRidingHoodedMercenary
{
    /// <summary>
    /// 赤红疤痕（EgoWeapon_LittleRedRidingHoodedMercenary）双持武器（左手猩红手炮 + 右手弯镰）与
    /// 「猩红狩猎连射」主动技能动画处理器，还原参考视频与 skeleton #223139 动作节奏。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class CrimsonScarWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_LittleRedRidingHoodedMercenary";

        public const string ClipRangeShot = "CrimsonScar_RangeShot";
        public const string ClipMeleeSlash = "CrimsonScar_MeleeSlash";
        public const string ClipSkillWarmup = "CrimsonScar_SkillWarmup";
        public const string ClipSkillClose = "CrimsonScar_SkillClose";
        public const string ClipSkillRanged = "CrimsonScar_SkillRanged";

        private static readonly Material GunMat;
        private static readonly Material SickleMat;

        private static readonly Vector2 GunDrawSize = new Vector2(0.86f, 0.86f);
        private static readonly Vector2 SickleDrawSize = new Vector2(0.92f, 0.92f);

        private static readonly Color CrimsonGhostTint = new Color(1.0f, 0.24f, 0.26f, 0.85f);

        static CrimsonScarWeaponAnimHandler()
        {
            Texture2D gunTex = ContentFinder<Texture2D>.Get("Things/Item/Equipment/WeaponRanged/EgoWeapon_CrimsonScar_Gun", reportFailure: false);
            Texture2D sickleTex = ContentFinder<Texture2D>.Get("Things/Item/Equipment/WeaponRanged/EgoWeapon_CrimsonScar_Sickle", reportFailure: false);

            if (gunTex != null)
            {
                GunMat = MaterialPool.MatFrom(new MaterialRequest
                {
                    mainTex = gunTex,
                    shader = ShaderDatabase.Cutout,
                    color = Color.white
                });
            }

            if (sickleTex != null)
            {
                SickleMat = MaterialPool.MatFrom(new MaterialRequest
                {
                    mainTex = sickleTex,
                    shader = ShaderDatabase.Cutout,
                    color = Color.white
                });
            }
        }

        // === Normal Range Shot (34 ticks): sharp upward muzzle-flip recoil ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, 0f, 0f, 0f),
            new BodyKeyframe(3f, -0.06f, 0.01f, -5f),
            new BodyKeyframe(14f, -0.03f, 0f, -2f),
            new BodyKeyframe(34f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotGun =
        {
            new WeaponKeyframe(0f, 0.36f, -0.02f, 2f, 1.0f),
            new WeaponKeyframe(4f, 0.28f, 0.14f, -68f, 1.04f),
            new WeaponKeyframe(12f, 0.31f, 0.09f, -46f, 1.02f),
            new WeaponKeyframe(22f, 0.35f, 0.02f, -12f, 1.0f),
            new WeaponKeyframe(34f, 0.36f, -0.02f, 2f, 1.0f)
        };

        // === Normal Melee Slash (48 ticks): overhead sickle arc + forward lunge ===
        private static readonly BodyKeyframe[] MeleeSlashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(10f, -0.08f, 0.02f, -9f),
            new BodyKeyframe(18f, 0.14f, -0.01f, 11f),
            new BodyKeyframe(28f, 0.12f, 0f, 8f),
            new BodyKeyframe(48f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeSlashSickle =
        {
            new WeaponKeyframe(0f, -0.10f, -0.12f, -20f, 1.0f),
            new WeaponKeyframe(10f, -0.14f, 0.34f, -135f, 1.06f),
            new WeaponKeyframe(18f, 0.42f, -0.04f, 58f, 1.12f),
            new WeaponKeyframe(26f, 0.44f, -0.12f, 68f, 1.06f),
            new WeaponKeyframe(48f, -0.18f, -0.18f, -24f, 1.0f)
        };

        // === Skill Warmup (normalized 0..1): tight predatory crouch ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.0f, 0f, 0f, 0f),
            new BodyKeyframe(0.6f, -0.08f, -0.02f, -8f),
            new BodyKeyframe(1.0f, -0.10f, -0.03f, -10f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupGun =
        {
            new WeaponKeyframe(0.0f, 0.32f, -0.08f, 18f, 1.0f),
            new WeaponKeyframe(1.0f, 0.36f, -0.02f, 0f, 1.02f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupSickle =
        {
            new WeaponKeyframe(0.0f, -0.18f, -0.18f, -24f, 1.0f),
            new WeaponKeyframe(1.0f, -0.14f, 0.34f, -132f, 1.06f)
        };

        // === Skill Close Combo (126 ticks): double sickle cross-slash (ticks 8, 54) + twin point-blank shots (ticks 86, 106) ===
        private static readonly BodyKeyframe[] SkillCloseBody =
        {
            new BodyKeyframe(0f, -0.10f, -0.02f, -10f),
            new BodyKeyframe(3f, -0.02f, 0.01f, 0f),
            new BodyKeyframe(9f, 0.20f, -0.02f, 14f),
            new BodyKeyframe(32f, 0.14f, 0f, 10f),
            new BodyKeyframe(44f, 0.04f, 0.01f, -4f),
            new BodyKeyframe(54f, 0.24f, -0.02f, 16f),
            new BodyKeyframe(74f, 0.08f, 0f, 4f),
            new BodyKeyframe(86f, -0.02f, 0.01f, -6f),
            new BodyKeyframe(96f, 0.02f, 0f, 0f),
            new BodyKeyframe(106f, -0.06f, 0.01f, -8f),
            new BodyKeyframe(126f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillCloseSickle =
        {
            new WeaponKeyframe(0f, -0.14f, 0.34f, -132f, 1.06f),
            new WeaponKeyframe(3f, 0.12f, 0.36f, -64f, 1.12f),
            new WeaponKeyframe(9f, 0.48f, -0.08f, 64f, 1.16f),
            new WeaponKeyframe(30f, 0.44f, -0.16f, 76f, 1.08f),
            new WeaponKeyframe(44f, 0.22f, -0.24f, 84f, 1.06f),
            new WeaponKeyframe(54f, 0.46f, 0.28f, -88f, 1.16f),
            new WeaponKeyframe(68f, 0.10f, 0.22f, -110f, 1.04f),
            new WeaponKeyframe(80f, -0.18f, -0.18f, -24f, 1.0f),
            new WeaponKeyframe(126f, -0.18f, -0.18f, -24f, 1.0f)
        };

        private static readonly WeaponKeyframe[] SkillCloseGun =
        {
            new WeaponKeyframe(0f, 0.20f, -0.14f, 28f, 1.0f),
            new WeaponKeyframe(72f, 0.22f, -0.12f, 24f, 1.0f),
            new WeaponKeyframe(82f, 0.38f, -0.02f, 2f, 1.02f),
            new WeaponKeyframe(86f, 0.28f, 0.16f, -72f, 1.06f),
            new WeaponKeyframe(96f, 0.36f, 0.01f, -8f, 1.02f),
            new WeaponKeyframe(102f, 0.38f, -0.02f, 0f, 1.02f),
            new WeaponKeyframe(106f, 0.26f, 0.18f, -78f, 1.08f),
            new WeaponKeyframe(116f, 0.34f, 0.04f, -16f, 1.0f),
            new WeaponKeyframe(126f, 0.32f, -0.08f, 18f, 1.0f)
        };

        // === Skill Ranged Combo (108 ticks): twin shots (ticks 4, 26) + spinning boomerang sickle throw (ticks 56..98) ===
        private static readonly BodyKeyframe[] SkillRangedBody =
        {
            new BodyKeyframe(0f, 0f, 0f, 0f),
            new BodyKeyframe(4f, -0.07f, 0.01f, -6f),
            new BodyKeyframe(18f, 0f, 0f, 0f),
            new BodyKeyframe(26f, -0.08f, 0.01f, -7f),
            new BodyKeyframe(44f, 0f, 0f, 0f),
            new BodyKeyframe(64f, -0.12f, 0.02f, -12f),
            new BodyKeyframe(74f, 0.16f, -0.01f, 14f),
            new BodyKeyframe(92f, 0.08f, 0f, 6f),
            new BodyKeyframe(108f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillRangedGun =
        {
            new WeaponKeyframe(0f, 0.36f, -0.02f, 2f, 1.02f),
            new WeaponKeyframe(4f, 0.26f, 0.15f, -70f, 1.06f),
            new WeaponKeyframe(16f, 0.36f, 0f, -4f, 1.02f),
            new WeaponKeyframe(22f, 0.38f, -0.02f, 0f, 1.02f),
            new WeaponKeyframe(26f, 0.25f, 0.17f, -74f, 1.06f),
            new WeaponKeyframe(40f, 0.35f, -0.02f, 2f, 1.0f),
            new WeaponKeyframe(64f, 0.22f, -0.12f, 28f, 1.0f),
            new WeaponKeyframe(96f, 0.30f, -0.08f, 18f, 1.0f),
            new WeaponKeyframe(108f, 0.32f, -0.08f, 18f, 1.0f)
        };

        private static readonly WeaponKeyframe[] SkillRangedSickle =
        {
            new WeaponKeyframe(0f, -0.18f, -0.18f, -24f, 1.0f),
            new WeaponKeyframe(46f, -0.18f, -0.16f, -26f, 1.0f),
            new WeaponKeyframe(64f, -0.18f, 0.38f, -138f, 1.08f),
            new WeaponKeyframe(74f, 0.44f, 0.08f, 22f, 1.12f),
            new WeaponKeyframe(98f, 0.22f, -0.12f, -12f, 1.02f),
            new WeaponKeyframe(108f, -0.18f, -0.18f, -24f, 1.0f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null || EgoWeaponAnimationManager.IsPlaying(attacker, "CrimsonScar_Skill"))
            {
                return;
            }

            bool berserk = attacker.health?.summaryHealth != null && attacker.health.summaryHealth.SummaryHealthPercent < 0.55f;
            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(
                attacker,
                weapon,
                ClipMeleeSlash,
                48,
                aimAngle,
                variant: berserk ? 1 : 0);
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
                case ClipRangeShot:
                    kf = EgoWeaponAnimationUtility.SampleBody(RangeShotBody, elapsedTicks);
                    break;
                case ClipMeleeSlash:
                    kf = EgoWeaponAnimationUtility.SampleBody(MeleeSlashBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks)));
                    break;
                case ClipSkillClose:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillCloseBody, elapsedTicks);
                    break;
                case ClipSkillRanged:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillRangedBody, elapsedTicks);
                    break;
                default:
                    return EgoWeaponBodyPose.None;
            }

            return EgoWeaponAnimationUtility.ToWorldBodyPose(kf.OffsetX, kf.OffsetZ, kf.LeanDeg, playback.FacingLeft);
        }

        public bool TryDrawWeapon(in EgoWeaponDrawContext ctx)
        {
            Material gunMat = GunMat ?? ctx.Weapon.Graphic?.MatSingleFor(ctx.Weapon);
            Material sickleMat = SickleMat;
            if (gunMat == null)
            {
                return false;
            }

            float baseAlt = ctx.BaseDrawPos.y;
            float frontAlt = baseAlt + 0.015f;
            float backAlt = baseAlt - 0.015f;

            if (ctx.ActiveAnim == null)
            {
                float gunPosX = ctx.IsAiming ? 0.36f : 0.30f;
                float gunPosZ = ctx.IsAiming ? -0.02f : -0.14f;
                float gunAngle = ComputeAimLocalTilt(ctx.AimAngleDeg, ctx.FacingLeft, ctx.IsAiming ? 0f : 30f);

                float idleBob = Mathf.Sin((Find.TickManager?.TicksGame ?? 0) * 0.08f) * 3.5f;
                if (sickleMat != null)
                {
                    EgoWeaponAnimationUtility.DrawWeaponSprite(
                        sickleMat,
                        ctx.BaseDrawPos,
                        backAlt,
                        -0.20f,
                        -0.16f,
                        -22f + idleBob,
                        SickleDrawSize,
                        ctx.FacingLeft);
                }

                EgoWeaponAnimationUtility.DrawWeaponSprite(
                    gunMat,
                    ctx.BaseDrawPos,
                    frontAlt,
                    gunPosX,
                    gunPosZ,
                    gunAngle,
                    GunDrawSize,
                    ctx.FacingLeft);

                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe gk = EgoWeaponAnimationUtility.SampleWeapon(RangeShotGun, t);
                    float aimTilt = ComputeAimLocalTilt(ctx.AimAngleDeg, ctx.FacingLeft, 0f);
                    if (sickleMat != null)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponSprite(sickleMat, ctx.BaseDrawPos, backAlt, -0.20f, -0.16f, -24f, SickleDrawSize, ctx.FacingLeft);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(gunMat, ctx.BaseDrawPos, frontAlt, gk.PosX, gk.PosZ, gk.AngleDeg + aimTilt, GunDrawSize, ctx.FacingLeft, gk.Scale);
                    return true;
                }

                case ClipMeleeSlash:
                {
                    WeaponKeyframe sk = EgoWeaponAnimationUtility.SampleWeapon(MeleeSlashSickle, t);
                    if (sickleMat != null)
                    {
                        if (t >= 10f && t <= 27f)
                        {
                            EgoWeaponAnimationUtility.DrawWeaponAfterimages(sickleMat, MeleeSlashSickle, t, ctx.BaseDrawPos, frontAlt, SickleDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                        }
                        EgoWeaponAnimationUtility.DrawWeaponSprite(sickleMat, ctx.BaseDrawPos, frontAlt, sk.PosX, sk.PosZ, sk.AngleDeg, SickleDrawSize, ctx.FacingLeft, sk.Scale);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(gunMat, ctx.BaseDrawPos, backAlt, 0.22f, -0.14f, 26f, GunDrawSize, ctx.FacingLeft);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe gk = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupGun, prog);
                    WeaponKeyframe sk = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupSickle, prog);
                    if (sickleMat != null)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponSprite(sickleMat, ctx.BaseDrawPos, frontAlt, sk.PosX, sk.PosZ, sk.AngleDeg, SickleDrawSize, ctx.FacingLeft, sk.Scale);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(gunMat, ctx.BaseDrawPos, backAlt, gk.PosX, gk.PosZ, gk.AngleDeg, GunDrawSize, ctx.FacingLeft, gk.Scale);
                    return true;
                }

                case ClipSkillClose:
                {
                    WeaponKeyframe sk = EgoWeaponAnimationUtility.SampleWeapon(SkillCloseSickle, t);
                    WeaponKeyframe gk = EgoWeaponAnimationUtility.SampleWeapon(SkillCloseGun, t);
                    bool sickleActive = t < 78f;

                    if (sickleMat != null)
                    {
                        if ((t >= 1f && t <= 16f) || (t >= 44f && t <= 62f))
                        {
                            EgoWeaponAnimationUtility.DrawWeaponAfterimages(sickleMat, SkillCloseSickle, t, ctx.BaseDrawPos, frontAlt, SickleDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                        }
                        EgoWeaponAnimationUtility.DrawWeaponSprite(sickleMat, ctx.BaseDrawPos, sickleActive ? frontAlt : backAlt, sk.PosX, sk.PosZ, sk.AngleDeg, SickleDrawSize, ctx.FacingLeft, sk.Scale);
                    }

                    float gunAim = t >= 76f ? ComputeAimLocalTilt(ctx.AimAngleDeg, ctx.FacingLeft, 0f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(gunMat, ctx.BaseDrawPos, sickleActive ? backAlt : frontAlt, gk.PosX, gk.PosZ, gk.AngleDeg + gunAim, GunDrawSize, ctx.FacingLeft, gk.Scale);
                    return true;
                }

                case ClipSkillRanged:
                {
                    WeaponKeyframe gk = EgoWeaponAnimationUtility.SampleWeapon(SkillRangedGun, t);
                    WeaponKeyframe sk = EgoWeaponAnimationUtility.SampleWeapon(SkillRangedSickle, t);
                    float gunAim = t < 56f ? ComputeAimLocalTilt(ctx.AimAngleDeg, ctx.FacingLeft, 0f) : 0f;

                    EgoWeaponAnimationUtility.DrawWeaponSprite(gunMat, ctx.BaseDrawPos, t < 56f ? frontAlt : backAlt, gk.PosX, gk.PosZ, gk.AngleDeg + gunAim, GunDrawSize, ctx.FacingLeft, gk.Scale);

                    // Hide held sickle while the boomerang sickle is flying (ticks 74..98)
                    if (sickleMat != null && (t < 74f || t >= 98f))
                    {
                        if (t >= 64f && t < 74f)
                        {
                            EgoWeaponAnimationUtility.DrawWeaponAfterimages(sickleMat, SkillRangedSickle, t, ctx.BaseDrawPos, frontAlt, SickleDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                        }
                        EgoWeaponAnimationUtility.DrawWeaponSprite(sickleMat, ctx.BaseDrawPos, t >= 56f ? frontAlt : backAlt, sk.PosX, sk.PosZ, sk.AngleDeg, SickleDrawSize, ctx.FacingLeft, sk.Scale);
                    }
                    return true;
                }

                default:
                    return false;
            }
        }

        private static float ComputeAimLocalTilt(float aimAngleDeg, bool facingLeft, float idleFallbackTilt)
        {
            if (Mathf.Abs(Mathf.Sin(aimAngleDeg * Mathf.Deg2Rad)) < 0.08f)
            {
                return idleFallbackTilt;
            }

            float norm = ((aimAngleDeg % 360f) + 360f) % 360f;
            float tilt = facingLeft ? (270f - norm) : (norm - 90f);
            return Mathf.Clamp(tilt, -55f, 55f) + idleFallbackTilt * 0.25f;
        }
    }

    /// <summary>赤红疤痕主动技能「猩红狩猎连射」蓄力动词，在起手瞬间播放压低身形的准备姿势。</summary>
    public class Verb_CastAbility_CrimsonScarBarrage : Verb_CastAbility
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && Caster is Pawn casterPawn && casterPawn.equipment?.Primary != null &&
                casterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (casterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    CrimsonScarWeaponAnimHandler.ClipSkillWarmup,
                    Mathf.Max(6, warmup.ticksLeft + 2),
                    aim,
                    requiredWarmupVerb: this);
            }

            return started;
        }
    }

    public class CompProperties_AbilityCrimsonScarBarrage : CompProperties_AbilityEffect
    {
        public FloatRange sickleDamageRange = new FloatRange(22f, 28f);
        public FloatRange shotDamageRange = new FloatRange(18f, 24f);
        public float armorPenetration = 0.50f;
        public float closeRangeThreshold = 3.5f;

        public CompProperties_AbilityCrimsonScarBarrage()
        {
            compClass = typeof(CompAbilityEffect_CrimsonScarBarrage);
        }
    }

    /// <summary>
    /// 赤红疤痕 E.G.O. 武器主动技能「猩红狩猎连射」：
    /// 近距触发“猩红双镰交叉连斩 + 贴脸双联手炮齐射”，中远距触发“双联手炮连射 + 跨步回旋飞镰”。
    /// </summary>
    public class CompAbilityEffect_CrimsonScarBarrage : CompAbilityEffect
    {
        public new CompProperties_AbilityCrimsonScarBarrage Props => (CompProperties_AbilityCrimsonScarBarrage)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || victim == null || victim.Dead || victim.Destroyed)
            {
                return;
            }

            Map map = victim.MapHeld ?? caster.MapHeld;
            Vector3 casterPos = caster.DrawPos.Yto0();
            Vector3 victimPos = victim.DrawPos.Yto0();
            float dist = (victimPos - casterPos).magnitude;
            bool isCloseRange = dist <= Props.closeRangeThreshold;

            float hpPct = caster.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            bool berserk = hpPct < 0.55f;
            float missingFraction = Mathf.Clamp01(1f - hpPct);
            float bonusMult = 1f + Mathf.Clamp01(missingFraction / 0.6f) * 0.50f;

            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, victimPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == CrimsonScarWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(
                    caster,
                    weapon,
                    isCloseRange ? CrimsonScarWeaponAnimHandler.ClipSkillClose : CrimsonScarWeaponAnimHandler.ClipSkillRanged,
                    isCloseRange ? 126 : 108,
                    aimAngle,
                    variant: berserk ? 1 : 0);
            }

            // Beat 1 executes synchronously on Apply so single-tick callers/tests observe immediate impact
            if (isCloseRange)
            {
                ExecuteSickleBeat(caster, victim, Props.sickleDamageRange.RandomInRange * bonusMult, Props.armorPenetration, berserk, flipSweep: false);
            }
            else
            {
                ExecuteShotgunBeat(caster, victim, Props.shotDamageRange.RandomInRange * bonusMult, Props.armorPenetration, berserk);
            }

            if (map != null && LittleRedDefOf.LC_CrimsonScarSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(LittleRedDefOf.LC_CrimsonScarSkillRunnerFX, caster.PositionHeld, map) is Thing_CrimsonScarSkillRunner runner)
                {
                    runner.Initialize(
                        caster,
                        victim,
                        isCloseRange,
                        Props.sickleDamageRange,
                        Props.shotDamageRange,
                        Props.armorPenetration,
                        bonusMult,
                        berserk);
                }
            }
        }

        internal static void ExecuteSickleBeat(Pawn caster, Pawn victim, float damage, float armorPen, bool berserk, bool flipSweep)
        {
            if (caster == null || victim == null || victim.Dead || victim.Destroyed || !victim.Spawned)
            {
                return;
            }

            Map map = victim.MapHeld ?? caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 vPos = victim.DrawPos.Yto0();
            Vector3 dir = (vPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float yaw = LittleRedVfxUtility.VectorToYawDeg(dir);
            SoundDef sfx = berserk ? LittleRedDefOf.LC_LittleRed_AtkPower : (flipSweep ? LittleRedDefOf.LC_LittleRed_Atk2 : LittleRedDefOf.LC_LittleRed_Atk1);
            LCAbnormalitiesHelper.PlaySoundAt(sfx, victim);

            LittleRedVfxUtility.SpawnSlash(
                map,
                Vector3.Lerp(cPos, vPos, 0.58f),
                yaw,
                scale: berserk ? 2.55f : 2.15f,
                flipSweep: flipSweep,
                berserk: berserk,
                lifeTicks: berserk ? 17 : 14,
                spanRad: berserk ? 2.95f : 2.55f);
            LittleRedVfxUtility.SpawnImpactBurst(map, vPos, berserk ? 1.35f : 1.05f, berserk);

            DamageDef skillDmg = LittleRedDefOf.LC_CrimsonScar_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            ThingDef weaponDef = caster.equipment?.Primary?.def;
            victim.TakeDamage(new DamageInfo(skillDmg, damage, armorPen, instigator: caster, weapon: weaponDef));

            if (berserk)
            {
                List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < nearby.Count; i++)
                {
                    Pawn other = nearby[i];
                    if (other == null || other == caster || other == victim || other.Dead || other.Destroyed || !other.Spawned)
                    {
                        continue;
                    }

                    if (!other.HostileTo(caster))
                    {
                        continue;
                    }

                    if (other.Position.DistanceTo(victim.Position) <= 2.4f)
                    {
                        other.TakeDamage(new DamageInfo(skillDmg, Mathf.Max(4f, damage * 0.65f), armorPen, instigator: caster, weapon: weaponDef));
                        LittleRedVfxUtility.SpawnImpactBurst(map, other.DrawPos.Yto0(), 1.0f, berserk: true);
                        break;
                    }
                }
            }
        }

        internal static void ExecuteShotgunBeat(Pawn caster, Pawn victim, float damage, float armorPen, bool berserk)
        {
            if (caster == null || caster.Dead || !caster.Spawned || victim == null || victim.Dead || victim.Destroyed || !victim.Spawned)
            {
                return;
            }

            Map map = victim.MapHeld ?? caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 vPos = victim.DrawPos.Yto0();
            Vector3 dir = (vPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            Vector3 muzzlePos = cPos + dir * 0.58f;
            LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Gun, caster);
            LittleRedVfxUtility.SpawnShotgunCone(
                map,
                muzzlePos,
                dir,
                length: berserk ? 5.0f : 3.9f,
                width: berserk ? 2.45f : 1.90f,
                spread: berserk ? 0.68f : 0.54f,
                berserk: berserk,
                lifeTicks: berserk ? 16 : 13);
            FleckMaker.ThrowSmoke(muzzlePos, map, berserk ? 1.2f : 0.9f);
            LittleRedVfxUtility.SpawnImpactBurst(map, vPos, berserk ? 1.40f : 1.05f, berserk);

            DamageDef skillDmg = LittleRedDefOf.LC_CrimsonScar_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            ThingDef weaponDef = caster.equipment?.Primary?.def;
            victim.TakeDamage(new DamageInfo(skillDmg, damage, armorPen, instigator: caster, weapon: weaponDef));
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

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

            if (parent?.pawn != null && !pawn.HostileTo(parent.pawn) && pawn.Faction == parent.pawn.Faction)
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

    /// <summary>驱动赤红疤痕「猩红狩猎连射」后续节拍（交叉重劈/贴脸双枪/中距连射+回旋飞镰）的演出实体。</summary>
    public class Thing_CrimsonScarSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private bool isCloseRange;
        private FloatRange sickleRange;
        private FloatRange shotRange;
        private float armorPen;
        private float bonusMult;
        private bool berserk;
        private int ageTicks;
        private int totalTicks;
        private int nextBeatIndex;
        private bool initialized;

        private static readonly int[] CloseBeats = { 54, 86, 106 };
        private static readonly int[] RangedBeats = { 26, 74 };

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            bool closeCombo,
            FloatRange sickleDmg,
            FloatRange shotDmg,
            float armorPenetration,
            float damageMultiplier,
            bool isBerserk)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            isCloseRange = closeCombo;
            sickleRange = sickleDmg;
            shotRange = shotDmg;
            armorPen = armorPenetration;
            bonusMult = damageMultiplier;
            berserk = isBerserk;
            ageTicks = 0;
            totalTicks = closeCombo ? 124 : 106;
            nextBeatIndex = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != CrimsonScarWeaponAnimHandler.WeaponDefName)
            {
                Destroy();
                return;
            }

            ageTicks++;

            Pawn beatTarget = ResolveBeatTarget();
            if (beatTarget != null && beatTarget.Spawned)
            {
                float updatedAim = EgoWeaponAnimationUtility.ResolveAimAngle(caster, beatTarget.DrawPos);
                EgoWeaponAnimationManager.UpdateAimAngle(caster, updatedAim);
            }

            int[] schedule = isCloseRange ? CloseBeats : RangedBeats;
            while (nextBeatIndex < schedule.Length && ageTicks >= schedule[nextBeatIndex])
            {
                int idx = nextBeatIndex++;
                if (beatTarget == null || beatTarget.Dead || beatTarget.Destroyed || !beatTarget.Spawned)
                {
                    continue;
                }

                if (isCloseRange)
                {
                    if (idx == 0)
                    {
                        CompAbilityEffect_CrimsonScarBarrage.ExecuteSickleBeat(
                            caster,
                            beatTarget,
                            sickleRange.RandomInRange * bonusMult * 1.10f,
                            armorPen,
                            berserk,
                            flipSweep: true);
                    }
                    else
                    {
                        CompAbilityEffect_CrimsonScarBarrage.ExecuteShotgunBeat(
                            caster,
                            beatTarget,
                            shotRange.RandomInRange * bonusMult * (idx == 2 ? 1.15f : 1.0f),
                            armorPen,
                            berserk);
                    }
                }
                else
                {
                    if (idx == 0)
                    {
                        CompAbilityEffect_CrimsonScarBarrage.ExecuteShotgunBeat(
                            caster,
                            beatTarget,
                            shotRange.RandomInRange * bonusMult * 1.10f,
                            armorPen,
                            berserk);
                    }
                    else
                    {
                        LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Throw, caster);
                        Vector3 start = caster.DrawPos.Yto0();
                        Vector3 dest = beatTarget.DrawPos.Yto0();
                        LittleRedVfxUtility.SpawnThrownSickle(
                            Map,
                            caster,
                            start,
                            dest,
                            sickleRange.RandomInRange * bonusMult,
                            isHired: true);
                    }
                }
            }

            if (ageTicks >= totalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }

        private Pawn ResolveBeatTarget()
        {
            if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed && primaryTarget.Spawned && primaryTarget.Map == Map)
            {
                return primaryTarget;
            }

            if (Map == null || caster == null)
            {
                return null;
            }

            Pawn best = null;
            float bestDistSq = 12.5f * 12.5f;
            IReadOnlyList<Pawn> pawns = Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate == caster || candidate.Dead || candidate.Destroyed || !candidate.Spawned)
                {
                    continue;
                }

                if (!candidate.HostileTo(caster))
                {
                    continue;
                }

                float dSq = (candidate.Position - caster.Position).LengthHorizontalSquared;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = candidate;
                }
            }

            if (best != null)
            {
                primaryTarget = best;
            }
            return best;
        }
    }
}
