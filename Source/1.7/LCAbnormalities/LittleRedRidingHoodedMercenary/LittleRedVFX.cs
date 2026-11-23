using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.LittleRedRidingHoodedMercenary
{
    /// <summary>小红帽雇佣兵与赤红疤痕 E.G.O. 专属自定义 GLSL 450 着色器与解包特效材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class LittleRedMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropTipColor = Shader.PropertyToID("_TipColor");
        public static readonly int PropGoldColor = Shader.PropertyToID("_GoldColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropArcSpan = Shader.PropertyToID("_ArcSpan");
        public static readonly int PropSpread = Shader.PropertyToID("_Spread");
        public static readonly int PropBerserk = Shader.PropertyToID("_Berserk");
        public static readonly int PropPulse = Shader.PropertyToID("_Pulse");

        public static readonly Material SlashMat;
        public static readonly Material ShotgunBlastMat;
        public static readonly Material HuntMarkMat;
        public static readonly Material ThrownSickleMat;
        public static readonly Material DeadPoseMat;

        static LittleRedMaterials()
        {
            Shader slashShader = ShaderDatabase.LoadShader("Map/LC_LittleRedSlash") ?? ShaderDatabase.Transparent;
            Shader blastShader = ShaderDatabase.LoadShader("Map/LC_LittleRedShotgunBlast") ?? ShaderDatabase.MoteGlow;
            Shader markShader = ShaderDatabase.LoadShader("Map/LC_LittleRedHuntMark") ?? ShaderDatabase.Transparent;

            Texture2D huntMarkTex = ContentFinder<Texture2D>.Get("Things/VFX/LittleRedRidingHoodedMercenary/LittleRed_HuntMark", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D thrownSickleTex = ContentFinder<Texture2D>.Get("Things/VFX/LittleRedRidingHoodedMercenary/LittleRed_ThrownSickle", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D deadPoseTex = ContentFinder<Texture2D>.Get("Things/VFX/LittleRedRidingHoodedMercenary/LittleRed_DeadPose", reportFailure: false) ?? BaseContent.WhiteTex;

            SlashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = slashShader,
                color = new Color(0.95f, 0.09f, 0.14f, 1.0f)
            });

            ShotgunBlastMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = blastShader,
                color = new Color(1.0f, 0.24f, 0.14f, 1.0f)
            });

            HuntMarkMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = huntMarkTex,
                shader = markShader,
                color = new Color(1.0f, 0.16f, 0.16f, 1.0f)
            });

            ThrownSickleMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = thrownSickleTex,
                shader = ShaderDatabase.Cutout,
                color = Color.white
            });

            DeadPoseMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = deadPoseTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 猩红黑核弯镰月牙斩击特效（自定义 GLSL LC_LittleRedSlash）：
    /// 还原脑叶公司 slash-half-circle-1C / slash-strong 的黑芯猩红刀光，支持常规斩、冲刺斩、强力二连重劈及狂暴猩红增幅。
    /// </summary>
    public class Thing_LittleRedSlash : Thing
    {
        private Vector3 exactPos;
        private float yawDeg;
        private float scale = 2.0f;
        private float arcSpanRad = 2.55f;
        private bool flipSweep;
        private bool isBerserk;
        private int ageTicks;
        private int durationTicks = 16;
        private bool initialized;

        private MaterialPropertyBlock slashProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float slashScale, bool flip, bool berserk = false, int lifeTicks = 16, float spanRad = 2.55f)
        {
            exactPos = pos.Yto0();
            yawDeg = yawAngleDeg;
            scale = Mathf.Max(0.75f, slashScale);
            flipSweep = flip;
            isBerserk = berserk;
            durationTicks = Mathf.Max(8, lifeTicks);
            arcSpanRad = Mathf.Clamp(spanRad, 1.4f, 3.6f);
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
            if (!initialized || LittleRedMaterials.SlashMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - prog, 0.72f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            float sz = scale * Mathf.Lerp(0.88f, 1.18f, 1f - Mathf.Pow(1f - prog, 2f));

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(LittleRedMaterials.PropColor, isBerserk
                ? new Color(1.0f, 0.08f, 0.14f, 1.0f)
                : new Color(0.92f, 0.09f, 0.15f, 1.0f));
            slashProps.SetColor(LittleRedMaterials.PropCoreColor, new Color(0.03f, 0.005f, 0.01f, 1.0f));
            slashProps.SetColor(LittleRedMaterials.PropTipColor, isBerserk
                ? new Color(1.0f, 0.90f, 0.50f, 1.0f)
                : new Color(1.0f, 0.78f, 0.56f, 1.0f));
            slashProps.SetFloat(LittleRedMaterials.PropProgress, prog);
            slashProps.SetFloat(LittleRedMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(LittleRedMaterials.PropAlpha, alpha);
            slashProps.SetFloat(LittleRedMaterials.PropArcSpan, arcSpanRad);
            slashProps.SetFloat(LittleRedMaterials.PropBerserk, isBerserk ? 1.0f : 0.0f);

            Quaternion rot = Quaternion.AngleAxis(yawDeg, Vector3.up);
            Vector3 meshScale = new Vector3(sz, 1f, flipSweep ? -sz : sz);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(exactPos.x, alt, exactPos.z), rot, meshScale);
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.SlashMat, 0, null, 0, slashProps);
        }
    }

    /// <summary>
    /// 猩红霰弹手炮枪口火光锥与散射曳光弹道特效（自定义 GLSL LC_LittleRedShotgunBlast）：
    /// 呈现炽烈金橙枪口爆焰、5 束散射猩红曳光弹线与冲击波弧。
    /// </summary>
    public class Thing_LittleRedShotgunCone : Thing
    {
        private Vector3 muzzlePos;
        private Vector3 dir = Vector3.right;
        private float coneLength = 4.2f;
        private float coneWidth = 2.1f;
        private float spread = 0.58f;
        private bool isBerserk;
        private int ageTicks;
        private int durationTicks = 15;
        private bool initialized;

        private MaterialPropertyBlock blastProps;

        public void Initialize(Vector3 start, Vector3 aimDir, float length = 4.2f, float width = 2.1f, float coneSpread = 0.58f, bool berserk = false, int lifeTicks = 15)
        {
            muzzlePos = start.Yto0();
            dir = aimDir.Yto0().sqrMagnitude > 0.001f ? aimDir.Yto0().normalized : Vector3.right;
            coneLength = Mathf.Max(1.6f, length);
            coneWidth = Mathf.Max(0.9f, width);
            spread = Mathf.Clamp(coneSpread, 0.25f, 0.92f);
            isBerserk = berserk;
            durationTicks = Mathf.Max(8, lifeTicks);
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
            if (!initialized || LittleRedMaterials.ShotgunBlastMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - prog, 0.85f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Vector3 center = muzzlePos + dir * (coneLength * 0.5f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            blastProps ??= new MaterialPropertyBlock();
            blastProps.SetColor(LittleRedMaterials.PropColor, isBerserk
                ? new Color(1.0f, 0.12f, 0.14f, 1.0f)
                : new Color(1.0f, 0.30f, 0.14f, 1.0f));
            blastProps.SetColor(LittleRedMaterials.PropCoreColor, new Color(1.0f, 0.93f, 0.64f, 1.0f));
            blastProps.SetFloat(LittleRedMaterials.PropProgress, prog);
            blastProps.SetFloat(LittleRedMaterials.PropAgeSecs, ageTicks / 60f);
            blastProps.SetFloat(LittleRedMaterials.PropAlpha, alpha);
            blastProps.SetFloat(LittleRedMaterials.PropSpread, spread);
            blastProps.SetFloat(LittleRedMaterials.PropBerserk, isBerserk ? 1.0f : 0.0f);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(center.x, alt, center.z),
                rot,
                new Vector3(coneWidth, 1f, coneLength));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.ShotgunBlastMat, 0, null, 0, blastProps);
        }
    }

    /// <summary>
    /// 小红帽猎杀标记特效（还原解包 eye_hunt_noeye.png + LC_LittleRedHuntMark 自定义着色器）：
    /// 悬停在被雇佣猎杀或暴走锁定的目标头顶，伴随血红收缩准星脉冲。
    /// </summary>
    public class Thing_LittleRedHuntMark : Thing
    {
        private Pawn hunter;
        private Pawn prey;
        private int ageTicks;
        private bool fadingOut;
        private int fadeTicks;
        private const int FadeDuration = 18;
        private bool initialized;

        private MaterialPropertyBlock markProps;

        public Pawn MarkedPrey => prey;

        public void Initialize(Pawn hunterPawn, Pawn targetPrey)
        {
            hunter = hunterPawn;
            prey = targetPrey;
            ageTicks = 0;
            fadingOut = false;
            fadeTicks = 0;
            initialized = true;
        }

        public void Retarget(Pawn newPrey)
        {
            prey = newPrey;
            fadingOut = false;
            fadeTicks = 0;
        }

        public void BeginFadeOut()
        {
            fadingOut = true;
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (!fadingOut)
            {
                if (hunter == null || hunter.Dead || hunter.Destroyed || !hunter.Spawned ||
                    prey == null || prey.Dead || prey.Destroyed || !prey.Spawned || prey.Map != Map)
                {
                    fadingOut = true;
                }
                else
                {
                    CompLittleRedRidingHoodedMercenary comp = hunter.TryGetComp<CompLittleRedRidingHoodedMercenary>();
                    if (comp == null || !comp.Escaped)
                    {
                        fadingOut = true;
                    }
                    else
                    {
                        IntVec3 preyCell = prey.Position.ClampInsideMap(Map);
                        if (preyCell != Position)
                        {
                            Position = preyCell;
                        }
                    }
                }
            }
            else
            {
                fadeTicks++;
                if (fadeTicks >= FadeDuration)
                {
                    Destroy();
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || LittleRedMaterials.HuntMarkMat == null)
            {
                return;
            }

            float openAlpha = Mathf.Clamp01(ageTicks / 14f);
            float closeAlpha = fadingOut ? Mathf.Clamp01(1f - fadeTicks / (float)FadeDuration) : 1f;
            float alpha = openAlpha * closeAlpha;
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 anchor = (prey != null && prey.Spawned) ? prey.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            float hoverZ = 1.05f + 0.06f * Mathf.Sin(ageTicks * 0.14f);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.08f;
            float scale = Mathf.Lerp(1.55f, 1.15f, openAlpha);

            markProps ??= new MaterialPropertyBlock();
            markProps.SetColor(LittleRedMaterials.PropColor, new Color(1.0f, 0.14f, 0.16f, 1.0f));
            markProps.SetColor(LittleRedMaterials.PropGoldColor, new Color(1.0f, 0.86f, 0.30f, 1.0f));
            markProps.SetFloat(LittleRedMaterials.PropAgeSecs, ageTicks / 60f);
            markProps.SetFloat(LittleRedMaterials.PropAlpha, alpha * 0.95f);
            markProps.SetFloat(LittleRedMaterials.PropPulse, 1.15f);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(anchor.x, alt, anchor.z + hoverZ),
                Quaternion.identity,
                new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.HuntMarkMat, 0, null, 0, markProps);
        }
    }

    /// <summary>
    /// 小红帽跨房回旋飞镰投掷体（对应脑叶公司 RedHoodThrowedAxe + Attack_axe_throw）：
    /// 展开的赤红弯镰高速自旋飞向目标并在沿途切裂敌方单位，抵达顶点后沿回旋轨迹飞回小红帽手中！
    /// </summary>
    public class Thing_LittleRedThrownSickle : Thing
    {
        private Pawn owner;
        private Vector3 exactPos;
        private Vector3 apexPos;
        private float flightSpeed = 0.42f; // cells per tick (~25.2 cells/sec)
        private float damageAmount = 22f;
        private bool ownerIsHired;
        private bool returning;
        private float spinAngleDeg;
        private int ageTicks;
        private const int MaxLifetimeTicks = 135;
        private bool initialized;

        private readonly HashSet<int> hitOutbound = new HashSet<int>();
        private readonly HashSet<int> hitReturn = new HashSet<int>();

        public void Initialize(Pawn thrower, Vector3 startPos, Vector3 targetPos, float damage, bool isHired)
        {
            owner = thrower;
            exactPos = startPos.Yto0();
            Vector3 delta = (targetPos.Yto0() - exactPos);
            float dist = delta.magnitude;
            Vector3 dir = dist > 0.05f ? (delta / dist) : Vector3.right;
            // Overshoot target slightly by 1.5 cells so the sickle visibly carves through the prey before booming back
            float reach = Mathf.Clamp(dist + 1.5f, 4.5f, 16.0f);
            apexPos = exactPos + dir * reach;
            damageAmount = Mathf.Max(8f, damage);
            ownerIsHired = isHired;
            returning = false;
            spinAngleDeg = 0f;
            ageTicks = 0;
            hitOutbound.Clear();
            hitReturn.Clear();
            initialized = true;
        }

        private void NotifyReturnAndDestroy()
        {
            if (owner != null && !owner.Destroyed)
            {
                owner.TryGetComp<CompLittleRedRidingHoodedMercenary>()?.NotifyThrownSickleReturned();
            }

            if (!Destroyed)
            {
                Destroy();
            }
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                NotifyReturnAndDestroy();
                return;
            }

            ageTicks++;
            spinAngleDeg = (spinAngleDeg + 34f) % 360f;

            if (ageTicks >= MaxLifetimeTicks || owner == null || owner.Dead || owner.Destroyed || !owner.Spawned)
            {
                NotifyReturnAndDestroy();
                return;
            }

            Vector3 dest = returning ? owner.DrawPos.Yto0() : apexPos;
            Vector3 toDest = dest - exactPos;
            float dist = toDest.magnitude;

            if (!returning && dist <= flightSpeed * 1.15f)
            {
                exactPos = apexPos;
                returning = true;
                // Dramatic crimson vortex slash at turnaround apex
                LittleRedVfxUtility.SpawnSlash(Map, exactPos, Rand.Range(0f, 360f), 2.1f, flipSweep: false, berserk: true, lifeTicks: 14, spanRad: 3.1f);
            }
            else if (returning && dist <= 0.85f)
            {
                NotifyReturnAndDestroy();
                return;
            }
            else if (dist > 0.001f)
            {
                float curSpeed = returning ? flightSpeed * 1.18f : flightSpeed;
                exactPos += (toDest / dist) * Mathf.Min(curSpeed, dist);
            }

            IntVec3 cell = exactPos.ToIntVec3().ClampInsideMap(Map);
            if (cell != Position)
            {
                Position = cell;
            }

            // Spawn trailing crimson crescent whoosh rings every 5 ticks
            if (ageTicks % 5 == 0)
            {
                LittleRedVfxUtility.SpawnSlash(Map, exactPos, spinAngleDeg, 1.55f, flipSweep: (ageTicks % 10 == 0), berserk: false, lifeTicks: 9, spanRad: 2.35f);
            }

            // Carve enemies within 1.35 cells of spinning sickle
            HashSet<int> hitSet = returning ? hitReturn : hitOutbound;
            IReadOnlyList<Pawn> pawns = Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate == owner || candidate.Dead || candidate.Destroyed || !candidate.Spawned)
                {
                    continue;
                }

                if (hitSet.Contains(candidate.thingIDNumber))
                {
                    continue;
                }

                if (ownerIsHired ? !candidate.HostileTo(Faction.OfPlayer) : !(candidate.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if ((candidate.DrawPos.Yto0() - exactPos).sqrMagnitude <= 1.45f * 1.45f)
                {
                    hitSet.Add(candidate.thingIDNumber);
                    float dmg = returning ? damageAmount * 0.80f : damageAmount;
                    candidate.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        dmg,
                        0.50f,
                        instigator: owner));

                    LittleRedVfxUtility.SpawnSlash(Map, candidate.DrawPos.Yto0(), Rand.Range(0f, 360f), 1.85f, flipSweep: returning, berserk: false, lifeTicks: 13);
                    LittleRedVfxUtility.SpawnImpactBurst(Map, candidate.DrawPos.Yto0(), 1.0f, berserk: false);
                    LCAbnormalitiesHelper.PlaySoundAt(Rand.Bool ? LittleRedDefOf.LC_LittleRed_Atk1 : LittleRedDefOf.LC_LittleRed_Atk2, candidate);
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || LittleRedMaterials.ThrownSickleMat == null)
            {
                return;
            }

            float alt = AltitudeLayer.Projectile.AltitudeFor() + 0.04f;
            Quaternion rot = Quaternion.AngleAxis(spinAngleDeg, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                rot,
                new Vector3(1.45f, 1f, 1.45f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.ThrownSickleMat, 0);
        }
    }

    /// <summary>小红帽与赤红疤痕命中瞬间的猩红冲击波环与灼热火星爆发特效。</summary>
    public class Thing_LittleRedImpactBurst : Thing
    {
        private Vector3 exactPos;
        private float scale = 1.15f;
        private bool isBerserk;
        private int ageTicks;
        private const int DurationTicks = 14;
        private bool initialized;

        private MaterialPropertyBlock ringProps;

        public void Initialize(Vector3 pos, float burstScale = 1.15f, bool berserk = false)
        {
            exactPos = pos.Yto0();
            scale = Mathf.Max(0.6f, burstScale);
            isBerserk = berserk;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || LittleRedMaterials.SlashMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float alpha = Mathf.Pow(1f - t, 1.3f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
            float sz = scale * Mathf.Lerp(0.55f, 1.65f, 1f - Mathf.Pow(1f - t, 2f));

            ringProps ??= new MaterialPropertyBlock();
            ringProps.SetColor(LittleRedMaterials.PropColor, isBerserk
                ? new Color(1.0f, 0.12f, 0.12f, 1.0f)
                : new Color(1.0f, 0.36f, 0.14f, 1.0f));
            ringProps.SetColor(LittleRedMaterials.PropCoreColor, new Color(0.05f, 0.01f, 0.01f, 1.0f));
            ringProps.SetColor(LittleRedMaterials.PropTipColor, new Color(1.0f, 0.92f, 0.58f, 1.0f));
            ringProps.SetFloat(LittleRedMaterials.PropProgress, t);
            ringProps.SetFloat(LittleRedMaterials.PropAgeSecs, ageTicks / 60f);
            ringProps.SetFloat(LittleRedMaterials.PropAlpha, alpha);
            ringProps.SetFloat(LittleRedMaterials.PropArcSpan, 3.55f);
            ringProps.SetFloat(LittleRedMaterials.PropBerserk, isBerserk ? 1.0f : 0.35f);

            Quaternion rot = Quaternion.AngleAxis(ageTicks * 18f, Vector3.up);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(exactPos.x, alt, exactPos.z), rot, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.SlashMat, 0, null, 0, ringProps);
        }
    }

    /// <summary>小红帽雇佣兵镇压消散演出：还原单膝跪地插镰的战败姿势与猩红斗篷余烬消散，最终凝结为收容蛋。</summary>
    public class DyingLittleRed : LC_FX_Standard
    {
        private const int TotalDurationTicks = 84;
        private int startTick;
        private MaterialPropertyBlock fadeProps;

        public override void InitWith(Pawn targetPawn)
        {
            CompAbnormality comp = targetPawn?.TryGetComp<CompAbnormality>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            startTick = Find.TickManager?.TicksGame ?? TickSpawned;
            completeTick = startTick + TotalDurationTicks;
            hasInited = true;

            if (Map != null && Position.InBounds(Map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Change, this);
                Vector3 center = Position.ToVector3Shifted();
                FleckMaker.ThrowDustPuffThick(center, Map, 2.0f, new Color(0.68f, 0.08f, 0.10f));
                FleckMaker.ThrowMicroSparks(center, Map);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!hasInited || LittleRedMaterials.DeadPoseMat == null)
            {
                return;
            }

            int elapsed = Mathf.Clamp((Find.TickManager?.TicksGame ?? startTick) - startTick, 0, TotalDurationTicks);
            float t = elapsed / (float)TotalDurationTicks;
            // Hold kneeling defeated pose clearly for first 55%, then dissolve into crimson mist
            float fadeT = Mathf.Clamp01((t - 0.50f) / 0.50f);
            float alpha = Mathf.Pow(1f - fadeT, 1.35f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.Pawn.AltitudeFor();
            float sz = Mathf.Lerp(2.15f, 1.90f, fadeT);
            fadeProps ??= new MaterialPropertyBlock();
            fadeProps.SetColor(LittleRedMaterials.PropColor, new Color(1f, Mathf.Lerp(1f, 0.35f, fadeT), Mathf.Lerp(1f, 0.35f, fadeT), alpha));

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(drawLoc.x, alt, drawLoc.z),
                Quaternion.identity,
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LittleRedMaterials.DeadPoseMat, 0, null, 0, fadeProps);
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            EggReturnExtension ext = def.GetModExtension<EggReturnExtension>();
            if (ext?.eggDef != null && MapHeld != null && PositionHeld.InBounds(MapHeld))
            {
                Thing egg = ThingMaker.MakeThing(ext.eggDef);
                AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, PositionHeld, MapHeld);
            }

            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "lrDeadStartTick", 0);
        }
    }

    /// <summary>小红帽雇佣兵与赤红疤痕 VFX 生成辅助工具。</summary>
    public static class LittleRedVfxUtility
    {
        public static void SpawnSlash(Map map, Vector3 pos, float yawAngleDeg, float scale = 2.0f, bool flipSweep = false, bool berserk = false, int lifeTicks = 16, float spanRad = 2.55f)
        {
            if (map == null || LittleRedDefOf.LC_LittleRedSlashFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(LittleRedDefOf.LC_LittleRedSlashFX, cell, map) is Thing_LittleRedSlash slash)
            {
                slash.Initialize(pos, yawAngleDeg, scale, flipSweep, berserk, lifeTicks, spanRad);
            }
        }

        public static void SpawnShotgunCone(Map map, Vector3 muzzlePos, Vector3 dir, float length = 4.2f, float width = 2.1f, float spread = 0.58f, bool berserk = false, int lifeTicks = 15)
        {
            if (map == null || LittleRedDefOf.LC_LittleRedShotgunConeFX == null)
            {
                return;
            }

            IntVec3 cell = muzzlePos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(LittleRedDefOf.LC_LittleRedShotgunConeFX, cell, map) is Thing_LittleRedShotgunCone cone)
            {
                cone.Initialize(muzzlePos, dir, length, width, spread, berserk, lifeTicks);
            }
        }

        public static Thing_LittleRedHuntMark SpawnHuntMark(Map map, Pawn hunter, Pawn prey)
        {
            if (map == null || hunter == null || prey == null || !prey.Spawned || LittleRedDefOf.LC_LittleRedHuntMarkFX == null)
            {
                return null;
            }

            IntVec3 cell = prey.Position.ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return null;
            }

            if (GenSpawn.Spawn(LittleRedDefOf.LC_LittleRedHuntMarkFX, cell, map) is Thing_LittleRedHuntMark mark)
            {
                mark.Initialize(hunter, prey);
                return mark;
            }

            return null;
        }

        public static Thing_LittleRedThrownSickle SpawnThrownSickle(Map map, Pawn thrower, Vector3 startPos, Vector3 targetPos, float damage, bool isHired)
        {
            if (map == null || thrower == null || LittleRedDefOf.LC_LittleRedThrownSickleFX == null)
            {
                return null;
            }

            IntVec3 cell = startPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return null;
            }

            if (GenSpawn.Spawn(LittleRedDefOf.LC_LittleRedThrownSickleFX, cell, map) is Thing_LittleRedThrownSickle sickle)
            {
                sickle.Initialize(thrower, startPos, targetPos, damage, isHired);
                return sickle;
            }

            return null;
        }

        public static void SpawnImpactBurst(Map map, Vector3 pos, float scale = 1.15f, bool berserk = false)
        {
            if (map == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (LittleRedDefOf.LC_LittleRedImpactFX != null &&
                GenSpawn.Spawn(LittleRedDefOf.LC_LittleRedImpactFX, cell, map) is Thing_LittleRedImpactBurst burst)
            {
                burst.Initialize(pos, scale, berserk);
            }

            FleckMaker.ThrowMicroSparks(pos, map);
            if (berserk)
            {
                FleckMaker.ThrowDustPuffThick(pos, map, 1.15f, new Color(0.85f, 0.10f, 0.14f, 0.85f));
            }
        }

        public static float VectorToYawDeg(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// 赤红疤痕 E.G.O. 武器专用射击动词：
    /// 每次击发均在枪口绽放自定义 GLSL 猩红霰弹爆焰锥与散射曳光弹线，并在持握者半血以下触发猩红狂暴强化光效。
    /// </summary>
    public class Verb_ShootCrimsonScar : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && Caster != null && Caster.MapHeld != null)
            {
                Vector3 origin = Caster.DrawPos.Yto0();
                Vector3 targetPos = currentTarget.IsValid ? currentTarget.CenterVector3.Yto0() : (origin + Vector3.right);
                Vector3 dir = (targetPos - origin).Yto0();
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }
                dir.Normalize();

                bool berserk = Caster is Pawn p && !p.Dead && p.health?.summaryHealth != null && p.health.summaryHealth.SummaryHealthPercent < 0.55f;
                Vector3 muzzlePos = origin + dir * 0.55f;
                LittleRedVfxUtility.SpawnShotgunCone(
                    Caster.MapHeld,
                    muzzlePos,
                    dir,
                    length: berserk ? 4.8f : 3.6f,
                    width: berserk ? 2.35f : 1.75f,
                    spread: berserk ? 0.65f : 0.52f,
                    berserk: berserk,
                    lifeTicks: berserk ? 16 : 13);

                FleckMaker.ThrowSmoke(muzzlePos, Caster.MapHeld, berserk ? 1.1f : 0.85f);
            }

            return fired;
        }
    }

    /// <summary>
    /// 赤红疤痕 E.G.O. 武器与小红帽通用猩红霰弹投射物：
    /// 飞行沿途抛洒炽热红金火星，命中时引爆猩红冲击波与灼热火星特效（低血狂暴时附带猩红斩裂光弧）。
    /// </summary>
    public class Projectile_CrimsonScarShot : Bullet
    {
        internal static bool IsApplyingImpact;

        private int flightTicks;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Spawned || Map == null)
            {
                return;
            }

            flightTicks += delta;
            if (flightTicks % 2 == 0)
            {
                FleckMaker.ThrowMicroSparks(ExactPosition, Map);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = hitThing != null && hitThing.Spawned ? hitThing.DrawPos.Yto0() : ExactPosition.Yto0();
            Vector3 shotDir = (destination - origin).Yto0();
            bool berserk = launcher is Pawn lp && !lp.Dead && lp.health?.summaryHealth != null && lp.health.summaryHealth.SummaryHealthPercent < 0.55f;

            IsApplyingImpact = true;
            try
            {
                base.Impact(hitThing, blockedByShield);
            }
            finally
            {
                IsApplyingImpact = false;
            }

            if (map != null)
            {
                LittleRedVfxUtility.SpawnImpactBurst(map, impactPos, berserk ? 1.45f : 1.05f, berserk);
                if (berserk && hitThing != null)
                {
                    float yaw = LittleRedVfxUtility.VectorToYawDeg(shotDir);
                    LittleRedVfxUtility.SpawnSlash(map, impactPos, yaw + Rand.Range(-30f, 30f), 1.65f, flipSweep: Rand.Bool, berserk: true, lifeTicks: 14, spanRad: 2.65f);
                }
            }
        }
    }
}
