using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.BigBird
{
    /// <summary>
    /// O-02-40 大鸟 E.G.O. 武器「目灯」（EgoWeapon_BigBird）专属动画处理器：
    /// 贴图原图呈 0° 水平朝右（长柄在左、琥珀金焰鸟笼锤头在右）。
    /// 支持沉重单手/双手提灯待机微晃、重锤蓄力高举挥砸（ClipNormalSmash），
    /// 以及主动技能「提灯引渡」的过顶摇灯诱惑蓄力与破防金黑重砸演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class LampWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_BigBird";

        public const string ClipNormalSmash = "Lamp_NormalSmash";
        public const string ClipSkillWarmup = "Lamp_SkillWarmup";
        public const string ClipSkillSlam = "Lamp_SkillSlam";

        private static readonly Vector2 HammerDrawSize = new Vector2(2.05f, 2.05f);
        private static readonly Color AmberGhostTint = new Color(1.0f, 0.80f, 0.24f, 0.85f);

        // === Normal Smash (64 ticks): overhead hoist -> heavy lantern crush -> recovery ===
        private static readonly BodyKeyframe[] NormalSmashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(11f, -0.10f, 0.02f, -12f),
            new BodyKeyframe(18f, 0.20f, -0.03f, 18f),
            new BodyKeyframe(34f, 0.14f, -0.01f, 12f),
            new BodyKeyframe(64f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalSmashHammer =
        {
            new WeaponKeyframe(0f, 0.30f, -0.14f, 14f, 1.00f),
            new WeaponKeyframe(11f, -0.14f, 0.54f, -112f, 1.08f),
            new WeaponKeyframe(18f, 0.62f, -0.22f, 34f, 1.15f),
            new WeaponKeyframe(34f, 0.56f, -0.24f, 38f, 1.06f),
            new WeaponKeyframe(64f, 0.30f, -0.14f, 14f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise glowing brass lantern high & sway solemnly ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.35f, -0.07f, 0.02f, -8f),
            new BodyKeyframe(1.00f, -0.12f, 0.03f, -14f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupHammer =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.14f, 14f, 1.00f),
            new WeaponKeyframe(0.40f, 0.14f, 0.42f, -68f, 1.06f),
            new WeaponKeyframe(1.00f, 0.02f, 0.56f, -88f, 1.10f)
        };

        // === Skill Slam (96 ticks): beacon flare sweep + colossal overhead lantern crush (beat 2 @ tick 26) ===
        private static readonly BodyKeyframe[] SkillSlamBody =
        {
            new BodyKeyframe(0f, -0.12f, 0.03f, -14f),
            new BodyKeyframe(10f, 0.08f, 0.00f, 8f),
            new BodyKeyframe(18f, -0.18f, 0.04f, -20f),
            new BodyKeyframe(26f, 0.28f, -0.05f, 28f),
            new BodyKeyframe(46f, 0.24f, -0.04f, 22f),
            new BodyKeyframe(68f, 0.12f, -0.02f, 10f),
            new BodyKeyframe(96f, 0.00f, 0.00f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillSlamHammer =
        {
            new WeaponKeyframe(0f, 0.02f, 0.56f, -88f, 1.10f),
            new WeaponKeyframe(8f, 0.46f, 0.12f, -14f, 1.14f),
            new WeaponKeyframe(18f, -0.18f, 0.60f, -124f, 1.18f),
            new WeaponKeyframe(26f, 0.68f, -0.24f, 38f, 1.24f),
            new WeaponKeyframe(44f, 0.66f, -0.25f, 40f, 1.14f),
            new WeaponKeyframe(68f, 0.48f, -0.20f, 24f, 1.06f),
            new WeaponKeyframe(96f, 0.30f, -0.14f, 14f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillSlam))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalSmash, 64, aimAngle);
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
                case ClipNormalSmash:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalSmashBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillSlam:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillSlamBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 5.5f : 3.0f);
                float posX = moving ? 0.20f : 0.30f;
                float posZ = (moving ? 0.02f : -0.14f) + Mathf.Sin(tickPhase * 0.75f) * 0.018f;
                float angle = (moving ? -18f : 14f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(
                    mat,
                    ctx.BaseDrawPos,
                    alt,
                    posX,
                    posZ,
                    angle,
                    HammerDrawSize,
                    ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalSmash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalSmashHammer, t);
                    if (t >= 10f && t <= 24f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalSmashHammer, t, ctx.BaseDrawPos, alt, HammerDrawSize, ctx.FacingLeft, AmberGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, HammerDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupHammer, prog);
                    float lanternSway = Mathf.Sin(t * 0.16f) * 10f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg + lanternSway, HammerDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillSlam:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillSlamHammer, t);
                    if (t >= 18f && t <= 32f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillSlamHammer, t, ctx.BaseDrawPos, alt, HammerDrawSize, ctx.FacingLeft, AmberGhostTint);
                    }
                    float impactQuake = (t >= 25f && t <= 35f) ? Mathf.Sin(t * 2.5f) * 0.025f * (1f - (t - 25f) / 10f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + impactQuake, kf.AngleDeg, HammerDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「提灯引渡」蓄力阶段环绕持握者脉动的金色明灯与群瞳视界光环特效。</summary>
    public class Thing_LampSkillBeacon : Thing
    {
        private Pawn caster;
        private Verb warmupVerb;
        private int ageTicks;
        private int maxTicks = 80;
        private bool initialized;

        private MaterialPropertyBlock waveProps;
        private MaterialPropertyBlock eyeProps;

        public void Initialize(Pawn casterPawn, Verb verb, int expectedTicks)
        {
            caster = casterPawn;
            warmupVerb = verb;
            ageTicks = 0;
            maxTicks = Mathf.Max(16, expectedTicks);
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
            if (!initialized || caster == null || !caster.Spawned)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            float alpha = Mathf.Clamp01(ageTicks / 6f) * (t > 0.85f ? Mathf.Clamp01((1f - t) / 0.15f) : 1f);
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            float ageSecs = ageTicks / 60f;

            if (BigBirdMaterials.LanternWaveMat != null)
            {
                waveProps ??= new MaterialPropertyBlock();
                waveProps.SetColor(BigBirdMaterials.PropColor, new Color(1.0f, 0.84f, 0.24f, 1.0f));
                waveProps.SetFloat(BigBirdMaterials.PropProgress, (ageTicks % 24) / 24f);
                waveProps.SetFloat(BigBirdMaterials.PropAgeSecs, ageSecs);
                waveProps.SetFloat(BigBirdMaterials.PropAlpha, alpha * 0.72f);
                waveProps.SetFloat(BigBirdMaterials.PropIntensity, 1.35f);

                float sz = Mathf.Lerp(1.6f, 3.0f, (ageTicks % 24) / 24f);
                Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(cPos.x, alt, cPos.z + 0.25f), Quaternion.identity, new Vector3(sz, 1f, sz));
                Graphics.DrawMesh(MeshPool.plane10, matrix, BigBirdMaterials.LanternWaveMat, 0, null, 0, waveProps);
            }

            if (BigBirdMaterials.EyeMarkMat != null)
            {
                eyeProps ??= new MaterialPropertyBlock();
                eyeProps.SetColor(BigBirdMaterials.PropEyeTint, new Color(1.0f, 0.88f, 0.22f, 1.0f));
                eyeProps.SetFloat(BigBirdMaterials.PropProgress, t);
                eyeProps.SetFloat(BigBirdMaterials.PropAgeSecs, ageSecs);
                eyeProps.SetFloat(BigBirdMaterials.PropAlpha, alpha * 0.90f);
                eyeProps.SetFloat(BigBirdMaterials.PropDanger, 0.65f);

                for (int i = 0; i < 3; i++)
                {
                    float orbitAng = ageSecs * 2.4f + i * (Mathf.PI * 2f / 3f);
                    Vector3 eyePos = cPos + new Vector3(Mathf.Cos(orbitAng) * 0.72f, 0f, Mathf.Sin(orbitAng) * 0.56f + 0.55f);
                    eyePos.y = alt + 0.03f;
                    Matrix4x4 emat = Matrix4x4.TRS(eyePos, Quaternion.identity, new Vector3(0.70f, 1f, 0.70f));
                    Graphics.DrawMesh(MeshPool.plane10, emat, BigBirdMaterials.EyeMarkMat, 0, null, 0, eyeProps);
                }
            }
        }
    }

    /// <summary>目灯主动技能「提灯引渡」蓄力动词：高举明灯摇曳并唤出三枚金色环绕眼眸印记。</summary>
    public class Verb_CastAbility_LampSalvation : Verb_CastAbility
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
                    LampWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null && BigBirdDefOf.LC_LampSkillBeaconFX != null && casterPawn.PositionHeld.InBounds(map))
                {
                    if (GenSpawn.Spawn(BigBirdDefOf.LC_LampSkillBeaconFX, casterPawn.PositionHeld, map) is Thing_LampSkillBeacon beacon)
                    {
                        beacon.Initialize(casterPawn, this, warmupTicks);
                    }
                }

                LCAbnormalitiesHelper.PlaySoundAt(BigBirdDefOf.LC_BigBird_Attract, casterPawn);
            }

            return started;
        }
    }

    public class CompProperties_AbilityLampSalvation : CompProperties_AbilityEffect
    {
        public float pulseRadius = 5.5f;
        public float slamRadius = 3.2f;
        public FloatRange pulseDamageRange = new FloatRange(16f, 22f);
        public FloatRange slamDamageRange = new FloatRange(36f, 48f);
        public float armorPenetration = 0.45f;

        public CompProperties_AbilityLampSalvation()
        {
            compClass = typeof(CompAbilityEffect_LampSalvation);
        }
    }

    /// <summary>
    /// 目灯主动技能「提灯引渡」效果处理器：
    /// 第 1 拍释放 5.5 格范围黄金惑光波晕眩敌军并施加「目灯凝视」，
    /// 第 2 拍（26 ticks 后）随巨型铜灯砸下造成毁灭性黑伤冲击（对受凝视目标 +30% 增伤）。
    /// </summary>
    public class CompAbilityEffect_LampSalvation : CompAbilityEffect
    {
        public new CompProperties_AbilityLampSalvation Props => (CompProperties_AbilityLampSalvation)props;

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
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == LampWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, LampWeaponAnimHandler.ClipSkillSlam, 96, aimAngle);
            }

            // Beat 1: glowing golden lantern allure pulse around caster (dazes + stuns hostile foes)
            BigBirdVfxUtility.SpawnEnchantWave(map, casterPos, Props.pulseRadius, duration: 48, intensity: 1.65f);
            DamageDef skillDmg = BigBirdDefOf.LC_Lamp_SkillBlack ?? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(LampWeaponAnimHandler.WeaponDefName);

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned || !foe.HostileTo(caster))
                {
                    continue;
                }

                if ((foe.DrawPos.Yto0() - casterPos).sqrMagnitude <= Props.pulseRadius * Props.pulseRadius)
                {
                    if (BigBirdDefOf.LC_LampDazed != null)
                    {
                        LCAbnormalitiesHelper.ApplyOrRefreshHediff(foe, BigBirdDefOf.LC_LampDazed, 1.0f);
                        BigBirdVfxUtility.SpawnLampEyeMark(foe, duration: 90);
                    }
                    foe.stances?.stunner?.StunFor(60, caster, addBattleLog: false);
                    foe.TakeDamage(new DamageInfo(skillDmg, Props.pulseDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
                }
            }

            if (BigBirdDefOf.LC_LampSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(BigBirdDefOf.LC_LampSkillRunnerFX, caster.PositionHeld, map) is Thing_LampSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.slamRadius, Props.slamDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「提灯引渡」第 2 拍（第 26 tick）重锤砸地爆裂演出的逻辑实体。</summary>
    public class Thing_LampSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 lastAnchorPos;
        private float slamRadius;
        private FloatRange slamDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool slamExecuted;
        private bool initialized;

        private const int SlamTriggerTick = 26;
        private const int TotalDurationTicks = 92;

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
            lastAnchorPos = initialAnchor.Yto0();
            slamRadius = Mathf.Max(2.0f, radius);
            slamDmgRange = damageRange;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            slamExecuted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != LampWeaponAnimHandler.WeaponDefName)
            {
                Destroy();
                return;
            }

            ageTicks++;

            Pawn target = ResolveTarget();
            if (target != null && target.Spawned)
            {
                lastAnchorPos = target.DrawPos.Yto0();
                float updatedAim = EgoWeaponAnimationUtility.ResolveAimAngle(caster, lastAnchorPos);
                EgoWeaponAnimationManager.UpdateAimAngle(caster, updatedAim);
            }

            if (!slamExecuted && ageTicks >= SlamTriggerTick)
            {
                slamExecuted = true;
                ExecuteSlam();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteSlam()
        {
            if (Map == null)
            {
                return;
            }

            IntVec3 anchorCell = lastAnchorPos.ToIntVec3().ClampInsideMap(Map);
            if (!anchorCell.InBounds(Map))
            {
                return;
            }

            if (BigBirdDefOf.LC_Lamp_Hammer != null)
            {
                BigBirdDefOf.LC_Lamp_Hammer.PlayOneShot(new TargetInfo(anchorCell, Map));
            }
            if (BigBirdDefOf.LC_BigBird_Dead1 != null)
            {
                BigBirdDefOf.LC_BigBird_Dead1.PlayOneShot(new TargetInfo(anchorCell, Map));
            }

            BigBirdVfxUtility.SpawnLampImpact(Map, lastAnchorPos, radius: slamRadius + 0.3f, proc: true);
            BigBirdVfxUtility.SpawnBiteBurst(Map, lastAnchorPos, scale: 2.8f, duration: 28, windup: false);

            DamageDef skillDmg = BigBirdDefOf.LC_Lamp_SkillBlack ?? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            List<Pawn> candidates = new List<Pawn>(Map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned || !foe.HostileTo(caster))
                {
                    continue;
                }

                if ((foe.DrawPos.Yto0() - lastAnchorPos).sqrMagnitude <= slamRadius * slamRadius)
                {
                    bool marked = BigBirdDefOf.LC_LampDazed != null && (foe.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_LampDazed) ?? false);
                    float dmg = slamDmgRange.RandomInRange * (marked ? 1.30f : 1.0f);
                    foe.TakeDamage(new DamageInfo(skillDmg, dmg, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && BigBirdDefOf.LC_LampDazed != null)
                    {
                        LCAbnormalitiesHelper.ApplyOrRefreshHediff(foe, BigBirdDefOf.LC_LampDazed, 1.0f);
                        BigBirdVfxUtility.SpawnLampEyeMark(foe, duration: 75);
                    }
                }
            }
        }

        private Pawn ResolveTarget()
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
            float bestDistSq = 6.0f * 6.0f;
            IReadOnlyList<Pawn> pawns = Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate == caster || candidate.Dead || candidate.Destroyed || !candidate.Spawned || !candidate.HostileTo(caster))
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

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
