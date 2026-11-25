using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.NotesFromCrazedResearcher
{
    [DefOf]
    public static class ResearcherNoteVfxDefOf
    {
        public static ThingDef LC_ResearcherNoteBearerAuraFX;
        public static ThingDef LC_ResearcherNoteBurstFX;
        public static ThingDef LC_ResearcherNoteGutsFX;

        static ResearcherNoteVfxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ResearcherNoteVfxDefOf));
        }
    }

    /// <summary>
    /// T-09-78 癫狂研究员的笔记视觉特效材质缓存：
    /// 包含收容基座（KitCreatureContainer）、活体笔记本（GLSL Core）、癫狂符文光环（GLSL Aura）及 12 块解包爆裂血肉块（Gut_0..11）。
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ResearcherNoteMaterials
    {
        public static readonly Shader CoreShader;
        public static readonly Shader AuraShader;

        public static readonly Material ContainerMat;
        public static readonly Material NotebookMat;
        public static readonly Material AuraMat;
        public static readonly Material[] GutMats;

        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropDangerAmt = Shader.PropertyToID("_DangerAmt");
        public static readonly int PropReadyAmt = Shader.PropertyToID("_ReadyAmt");

        private static readonly MaterialPropertyBlock ContainerBlock = new MaterialPropertyBlock();
        private static readonly MaterialPropertyBlock NoteBlock = new MaterialPropertyBlock();
        private static readonly MaterialPropertyBlock PedestalAuraBlock = new MaterialPropertyBlock();

        static ResearcherNoteMaterials()
        {
            CoreShader = ShaderDatabase.LoadShader("Map/LC_ResearcherNoteCore") ?? ShaderDatabase.Transparent;
            AuraShader = ShaderDatabase.LoadShader("Map/LC_ResearcherNoteAura") ?? ShaderDatabase.MoteGlow;

            Texture2D containerTex = ContentFinder<Texture2D>.Get("Things/Building/NotesFromCrazedResearcher/NotesFromCrazedResearcher_Container", reportFailure: false)
                ?? ContentFinder<Texture2D>.Get("Things/Building/NotesFromCrazedResearcher/NotesFromCrazedResearcher_Empty", reportFailure: false)
                ?? BaseContent.ClearTex;
            ContainerMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = containerTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            Texture2D noteTex = ContentFinder<Texture2D>.Get("Things/Building/NotesFromCrazedResearcher/NotesFromCrazedResearcher", reportFailure: false)
                ?? BaseContent.ClearTex;
            NotebookMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = noteTex,
                shader = CoreShader,
                color = Color.white
            });

            AuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = AuraShader,
                color = new Color(0.45f, 0.95f, 0.42f, 1.0f)
            });

            GutMats = new Material[12];
            for (int i = 0; i < GutMats.Length; i++)
            {
                Texture2D gutTex = ContentFinder<Texture2D>.Get($"Things/Building/NotesFromCrazedResearcher/Guts/Gut_{i}", reportFailure: false)
                    ?? BaseContent.ClearTex;
                GutMats[i] = MaterialPool.MatFrom(new MaterialRequest
                {
                    mainTex = gutTex,
                    shader = ShaderDatabase.Transparent,
                    color = Color.white
                });
            }
        }

        /// <summary>绘制收容平台上的原版工具收容仓与悬浮活体笔记本动态。</summary>
        public static void DrawContainedContainer(Vector3 drawLoc, float baseY, float ageSecs, bool isEquipped, float dangerAmt, float readyAmt)
        {
            // 1. 地面幽光环（在仓基座下微弱脉动）
            Color auraColor = dangerAmt >= 0.45f
                ? new Color(0.95f, 0.22f, 0.28f, isEquipped ? 0.55f : 0.35f)
                : (readyAmt > 0.5f
                    ? new Color(0.28f, 0.96f, 0.72f, 0.48f)
                    : new Color(0.45f, 0.95f, 0.42f, isEquipped ? 0.24f : 0.42f));

            PedestalAuraBlock.Clear();
            PedestalAuraBlock.SetColor(PropColor, auraColor);
            PedestalAuraBlock.SetColor(PropCoreColor, new Color(0.92f, 1.0f, 0.85f, 0.8f));
            PedestalAuraBlock.SetFloat(PropProgress, isEquipped ? dangerAmt : 0.2f);
            PedestalAuraBlock.SetFloat(PropAgeSecs, ageSecs);
            PedestalAuraBlock.SetFloat(PropAlpha, isEquipped ? 0.42f : 0.72f);
            PedestalAuraBlock.SetFloat(PropIntensity, 1.15f);
            PedestalAuraBlock.SetFloat(PropMode, 0f);

            Vector3 auraPos = new Vector3(drawLoc.x, baseY - 0.002f, drawLoc.z);
            Matrix4x4 auraMtx = Matrix4x4.TRS(auraPos, Quaternion.identity, new Vector3(1.95f, 1f, 1.95f));
            Graphics.DrawMesh(MeshPool.plane10, auraMtx, AuraMat, 0, null, 0, PedestalAuraBlock);

            // 2. 工具异想体玻璃金属收容仓底座
            ContainerBlock.Clear();
            ContainerBlock.SetColor(PropColor, isEquipped ? new Color(0.82f, 0.88f, 0.86f, 0.92f) : Color.white);
            Vector3 containerPos = new Vector3(drawLoc.x, baseY + 0.004f, drawLoc.z + 0.06f);
            Matrix4x4 containerMtx = Matrix4x4.TRS(containerPos, Quaternion.identity, new Vector3(1.80f, 1f, 1.80f));
            Graphics.DrawMesh(MeshPool.plane10, containerMtx, ContainerMat, 0, null, 0, ContainerBlock);

            // 3. 若笔记尚在收容仓内，绘制柔和悬浮晃动与书页微光的笔记本体
            if (!isEquipped)
            {
                float hoverZ = Mathf.Sin(ageSecs * 2.1f) * 0.055f;
                float tiltAngle = Mathf.Sin(ageSecs * 1.45f) * 4.5f;
                float noteScale = 1.04f + Mathf.Sin(ageSecs * 3.0f) * 0.025f;

                NoteBlock.Clear();
                NoteBlock.SetColor(PropColor, Color.white);
                NoteBlock.SetColor(PropGlowColor, new Color(0.45f, 0.95f, 0.42f, 1.0f));
                NoteBlock.SetFloat(PropAgeSecs, ageSecs);
                NoteBlock.SetFloat(PropDangerAmt, 0f);
                NoteBlock.SetFloat(PropReadyAmt, 0f);

                Vector3 notePos = new Vector3(drawLoc.x, baseY + 0.014f, drawLoc.z + 0.08f + hoverZ);
                Matrix4x4 noteMtx = Matrix4x4.TRS(notePos, Quaternion.AngleAxis(tiltAngle, Vector3.up), new Vector3(noteScale, 1f, noteScale));
                Graphics.DrawMesh(MeshPool.plane10, noteMtx, NotebookMat, 0, null, 0, NoteBlock);
            }
        }
    }

    /// <summary>
    /// 佩戴“癫狂研究员的笔记”期间的随行视觉表现：
    /// 脚下环绕躁动符文阵、肩侧盘旋浮空笔记本；当完成安全归还条件时转绿松辉光，当累计承伤逼近阈值时剧烈膨胀颤抖泛红。
    /// </summary>
    public class Thing_ResearcherNoteBearerAura : Thing
    {
        private Pawn hostPawn;
        private CompNotesFromCrazedResearcher toolComp;
        private int ageTicks;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock noteProps;

        public Pawn HostPawn => hostPawn;

        public void Attach(Pawn bearer, CompNotesFromCrazedResearcher tool)
        {
            hostPawn = bearer;
            toolComp = tool;
            ageTicks = 0;
        }

        private HediffComp_ResearcherNote ResolveNoteComp()
        {
            HediffDef def = toolComp?.Props?.equipHediffDef ?? HediffDef.Named("LC_ResearcherNoteBuff");
            HediffComp_ResearcherNote comp = hostPawn?.health?.hediffSet?.GetFirstHediffOfDef(def)?.TryGetComp<HediffComp_ResearcherNote>();
            if (toolComp == null && comp?.toolThing != null)
            {
                toolComp = comp.toolThing.TryGetComp<CompNotesFromCrazedResearcher>();
            }
            return comp;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            HediffComp_ResearcherNote noteComp = ResolveNoteComp();
            if (hostPawn == null || hostPawn.Dead || !hostPawn.Spawned || Map == null || hostPawn.Map != Map || noteComp == null)
            {
                Destroy();
                return;
            }

            if (hostPawn.Position.InBounds(Map) && Position != hostPawn.Position)
            {
                Position = hostPawn.Position;
            }

            float maxDmg = Mathf.Max(1f, noteComp.Props?.maxCumulativeDamage ?? 60f);
            float dangerAmt = Mathf.Clamp01(noteComp.CumulativeDamageTaken / maxDmg);
            if (dangerAmt >= 0.45f && ageTicks % 16 == 0 && Map.flecks != null && Position.InBounds(Map))
            {
                Vector3 bloodPuff = hostPawn.DrawPos + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.1f, 0.35f));
                if (bloodPuff.ToIntVec3().InBounds(Map))
                {
                    FleckMaker.ThrowDustPuffThick(bloodPuff, Map, Rand.Range(0.55f, 0.90f), new Color(0.88f, 0.14f, 0.20f, 0.65f));
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (hostPawn == null || !hostPawn.Spawned)
            {
                return;
            }

            HediffComp_ResearcherNote noteComp = ResolveNoteComp();
            if (noteComp == null)
            {
                return;
            }

            float maxDmg = Mathf.Max(1f, noteComp.Props?.maxCumulativeDamage ?? 60f);
            float dangerAmt = Mathf.Clamp01(noteComp.CumulativeDamageTaken / maxDmg);
            int minTicks = toolComp?.PropsNote?.minSafeReturnTicks ?? 1800;
            int equipTicks = toolComp != null ? toolComp.CurrentEquipTicks : 0;
            bool isReady = equipTicks >= minTicks && noteComp.CompletedStudies >= 1;
            float readyAmt = isReady ? 1f : 0f;

            float ageSecs = ageTicks / 60f;
            Vector3 hostPos = hostPawn.DrawPos;

            Color auraCol = dangerAmt >= 0.45f
                ? Color.Lerp(new Color(0.95f, 0.42f, 0.20f, 0.85f), new Color(1.0f, 0.15f, 0.22f, 0.98f), (dangerAmt - 0.45f) / 0.55f)
                : (isReady
                    ? new Color(0.30f, 0.96f, 0.72f, 0.82f)
                    : new Color(0.56f, 0.94f, 0.38f, 0.74f));
            Color coreCol = dangerAmt >= 0.45f
                ? new Color(1.0f, 0.72f, 0.68f, 0.95f)
                : new Color(0.92f, 1.0f, 0.86f, 0.90f);

            // 1. 脚下狂热研究符文光环
            if (auraProps == null) auraProps = new MaterialPropertyBlock();
            auraProps.Clear();
            auraProps.SetColor(ResearcherNoteMaterials.PropColor, auraCol);
            auraProps.SetColor(ResearcherNoteMaterials.PropCoreColor, coreCol);
            auraProps.SetFloat(ResearcherNoteMaterials.PropProgress, dangerAmt);
            auraProps.SetFloat(ResearcherNoteMaterials.PropAgeSecs, ageSecs);
            auraProps.SetFloat(ResearcherNoteMaterials.PropAlpha, 0.78f);
            auraProps.SetFloat(ResearcherNoteMaterials.PropIntensity, 1.25f + dangerAmt * 0.45f);
            auraProps.SetFloat(ResearcherNoteMaterials.PropMode, 0f);

            float auraScale = Mathf.Lerp(1.60f, 2.25f, dangerAmt);
            Vector3 auraPos = new Vector3(hostPos.x, AltitudeLayer.MoteLow.AltitudeFor(), hostPos.z);
            Matrix4x4 auraMtx = Matrix4x4.TRS(auraPos, Quaternion.identity, new Vector3(auraScale, 1f, auraScale));
            Graphics.DrawMesh(MeshPool.plane10, auraMtx, ResearcherNoteMaterials.AuraMat, 0, null, 0, auraProps);

            // 2. 盘旋于佩戴者肩畔的缩小活体笔记本（临爆时膨胀抽搐）
            float dangerShake = dangerAmt >= 0.45f ? (dangerAmt - 0.40f) * 0.14f : 0f;
            float tremorX = dangerShake * Mathf.Sin(ageSecs * 48f);
            float tremorZ = dangerShake * Mathf.Cos(ageSecs * 57f);
            float orbitX = -0.34f + Mathf.Cos(ageSecs * 1.8f) * 0.04f + tremorX;
            float orbitZ = 0.32f + Mathf.Sin(ageSecs * 2.9f) * 0.055f + tremorZ;
            float swellScale = 0.56f * (1f + dangerAmt * 0.45f + (dangerAmt >= 0.45f ? Mathf.Sin(ageSecs * 15f) * 0.12f : 0f));
            float tilt = Mathf.Sin(ageSecs * 2.2f) * (8f + dangerAmt * 16f);

            if (noteProps == null) noteProps = new MaterialPropertyBlock();
            noteProps.Clear();
            noteProps.SetColor(ResearcherNoteMaterials.PropColor, Color.white);
            noteProps.SetColor(ResearcherNoteMaterials.PropGlowColor, auraCol);
            noteProps.SetFloat(ResearcherNoteMaterials.PropAgeSecs, ageSecs);
            noteProps.SetFloat(ResearcherNoteMaterials.PropDangerAmt, dangerAmt);
            noteProps.SetFloat(ResearcherNoteMaterials.PropReadyAmt, readyAmt);

            Vector3 notePos = new Vector3(hostPos.x + orbitX, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f, hostPos.z + orbitZ);
            Matrix4x4 noteMtx = Matrix4x4.TRS(notePos, Quaternion.AngleAxis(tilt, Vector3.up), new Vector3(swellScale, 1f, swellScale));
            Graphics.DrawMesh(MeshPool.plane10, noteMtx, ResearcherNoteMaterials.NotebookMat, 0, null, 0, noteProps);
        }
    }

    /// <summary>
    /// 癫狂研究员的笔记冲击波视觉实体（Mode 1: 灵感翻页 / 安全就绪；Mode 2: 膨胀血肉爆炸）。
    /// </summary>
    public class Thing_ResearcherNoteBurst : Thing
    {
        private Vector3 exactPos;
        private float maxRadius = 2.5f;
        private float mode = 1f;
        private Color mainColor = new Color(0.45f, 0.95f, 0.42f, 1.0f);
        private Color coreColor = new Color(0.92f, 1.0f, 0.85f, 1.0f);
        private int ageTicks;
        private int durationTicks = 30;
        private MaterialPropertyBlock waveProps;

        public void Configure(Vector3 pos, float radius, float shaderMode, Color primary, Color core, int lifetime)
        {
            exactPos = pos;
            maxRadius = Mathf.Max(0.9f, radius);
            mode = shaderMode;
            mainColor = primary;
            coreColor = core;
            durationTicks = Mathf.Max(10, lifetime);
            ageTicks = 0;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;
            if (ageTicks >= durationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, durationTicks));
            float fade = 1f - progress * progress;

            if (waveProps == null) waveProps = new MaterialPropertyBlock();
            waveProps.Clear();
            waveProps.SetColor(ResearcherNoteMaterials.PropColor, mainColor);
            waveProps.SetColor(ResearcherNoteMaterials.PropCoreColor, coreColor);
            waveProps.SetFloat(ResearcherNoteMaterials.PropProgress, progress);
            waveProps.SetFloat(ResearcherNoteMaterials.PropAgeSecs, ageTicks / 60f);
            waveProps.SetFloat(ResearcherNoteMaterials.PropAlpha, fade);
            waveProps.SetFloat(ResearcherNoteMaterials.PropIntensity, mode > 1.5f ? 1.65f : 1.35f);
            waveProps.SetFloat(ResearcherNoteMaterials.PropMode, mode);

            float size = maxRadius * 2f;
            Vector3 drawCenter = new Vector3(exactPos.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f, exactPos.z);
            Matrix4x4 waveMtx = Matrix4x4.TRS(drawCenter, Quaternion.identity, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, waveMtx, ResearcherNoteMaterials.AuraMat, 0, null, 0, waveProps);
        }
    }

    /// <summary>
    /// 佩戴者炸裂飞溅的解包内脏碎片粒子簇（GutsAtlas 12 块血肉弧线飞散、旋转并伴随血雾消散）。
    /// </summary>
    public class Thing_ResearcherNoteGuts : Thing
    {
        private struct GutParticle
        {
            public int SpriteIndex;
            public Vector2 Direction;
            public float Distance;
            public float PeakHeight;
            public float StartAngle;
            public float SpinSpeed;
            public float Scale;
            public int Lifetime;
        }

        private readonly List<GutParticle> particles = new List<GutParticle>(14);
        private Vector3 originPos;
        private int ageTicks;
        private int maxTicks = 84;
        private MaterialPropertyBlock gutBlock;

        public void Init(Vector3 origin, float blastRadius)
        {
            originPos = origin;
            ageTicks = 0;
            maxTicks = 84;
            particles.Clear();

            float scatterMax = Mathf.Clamp(blastRadius * 0.72f, 2.0f, 4.6f);
            int count = 14;
            for (int i = 0; i < count; i++)
            {
                float angle = (i * (Mathf.PI * 2f / count)) + Rand.Range(-0.22f, 0.22f);
                particles.Add(new GutParticle
                {
                    SpriteIndex = i % ResearcherNoteMaterials.GutMats.Length,
                    Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    Distance = Rand.Range(scatterMax * 0.35f, scatterMax),
                    PeakHeight = Rand.Range(0.45f, 1.35f),
                    StartAngle = Rand.Range(0f, 360f),
                    SpinSpeed = Rand.Range(-320f, 320f),
                    Scale = Rand.Range(0.55f, 0.88f),
                    Lifetime = Rand.Range(60, 84)
                });
            }
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            if (ageTicks <= 14 && ageTicks % 3 == 0 && Map != null && Map.flecks != null && originPos.ToIntVec3().InBounds(Map))
            {
                Vector3 mistPos = originPos + new Vector3(Rand.Range(-0.8f, 0.8f), 0f, Rand.Range(-0.8f, 0.8f));
                if (mistPos.ToIntVec3().InBounds(Map))
                {
                    FleckMaker.ThrowDustPuffThick(mistPos, Map, Rand.Range(1.0f, 1.6f), new Color(0.68f, 0.05f, 0.08f, 0.78f));
                }
            }

            if (ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (ResearcherNoteMaterials.GutMats == null || particles.Count == 0)
            {
                return;
            }

            if (gutBlock == null) gutBlock = new MaterialPropertyBlock();
            float ageSecs = ageTicks / 60f;
            float baseY = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;

            for (int i = 0; i < particles.Count; i++)
            {
                GutParticle p = particles[i];
                if (ageTicks > p.Lifetime)
                {
                    continue;
                }

                float t = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, p.Lifetime));
                // 先快后慢阻尼外飞 + 抛物线弹道高度
                float travel = 1f - Mathf.Pow(1f - Mathf.Min(1f, t * 1.25f), 2.2f);
                float arcZ = 4f * p.PeakHeight * t * (1f - t);
                float alpha = t > 0.72f ? Mathf.Lerp(1f, 0f, (t - 0.72f) / 0.28f) : 1f;

                gutBlock.Clear();
                gutBlock.SetColor(ResearcherNoteMaterials.PropColor, new Color(1f, 0.92f, 0.92f, alpha));

                Vector3 pos = new Vector3(
                    originPos.x + p.Direction.x * p.Distance * travel,
                    baseY + (i * 0.0004f),
                    originPos.z + p.Direction.y * p.Distance * travel + arcZ);
                float rot = p.StartAngle + p.SpinSpeed * ageSecs;
                Matrix4x4 mtx = Matrix4x4.TRS(pos, Quaternion.AngleAxis(rot, Vector3.up), new Vector3(p.Scale, 1f, p.Scale));
                Graphics.DrawMesh(MeshPool.plane10, mtx, ResearcherNoteMaterials.GutMats[p.SpriteIndex], 0, null, 0, gutBlock);
            }
        }
    }

    internal static class ResearcherNoteVfxUtility
    {
        public static void EnsureBearerAura(Pawn wearer, CompNotesFromCrazedResearcher tool)
        {
            if (wearer == null || wearer.Dead || !wearer.Spawned || wearer.Map == null || ResearcherNoteVfxDefOf.LC_ResearcherNoteBearerAuraFX == null)
            {
                return;
            }

            Map map = wearer.Map;
            List<Thing> existing = map.listerThings.ThingsOfDef(ResearcherNoteVfxDefOf.LC_ResearcherNoteBearerAuraFX);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] is Thing_ResearcherNoteBearerAura aura && aura.HostPawn == wearer && !aura.Destroyed)
                {
                    return;
                }
            }

            if (wearer.Position.InBounds(map)
                && GenSpawn.Spawn(ResearcherNoteVfxDefOf.LC_ResearcherNoteBearerAuraFX, wearer.Position, map) is Thing_ResearcherNoteBearerAura created)
            {
                created.Attach(wearer, tool);
            }
        }

        public static void SpawnInsightPulse(Map map, Vector3 pos, bool isSafeReady)
        {
            Color primary = isSafeReady
                ? new Color(0.32f, 0.98f, 0.74f, 1.0f)
                : new Color(0.54f, 0.96f, 0.42f, 0.95f);
            Color core = new Color(0.92f, 1.0f, 0.88f, 1.0f);
            SpawnBurst(map, pos, radius: isSafeReady ? 2.4f : 1.85f, mode: 1f, primary: primary, core: core, lifetime: 30);
        }

        public static void SpawnGoreExplosion(Map map, Vector3 pos, float radius)
        {
            if (map == null)
            {
                return;
            }

            SpawnBurst(
                map,
                pos,
                radius: Mathf.Max(3.6f, radius),
                mode: 2f,
                primary: new Color(0.94f, 0.12f, 0.16f, 1.0f),
                core: new Color(1.0f, 0.62f, 0.46f, 1.0f),
                lifetime: 40);

            IntVec3 center = pos.ToIntVec3().ClampInsideMap(map);
            if (center.InBounds(map))
            {
                if (ResearcherNoteVfxDefOf.LC_ResearcherNoteGutsFX != null
                    && GenSpawn.Spawn(ResearcherNoteVfxDefOf.LC_ResearcherNoteGutsFX, center, map) is Thing_ResearcherNoteGuts guts)
                {
                    guts.Init(pos, radius);
                }

                if (RimWorld.ThingDefOf.Filth_Blood != null)
                {
                    foreach (IntVec3 c in GenRadial.RadialCellsAround(center, Mathf.Min(3.2f, radius * 0.55f), useCenter: true))
                    {
                        if (c.InBounds(map) && c.Walkable(map) && (c == center || Rand.Chance(0.58f)))
                        {
                            FilthMaker.TryMakeFilth(c, map, RimWorld.ThingDefOf.Filth_Blood);
                        }
                    }
                }
            }
        }

        private static void SpawnBurst(Map map, Vector3 pos, float radius, float mode, Color primary, Color core, int lifetime)
        {
            if (map == null || ResearcherNoteVfxDefOf.LC_ResearcherNoteBurstFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(ResearcherNoteVfxDefOf.LC_ResearcherNoteBurstFX, cell, map) is Thing_ResearcherNoteBurst fx)
            {
                fx.Configure(pos, radius, mode, primary, core, lifetime);
            }
        }
    }
}
