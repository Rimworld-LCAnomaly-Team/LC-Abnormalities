using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.VoidDream
{
    /// <summary>虚空之梦与 E.G.O. 武器“梦乡”专属 GLSL 450 着色器与材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class VoidDreamMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropAccentCol = Shader.PropertyToID("_AccentCol");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropDangerCol = Shader.PropertyToID("_DangerCol");
        public static readonly int PropVoidColor = Shader.PropertyToID("_VoidColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropDanger = Shader.PropertyToID("_Danger");

        public static readonly Material OrbMat;
        public static readonly Material CrowWaveMat;
        public static readonly Material SleepCloudMat;
        public static readonly Material NightmareBurstMat;
        public static readonly Material SleepZMat;

        static VoidDreamMaterials()
        {
            Shader orbShader = ShaderDatabase.LoadShader("Map/LC_VoidDreamOrb") ?? ShaderDatabase.MoteGlow;
            Shader waveShader = ShaderDatabase.LoadShader("Map/LC_VoidDreamCrowWave") ?? ShaderDatabase.MoteGlow;
            Shader cloudShader = ShaderDatabase.LoadShader("Map/LC_VoidDreamSleepCloud") ?? ShaderDatabase.Transparent;
            Shader nightmareShader = ShaderDatabase.LoadShader("Map/LC_VoidDreamNightmareBurst") ?? ShaderDatabase.Transparent;

            Texture2D cloudTex = ContentFinder<Texture2D>.Get("Things/VFX/VoidDream/VoidDream_CloudPuff", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D sleepZTex = ContentFinder<Texture2D>.Get("Things/VFX/VoidDream/VoidDream_SleepZ", reportFailure: false) ?? BaseContent.WhiteTex;

            OrbMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = orbShader,
                color = new Color(0.76f, 0.44f, 1.0f, 1.0f)
            });

            CrowWaveMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = waveShader,
                color = new Color(0.66f, 0.34f, 0.96f, 1.0f)
            });

            SleepCloudMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = cloudTex,
                shader = cloudShader,
                color = new Color(0.84f, 0.76f, 0.98f, 0.90f)
            });

            NightmareBurstMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = nightmareShader,
                color = new Color(0.84f, 0.22f, 0.72f, 1.0f)
            });

            SleepZMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = sleepZTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 绵羊形态发射的催眠梦境光球（BakuSheepSkill / Dreamy_Ball）：
    /// 沿微幅正弦轨迹飘浮飞向目标人类，沿途洒落紫/绿梦境尘辉，命中后令目标陷入虚空催眠（LC_VoidDreamSleep）。
    /// </summary>
    public class Thing_VoidDreamSleepOrb : Thing
    {
        private Pawn caster;
        private Pawn targetPawn;
        private Vector3 exactPos;
        private Vector3 velocity;
        private float speedPerTick = 0.145f;
        private int ageTicks;
        private int maxLifetimeTicks = 170;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn source, Pawn target, Vector3 startPos, Vector3 initialDir, float speed = 0.145f, int lifetime = 170)
        {
            caster = source;
            targetPawn = target;
            exactPos = startPos.Yto0();
            Vector3 dir = initialDir.Yto0();
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();
            speedPerTick = Mathf.Clamp(speed, 0.07f, 0.28f);
            velocity = dir * speedPerTick;
            maxLifetimeTicks = Mathf.Max(30, lifetime);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (ageTicks >= maxLifetimeTicks)
            {
                VoidDreamVfxUtility.SpawnCrowWave(Map, exactPos, 1.5f, duration: 22,
                    color: new Color(0.76f, 0.44f, 1.0f, 0.85f),
                    accentColor: new Color(0.38f, 0.96f, 0.66f, 0.85f),
                    intensity: 1.05f);
                Destroy();
                return;
            }

            // Gentle homing steer toward intended victim if still awake and valid
            if (targetPawn != null && !targetPawn.Dead && targetPawn.Spawned && targetPawn.Map == Map)
            {
                Vector3 toTarget = (targetPawn.DrawPos.Yto0() - exactPos);
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 desired = toTarget.normalized * speedPerTick;
                    velocity = Vector3.Lerp(velocity, desired, 0.09f).normalized * speedPerTick;
                }
            }

            // Dreamy sinusoidal lateral weave
            Vector3 perp = new Vector3(-velocity.z, 0f, velocity.x).normalized;
            float weave = Mathf.Sin(ageTicks * 0.22f) * 0.025f;
            exactPos += velocity + perp * weave;

            IntVec3 cell = exactPos.ToIntVec3();
            if (!cell.InBounds(Map))
            {
                Destroy();
                return;
            }

            if (Position != cell)
            {
                Position = cell;
            }

            // Soft alternating pink-lavender & emerald-green dream stardust trail
            if (ageTicks % 6 == 0)
            {
                Color moteCol = (ageTicks % 12 == 0)
                    ? new Color(0.84f, 0.56f, 1.0f)
                    : new Color(0.45f, 0.94f, 0.68f);
                FleckMaker.ThrowDustPuffThick(exactPos, Map, 0.55f, moteCol);
            }

            // Check impact with any awake humanlike within 0.85 cells
            IReadOnlyList<Pawn> pawns = Map.mapPawns?.AllPawnsSpawned;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == null || p == caster || p.Dead || !p.Spawned || !(p.RaceProps?.Humanlike ?? false))
                    {
                        continue;
                    }

                    if (VoidDreamDefOf.LC_VoidDreamSleep != null
                        && (p.health?.hediffSet?.HasHediff(VoidDreamDefOf.LC_VoidDreamSleep) ?? false))
                    {
                        continue;
                    }

                    float distSq = (p.DrawPos.Yto0() - exactPos).sqrMagnitude;
                    if (distSq <= 0.85f * 0.85f)
                    {
                        TriggerSleepHit(p);
                        Destroy();
                        return;
                    }
                }
            }
        }

        private void TriggerSleepHit(Pawn victim)
        {
            Map map = Map;
            if (map == null || victim == null || victim.Dead)
            {
                return;
            }

            LCAbnormalitiesHelper.ApplyOrRefreshHediff(victim, VoidDreamDefOf.LC_VoidDreamSleep, 1f);
            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Skill, victim);
            VoidDreamVfxUtility.SpawnCrowWave(map, victim.DrawPos, 2.6f, duration: 30,
                color: new Color(0.76f, 0.44f, 1.0f, 1.0f),
                accentColor: new Color(0.38f, 0.96f, 0.66f, 1.0f),
                intensity: 1.35f);
            VoidDreamVfxUtility.EnsureSleepAura(victim);
            Messages.Message("LC_VoidDream_SleepMsg".Translate(victim.LabelShortCap), victim, MessageTypeDefOf.ThreatSmall, historical: false);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || VoidDreamMaterials.OrbMat == null)
            {
                return;
            }

            float fadeIn = Mathf.Clamp01(ageTicks / 10f);
            float fadeOut = Mathf.Clamp01((maxLifetimeTicks - ageTicks) / 14f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float size = 1.25f + 0.14f * Mathf.Sin(ageSecs * 8.5f);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            props ??= new MaterialPropertyBlock();
            props.SetColor(VoidDreamMaterials.PropColor, new Color(0.78f, 0.44f, 1.0f, 1.0f));
            props.SetColor(VoidDreamMaterials.PropAccentCol, new Color(0.38f, 0.96f, 0.66f, 1.0f));
            props.SetColor(VoidDreamMaterials.PropCoreColor, new Color(1.0f, 0.97f, 1.0f, 1.0f));
            props.SetFloat(VoidDreamMaterials.PropAgeSecs, ageSecs);
            props.SetFloat(VoidDreamMaterials.PropAlpha, alpha);
            props.SetFloat(VoidDreamMaterials.PropIntensity, 1.55f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, VoidDreamMaterials.OrbMat, 0, null, 0, props);
        }
    }

    /// <summary>虚空之梦声波扩散环特效：用于公鸡三连啼鸣全屏催眠波、变身爆发波与 E.G.O. 武器“梦乡”声波打击。</summary>
    public class Thing_VoidDreamCrowWave : Thing
    {
        private Vector3 centerPos;
        private float maxDiameter = 18f;
        private int durationTicks = 48;
        private int ageTicks;
        private Color waveColor = new Color(0.66f, 0.34f, 0.96f, 1.0f);
        private Color accentColor = new Color(0.96f, 0.28f, 0.62f, 1.0f);
        private Color coreColor = new Color(0.98f, 0.95f, 1.0f, 1.0f);
        private float intensity = 1.45f;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(
            Vector3 pos,
            float radius,
            int duration = 48,
            Color? color = null,
            Color? accent = null,
            Color? core = null,
            float customIntensity = 1.45f)
        {
            centerPos = pos.Yto0();
            maxDiameter = Mathf.Max(1.2f, radius * 2.0f);
            durationTicks = Mathf.Max(12, duration);
            waveColor = color ?? new Color(0.66f, 0.34f, 0.96f, 1.0f);
            accentColor = accent ?? new Color(0.96f, 0.28f, 0.62f, 1.0f);
            coreColor = core ?? new Color(0.98f, 0.95f, 1.0f, 1.0f);
            intensity = customIntensity;
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
            if (!initialized || VoidDreamMaterials.CrowWaveMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            float alpha = Mathf.Pow(1f - t, 1.25f);
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(VoidDreamMaterials.PropColor, waveColor);
            props.SetColor(VoidDreamMaterials.PropAccentCol, accentColor);
            props.SetColor(VoidDreamMaterials.PropCoreColor, coreColor);
            props.SetFloat(VoidDreamMaterials.PropProgress, easeOut);
            props.SetFloat(VoidDreamMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(VoidDreamMaterials.PropAlpha, alpha);
            props.SetFloat(VoidDreamMaterials.PropIntensity, intensity);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(centerPos.x, alt, centerPos.z),
                Quaternion.identity,
                new Vector3(maxDiameter, 1f, maxDiameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, VoidDreamMaterials.CrowWaveMat, 0, null, 0, props);
        }
    }

    /// <summary>
    /// 虚空催眠云与漂浮 Zzz 标记（绑定于 LC_VoidDreamSleep 昏睡目标）：
    /// 呈现托举沉睡者的梦幻云团与升腾 Z 字符；随着 60 秒噩梦倒计时逼近，云团由柔紫逐渐转为闪烁猩红噩梦脉冲。
    /// </summary>
    public class Thing_VoidDreamSleepAura : Thing
    {
        private Pawn sleeper;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock cloudProps;
        private MaterialPropertyBlock zProps;

        public Pawn TargetSleeper => sleeper;

        public void Initialize(Pawn victim)
        {
            sleeper = victim;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || sleeper == null || sleeper.Destroyed || sleeper.Dead || !sleeper.Spawned || sleeper.Map != Map)
            {
                Destroy();
                return;
            }

            Hediff sleepHediff = VoidDreamDefOf.LC_VoidDreamSleep != null
                ? sleeper.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep)
                : null;
            if (sleepHediff == null)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (sleeper.Position.InBounds(Map) && Position != sleeper.Position)
            {
                Position = sleeper.Position;
            }

            if (ageTicks % 32 == 0)
            {
                FleckMaker.ThrowDustPuffThick(sleeper.DrawPos + new Vector3(0f, 0f, 0.25f), Map, 0.48f, new Color(0.76f, 0.54f, 0.98f));
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || sleeper == null || !sleeper.Spawned || VoidDreamMaterials.SleepCloudMat == null)
            {
                return;
            }

            HediffWithComps hwc = sleeper.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep) as HediffWithComps;
            HediffComp_VoidDreamSleep sleepComp = hwc?.TryGetComp<HediffComp_VoidDreamSleep>();
            float danger = sleepComp != null ? sleepComp.DangerFraction : 0f;

            float ageSecs = ageTicks / 60f;
            float fadeIn = Mathf.Clamp01(ageTicks / 16f);
            Vector3 p = sleeper.DrawPos.Yto0();

            // Pass 1: Swirling hypnotic dream cloud halo underneath/around the sleeping colonist
            cloudProps ??= new MaterialPropertyBlock();
            cloudProps.SetColor(VoidDreamMaterials.PropColor, new Color(0.84f, 0.76f, 0.98f, 0.90f));
            cloudProps.SetColor(VoidDreamMaterials.PropDangerCol, new Color(0.92f, 0.16f, 0.44f, 0.96f));
            cloudProps.SetFloat(VoidDreamMaterials.PropAgeSecs, ageSecs);
            cloudProps.SetFloat(VoidDreamMaterials.PropAlpha, fadeIn);
            cloudProps.SetFloat(VoidDreamMaterials.PropDanger, danger);

            float cloudScale = 1.65f + 0.08f * Mathf.Sin(ageSecs * (2.6f + danger * 5.0f));
            float cloudAlt = AltitudeLayer.Blueprint.AltitudeFor() + 0.04f;
            Matrix4x4 cloudMat = Matrix4x4.TRS(
                new Vector3(p.x, cloudAlt, p.z - 0.08f),
                Quaternion.identity,
                new Vector3(cloudScale * 1.12f, 1f, cloudScale * 0.86f));
            Graphics.DrawMesh(MeshPool.plane10, cloudMat, VoidDreamMaterials.SleepCloudMat, 0, null, 0, cloudProps);

            // Pass 2: Two staggered floating bubbly 'Z' sleep glyphs drifting upward overhead
            if (VoidDreamMaterials.SleepZMat != null)
            {
                zProps ??= new MaterialPropertyBlock();
                float zAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
                for (int i = 0; i < 2; i++)
                {
                    float phase = (ageSecs * 0.65f + i * 0.5f) % 1.0f;
                    float zAlpha = Mathf.Sin(phase * Mathf.PI) * fadeIn;
                    if (zAlpha <= 0.02f) continue;

                    float zSize = Mathf.Lerp(0.32f, 0.58f, phase);
                    float zx = p.x + 0.22f + 0.18f * Mathf.Sin(phase * 4.2f + i * 2.1f);
                    float zz = p.z + 0.58f + phase * 0.68f;
                    Color zTint = Color.Lerp(new Color(0.95f, 0.92f, 1.0f, zAlpha), new Color(1.0f, 0.42f, 0.64f, zAlpha), danger);
                    zProps.SetColor(VoidDreamMaterials.PropColor, zTint);

                    Matrix4x4 zMat = Matrix4x4.TRS(
                        new Vector3(zx, zAlt + i * 0.01f, zz),
                        Quaternion.AngleAxis(Mathf.Sin(phase * 5.0f) * 10f, Vector3.up),
                        new Vector3(zSize, 1f, zSize));
                    Graphics.DrawMesh(MeshPool.plane10, zMat, VoidDreamMaterials.SleepZMat, 0, null, 0, zProps);
                }
            }
        }
    }

    /// <summary>噩梦反噬 / 二次啼鸣撕裂爆发特效（暗紫黑域 + 绯红白黑双伤碎梦冲击）。</summary>
    public class Thing_VoidDreamNightmareBurst : Thing
    {
        private Vector3 exactPos;
        private float scale = 3.1f;
        private int ageTicks;
        private int durationTicks = 34;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float burstScale = 3.1f, int duration = 34)
        {
            exactPos = pos.Yto0();
            scale = Mathf.Max(1.4f, burstScale);
            durationTicks = Mathf.Max(16, duration);
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
            if (!initialized || VoidDreamMaterials.NightmareBurstMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - t, 1.25f);
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(VoidDreamMaterials.PropColor, new Color(0.88f, 0.24f, 0.72f, 1.0f));
            props.SetColor(VoidDreamMaterials.PropVoidColor, new Color(0.05f, 0.01f, 0.10f, 0.92f));
            props.SetFloat(VoidDreamMaterials.PropProgress, t);
            props.SetFloat(VoidDreamMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(VoidDreamMaterials.PropAlpha, alpha);

            float curSize = scale * Mathf.Lerp(0.70f, 1.38f, 1f - Mathf.Pow(1f - t, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(curSize, 1f, curSize));
            Graphics.DrawMesh(MeshPool.plane10, mat, VoidDreamMaterials.NightmareBurstMat, 0, null, 0, props);
        }
    }

    /// <summary>E.G.O. 武器“梦乡（Engulfing Dream）”开火动词：在公鸡喙口喷射紫白声波环（BakuWeaponEffect）。</summary>
    public class Verb_ShootVoidDreamEgo : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && caster != null && caster.Spawned && caster.Map != null)
            {
                Vector3 shotTarget = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (caster.DrawPos.Yto0() + Vector3.right);
                if (caster is Pawn shooterPawn &&
                    !EgoWeaponAnimationManager.IsPlaying(shooterPawn, VoidDreamWeaponAnimHandler.ClipSkillWarmup) &&
                    !EgoWeaponAnimationManager.IsPlaying(shooterPawn, VoidDreamWeaponAnimHandler.ClipSkillLullaby))
                {
                    float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(shooterPawn, shotTarget);
                    EgoWeaponAnimationManager.Play(shooterPawn, shooterPawn.equipment?.Primary, VoidDreamWeaponAnimHandler.ClipRangeShot, 24, aimAngle);
                }
                Vector3 origin = caster.DrawPos.Yto0();
                Vector3 aim = currentTarget.CenterVector3.Yto0() - origin;
                Vector3 dir = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector3.right;
                Vector3 muzzlePos = origin + dir * 0.58f;

                VoidDreamVfxUtility.SpawnCrowWave(
                    caster.Map,
                    muzzlePos,
                    radius: 0.85f,
                    duration: 16,
                    color: new Color(0.82f, 0.62f, 1.0f, 0.92f),
                    accentColor: new Color(0.62f, 0.95f, 0.96f, 0.88f),
                    coreColor: new Color(1.0f, 0.98f, 1.0f, 1.0f),
                    intensity: 1.25f);
                FleckMaker.ThrowDustPuffThick(muzzlePos, caster.Map, 0.42f, new Color(0.86f, 0.72f, 1.0f));
            }
            return fired;
        }
    }

    /// <summary>
    /// E.G.O. 武器“梦乡（Engulfing Dream）”专属白伤声波弹丸：
    /// 飞行时携带柔光紫白梦境光晕与微尘尾迹，命中目标时绽放原版风格 BakuWeaponEffect 白伤催眠声波涟漪。
    /// </summary>
    public class Projectile_VoidDreamEgo : Bullet
    {
        private MaterialPropertyBlock haloProps;
        private int flightTicks;

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed || !Spawned || Map == null)
            {
                return;
            }

            flightTicks++;
            if (flightTicks % 2 == 0)
            {
                FleckMaker.ThrowDustPuffThick(ExactPosition, Map, 0.34f, new Color(0.84f, 0.68f, 1.0f));
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            if (VoidDreamMaterials.OrbMat != null)
            {
                haloProps ??= new MaterialPropertyBlock();
                haloProps.SetColor(VoidDreamMaterials.PropColor, new Color(0.82f, 0.58f, 1.0f, 0.88f));
                haloProps.SetColor(VoidDreamMaterials.PropAccentCol, new Color(0.62f, 0.96f, 0.92f, 0.82f));
                haloProps.SetColor(VoidDreamMaterials.PropCoreColor, new Color(1.0f, 0.98f, 1.0f, 1.0f));
                haloProps.SetFloat(VoidDreamMaterials.PropAgeSecs, flightTicks / 60f);
                haloProps.SetFloat(VoidDreamMaterials.PropAlpha, 0.85f);
                haloProps.SetFloat(VoidDreamMaterials.PropIntensity, 1.25f);

                Vector3 p = ExactPosition.Yto0();
                float alt = AltitudeLayer.Projectile.AltitudeFor() - 0.01f;
                Matrix4x4 mat = Matrix4x4.TRS(
                    new Vector3(p.x, alt, p.z),
                    Quaternion.identity,
                    new Vector3(0.76f, 1f, 0.76f));
                Graphics.DrawMesh(MeshPool.plane10, mat, VoidDreamMaterials.OrbMat, 0, null, 0, haloProps);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 pos = hitThing != null ? hitThing.DrawPos : ExactPosition;
            base.Impact(hitThing, blockedByShield);

            if (map != null)
            {
                VoidDreamVfxUtility.SpawnEgoWeaponImpact(map, pos);
            }
        }
    }

    /// <summary>虚空之梦与 E.G.O. “梦乡”特效生成辅助工具。</summary>
    public static class VoidDreamVfxUtility
    {
        public static Thing_VoidDreamSleepOrb SpawnSleepOrb(Pawn caster, Pawn target, Vector3 startPos, Vector3 dir, float speed = 0.145f)
        {
            if (caster == null || !caster.Spawned || caster.Map == null || VoidDreamDefOf.LC_VoidDreamSleepOrbFX == null)
            {
                return null;
            }

            IntVec3 anchor = startPos.ToIntVec3().ClampInsideMap(caster.Map);
            Thing_VoidDreamSleepOrb orb = (Thing_VoidDreamSleepOrb)GenSpawn.Spawn(
                VoidDreamDefOf.LC_VoidDreamSleepOrbFX, anchor, caster.Map);
            orb.Initialize(caster, target, startPos, dir, speed);
            return orb;
        }

        public static void SpawnCrowWave(
            Map map,
            Vector3 pos,
            float radius,
            int duration = 48,
            Color? color = null,
            Color? accentColor = null,
            Color? coreColor = null,
            float intensity = 1.45f)
        {
            if (map == null || VoidDreamDefOf.LC_VoidDreamCrowWaveFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_VoidDreamCrowWave wave = (Thing_VoidDreamCrowWave)GenSpawn.Spawn(
                VoidDreamDefOf.LC_VoidDreamCrowWaveFX, anchor, map);
            wave.Initialize(pos, radius, duration, color, accentColor, coreColor, intensity);
        }

        public static void EnsureSleepAura(Pawn victim)
        {
            if (victim == null || !victim.Spawned || victim.Map == null || VoidDreamDefOf.LC_VoidDreamSleepAuraFX == null)
            {
                return;
            }

            List<Thing> existing = victim.Map.listerThings?.ThingsOfDef(VoidDreamDefOf.LC_VoidDreamSleepAuraFX);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] is Thing_VoidDreamSleepAura aura && !aura.Destroyed && aura.TargetSleeper == victim)
                    {
                        return;
                    }
                }
            }

            Thing_VoidDreamSleepAura spawned = (Thing_VoidDreamSleepAura)GenSpawn.Spawn(
                VoidDreamDefOf.LC_VoidDreamSleepAuraFX, victim.Position, victim.Map);
            spawned.Initialize(victim);
        }

        public static void SpawnNightmareBurst(Map map, Vector3 pos, float scale = 3.2f)
        {
            if (map == null)
            {
                return;
            }

            if (VoidDreamDefOf.LC_VoidDreamNightmareBurstFX != null)
            {
                IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
                Thing_VoidDreamNightmareBurst burst = (Thing_VoidDreamNightmareBurst)GenSpawn.Spawn(
                    VoidDreamDefOf.LC_VoidDreamNightmareBurstFX, anchor, map);
                burst.Initialize(pos, scale, 36);
            }

            SpawnCrowWave(map, pos, scale * 1.15f, duration: 30,
                color: new Color(0.88f, 0.22f, 0.72f, 1.0f),
                accentColor: new Color(0.28f, 0.08f, 0.56f, 1.0f),
                intensity: 1.55f);

            FleckMaker.ThrowDustPuffThick(pos, map, 1.9f, new Color(0.22f, 0.06f, 0.34f));
            FleckMaker.ThrowDustPuffThick(pos, map, 1.5f, new Color(0.92f, 0.30f, 0.78f));
            FleckMaker.ThrowMicroSparks(pos, map);

            if (Find.CurrentMap == map && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(0.45f);
            }
        }

        public static void SpawnEgoWeaponImpact(Map map, Vector3 pos)
        {
            if (map == null)
            {
                return;
            }

            SpawnCrowWave(
                map,
                pos,
                radius: 1.45f,
                duration: 22,
                color: new Color(0.80f, 0.58f, 1.0f, 1.0f),
                accentColor: new Color(0.55f, 0.96f, 0.92f, 1.0f),
                coreColor: new Color(1.0f, 0.98f, 1.0f, 1.0f),
                intensity: 1.38f);
            FleckMaker.ThrowDustPuffThick(pos, map, 0.72f, new Color(0.88f, 0.76f, 1.0f));
            FleckMaker.ThrowMicroSparks(pos, map);
        }
    }
}
