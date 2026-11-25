using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.JudgementBird
{
    /// <summary>
    /// 正义裁决者（EgoWeapon_JudgementBird）E.G.O. 武器持握姿势、垂直竖举斩击（Attack_JudeSword_01）与
    /// 「苍白裁决」多段连斩风暴（Attack_JudgeSword_02）动画处理器。
    /// 注：EgoWeapon_JudgementBird.png 刀柄在左下、剑刃指向右上 45°（angle=0° 右上、+45° 水平向前、-45° 垂直向上、-115° 脑后高举）。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class JustitiaWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_JudgementBird";

        public const string ClipNormalSlash = "Justitia_NormalSlash";
        public const string ClipVerdictCharge = "Justitia_VerdictCharge";
        public const string ClipVerdictFlurry = "Justitia_VerdictFlurry";

        private static readonly Vector2 BladeDrawSize = new Vector2(2.05f, 2.05f);
        private static readonly Color PaleGhostTint = new Color(0.60f, 0.94f, 1.0f, 0.85f);
        private static readonly Color GoldGhostTint = new Color(1.0f, 0.90f, 0.56f, 0.80f);

        // === Normal Overhead Slash (62 ticks, Attack_JudeSword_01) ===
        private static readonly BodyKeyframe[] NormalSlashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, -0.08f, 0.02f, -8f),
            new BodyKeyframe(14f, 0.18f, -0.01f, 12f),
            new BodyKeyframe(38f, 0.14f, 0f, 9f),
            new BodyKeyframe(62f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalSlashSword =
        {
            new WeaponKeyframe(0f, 0.34f, -0.22f, 48f, 1.00f),
            new WeaponKeyframe(7f, 0.02f, 0.52f, -48f, 1.06f),
            new WeaponKeyframe(14f, 0.52f, -0.14f, 56f, 1.12f),
            new WeaponKeyframe(38f, 0.50f, -0.16f, 60f, 1.04f),
            new WeaponKeyframe(62f, 0.34f, -0.22f, 48f, 1.00f)
        };

        // === Verdict Charge (normalized 0..1, Attack_JudgeSword_02 0..1.33s): solemn upright stance ===
        private static readonly BodyKeyframe[] VerdictChargeBody =
        {
            new BodyKeyframe(0.0f, 0f, 0f, 0f),
            new BodyKeyframe(0.35f, -0.03f, 0.01f, -3f),
            new BodyKeyframe(1.0f, -0.05f, 0.02f, -4f)
        };

        private static readonly WeaponKeyframe[] VerdictChargeSword =
        {
            new WeaponKeyframe(0.0f, 0.34f, -0.22f, 48f, 1.00f),
            new WeaponKeyframe(0.5f, 0.28f, -0.19f, 45f, 1.02f),
            new WeaponKeyframe(1.0f, 0.26f, -0.18f, 44f, 1.04f)
        };

        // === Verdict Flurry (150 ticks, Attack_JudgeSword_02 1.33..5.00s): 3 heavy cuts + overhead hoist + 7-strike Pale storm ===
        private static readonly BodyKeyframe[] VerdictFlurryBody =
        {
            new BodyKeyframe(0f, -0.05f, 0.02f, -4f),
            new BodyKeyframe(6f, -0.08f, 0.02f, -9f),
            new BodyKeyframe(14f, 0.18f, -0.01f, 16f),
            new BodyKeyframe(22f, 0.12f, 0f, 8f),
            new BodyKeyframe(32f, 0.20f, -0.01f, 15f),
            new BodyKeyframe(44f, 0.14f, 0f, 10f),
            new BodyKeyframe(54f, 0.28f, -0.02f, 18f),
            new BodyKeyframe(64f, -0.02f, 0.01f, -6f),
            new BodyKeyframe(74f, -0.12f, 0.02f, -15f),
            new BodyKeyframe(82f, 0.32f, -0.02f, 14f),
            new BodyKeyframe(112f, 0.26f, -0.01f, 10f),
            new BodyKeyframe(150f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] VerdictFlurrySword =
        {
            new WeaponKeyframe(0f, 0.26f, -0.18f, 44f, 1.04f),
            new WeaponKeyframe(5f, -0.02f, 0.48f, -105f, 1.08f),
            new WeaponKeyframe(14f, 0.56f, -0.16f, 54f, 1.12f),
            new WeaponKeyframe(22f, 0.38f, -0.04f, 32f, 1.08f),
            new WeaponKeyframe(32f, 0.58f, 0.02f, 45f, 1.14f),
            new WeaponKeyframe(42f, 0.36f, 0.02f, 44f, 1.08f),
            new WeaponKeyframe(54f, 0.74f, 0.04f, 44f, 1.16f),
            new WeaponKeyframe(64f, 0.20f, 0.40f, -45f, 1.10f),
            new WeaponKeyframe(74f, -0.14f, 0.58f, -115f, 1.15f),
            new WeaponKeyframe(82f, 0.62f, -0.12f, 50f, 1.18f),
            new WeaponKeyframe(112f, 0.58f, -0.18f, 54f, 1.10f),
            new WeaponKeyframe(130f, 0.46f, -0.20f, 52f, 1.04f),
            new WeaponKeyframe(150f, 0.34f, -0.22f, 48f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipVerdictCharge)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipVerdictFlurry))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalSlash, 62, aimAngle);
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
                case ClipNormalSlash:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalSlashBody, elapsedTicks);
                    break;
                case ClipVerdictCharge:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(VerdictChargeBody, prog);
                    break;
                }
                case ClipVerdictFlurry:
                    kf = EgoWeaponAnimationUtility.SampleBody(VerdictFlurryBody, elapsedTicks);
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
                // Low horizontal trailing bandaged greatsword carry matching Video 3 (00:00-00:17)
                EgoWeaponAnimationUtility.DrawWeaponSprite(
                    mat,
                    ctx.BaseDrawPos,
                    alt,
                    0.34f,
                    -0.22f,
                    48f,
                    BladeDrawSize,
                    ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalSlash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalSlashSword, t);
                    if (t >= 6f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalSlashSword, t, ctx.BaseDrawPos, alt, BladeDrawSize, ctx.FacingLeft, PaleGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipVerdictCharge:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(VerdictChargeSword, prog);
                    float pulse = Mathf.Sin(t * 0.35f) * 0.012f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + pulse, kf.AngleDeg, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipVerdictFlurry:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(VerdictFlurrySword, t);
                    float extraAngle = 0f;

                    // During rapid 7-strike Pale storm finisher (ticks 82..112), draw shimmering criss-cross Pale & Gold blade ghosts!
                    if (t >= 82f && t <= 112f)
                    {
                        extraAngle = Mathf.Sin(t * 1.65f) * 14f;
                        float ghostSweepA = Mathf.Sin(t * 1.15f) * 48f + 16f;
                        float ghostSweepB = -Mathf.Cos(t * 1.35f) * 42f + 22f;
                        EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt - 0.003f, 0.56f, 0.02f, ghostSweepA, BladeDrawSize, ctx.FacingLeft, 1.16f, 0.45f, PaleGhostTint);
                        EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt - 0.006f, 0.52f, -0.06f, ghostSweepB, BladeDrawSize, ctx.FacingLeft, 1.12f, 0.38f, GoldGhostTint);
                    }
                    else if ((t >= 4f && t <= 18f) || (t >= 26f && t <= 38f) || (t >= 48f && t <= 60f) || (t >= 74f && t < 82f))
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, VerdictFlurrySword, t, ctx.BaseDrawPos, alt, BladeDrawSize, ctx.FacingLeft, PaleGhostTint);
                    }

                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg + extraAngle, BladeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 正义裁决者蓄力阶段（0.85s）悬停于持握者头顶的发光黄金天平印记与眼部苍白裁决微光特效。
    /// </summary>
    public class Thing_JustitiaChargeSigil : Thing
    {
        private Pawn caster;
        private Verb warmupVerb;
        private int ageTicks;
        private int totalTicks = 54;
        private bool initialized;

        private MaterialPropertyBlock scaleProps;
        private MaterialPropertyBlock glintProps;

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
            if (ageTicks % 14 == 0 && Map != null)
            {
                JudgementBirdVfxUtility.SpawnBandageWisp(
                    Map,
                    caster.DrawPos.Yto0() + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(-0.1f, 0.6f)),
                    new Vector3(Rand.Range(-0.015f, 0.015f), 0f, Rand.Range(0.016f, 0.032f)),
                    Rand.Range(0.42f, 0.68f),
                    Rand.Range(32, 48));
            }

            if (++ageTicks >= totalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, totalTicks));
            float alpha = Mathf.Clamp01(ageTicks / 10f);
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 anchor = caster.DrawPos.Yto0();
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
            float ageSecs = ageTicks / 60f;

            // 1. Golden glowing Unbalanced Scale hovering overhead
            if (JudgementBirdMaterials.ScaleGlowMat != null)
            {
                float tilt = Mathf.Lerp(-8f, 18f, Mathf.SmoothStep(0f, 1f, prog));
                float sz = Mathf.Lerp(1.15f, 1.55f, prog);
                scaleProps ??= new MaterialPropertyBlock();
                scaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(1.0f, 0.91f, 0.54f, 1.0f));
                scaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, new Color(0.66f, 0.96f, 1.0f, 1.0f));
                scaleProps.SetFloat(JudgementBirdMaterials.PropProgress, prog);
                scaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                scaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha * 0.94f);
                scaleProps.SetFloat(JudgementBirdMaterials.PropPulse, 1.25f + 0.45f * prog);

                Matrix4x4 smat = Matrix4x4.TRS(
                    new Vector3(anchor.x, alt, anchor.z + 1.05f),
                    Quaternion.AngleAxis(tilt, Vector3.up),
                    new Vector3(sz, 1f, sz));
                Graphics.DrawMesh(MeshPool.plane10, smat, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, scaleProps);
            }

            // 2. Sharp horizontal Pale eye-glint flare across the wielder's brow
            if (JudgementBirdMaterials.JustitiaSlashMat != null && prog > 0.25f)
            {
                float glintProg = (prog - 0.25f) / 0.75f;
                glintProps ??= new MaterialPropertyBlock();
                glintProps.SetColor(JudgementBirdMaterials.PropColor, new Color(0.65f, 0.96f, 1.0f, 1.0f));
                glintProps.SetColor(JudgementBirdMaterials.PropCoreColor, Color.white);
                glintProps.SetColor(JudgementBirdMaterials.PropEdgeColor, new Color(1.0f, 0.90f, 0.56f, 1.0f));
                glintProps.SetFloat(JudgementBirdMaterials.PropProgress, Mathf.Lerp(0.20f, 0.72f, glintProg));
                glintProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                glintProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha * 0.85f);
                glintProps.SetFloat(JudgementBirdMaterials.PropArcSpan, 1.55f);

                Matrix4x4 gmat = Matrix4x4.TRS(
                    new Vector3(anchor.x, alt + 0.02f, anchor.z + 0.34f),
                    Quaternion.identity,
                    new Vector3(0.95f, 1f, 0.42f));
                Graphics.DrawMesh(MeshPool.plane10, gmat, JudgementBirdMaterials.JustitiaSlashMat, 0, null, 0, glintProps);
            }
        }
    }

    /// <summary>正义裁决者主动技能「苍白裁决」起手动词，在读条阶段播放庄严蓄力站姿与头顶天平印记。</summary>
    public class Verb_CastAbilityTouch_JustitiaVerdict : Verb_CastAbilityTouch
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
                    JustitiaWeaponAnimHandler.ClipVerdictCharge,
                    warmupTicks,
                    aimAngle,
                    requiredWarmupVerb: this);

                if (casterPawn.MapHeld != null && JudgementBirdDefOf.LC_JustitiaChargeSigilFX != null &&
                    casterPawn.PositionHeld.InBounds(casterPawn.MapHeld))
                {
                    if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JustitiaChargeSigilFX, casterPawn.PositionHeld, casterPawn.MapHeld) is Thing_JustitiaChargeSigil sigil)
                    {
                        sigil.Initialize(casterPawn, this, warmupTicks);
                    }
                }

                LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_On, casterPawn);
            }

            return started;
        }
    }

    public class CompProperties_AbilityJustitiaVerdict : CompProperties_AbilityEffect
    {
        public float openingPaleDamage = 10f;
        public float sweepPaleDamage = 9f;
        public float stormTickPaleDamage = 4.5f;
        public float cleaveRadius = 2.8f;

        public CompProperties_AbilityJustitiaVerdict()
        {
            compClass = typeof(CompAbilityEffect_JustitiaVerdict);
        }
    }

    /// <summary>
    /// 正义裁决者主动技能「苍白裁决」效果实现：
    /// 同步打出第 1 段高伤苍白裁决斩，并驱动后续横扫、突刺、高举蓄力与 7 连苍白风暴终结技。
    /// </summary>
    public class CompAbilityEffect_JustitiaVerdict : CompAbilityEffect
    {
        public new CompProperties_AbilityJustitiaVerdict Props => (CompProperties_AbilityJustitiaVerdict)props;

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
            Vector3 victimPos = victim.Spawned ? victim.DrawPos.Yto0() : victim.PositionHeld.ToVector3Shifted().Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, victimPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == JustitiaWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, JustitiaWeaponAnimHandler.ClipVerdictFlurry, 150, aimAngle);
            }

            // Execute Beat 1 immediately on Apply so single-tick tests observe immediate Pale damage
            ExecutePaleCleaveBeat(caster, victim, victimPos, Props.openingPaleDamage, Props.cleaveRadius, maxSecondary: 2, isGolden: true, flipSweep: false, scale: 2.35f, sfx: JudgementBirdDefOf.LC_Justitia_Slash4 ?? JudgementBirdDefOf.LC_Justitia_Slash1);

            if (map != null && JudgementBirdDefOf.LC_JustitiaVerdictRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JustitiaVerdictRunnerFX, caster.PositionHeld, map) is Thing_JustitiaVerdictRunner runner)
                {
                    runner.Initialize(
                        caster,
                        victim,
                        victimPos,
                        Props.sweepPaleDamage,
                        Props.stormTickPaleDamage,
                        Props.cleaveRadius);
                }
            }
        }

        internal static void ExecutePaleCleaveBeat(
            Pawn caster,
            Pawn primary,
            Vector3 anchorPos,
            float paleDamage,
            float radius,
            int maxSecondary,
            bool isGolden,
            bool flipSweep,
            float scale,
            SoundDef sfx,
            float spanRad = 2.65f)
        {
            if (caster == null || caster.Dead || !caster.Spawned)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 focusPos = (primary != null && primary.Spawned) ? primary.DrawPos.Yto0() : anchorPos;
            Vector3 dir = (focusPos - cPos).Yto0();
            float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg : 0f;

            JudgementBirdVfxUtility.SpawnPaleSlash(
                map,
                focusPos,
                yaw + Rand.Range(-22f, 22f),
                scale,
                flipSweep: flipSweep,
                isGoldenVerdict: isGolden,
                lifeTicks: isGolden ? 18 : 13,
                spanRad: spanRad);

            if (sfx != null)
            {
                LCAbnormalitiesHelper.PlaySoundAt(sfx, caster);
            }

            DamageDef verdictDef = JudgementBirdDefOf.LC_Justitia_VerdictPale ?? LCAnomalyCore.Defs.DamageDefOf.LC_PaleDamage;
            ThingDef weaponDef = caster.equipment?.Primary?.def;

            if (primary != null && !primary.Dead && !primary.Destroyed && primary.Spawned &&
                (primary.DrawPos.Yto0() - cPos).sqrMagnitude <= (radius + 0.6f) * (radius + 0.6f))
            {
                primary.TakeDamage(new DamageInfo(verdictDef, paleDamage, 1f, instigator: caster, weapon: weaponDef));
            }

            if (maxSecondary <= 0)
            {
                return;
            }

            List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            int cleaved = 0;
            for (int i = 0; i < nearby.Count && cleaved < maxSecondary; i++)
            {
                Pawn other = nearby[i];
                if (other == null || other == caster || other == primary || other.Dead || other.Destroyed || !other.Spawned)
                {
                    continue;
                }

                if (!other.HostileTo(caster))
                {
                    continue;
                }

                if ((other.DrawPos.Yto0() - focusPos).sqrMagnitude <= radius * radius)
                {
                    JudgementBirdVfxUtility.SpawnPaleSlash(
                        map,
                        other.DrawPos.Yto0(),
                        yaw + Rand.Range(-45f, 45f),
                        scale * 0.80f,
                        flipSweep: !flipSweep,
                        isGoldenVerdict: isGolden,
                        lifeTicks: 14);
                    other.TakeDamage(new DamageInfo(verdictDef, paleDamage * 0.72f, 1f, instigator: caster, weapon: weaponDef));
                    cleaved++;
                }
            }
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

    /// <summary>
    /// 驱动正义裁决者「苍白裁决」第 2~10 节拍（横扫、突刺、高举黄金天平蓄势及 7 连苍白风暴）的演出实体。
    /// </summary>
    public class Thing_JustitiaVerdictRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 lastAnchorPos;
        private float sweepPaleDmg;
        private float stormTickDmg;
        private float cleaveRadius;
        private int ageTicks;
        private const int TotalTicks = 148;
        private int nextBeatIndex;
        private bool initialized;

        private MaterialPropertyBlock scaleProps;

        private static readonly int[] ScheduleTicks = { 22, 44, 68, 82, 86, 90, 94, 98, 102, 106 };

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float sweepDamage,
            float stormDamage,
            float radius)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            lastAnchorPos = initialAnchor.Yto0();
            sweepPaleDmg = sweepDamage;
            stormTickDmg = stormDamage;
            cleaveRadius = Mathf.Max(1.8f, radius);
            ageTicks = 0;
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

            if (caster.equipment?.Primary?.def?.defName != JustitiaWeaponAnimHandler.WeaponDefName)
            {
                Destroy();
                return;
            }

            ageTicks++;

            Pawn beatTarget = ResolveBeatTarget();
            if (beatTarget != null && beatTarget.Spawned)
            {
                lastAnchorPos = beatTarget.DrawPos.Yto0();
                float updatedAim = EgoWeaponAnimationUtility.ResolveAimAngle(caster, lastAnchorPos);
                EgoWeaponAnimationManager.UpdateAimAngle(caster, updatedAim);
            }
            else
            {
                Vector3 fallbackDir = caster.DrawPos.Yto0() + (caster.Rotation == Rot4.West ? Vector3.left : Vector3.right) * 1.4f;
                lastAnchorPos = fallbackDir;
            }

            while (nextBeatIndex < ScheduleTicks.Length && ageTicks >= ScheduleTicks[nextBeatIndex])
            {
                int idx = nextBeatIndex++;
                if (idx == 0)
                {
                    // Beat 2 (tick 22): flat horizontal sweep
                    CompAbilityEffect_JustitiaVerdict.ExecutePaleCleaveBeat(
                        caster, beatTarget, lastAnchorPos, sweepPaleDmg, cleaveRadius, maxSecondary: 2,
                        isGolden: false, flipSweep: true, scale: 2.25f, sfx: JudgementBirdDefOf.LC_Justitia_Slash2, spanRad: 2.95f);
                }
                else if (idx == 1)
                {
                    // Beat 3 (tick 44): deep piercing thrust + bandage wisps
                    CompAbilityEffect_JustitiaVerdict.ExecutePaleCleaveBeat(
                        caster, beatTarget, lastAnchorPos, sweepPaleDmg * 1.12f, cleaveRadius, maxSecondary: 2,
                        isGolden: true, flipSweep: false, scale: 2.40f, sfx: JudgementBirdDefOf.LC_Justitia_Slash3 ?? JudgementBirdDefOf.LC_Justitia_Slash1, spanRad: 2.50f);

                    for (int b = 0; b < 3; b++)
                    {
                        JudgementBirdVfxUtility.SpawnBandageWisp(
                            Map,
                            lastAnchorPos + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(0f, 0.6f)),
                            new Vector3(Rand.Range(-0.024f, 0.024f), 0f, Rand.Range(0.02f, 0.045f)),
                            Rand.Range(0.45f, 0.72f),
                            Rand.Range(28, 44));
                    }
                }
                else if (idx == 2)
                {
                    // Beat 4 (tick 68): overhead golden scale flare
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_On, caster);
                }
                else
                {
                    // Beats 5..11 (ticks 82..106): 7-strike Pale storm finisher!
                    int stormIdx = idx - 3;
                    if (stormIdx == 0)
                    {
                        JudgementBirdVfxUtility.SpawnPaleWave(Map, lastAnchorPos, 3.0f);
                    }

                    SoundDef s = (stormIdx == 0 || stormIdx == 6)
                        ? (JudgementBirdDefOf.LC_Justitia_Slash4 ?? JudgementBirdDefOf.LC_Justitia_Slash1)
                        : ((stormIdx % 2 == 0) ? JudgementBirdDefOf.LC_Justitia_Slash2 : JudgementBirdDefOf.LC_Justitia_Slash1);

                    CompAbilityEffect_JustitiaVerdict.ExecutePaleCleaveBeat(
                        caster, beatTarget, lastAnchorPos, stormTickDmg, cleaveRadius, maxSecondary: 2,
                        isGolden: stormIdx == 0 || stormIdx == 6,
                        flipSweep: (stormIdx % 2) == 1,
                        scale: stormIdx == 6 ? 2.55f : Rand.Range(1.65f, 2.05f),
                        sfx: s,
                        spanRad: stormIdx == 6 ? 3.15f : 2.45f);
                }
            }

            if (ageTicks >= TotalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned || JudgementBirdMaterials.ScaleGlowMat == null)
            {
                return;
            }

            // Flash golden Unbalanced Scale above target/caster during overhead hoist & storm (ticks 64..116)
            if (ageTicks < 64 || ageTicks > 116)
            {
                return;
            }

            float localT = (ageTicks - 64) / 52f;
            float fadeIn = Mathf.Clamp01(localT / 0.16f);
            float fadeOut = localT > 0.65f ? Mathf.Clamp01(1f - (localT - 0.65f) / 0.35f) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 pos = lastAnchorPos;
            float tilt = Mathf.Lerp(-12f, 22f, Mathf.SmoothStep(0f, 1f, localT));
            float sz = Mathf.Lerp(1.25f, 1.85f, 1f - Mathf.Pow(1f - localT, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.07f;

            scaleProps ??= new MaterialPropertyBlock();
            scaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(1.0f, 0.92f, 0.56f, 1.0f));
            scaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, new Color(0.70f, 0.96f, 1.0f, 1.0f));
            scaleProps.SetFloat(JudgementBirdMaterials.PropProgress, localT);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageTicks / 60f);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha * 0.94f);
            scaleProps.SetFloat(JudgementBirdMaterials.PropPulse, 1.65f);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(pos.x, alt, pos.z + 1.0f),
                Quaternion.AngleAxis(tilt, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, scaleProps);
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
            float bestDistSq = (cleaveRadius + 1.2f) * (cleaveRadius + 1.2f);
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

                float dSq = (candidate.DrawPos.Yto0() - caster.DrawPos.Yto0()).sqrMagnitude;
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
