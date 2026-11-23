using LCAbnormalities.Shared;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.ScorchedGirl
{
    [DefOf]
    public static class ScorchedGirlDefOf
    {
        public static ThingDef ScorchedGirlEgg;

        // Sounds
        public static SoundDef LC_ScorchedGirl_Escape;
        public static SoundDef LC_ScorchedGirl_Fuse;
        public static SoundDef LC_ScorchedGirl_Explode;
        public static SoundDef LC_ScorchedGirl_SmallExplode;
        public static SoundDef LC_ScorchedGirl_WorkLoop;
        public static SoundDef LC_ScorchedGirl_Weeping;
        public static SoundDef LC_ScorchedGirl_Enter;
        public static SoundDef LC_ScorchedGirl_MatchStrike;
        public static SoundDef LC_ScorchedGirl_Walk;
        public static SoundDef LC_ScorchedGirl_Prime;
        public static SoundDef LC_ScorchedGirl_Death;
        public static SoundDef LC_ScorchedGirl_Cannon;

        // Animations
        public static AnimationDef ScorchedGirl_Idle;
        public static AnimationDef ScorchedGirl_Work;
        public static AnimationDef ScorchedGirl_Walk;
        public static AnimationDef ScorchedGirl_Attack;

        // Flecks (E.G.O. Weapon & Detonation VFX)
        public static FleckDef LC_FourthMatch_Muzzle;
        public static FleckDef LC_FourthMatch_Explosion;
        public static FleckDef LC_FourthMatch_HitRing;
        public static FleckDef LC_FourthMatch_Central;
        public static FleckDef LC_FourthMatch_Arc;
        public static FleckDef LC_FourthMatch_Glow;
        public static FleckDef LC_FourthMatch_Spark;
        public static FleckDef LC_ScorchedGirl_Mark;

        static ScorchedGirlDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ScorchedGirlDefOf));
        }
    }

    /// <summary>焦化少女与 E.G.O. 武器“第四把火”共用的原版序列帧火焰与爆炸特效生成器。</summary>
    public static class ScorchedGirlVfx
    {
        public static void SpawnMuzzleBlast(Vector3 origin, Vector3 directionFlat, Map map)
        {
            if (map == null || !origin.InBounds(map))
            {
                return;
            }

            Vector3 dir = directionFlat.Yto0();
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float angleFlat = dir.AngleFlat();
            Vector3 muzzlePos = origin + dir * 0.62f;

            // FireMuzzle.png 火焰默认朝东 (+X = 90°)，旋转对齐发射方向
            if (ScorchedGirlDefOf.LC_FourthMatch_Muzzle != null)
            {
                FleckCreationData data = FleckMaker.GetDataStatic(muzzlePos, map, ScorchedGirlDefOf.LC_FourthMatch_Muzzle, 1.05f);
                data.rotation = angleFlat - 90f;
                data.velocityAngle = angleFlat;
                data.velocitySpeed = 1.6f;
                map.flecks.CreateFleck(data);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Glow != null)
            {
                FleckCreationData glow = FleckMaker.GetDataStatic(muzzlePos, map, ScorchedGirlDefOf.LC_FourthMatch_Glow, 0.78f);
                glow.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(glow);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Spark != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    FleckCreationData spark = FleckMaker.GetDataStatic(
                        muzzlePos + new Vector3(Rand.Range(-0.14f, 0.14f), 0f, Rand.Range(-0.14f, 0.14f)),
                        map,
                        ScorchedGirlDefOf.LC_FourthMatch_Spark,
                        Rand.Range(0.55f, 0.95f));
                    spark.rotation = Rand.Range(0f, 360f);
                    spark.rotationRate = Rand.Range(-220f, 220f);
                    spark.velocityAngle = angleFlat + Rand.Range(-32f, 32f);
                    spark.velocitySpeed = Rand.Range(2.4f, 5.2f);
                    map.flecks.CreateFleck(spark);
                }
            }
        }

        public static void SpawnProjectileTrail(Vector3 pos, float travelAngleFlat, Map map)
        {
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Spark != null)
            {
                FleckCreationData spark = FleckMaker.GetDataStatic(
                    pos + new Vector3(Rand.Range(-0.12f, 0.12f), 0f, Rand.Range(-0.12f, 0.12f)),
                    map,
                    ScorchedGirlDefOf.LC_FourthMatch_Spark,
                    Rand.Range(0.50f, 0.85f));
                spark.rotation = Rand.Range(0f, 360f);
                spark.rotationRate = Rand.Range(-180f, 180f);
                spark.velocityAngle = travelAngleFlat + 180f + Rand.Range(-35f, 35f);
                spark.velocitySpeed = Rand.Range(0.8f, 2.2f);
                map.flecks.CreateFleck(spark);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Glow != null && Rand.Chance(0.55f))
            {
                FleckCreationData glow = FleckMaker.GetDataStatic(pos, map, ScorchedGirlDefOf.LC_FourthMatch_Glow, 0.48f);
                glow.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(glow);
            }

            if (Rand.Chance(0.35f))
            {
                FleckMaker.ThrowSmoke(pos, map, Rand.Range(0.55f, 0.9f));
            }
        }

        public static void SpawnFlameExplosion(Vector3 center, Map map, float scale = 1.0f)
        {
            if (map == null || !center.InBounds(map))
            {
                return;
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Glow != null)
            {
                FleckCreationData glow = FleckMaker.GetDataStatic(center, map, ScorchedGirlDefOf.LC_FourthMatch_Glow, 1.45f * scale);
                glow.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(glow);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_HitRing != null)
            {
                FleckCreationData ring = FleckMaker.GetDataStatic(center, map, ScorchedGirlDefOf.LC_FourthMatch_HitRing, 1.05f * scale);
                ring.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(ring);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Explosion != null)
            {
                FleckCreationData exp = FleckMaker.GetDataStatic(center, map, ScorchedGirlDefOf.LC_FourthMatch_Explosion, 1.10f * scale);
                exp.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(exp);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Central != null)
            {
                FleckCreationData central = FleckMaker.GetDataStatic(
                    center + new Vector3(Rand.Range(-0.15f, 0.15f), 0f, Rand.Range(-0.15f, 0.15f)),
                    map,
                    ScorchedGirlDefOf.LC_FourthMatch_Central,
                    1.0f * scale);
                central.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(central);
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Arc != null)
            {
                int arcCount = scale >= 1.5f ? 4 : 2;
                for (int i = 0; i < arcCount; i++)
                {
                    float ang = i * (360f / arcCount) + Rand.Range(-30f, 30f);
                    Vector3 arcPos = center + Quaternion.AngleAxis(ang, Vector3.up) * Vector3.forward * (0.45f * scale);
                    FleckCreationData arc = FleckMaker.GetDataStatic(arcPos, map, ScorchedGirlDefOf.LC_FourthMatch_Arc, 0.95f * scale);
                    arc.rotation = ang;
                    map.flecks.CreateFleck(arc);
                }
            }

            if (ScorchedGirlDefOf.LC_FourthMatch_Spark != null)
            {
                int sparkCount = scale >= 1.5f ? 12 : 7;
                for (int i = 0; i < sparkCount; i++)
                {
                    float ang = Rand.Range(0f, 360f);
                    FleckCreationData spark = FleckMaker.GetDataStatic(
                        center + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.25f, 0.25f)),
                        map,
                        ScorchedGirlDefOf.LC_FourthMatch_Spark,
                        Rand.Range(0.75f, 1.25f) * scale);
                    spark.rotation = ang;
                    spark.rotationRate = Rand.Range(-260f, 260f);
                    spark.velocityAngle = ang;
                    spark.velocitySpeed = Rand.Range(2.2f, 5.8f) * Mathf.Sqrt(scale);
                    map.flecks.CreateFleck(spark);
                }
            }

            FleckMaker.ThrowSmoke(center, map, 1.6f * scale);
        }

        public static void SpawnEmberSpark(Vector3 pos, Map map, float scale = 0.65f)
        {
            if (map == null || !pos.InBounds(map) || ScorchedGirlDefOf.LC_FourthMatch_Spark == null)
            {
                return;
            }

            FleckCreationData spark = FleckMaker.GetDataStatic(
                pos + new Vector3(Rand.Range(-0.18f, 0.18f), 0f, Rand.Range(0.05f, 0.28f)),
                map,
                ScorchedGirlDefOf.LC_FourthMatch_Spark,
                scale);
            spark.rotation = Rand.Range(0f, 360f);
            spark.rotationRate = Rand.Range(-150f, 150f);
            spark.velocityAngle = Rand.Range(-35f, 35f);
            spark.velocitySpeed = Rand.Range(0.7f, 1.8f);
            map.flecks.CreateFleck(spark);
        }

        public static void SpawnWarningMark(Vector3 pos, Map map, float scale = 1.0f)
        {
            if (map == null || !pos.InBounds(map) || ScorchedGirlDefOf.LC_ScorchedGirl_Mark == null)
            {
                return;
            }

            FleckCreationData mark = FleckMaker.GetDataStatic(pos + new Vector3(0f, 0f, 0.85f), map, ScorchedGirlDefOf.LC_ScorchedGirl_Mark, scale);
            mark.velocityAngle = 0f;
            mark.velocitySpeed = 0.32f;
            map.flecks.CreateFleck(mark);
        }
    }

    public class CompProperties_ScorchedGirl : CompProperties_LCAbnormality
    {
        public int fuseDurationTicks = 1200;
        public int primeDurationTicks = 90;
        public float explosionRadius = 4.2f;
        public int explosionDamage = 38;
        public float detonateDistance = 1.8f;

        public CompProperties_ScorchedGirl()
        {
            compClass = typeof(CompScorchedGirl);
        }
    }

    /// <summary>
    /// F-01-02 焦化少女核心逻辑：良评估 50%/差评估 100% 降计数；突破后点燃导火索伴随哭泣与脚步声冲向人类，
    /// 贴近目标或满 20 秒进入 1.5 秒睁红眼蓄力演出后引爆高额红伤自毁化蛋；若在引爆前被击杀则熄灭化蛋。
    /// </summary>
    public class CompScorchedGirl : CompLCAbnormality
    {
        private int fuseTicksLeft;
        private int detonationPrimeTicks;
        private bool detonated;
        private int studyingGraceTicks;
        private int walkStepCooldown;
        private bool nearTargetForRedEyes;
        private Sustainer fuseSustainer;
        private Sustainer weepingSustainer;

        public CompProperties_ScorchedGirl PropsScorched => (CompProperties_ScorchedGirl)props;

        public int FuseTicksLeft => fuseTicksLeft;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public bool IsPrimingDetonation => Escaped && !detonated && detonationPrimeTicks > 0;

        public float DetonationPrimeProgress
        {
            get
            {
                if (!IsPrimingDetonation)
                {
                    return 0f;
                }

                int total = Mathf.Max(1, PropsScorched.primeDurationTicks);
                return Mathf.Clamp01(1f - (detonationPrimeTicks / (float)total));
            }
        }

        public bool ShouldShowRedEyes =>
            IsPrimingDetonation || (Escaped && (nearTargetForRedEyes || fuseTicksLeft <= 300));

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            studyingGraceTicks = 30;
            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Enter, parent);
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            studyingGraceTicks = 25;
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (!Escaped && parent != null && !parent.Destroyed && Rand.Chance(0.5f))
            {
                QliphothCountCurrent--;
                EmitMatchFlareFeedback();
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (!Escaped && parent != null && !parent.Destroyed)
            {
                EmitMatchFlareFeedback();
            }
        }

        private void EmitMatchFlareFeedback()
        {
            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_MatchStrike, parent);
            if (parent?.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
            {
                Vector3 pos = parent.DrawPos;
                for (int i = 0; i < 3; i++)
                {
                    ScorchedGirlVfx.SpawnEmberSpark(pos, parent.MapHeld, Rand.Range(0.55f, 0.85f));
                }
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            fuseTicksLeft = PropsScorched.fuseDurationTicks;
            detonationPrimeTicks = 0;
            detonated = false;
            nearTargetForRedEyes = false;
            walkStepCooldown = 15;

            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_MatchStrike, parent);
            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Escape, parent);
            if (parent?.Map != null)
            {
                ScorchedGirlVfx.SpawnWarningMark(parent.DrawPos, parent.Map, 1.15f);
            }

            TryStartBreachSustainers();
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            EndBreachSustainers();
            fuseTicksLeft = 0;
            detonationPrimeTicks = 0;
            detonated = false;
            nearTargetForRedEyes = false;
            studyingGraceTicks = 0;
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }

            if (parent?.MapHeld == null || !parent.PositionHeld.InBounds(parent.MapHeld))
            {
                return;
            }

            // 火柴随计数器下降或工作激动而燃大，周期性飘散火星
            int interval = (QliphothCountCurrent <= 1 || IsBeingStudiedNow) ? 42 : 110;
            if (parent.IsHashIntervalTick(interval))
            {
                Vector3 sparkPos = parent.DrawPos + new Vector3(-0.32f, 0f, 0.14f);
                ScorchedGirlVfx.SpawnEmberSpark(sparkPos, parent.MapHeld, QliphothCountCurrent <= 1 ? 0.65f : 0.45f);
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || detonated || parent == null || parent.Destroyed || !parent.Spawned)
            {
                EndBreachSustainers();
                return;
            }

            MaintainBreachSustainers();

            Pawn pawn = parent as Pawn;

            // 自爆蓄力演出阶段（对应原版 MatchGirl_Attack_Baked：停步下蹲 -> 闭眼 -> 睁血红竖瞳 -> 核爆）
            if (detonationPrimeTicks > 0)
            {
                if (pawn?.pather?.Moving == true)
                {
                    pawn.pather.StopDead();
                }

                detonationPrimeTicks--;
                if (detonationPrimeTicks % 8 == 0 && parent.Map != null)
                {
                    ScorchedGirlVfx.SpawnEmberSpark(parent.DrawPos, parent.Map, Rand.Range(0.75f, 1.15f));
                    if (detonationPrimeTicks == PropsScorched.primeDurationTicks / 2 && ScorchedGirlDefOf.LC_FourthMatch_Glow != null)
                    {
                        FleckCreationData flash = FleckMaker.GetDataStatic(parent.DrawPos, parent.Map, ScorchedGirlDefOf.LC_FourthMatch_Glow, 1.15f);
                        parent.Map.flecks.CreateFleck(flash);
                    }
                }

                if (detonationPrimeTicks <= 0)
                {
                    DetonateAndReturnToEgg();
                }
                return;
            }

            // 行走脚步声（每 30 ticks 与 60-tick Walk 动画双脚落地卡点同步）
            if (pawn?.pather?.Moving == true)
            {
                walkStepCooldown--;
                if (walkStepCooldown <= 0)
                {
                    walkStepCooldown = 30;
                    LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Walk, parent);
                }
            }
            else
            {
                walkStepCooldown = 12;
            }

            // 沿途从火柴头飘洒燃烧火星与热辉光
            if (parent.IsHashIntervalTick(10) && parent.Map != null)
            {
                float side = (pawn as LC_ScorchedGirlPawn)?.IsFacingLeft == true ? 0.34f : -0.34f;
                ScorchedGirlVfx.SpawnEmberSpark(parent.DrawPos + new Vector3(side, 0f, 0.14f), parent.Map, Rand.Range(0.55f, 0.85f));
            }

            fuseTicksLeft--;
            Pawn closeTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position, PropsScorched.detonateDistance);
            if (parent.IsHashIntervalTick(15))
            {
                nearTargetForRedEyes = closeTarget != null
                    || LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position, 6.5f) != null;
            }

            if (fuseTicksLeft <= 0 || closeTarget != null)
            {
                BeginDetonationPrime(closeTarget);
            }
        }

        private void BeginDetonationPrime(Pawn target)
        {
            if (detonated || detonationPrimeTicks > 0 || parent == null || parent.Destroyed)
            {
                return;
            }

            detonationPrimeTicks = Mathf.Max(30, PropsScorched.primeDurationTicks);
            nearTargetForRedEyes = true;

            if (parent is LC_ScorchedGirlPawn sgPawn)
            {
                sgPawn.pather?.StopDead();
                if (target != null && target.Spawned)
                {
                    sgPawn.FaceCellHorizontal(target.Position);
                }
                sgPawn.UpdateAnimation(force: true);
            }

            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Prime, parent);
            if (parent.Map != null)
            {
                ScorchedGirlVfx.SpawnWarningMark(parent.DrawPos, parent.Map, 1.35f);
            }
        }

        public void DetonateAndReturnToEgg()
        {
            if (detonated || parent == null || parent.Destroyed)
            {
                return;
            }

            detonated = true;
            detonationPrimeTicks = 0;
            EndBreachSustainers();

            Map map = parent.MapHeld;
            IntVec3 pos = parent.PositionHeld;
            Vector3 drawPos = parent.Spawned ? parent.DrawPos : pos.ToVector3Shifted();
            int sig = biosignature;

            if (map != null && pos.InBounds(map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Explode, parent);
                ScorchedGirlVfx.SpawnFlameExplosion(drawPos, map, scale: 2.15f);
                if (Find.CurrentMap == map)
                {
                    Find.CameraDriver?.shaker?.DoShake(1.6f);
                }

                GenExplosion.DoExplosion(
                    pos,
                    map,
                    PropsScorched.explosionRadius,
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    parent,
                    damAmount: PropsScorched.explosionDamage,
                    armorPenetration: 0.45f,
                    explosionSound: ScorchedGirlDefOf.LC_ScorchedGirl_Explode,
                    chanceToStartFire: 0.35f,
                    doSoundEffects: false);
                Messages.Message(
                    "LC_ScorchedGirl_DetonatedMsg".Translate(),
                    new LookTargets(pos, map),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }

            LCAbnormalitiesHelper.ReturnEntityToEgg((Pawn)parent, ScorchedGirlDefOf.ScorchedGirlEgg, sig);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            EndBreachSustainers();
            detonationPrimeTicks = 0;
            if (!detonated)
            {
                if (prevMap != null && parent != null && parent.PositionHeld.InBounds(prevMap))
                {
                    ScorchedGirlDefOf.LC_ScorchedGirl_Death?.PlayOneShot(new TargetInfo(parent.PositionHeld, prevMap));
                }
                base.Notify_Killed(prevMap, dinfo);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            EndBreachSustainers();
            base.PostDeSpawn(map, mode);
        }

        private void TryStartBreachSustainers()
        {
            if (parent == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            if ((fuseSustainer == null || fuseSustainer.Ended) && ScorchedGirlDefOf.LC_ScorchedGirl_Fuse != null)
            {
                SoundInfo info = SoundInfo.InMap(parent, MaintenanceType.PerTick);
                fuseSustainer = ScorchedGirlDefOf.LC_ScorchedGirl_Fuse.TrySpawnSustainer(info);
            }

            if ((weepingSustainer == null || weepingSustainer.Ended) && ScorchedGirlDefOf.LC_ScorchedGirl_Weeping != null)
            {
                SoundInfo info = SoundInfo.InMap(parent, MaintenanceType.PerTick);
                weepingSustainer = ScorchedGirlDefOf.LC_ScorchedGirl_Weeping.TrySpawnSustainer(info);
            }
        }

        private void MaintainBreachSustainers()
        {
            TryStartBreachSustainers();
            fuseSustainer?.Maintain();
            weepingSustainer?.Maintain();
        }

        private void EndBreachSustainers()
        {
            if (fuseSustainer != null && !fuseSustainer.Ended)
            {
                fuseSustainer.End();
            }
            fuseSustainer = null;

            if (weepingSustainer != null && !weepingSustainer.Ended)
            {
                weepingSustainer.End();
            }
            weepingSustainer = null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref fuseTicksLeft, "sgFuseTicksLeft", 0);
            Scribe_Values.Look(ref detonationPrimeTicks, "sgDetonationPrimeTicks", 0);
            Scribe_Values.Look(ref detonated, "sgDetonated", false);
        }
    }

    public class LC_ScorchedGirlPawn : LC_StandardEntityPawn
    {
        private bool facingWest = false;

        public CompScorchedGirl ScorchedComp => EntityComp as CompScorchedGirl;

        public bool IsFacingLeft => facingWest;

        public override string CurrentBodyTexPath =>
            LCComp?.Escaped == true
                ? "Things/Pawn/ScorchedGirl/ScorchedGirl_Ignited"
                : "Things/Pawn/ScorchedGirl/ScorchedGirl";

        public override Vector2 CurrentBodyDrawSize => new Vector2(1.9f, 1.9f);

        public void FaceCellHorizontal(IntVec3 cell)
        {
            rotationTracker?.FaceCell(cell);
            if (Spawned && cell.IsValid)
            {
                if (cell.x < Position.x)
                {
                    facingWest = true;
                }
                else if (cell.x > Position.x)
                {
                    facingWest = false;
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompScorchedGirl comp = ScorchedComp;
            AnimationDef desired;
            if (comp != null && comp.IsPrimingDetonation)
            {
                desired = ScorchedGirlDefOf.ScorchedGirl_Attack;
            }
            else if (comp != null && comp.Escaped)
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? ScorchedGirlDefOf.ScorchedGirl_Walk
                    : ScorchedGirlDefOf.ScorchedGirl_Work;
            }
            else if (comp != null && (comp.IsBeingStudiedNow || comp.QliphothCountCurrent <= 1))
            {
                desired = ScorchedGirlDefOf.ScorchedGirl_Work;
            }
            else
            {
                desired = ScorchedGirlDefOf.ScorchedGirl_Idle;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override void TickHolded()
        {
            facingWest = false;
            base.TickHolded();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            CompScorchedGirl comp = ScorchedComp;
            if (comp != null && comp.Escaped && Spawned && !Dead)
            {
                UpdateHorizontalFacing();
            }
            else
            {
                facingWest = false;
            }

            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        private void UpdateHorizontalFacing()
        {
            if (pather != null && pather.Moving && pather.nextCell.IsValid)
            {
                if (pather.nextCell.x < Position.x)
                {
                    facingWest = true;
                    return;
                }

                if (pather.nextCell.x > Position.x)
                {
                    facingWest = false;
                    return;
                }
            }

            if (Rotation == Rot4.West)
            {
                facingWest = true;
            }
            else if (Rotation == Rot4.East)
            {
                facingWest = false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref facingWest, "sgFacingWest", false);
        }
    }

    public class JobGiver_ScorchedGirlCharge : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompScorchedGirl comp = pawn.TryGetComp<CompScorchedGirl>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsPrimingDetonation)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(target.Position) <= comp.PropsScorched.detonateDistance)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 15);
            }

            Verse.AI.Job charge = JobMaker.MakeJob(JobDefOf.Goto, target);
            charge.expiryInterval = 30;
            charge.checkOverrideOnExpire = true;
            charge.canBashDoors = true;
            charge.locomotionUrgency = LocomotionUrgency.Sprint;
            return charge;
        }
    }

    /// <summary>第四把火 E.G.O 火炮投射物：发射喷射原版炮口烈焰、飞行拖曳火星火尾、落点引爆 2.4 格红伤与原版烈焰爆炸动画。</summary>
    public class Projectile_FourthMatchFlame : Projectile
    {
        private int trailTickCounter;

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            trailTickCounter = 0;

            Map map = Map ?? launcher?.MapHeld;
            if (map != null)
            {
                Vector3 dir = (destination - origin).Yto0();
                ScorchedGirlVfx.SpawnMuzzleBlast(origin, dir, map);
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (Destroyed || Map == null)
            {
                return;
            }

            trailTickCounter += delta;
            if (trailTickCounter >= 2)
            {
                trailTickCounter = 0;
                Vector3 dir = (destination - origin).Yto0();
                float angle = dir.sqrMagnitude > 0.0001f ? dir.AngleFlat() : ExactRotation.eulerAngles.y;
                ScorchedGirlVfx.SpawnProjectileTrail(ExactPosition, angle, Map);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            Vector3 exactPos = ExactPosition;
            Thing inst = launcher;

            if (map != null && pos.InBounds(map) && !blockedByShield)
            {
                if (ScorchedGirlDefOf.LC_ScorchedGirl_SmallExplode != null)
                {
                    ScorchedGirlDefOf.LC_ScorchedGirl_SmallExplode.PlayOneShot(new TargetInfo(pos, map));
                }

                ScorchedGirlVfx.SpawnFlameExplosion(exactPos, map, scale: 1.05f);

                GenExplosion.DoExplosion(
                    pos,
                    map,
                    2.4f,
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    inst,
                    damAmount: 32,
                    armorPenetration: 0.35f,
                    explosionSound: ScorchedGirlDefOf.LC_ScorchedGirl_SmallExplode,
                    chanceToStartFire: 0.25f,
                    doSoundEffects: false);
            }

            base.Impact(hitThing, blockedByShield);
        }
    }
}
