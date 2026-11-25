using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OneSin.Effect
{
    [StaticConstructorOnStartup]
    public static class PenitenceMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material HaloMat;

        static PenitenceMaterials()
        {
            Shader shader = ShaderDatabase.LoadShader("Map/LC_PenitenceHalo") ?? ShaderDatabase.MoteGlow;
            HaloMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = shader,
                color = new Color(0.95f, 0.96f, 1.0f, 1.0f)
            });
        }
    }

    /// <summary>
    /// O-03-03 一罪与百善 E.G.O. 武器「忏悔」（EgoWeapon_OneSin）专属动画处理器：
    /// 贴图原图圣骨权杖朝右（0°），支持庄严高举重槌与十字圣光闪烁（ClipNormalSwing）及
    /// 主动技能「赎罪圣辉」的净罪光轮祈祷蓄力与大范围苍白赎罪圣洗演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class PenitenceWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_OneSin";

        public const string ClipNormalSwing = "Penitence_NormalSwing";
        public const string ClipSkillWarmup = "Penitence_SkillWarmup";
        public const string ClipSkillAbsolution = "Penitence_SkillAbsolution";

        private static readonly Vector2 MaceDrawSize = new Vector2(1.85f, 1.85f);
        private static readonly Color HolyGhostTint = new Color(0.98f, 0.94f, 0.72f, 0.85f);

        // === Normal Swing (52 ticks): solemn crown lift -> clean radiant mace slam -> recover ===
        private static readonly BodyKeyframe[] NormalSwingBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(9f, -0.09f, 0.02f, -9f),
            new BodyKeyframe(16f, 0.22f, -0.02f, 16f),
            new BodyKeyframe(30f, 0.12f, 0f, 8f),
            new BodyKeyframe(52f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalSwingMace =
        {
            new WeaponKeyframe(0f, 0.30f, -0.08f, -14f, 1.00f),
            new WeaponKeyframe(9f, 0.04f, 0.36f, -52f, 1.05f),
            new WeaponKeyframe(16f, 0.62f, -0.10f, 32f, 1.12f),
            new WeaponKeyframe(30f, 0.48f, -0.14f, 20f, 1.03f),
            new WeaponKeyframe(52f, 0.30f, -0.08f, -14f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise crown-mace upright in solemn absolution prayer ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.06f, 0.02f, -6f),
            new BodyKeyframe(1.00f, -0.12f, 0.04f, -12f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupMace =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.08f, -14f, 1.00f),
            new WeaponKeyframe(0.45f, 0.10f, 0.36f, -60f, 1.06f),
            new WeaponKeyframe(1.00f, 0.02f, 0.48f, -82f, 1.12f)
        };

        // === Skill Absolution (88 ticks): fierce smite @ 7 -> second elevated sanctum plant @ 20 ===
        private static readonly BodyKeyframe[] SkillAbsolutionBody =
        {
            new BodyKeyframe(0f, -0.12f, 0.04f, -12f),
            new BodyKeyframe(7f, 0.28f, -0.03f, 20f),
            new BodyKeyframe(14f, 0.04f, 0.02f, 2f),
            new BodyKeyframe(20f, 0.22f, -0.02f, 14f),
            new BodyKeyframe(48f, 0.08f, 0f, 5f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillAbsolutionMace =
        {
            new WeaponKeyframe(0f, 0.02f, 0.48f, -82f, 1.12f),
            new WeaponKeyframe(7f, 0.68f, -0.12f, 38f, 1.20f),
            new WeaponKeyframe(14f, 0.14f, 0.42f, -72f, 1.18f),
            new WeaponKeyframe(20f, 0.62f, -0.10f, 26f, 1.24f),
            new WeaponKeyframe(48f, 0.46f, -0.10f, 12f, 1.08f),
            new WeaponKeyframe(88f, 0.30f, -0.08f, -14f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillAbsolution))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalSwing, 52, aimAngle);

            Map map = attacker.MapHeld;
            if (map != null && OneSin.Def.ThingDefOf.LC_PenitenceHaloFX != null)
            {
                IntVec3 cell = targetPos.ToIntVec3().ClampInsideMap(map);
                if (cell.InBounds(map) && GenSpawn.Spawn(OneSin.Def.ThingDefOf.LC_PenitenceHaloFX, cell, map) is Thing_PenitenceHaloFX fx)
                {
                    fx.Initialize(targetPos, haloScale: 1.85f, duration: 18, haloMode: 0f);
                }
            }
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
                case ClipNormalSwing:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalSwingBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillAbsolution:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillAbsolutionBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.07f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.0f : 1.6f);
                float posX = moving ? 0.28f : 0.30f;
                float posZ = (moving ? -0.02f : -0.08f) + Mathf.Sin(tickPhase * 0.7f) * 0.010f;
                float angle = (moving ? -24f : -14f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, MaceDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalSwing:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalSwingMace, t);
                    if (t >= 7f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalSwingMace, t, ctx.BaseDrawPos, alt, MaceDrawSize, ctx.FacingLeft, HolyGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, MaceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupMace, prog);
                    float auraFloat = Mathf.Sin(t * 1.1f) * 0.012f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + auraFloat, kf.AngleDeg, MaceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillAbsolution:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillAbsolutionMace, t);
                    if (t >= 2f && t <= 30f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillAbsolutionMace, t, ctx.BaseDrawPos, alt, MaceDrawSize, ctx.FacingLeft, HolyGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, MaceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「忏悔」圣光十字与赎罪光轮视觉特效（Mode 0 = 十字闪耀，Mode 1 = 净罪圣域光环）。</summary>
    public class Thing_PenitenceHaloFX : Thing
    {
        private Vector3 exactPos;
        private float scale = 2.0f;
        private int ageTicks;
        private int durationTicks = 24;
        private float mode;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float haloScale = 2.0f, int duration = 24, float haloMode = 0f)
        {
            exactPos = pos.Yto0();
            scale = Mathf.Max(1.0f, haloScale);
            durationTicks = Mathf.Max(10, duration);
            mode = haloMode;
            ageTicks = 0;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref exactPos, "exactPos");
            Scribe_Values.Look(ref scale, "scale", 2.0f);
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref durationTicks, "durationTicks", 24);
            Scribe_Values.Look(ref mode, "mode", 0f);
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
            if (!initialized || PenitenceMaterials.HaloMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - t, 0.85f);
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(PenitenceMaterials.PropColor, mode < 0.5f ? new Color(0.92f, 0.96f, 1.0f, 1.0f) : new Color(0.86f, 0.98f, 1.0f, 1.0f));
            props.SetColor(PenitenceMaterials.PropCoreColor, new Color(1.0f, 0.94f, 0.68f, 1.0f));
            props.SetFloat(PenitenceMaterials.PropProgress, t);
            props.SetFloat(PenitenceMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(PenitenceMaterials.PropAlpha, alpha);
            props.SetFloat(PenitenceMaterials.PropMode, mode);

            float curSize = mode < 0.5f ? scale * Mathf.Lerp(0.8f, 1.25f, t) : scale * Mathf.Lerp(0.45f, 1.25f, 1f - Mathf.Pow(1f - t, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(curSize, 1f, curSize));
            Graphics.DrawMesh(MeshPool.plane10, mat, PenitenceMaterials.HaloMat, 0, null, 0, props);
        }
    }

    /// <summary>祈祷蓄力期间环绕持握者的圣光光环粒子实体。</summary>
    public class Thing_PenitenceChargeAura : Thing
    {
        private Pawn caster;
        private int ageTicks;
        private int durationTicks = 40;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn casterPawn, int duration = 40)
        {
            caster = casterPawn;
            durationTicks = Mathf.Max(12, duration);
            ageTicks = 0;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref durationTicks, "durationTicks", 40);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!initialized || caster == null || caster.Dead || !caster.Spawned || ++ageTicks >= durationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned || PenitenceMaterials.HaloMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.85f;
            if (alpha <= 0.02f)
            {
                return;
            }

            Vector3 p = caster.DrawPos.Yto0();
            props ??= new MaterialPropertyBlock();
            props.SetColor(PenitenceMaterials.PropColor, new Color(1.0f, 0.94f, 0.68f, 1.0f));
            props.SetColor(PenitenceMaterials.PropCoreColor, new Color(1.0f, 0.98f, 0.88f, 1.0f));
            props.SetFloat(PenitenceMaterials.PropProgress, (ageTicks % 24) / 24f);
            props.SetFloat(PenitenceMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(PenitenceMaterials.PropAlpha, alpha);
            props.SetFloat(PenitenceMaterials.PropMode, 1f);

            float size = Mathf.Lerp(1.6f, 2.3f, t);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            Matrix4x4 mat = Matrix4x4.TRS(new Vector3(p.x, alt, p.z), Quaternion.identity, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, PenitenceMaterials.HaloMat, 0, null, 0, props);
        }
    }

    /// <summary>「赎罪圣辉」蓄力动词：高举荆棘圣骨权杖祈祷聚光。</summary>
    public class Verb_CastAbility_PenitenceAbsolution : Verb_CastAbility
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
                    PenitenceWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = CasterPawn.MapHeld;
                if (map != null)
                {
                    SoundDef shieldSfx = DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletShield");
                    shieldSfx?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, map));

                    if (OneSin.Def.ThingDefOf.LC_PenitenceChargeAuraFX != null && CasterPawn.PositionHeld.InBounds(map))
                    {
                        if (GenSpawn.Spawn(OneSin.Def.ThingDefOf.LC_PenitenceChargeAuraFX, CasterPawn.PositionHeld, map) is Thing_PenitenceChargeAura aura)
                        {
                            aura.Initialize(CasterPawn, warmupTicks + 6);
                        }
                    }
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityPenitenceAbsolution : CompProperties_AbilityEffect
    {
        public float absolutionRadius = 3.8f;
        public FloatRange smiteDamageRange = new FloatRange(10f, 14f);
        public FloatRange sanctumDamageRange = new FloatRange(14f, 18f);
        public float armorPenetration = 0.28f;
        public float healAmount = 10f;

        public CompProperties_AbilityPenitenceAbsolution()
        {
            compClass = typeof(CompAbilityEffect_PenitenceAbsolution);
        }
    }

    /// <summary>
    /// 「忏悔」主动技能效果处理器：
    /// 第 1 拍圣光重槌镇压（白伤 + 十字闪光），第 20 tick 展开 3.8 格净罪光轮波，对敌军施以纯正白伤净罪与眩晕，并为持握者及友军疗愈伤势与精神。
    /// </summary>
    public class CompAbilityEffect_PenitenceAbsolution : CompAbilityEffect
    {
        public new CompProperties_AbilityPenitenceAbsolution Props => (CompProperties_AbilityPenitenceAbsolution)props;

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

            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == PenitenceWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, PenitenceWeaponAnimHandler.ClipSkillAbsolution, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(PenitenceWeaponAnimHandler.WeaponDefName);
            DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletShield")?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));

            if (OneSin.Def.ThingDefOf.LC_PenitenceHaloFX != null)
            {
                IntVec3 haloCell = anchorPos.ToIntVec3().ClampInsideMap(map);
                if (haloCell.InBounds(map) && GenSpawn.Spawn(OneSin.Def.ThingDefOf.LC_PenitenceHaloFX, haloCell, map) is Thing_PenitenceHaloFX fx)
                {
                    fx.Initialize(anchorPos, haloScale: 2.6f, duration: 22, haloMode: 0f);
                }
            }

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, Props.smiteDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
            }

            if (OneSin.Def.ThingDefOf.LC_PenitenceSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(OneSin.Def.ThingDefOf.LC_PenitenceSkillRunnerFX, caster.PositionHeld, map) is Thing_PenitenceSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.absolutionRadius, Props.sanctumDamageRange, Props.armorPenetration, Props.healAmount, weaponDef);
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

    /// <summary>驱动「赎罪圣辉」第 20 tick 圣域净罪光环与友军治愈的纯逻辑实体。</summary>
    public class Thing_PenitenceSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float absolutionRadius;
        private FloatRange sanctumDmgRange;
        private float armorPen;
        private float healAmount;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool burstDone;
        private bool initialized;

        private const int TotalDurationTicks = 80;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 anchor,
            float radius,
            FloatRange sanctumRange,
            float ap,
            float heal,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            absolutionRadius = Mathf.Max(2.0f, radius);
            sanctumDmgRange = sanctumRange;
            armorPen = ap;
            healAmount = heal;
            weaponDef = weapon;
            ageTicks = 0;
            burstDone = false;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref primaryTarget, "primaryTarget");
            Scribe_Values.Look(ref anchorPos, "anchorPos");
            Scribe_Values.Look(ref absolutionRadius, "absolutionRadius", 3.8f);
            Scribe_Values.Look(ref sanctumDmgRange, "sanctumDmgRange", new FloatRange(14f, 18f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.28f);
            Scribe_Values.Look(ref healAmount, "healAmount", 10f);
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref burstDone, "burstDone", false);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!initialized || Map == null || caster == null || caster.Destroyed || caster.Dead || !caster.Spawned)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            ageTicks++;

            if (!burstDone && ageTicks >= 20)
            {
                burstDone = true;
                ExecuteAbsolutionWave();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteAbsolutionWave()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == map)
                ? primaryTarget.DrawPos.Yto0()
                : anchorPos;

            IntVec3 cell = center.ToIntVec3().ClampInsideMap(map);
            DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletHeal")?.PlayOneShot(new TargetInfo(cell, map));

            if (OneSin.Def.ThingDefOf.LC_PenitenceHaloFX != null && cell.InBounds(map))
            {
                if (GenSpawn.Spawn(OneSin.Def.ThingDefOf.LC_PenitenceHaloFX, cell, map) is Thing_PenitenceHaloFX wave)
                {
                    wave.Initialize(center, haloScale: 6.2f, duration: 42, haloMode: 1f);
                }
            }

            FleckMaker.ThrowMicroSparks(center, map);

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn other = candidates[i];
                if (other == null || other.Dead || other.Destroyed || !other.Spawned)
                {
                    continue;
                }

                if (other == caster || (caster.Faction != null && other.Faction == caster.Faction))
                {
                    if (other == caster || (other.DrawPos.Yto0() - center).sqrMagnitude <= absolutionRadius * absolutionRadius)
                    {
                        LCAbnormalitiesHelper.HealInjuries(other, healAmount);
                        LCAbnormalitiesHelper.RestoreMentalHealth(other, 0.28f, 0.08f);
                    }
                    continue;
                }

                if (other != primaryTarget && !other.HostileTo(caster))
                {
                    continue;
                }

                if ((other.DrawPos.Yto0() - center).sqrMagnitude <= absolutionRadius * absolutionRadius)
                {
                    other.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, sanctumDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                    if (!other.Dead && !other.Destroyed)
                    {
                        other.stances?.stunner?.StunFor(55, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
