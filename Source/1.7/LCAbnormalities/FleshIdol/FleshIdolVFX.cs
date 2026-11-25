using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FleshIdol
{
    [DefOf]
    public static class FleshIdolVfxDefOf
    {
        public static ThingDef LC_FleshIdolPrayerAuraFX;
        public static ThingDef LC_FleshIdolMiracleWaveFX;
        public static ThingDef LC_FleshIdolHealMoteFX;

        static FleshIdolVfxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FleshIdolVfxDefOf));
        }
    }

    /// <summary>T-09-79 血肉偶像：活体十字架蠕动着色器、神迹治愈波与过载爆裂材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class FleshIdolMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropPhase = Shader.PropertyToID("_Phase");
        public static readonly int PropPulseAmt = Shader.PropertyToID("_PulseAmt");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material CrossMat;
        public static readonly Material MiracleWaveMat;
        public static readonly Material PrayerBeamMat;

        static FleshIdolMaterials()
        {
            Shader crossShader = ShaderDatabase.LoadShader("Map/LC_FleshIdolCross") ?? ShaderDatabase.Transparent;
            Shader waveShader = ShaderDatabase.LoadShader("Map/LC_FleshIdolMiracleWave") ?? ShaderDatabase.MoteGlow;
            Texture2D crossTex = ContentFinder<Texture2D>.Get("Things/Building/FleshIdol/FleshIdol", reportFailure: false) ?? BaseContent.WhiteTex;

            CrossMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = crossTex,
                shader = crossShader,
                color = Color.white
            });

            MiracleWaveMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = waveShader,
                color = new Color(1.0f, 0.86f, 0.48f, 1.0f)
            });

            PrayerBeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(1.0f, 0.84f, 0.48f, 0.40f)
            });
        }
    }

    /// <summary>
    /// 血肉偶像持续祈祷仪式灵光（绑定于活跃祈祷期间）：
    /// 随阶段动态演变：蓄能期（0-19s 暗红脉动）→ 神迹期（20-80s 圣金十字辉光）→ 过载期（>80s 狂暴猩红警报旋光），
    /// 并以信仰连线环绕正在祈祷的殖民者。
    /// </summary>
    public class Thing_FleshIdolPrayerAura : Thing
    {
        private Thing hostEntity;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock idolHaloProps;
        private MaterialPropertyBlock userHaloProps;
        private MaterialPropertyBlock beamProps;

        public Thing HostEntity => hostEntity;

        public void Initialize(Thing entity)
        {
            hostEntity = entity;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || hostEntity == null || hostEntity.Destroyed || Map == null)
            {
                Destroy();
                return;
            }

            CompFleshIdol comp = (hostEntity as ThingWithComps)?.TryGetComp<CompFleshIdol>();
            if (comp == null || !comp.IsOperational || comp.ActiveMap != Map || !comp.IsActiveContinuous)
            {
                Destroy();
                return;
            }

            ageTicks++;
            IntVec3 pos = comp.ActivePosition;
            if (pos.IsValid && pos.InBounds(Map) && Position != pos)
            {
                Position = pos;
            }

            int phase = comp.CurrentPhase;
            Vector3 anchor = comp.ActiveDrawPos;
            Pawn user = comp.ActiveContinuousUser;

            if (phase == 1 && ageTicks % 24 == 0)
            {
                FleckMaker.ThrowDustPuffThick(
                    anchor + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.25f, 0.45f)),
                    Map,
                    0.55f,
                    new Color(0.82f, 0.24f, 0.24f));
            }
            else if (phase == 2 && ageTicks % 18 == 0)
            {
                FleckMaker.ThrowDustPuffThick(
                    anchor + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(-0.20f, 0.55f)),
                    Map,
                    0.62f,
                    new Color(1.0f, 0.88f, 0.48f));
            }
            else if (phase == 3)
            {
                if (ageTicks % 9 == 0)
                {
                    FleckMaker.ThrowDustPuffThick(
                        anchor + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(-0.3f, 0.6f)),
                        Map,
                        0.72f,
                        new Color(0.92f, 0.14f, 0.14f));
                    FleckMaker.ThrowMicroSparks(anchor, Map);
                }

                if (user != null && user.Spawned && user.Map == Map && ageTicks % 15 == 0)
                {
                    FleckMaker.ThrowMicroSparks(user.DrawPos, Map);
                }

                if (ageTicks % 35 == 0 && Find.CurrentMap == Map && Find.CameraDriver != null)
                {
                    Find.CameraDriver.shaker.DoShake(0.18f);
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || hostEntity == null || FleshIdolMaterials.MiracleWaveMat == null)
            {
                return;
            }

            CompFleshIdol comp = (hostEntity as ThingWithComps)?.TryGetComp<CompFleshIdol>();
            if (comp == null || !comp.IsActiveContinuous)
            {
                return;
            }

            int phase = comp.CurrentPhase;
            float ageSecs = ageTicks / 60f;
            float fadeIn = Mathf.Clamp01(ageTicks / 18f);

            Color mainCol;
            Color coreCol;
            float danger;
            float intensity;
            float haloSize;

            switch (phase)
            {
                case 2:
                    mainCol = new Color(1.0f, 0.84f, 0.42f, 0.92f);
                    coreCol = new Color(1.0f, 0.98f, 0.88f, 1.0f);
                    danger = 0.0f;
                    intensity = 1.25f + 0.35f * comp.HealPulseFlash;
                    haloSize = 2.95f + 0.18f * comp.HealPulseFlash;
                    break;
                case 3:
                    mainCol = new Color(0.96f, 0.12f, 0.14f, 0.96f);
                    coreCol = new Color(1.0f, 0.82f, 0.78f, 1.0f);
                    danger = 1.0f;
                    intensity = 1.55f;
                    haloSize = 3.25f + 0.22f * Mathf.Sin(ageSecs * 15.0f);
                    break;
                default:
                    mainCol = new Color(0.86f, 0.26f, 0.28f, 0.82f);
                    coreCol = new Color(1.0f, 0.72f, 0.68f, 0.90f);
                    danger = 0.25f;
                    intensity = 0.95f;
                    haloSize = 2.45f;
                    break;
            }

            Vector3 anchor = comp.ActiveDrawPos.Yto0();
            float alt = AltitudeLayer.Blueprint.AltitudeFor() + 0.04f;

            // 1. Ritual halo under the Flesh Idol
            idolHaloProps ??= new MaterialPropertyBlock();
            idolHaloProps.SetColor(FleshIdolMaterials.PropColor, mainCol);
            idolHaloProps.SetColor(FleshIdolMaterials.PropCoreColor, coreCol);
            idolHaloProps.SetFloat(FleshIdolMaterials.PropProgress, danger);
            idolHaloProps.SetFloat(FleshIdolMaterials.PropAgeSecs, ageSecs);
            idolHaloProps.SetFloat(FleshIdolMaterials.PropAlpha, fadeIn * 0.86f);
            idolHaloProps.SetFloat(FleshIdolMaterials.PropIntensity, intensity);
            idolHaloProps.SetFloat(FleshIdolMaterials.PropMode, 1.0f);

            Matrix4x4 idolMat = Matrix4x4.TRS(
                new Vector3(anchor.x, alt, anchor.z),
                Quaternion.identity,
                new Vector3(haloSize, 1f, haloSize));
            Graphics.DrawMesh(MeshPool.plane10, idolMat, FleshIdolMaterials.MiracleWaveMat, 0, null, 0, idolHaloProps);

            // 2. Worshiper prayer aura & devotion beam connecting the praying pawn to the cross
            Pawn user = comp.ActiveContinuousUser;
            if (user != null && user.Spawned && user.Map == Map)
            {
                Vector3 userPos = user.DrawPos.Yto0();
                userHaloProps ??= new MaterialPropertyBlock();
                userHaloProps.SetColor(FleshIdolMaterials.PropColor, mainCol);
                userHaloProps.SetColor(FleshIdolMaterials.PropCoreColor, coreCol);
                userHaloProps.SetFloat(FleshIdolMaterials.PropProgress, danger);
                userHaloProps.SetFloat(FleshIdolMaterials.PropAgeSecs, ageSecs + 0.7f);
                userHaloProps.SetFloat(FleshIdolMaterials.PropAlpha, fadeIn * 0.72f);
                userHaloProps.SetFloat(FleshIdolMaterials.PropIntensity, intensity * 0.85f);
                userHaloProps.SetFloat(FleshIdolMaterials.PropMode, 1.0f);

                float uSize = 1.45f + 0.08f * Mathf.Sin(ageSecs * (phase == 3 ? 12f : 3.5f));
                Matrix4x4 uMat = Matrix4x4.TRS(
                    new Vector3(userPos.x, alt, userPos.z),
                    Quaternion.identity,
                    new Vector3(uSize, 1f, uSize));
                Graphics.DrawMesh(MeshPool.plane10, uMat, FleshIdolMaterials.MiracleWaveMat, 0, null, 0, userHaloProps);

                if (FleshIdolMaterials.PrayerBeamMat != null)
                {
                    Vector3 from = anchor + new Vector3(0f, 0f, 0.35f);
                    Vector3 to = userPos + new Vector3(0f, 0f, 0.20f);
                    Vector3 delta = to - from;
                    float len = delta.magnitude;
                    if (len > 0.25f)
                    {
                        Vector3 mid = (from + to) * 0.5f;
                        Quaternion rot = Quaternion.LookRotation(delta / len, Vector3.up);
                        float width = 0.09f + 0.04f * Mathf.Sin(ageSecs * (phase == 3 ? 14f : 5f));

                        beamProps ??= new MaterialPropertyBlock();
                        Color beamCol = mainCol;
                        beamCol.a = (phase == 3 ? 0.52f : 0.34f) * fadeIn;
                        beamProps.SetColor(FleshIdolMaterials.PropColor, beamCol);

                        Matrix4x4 bMat = Matrix4x4.TRS(
                            new Vector3(mid.x, AltitudeLayer.MoteOverhead.AltitudeFor() - 0.02f, mid.z),
                            rot,
                            new Vector3(width, 1f, len));
                        Graphics.DrawMesh(MeshPool.plane10, bMat, FleshIdolMaterials.PrayerBeamMat, 0, null, 0, beamProps);
                    }
                }
            }
        }
    }

    /// <summary>血肉偶像扩散冲击波：用于神迹治愈波（Mode 0）与 90s 灾变爆裂/早停反噬（Mode 2）。</summary>
    public class Thing_FleshIdolMiracleWave : Thing
    {
        private Vector3 centerPos;
        private float maxDiameter = 12f;
        private int durationTicks = 48;
        private int ageTicks;
        private Color waveColor = new Color(1.0f, 0.86f, 0.48f, 1.0f);
        private Color coreColor = new Color(1.0f, 0.98f, 0.90f, 1.0f);
        private float intensity = 1.4f;
        private float shaderMode;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float radius, int duration, Color color, Color core, float customIntensity, float mode)
        {
            centerPos = pos.Yto0();
            maxDiameter = Mathf.Max(1.8f, radius * 2.0f);
            durationTicks = Mathf.Max(16, duration);
            waveColor = color;
            coreColor = core;
            intensity = customIntensity;
            shaderMode = mode;
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
            if (!initialized || FleshIdolMaterials.MiracleWaveMat == null)
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
            props.SetColor(FleshIdolMaterials.PropColor, waveColor);
            props.SetColor(FleshIdolMaterials.PropCoreColor, coreColor);
            props.SetFloat(FleshIdolMaterials.PropProgress, easeOut);
            props.SetFloat(FleshIdolMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(FleshIdolMaterials.PropAlpha, alpha);
            props.SetFloat(FleshIdolMaterials.PropIntensity, intensity);
            props.SetFloat(FleshIdolMaterials.PropMode, shaderMode);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(centerPos.x, alt, centerPos.z),
                Quaternion.identity,
                new Vector3(maxDiameter, 1f, maxDiameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, FleshIdolMaterials.MiracleWaveMat, 0, null, 0, props);
        }
    }

    /// <summary>被血肉偶像神迹治愈的殖民者身上的圣金十字微光特效。</summary>
    public class Thing_FleshIdolHealMote : Thing
    {
        private Pawn targetPawn;
        private Vector3 fallbackPos;
        private int ageTicks;
        private const int DurationTicks = 34;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn healedPawn)
        {
            targetPawn = healedPawn;
            fallbackPos = healedPawn != null ? healedPawn.DrawPos.Yto0() : Vector3.zero;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
                return;
            }

            if (targetPawn != null && targetPawn.Spawned && targetPawn.Map == Map)
            {
                fallbackPos = targetPawn.DrawPos.Yto0();
                if (targetPawn.Position.InBounds(Map) && Position != targetPawn.Position)
                {
                    Position = targetPawn.Position;
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || FleshIdolMaterials.MiracleWaveMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.88f;
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(FleshIdolMaterials.PropColor, new Color(1.0f, 0.88f, 0.48f, 0.92f));
            props.SetColor(FleshIdolMaterials.PropCoreColor, new Color(1.0f, 0.99f, 0.90f, 1.0f));
            props.SetFloat(FleshIdolMaterials.PropProgress, t);
            props.SetFloat(FleshIdolMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(FleshIdolMaterials.PropAlpha, alpha);
            props.SetFloat(FleshIdolMaterials.PropIntensity, 1.25f);
            props.SetFloat(FleshIdolMaterials.PropMode, 0.0f);

            float size = Mathf.Lerp(1.05f, 1.75f, t);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(fallbackPos.x, alt, fallbackPos.z + t * 0.25f),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, FleshIdolMaterials.MiracleWaveMat, 0, null, 0, props);
        }
    }

    /// <summary>血肉偶像特效生成辅助工具。</summary>
    public static class FleshIdolVfxUtility
    {
        public static void EnsurePrayerAura(Thing hostEntity, Map map, IntVec3 pos)
        {
            if (hostEntity == null || map == null || !pos.IsValid || !pos.InBounds(map) || FleshIdolVfxDefOf.LC_FleshIdolPrayerAuraFX == null)
            {
                return;
            }

            var existing = map.listerThings?.ThingsOfDef(FleshIdolVfxDefOf.LC_FleshIdolPrayerAuraFX);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] is Thing_FleshIdolPrayerAura aura && !aura.Destroyed && aura.HostEntity == hostEntity)
                    {
                        return;
                    }
                }
            }

            Thing_FleshIdolPrayerAura spawned = (Thing_FleshIdolPrayerAura)GenSpawn.Spawn(
                FleshIdolVfxDefOf.LC_FleshIdolPrayerAuraFX, pos, map);
            spawned.Initialize(hostEntity);
        }

        public static void SpawnMiracleWave(Map map, Vector3 pos, float radius = 7.5f, int duration = 50)
        {
            if (map == null || FleshIdolVfxDefOf.LC_FleshIdolMiracleWaveFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            Thing_FleshIdolMiracleWave wave = (Thing_FleshIdolMiracleWave)GenSpawn.Spawn(
                FleshIdolVfxDefOf.LC_FleshIdolMiracleWaveFX, cell, map);
            wave.Initialize(
                pos,
                radius,
                duration,
                new Color(1.0f, 0.86f, 0.44f, 1.0f),
                new Color(1.0f, 0.99f, 0.90f, 1.0f),
                1.42f,
                mode: 0.0f);

            FleckMaker.ThrowDustPuffThick(pos, map, 1.5f, new Color(1.0f, 0.90f, 0.56f));
        }

        public static void SpawnHealMote(Pawn healedPawn)
        {
            if (healedPawn == null || !healedPawn.Spawned || healedPawn.Map == null || FleshIdolVfxDefOf.LC_FleshIdolHealMoteFX == null)
            {
                return;
            }

            Thing_FleshIdolHealMote mote = (Thing_FleshIdolHealMote)GenSpawn.Spawn(
                FleshIdolVfxDefOf.LC_FleshIdolHealMoteFX, healedPawn.Position, healedPawn.Map);
            mote.Initialize(healedPawn);
        }

        public static void SpawnRetributionBurst(Map map, Vector3 pos, bool catastrophic)
        {
            if (map == null || FleshIdolVfxDefOf.LC_FleshIdolMiracleWaveFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            Thing_FleshIdolMiracleWave wave = (Thing_FleshIdolMiracleWave)GenSpawn.Spawn(
                FleshIdolVfxDefOf.LC_FleshIdolMiracleWaveFX, cell, map);
            wave.Initialize(
                pos,
                radius: catastrophic ? 13.0f : 3.2f,
                duration: catastrophic ? 62 : 36,
                color: catastrophic ? new Color(0.95f, 0.10f, 0.14f, 1.0f) : new Color(0.74f, 0.14f, 0.42f, 1.0f),
                core: new Color(1.0f, 0.92f, 0.88f, 1.0f),
                customIntensity: catastrophic ? 1.80f : 1.40f,
                mode: 2.0f);

            FleckMaker.ThrowDustPuffThick(pos, map, catastrophic ? 3.2f : 1.6f, new Color(0.72f, 0.08f, 0.10f));
            FleckMaker.ThrowDustPuffThick(pos, map, catastrophic ? 2.4f : 1.2f, new Color(0.16f, 0.04f, 0.10f));
            FleckMaker.ThrowMicroSparks(pos, map);

            if (Find.CurrentMap == map && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(catastrophic ? 1.05f : 0.48f);
            }
        }
    }
}
