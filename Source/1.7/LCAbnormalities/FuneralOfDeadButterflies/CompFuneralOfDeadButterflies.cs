using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.FuneralOfDeadButterflies
{
    [DefOf]
    public static class FuneralDefOf
    {
        public static SoundDef LC_Funeral_WorkLoop;
        public static SoundDef LC_Funeral_Attack;
        public static SoundDef LC_Funeral_Open;
        public static SoundDef LC_Funeral_Skill;
        public static SoundDef LC_Funeral_Close;
        public static SoundDef LC_Funeral_Dead;
        public static SoundDef LC_SolemnLament_BloomWhite;
        public static SoundDef LC_SolemnLament_BloomBlack;
        public static SoundDef LC_SolemnLament_Shoot;
        public static ThingDef LC_SolemnLamentButterflyBloom;
        public static ThingDef LC_SolemnLamentButterflyFlutter;
        public static ThingDef LC_SolemnLamentButterflyStream;
        public static ThingDef LC_SolemnLamentSkillRunnerFX;

        static FuneralDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FuneralDefOf));
        }
    }

    [DefOf]
    public static class FuneralAnimationDefOf
    {
        public static AnimationDef Funeral_Idle;
        public static AnimationDef Funeral_Work;
        public static AnimationDef Funeral_SideIdle;
        public static AnimationDef Funeral_Walk;
        public static AnimationDef Funeral_Attack;
        public static AnimationDef Funeral_CoffinOpen;
        public static AnimationDef Funeral_CoffinLoop;
        public static AnimationDef Funeral_CoffinClose;

        static FuneralAnimationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FuneralAnimationDefOf));
        }
    }

    public enum FuneralCoffinPhase
    {
        None = 0,
        Opening = 1,
        Loop = 2,
        Closing = 3
    }

    public class CompProperties_FuneralOfDeadButterflies : CompProperties_LCAbnormality
    {
        public int burstIntervalTicks = 600;
        public int salvoIntervalTicks = 110;
        public float burstRadius = 6.5f;
        public float coffinStreamLength = 13f;
        public float coffinStreamHalfWidth = 1.75f;
        public float salvoRange = 18f;

        public CompProperties_FuneralOfDeadButterflies()
        {
            compClass = typeof(CompFuneralOfDeadButterflies);
        }
    }

    /// <summary>
    /// T-01-68 亡蝶葬仪核心逻辑：勇气高于 III 或正义低于 III 工作必降计数；
    /// 突破后定期开棺（开盖 -> 沿直线持续喷涌黑白蝴蝶洪流 -> 合盖）释放白黑混合群蝶冲击，并穿插手枪手势远程齐射。
    /// </summary>
    public class CompFuneralOfDeadButterflies : CompLCAbnormality
    {
        private const int CoffinOpenDuration = 40;
        private const int CoffinLoopDuration = 80;
        private const int CoffinCloseDuration = 30;
        private const int CoffinDamageWaveInterval = 20;
        private const int AttackAnimDuration = 60;
        private const int AttackFireTickOffset = 20;

        private FuneralCoffinPhase coffinPhase = FuneralCoffinPhase.None;
        private int coffinPhaseTicks;
        private Vector3 coffinStreamOrigin;
        private Vector3 coffinStreamDir = Vector3.right;
        private float coffinStreamActualLength = 13f;
        private int burstCooldownTicks = 240;
        private int salvoCooldownTicks = 60;
        private int attackAnimTicks;
        private int pendingSalvoFireTicks;
        private Pawn pendingSalvoTarget;
        private int studyingGraceTicks;
        private Sustainer coffinSkillSustainer;
        private SolemnLamentButterflyStream activeCoffinStream;

        public CompProperties_FuneralOfDeadButterflies PropsFuneral => (CompProperties_FuneralOfDeadButterflies)props;

        public FuneralCoffinPhase CoffinPhase => coffinPhase;

        public bool IsCoffinOpen => coffinPhase != FuneralCoffinPhase.None;

        public bool IsPlayingAttackAnim => attackAnimTicks > 0 && !IsCoffinOpen;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            studyingGraceTicks = 120;
            return base.Notify_StudyInterval(studier, workType);
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            studyingGraceTicks = 0;
            base.Notify_Studied(studier, interrupted);
            if (studier == null || parent == null || parent.Destroyed || Escaped)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status != null)
            {
                bool badFortitude = status.GetPawnStatusELevel(EPawnStatus.Fortitude) > EPawnLevel.III;
                bool badJustice = status.GetPawnStatusELevel(EPawnStatus.Justice) < EPawnLevel.III;
                if (badFortitude || badJustice)
                {
                    QliphothCountCurrent--;
                    Messages.Message("LC_Funeral_BadVirtueMsg".Translate(studier.LabelShortCap), parent, MessageTypeDefOf.NegativeEvent, historical: false);
                }
            }
        }

        public override void Notify_Escaped()
        {
            bool wasEscaped = Escaped;
            base.Notify_Escaped();
            EndCoffinSkillSustainer();
            StopActiveCoffinStream();
            coffinPhase = FuneralCoffinPhase.None;
            coffinPhaseTicks = 0;
            attackAnimTicks = 0;
            pendingSalvoFireTicks = 0;
            pendingSalvoTarget = null;
            studyingGraceTicks = 0;
            burstCooldownTicks = 240;
            salvoCooldownTicks = 60;
            if (!wasEscaped)
            {
                LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Open, parent);
            }
            (parent as LC_FuneralOfDeadButterfliesPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            EndCoffinSkillSustainer();
            StopActiveCoffinStream();
            coffinPhase = FuneralCoffinPhase.None;
            coffinPhaseTicks = 0;
            attackAnimTicks = 0;
            pendingSalvoFireTicks = 0;
            pendingSalvoTarget = null;
            studyingGraceTicks = 0;
            (parent as LC_FuneralOfDeadButterfliesPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            EndCoffinSkillSustainer();
            StopActiveCoffinStream();
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            EndCoffinSkillSustainer();
            StopActiveCoffinStream();
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            EndCoffinSkillSustainer();
            StopActiveCoffinStream();
            base.PostDestroy(mode, previousMap);
        }

        private void StopActiveCoffinStream()
        {
            if (activeCoffinStream != null)
            {
                if (!activeCoffinStream.Destroyed)
                {
                    activeCoffinStream.StopEmitting();
                }
                activeCoffinStream = null;
            }
        }

        private void MaintainCoffinSkillSustainer()
        {
            if (FuneralDefOf.LC_Funeral_Skill == null || parent == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            if (coffinSkillSustainer == null || coffinSkillSustainer.Ended)
            {
                coffinSkillSustainer = FuneralDefOf.LC_Funeral_Skill.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
            }
            else
            {
                coffinSkillSustainer.Maintain();
            }
        }

        private void EndCoffinSkillSustainer()
        {
            if (coffinSkillSustainer != null)
            {
                if (!coffinSkillSustainer.Ended)
                {
                    coffinSkillSustainer.End();
                }
                coffinSkillSustainer = null;
            }
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || parent == null || parent.Destroyed || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            LC_FuneralOfDeadButterfliesPawn selfPawn = parent as LC_FuneralOfDeadButterfliesPawn;

            if (coffinPhase != FuneralCoffinPhase.None)
            {
                TickCoffinPhase(selfPawn);
                return;
            }

            if (attackAnimTicks > 0)
            {
                attackAnimTicks--;
                if (pendingSalvoFireTicks > 0)
                {
                    pendingSalvoFireTicks--;
                    if (pendingSalvoFireTicks == 0)
                    {
                        ExecutePendingSalvoImpact();
                    }
                }
            }

            burstCooldownTicks--;
            if (burstCooldownTicks <= 0 && attackAnimTicks <= 0)
            {
                burstCooldownTicks = PropsFuneral.burstIntervalTicks;
                StartCoffinEruption(selfPawn);
                return;
            }

            salvoCooldownTicks--;
            if (salvoCooldownTicks <= 0 && attackAnimTicks <= 0)
            {
                salvoCooldownTicks = PropsFuneral.salvoIntervalTicks;
                TryStartButterflySalvo(selfPawn);
            }
        }

        private void StartCoffinEruption(LC_FuneralOfDeadButterfliesPawn selfPawn)
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            Pawn nearest = LCAbnormalitiesHelper.FindNearestHumanlike(map, pos, PropsFuneral.salvoRange);
            Vector3 desiredDir;
            if (nearest != null)
            {
                selfPawn?.FaceCellHorizontal(nearest.Position);
                desiredDir = (nearest.DrawPos - parent.DrawPos).Yto0();
            }
            else
            {
                desiredDir = (selfPawn?.IsFacingLeft ?? false) ? Vector3.left : Vector3.right;
            }

            if (desiredDir.sqrMagnitude < 0.001f)
            {
                desiredDir = (selfPawn?.IsFacingLeft ?? false) ? Vector3.left : Vector3.right;
            }
            coffinStreamDir = desiredDir.normalized;
            coffinStreamOrigin = GetCoffinMouthOrigin(selfPawn, coffinStreamDir);
            coffinStreamActualLength = ComputeWallClippedStreamLength(map, coffinStreamOrigin, coffinStreamDir, PropsFuneral.coffinStreamLength);

            if (selfPawn?.pather?.Moving == true)
            {
                selfPawn.pather.StopDead();
            }

            StopActiveCoffinStream();
            coffinPhase = FuneralCoffinPhase.Opening;
            coffinPhaseTicks = CoffinOpenDuration;
            LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Open, parent);
            selfPawn?.UpdateAnimation(force: true);
        }

        private void TickCoffinPhase(LC_FuneralOfDeadButterfliesPawn selfPawn)
        {
            if (selfPawn?.pather?.Moving == true)
            {
                selfPawn.pather.StopDead();
            }

            coffinPhaseTicks--;
            if (coffinPhase == FuneralCoffinPhase.Opening)
            {
                if (coffinPhaseTicks <= 0)
                {
                    coffinPhase = FuneralCoffinPhase.Loop;
                    coffinPhaseTicks = CoffinLoopDuration;
                    MaintainCoffinSkillSustainer();
                    BeginCoffinLineStreamAndVfx(selfPawn, CoffinLoopDuration);
                    ApplyCoffinLineStreamDamage(selfPawn, waveIndex: 0);
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
            else if (coffinPhase == FuneralCoffinPhase.Loop)
            {
                MaintainCoffinSkillSustainer();

                // 读档恢复或特效意外销毁时，自动补齐剩余时长的直线蝴蝶洪流
                if ((activeCoffinStream == null || activeCoffinStream.Destroyed) && coffinPhaseTicks > 6 && parent.Map != null)
                {
                    BeginCoffinLineStreamAndVfx(selfPawn, coffinPhaseTicks);
                }

                // 开棺持续 80 tick 内每隔 20 tick 沿整条直线蝶潮结算一次白+黑混合伤害（共 4 波）
                int elapsed = CoffinLoopDuration - coffinPhaseTicks;
                if (elapsed > 0 && elapsed < CoffinLoopDuration && elapsed % CoffinDamageWaveInterval == 0)
                {
                    ApplyCoffinLineStreamDamage(selfPawn, elapsed / CoffinDamageWaveInterval);
                }

                if (coffinPhaseTicks <= 0)
                {
                    EndCoffinSkillSustainer();
                    StopActiveCoffinStream();
                    coffinPhase = FuneralCoffinPhase.Closing;
                    coffinPhaseTicks = CoffinCloseDuration;
                    LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Close, parent);
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
            else if (coffinPhase == FuneralCoffinPhase.Closing)
            {
                EndCoffinSkillSustainer();
                StopActiveCoffinStream();
                if (coffinPhaseTicks <= 0)
                {
                    coffinPhase = FuneralCoffinPhase.None;
                    coffinPhaseTicks = 0;
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
        }

        private Vector3 GetCoffinMouthOrigin(LC_FuneralOfDeadButterfliesPawn selfPawn, Vector3 dir)
        {
            Vector3 d = dir.Yto0();
            if (d.sqrMagnitude < 0.001f)
            {
                d = (selfPawn?.IsFacingLeft ?? false) ? Vector3.left : Vector3.right;
            }
            else
            {
                d.Normalize();
            }

            Vector3 basePos = parent != null ? parent.DrawPos.Yto0() : Vector3.zero;
            return basePos + d * 0.42f + new Vector3(0f, 0f, 0.08f);
        }

        private static float ComputeWallClippedStreamLength(Map map, Vector3 origin, Vector3 dir, float maxLength)
        {
            float clampedMax = Mathf.Clamp(maxLength, 3f, 24f);
            if (map == null)
            {
                return clampedMax;
            }

            const float step = 0.5f;
            float dist = 1.0f;
            while (dist <= clampedMax)
            {
                Vector3 p = origin + dir * dist;
                IntVec3 cell = p.ToIntVec3();
                if (!cell.InBounds(map) || cell.Filled(map))
                {
                    return Mathf.Max(2.5f, dist - 0.35f);
                }
                dist += step;
            }

            return clampedMax;
        }

        private void BeginCoffinLineStreamAndVfx(LC_FuneralOfDeadButterfliesPawn selfPawn, int emitDurationTicks)
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            if (coffinStreamDir.sqrMagnitude < 0.001f)
            {
                coffinStreamDir = (selfPawn?.IsFacingLeft ?? false) ? Vector3.left : Vector3.right;
            }
            coffinStreamDir = coffinStreamDir.Yto0().normalized;
            coffinStreamOrigin = GetCoffinMouthOrigin(selfPawn, coffinStreamDir);
            coffinStreamActualLength = ComputeWallClippedStreamLength(map, coffinStreamOrigin, coffinStreamDir, PropsFuneral.coffinStreamLength);

            StopActiveCoffinStream();
            activeCoffinStream = FuneralVfxUtility.SpawnCoffinButterflyStream(
                map,
                coffinStreamOrigin,
                coffinStreamDir,
                coffinStreamActualLength,
                PropsFuneral.coffinStreamHalfWidth,
                emitDurationTicks);

            // 棺材口补一缕近身散逸亡蝶，衔接棺盖打开的层次感
            FuneralVfxUtility.SpawnCoffinSwarmWave(map, coffinStreamOrigin, coffinStreamDir.x < 0f, count: 5);
        }

        private void ApplyCoffinLineStreamDamage(LC_FuneralOfDeadButterfliesPawn selfPawn, int waveIndex)
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            Vector3 dir = coffinStreamDir.Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = (selfPawn?.IsFacingLeft ?? false) ? Vector3.left : Vector3.right;
            }
            dir.Normalize();
            Vector3 right = new Vector3(-dir.z, 0f, dir.x);
            Vector3 origin = coffinStreamOrigin.sqrMagnitude > 0.01f ? coffinStreamOrigin : GetCoffinMouthOrigin(selfPawn, dir);
            float maxLen = Mathf.Max(2.5f, coffinStreamActualLength);
            float halfW = Mathf.Max(1.0f, PropsFuneral.coffinStreamHalfWidth);
            const float closeAuraSq = 2.2f * 2.2f;

            bool blackFirst = (waveIndex % 2) == 1;
            List<Pawn> list = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < list.Count; i++)
            {
                Pawn target = list[i];
                if (target == null || target == parent || target.Dead || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                Vector3 rel = (target.DrawPos - origin).Yto0();
                float along = Vector3.Dot(rel, dir);
                float perp = Mathf.Abs(Vector3.Dot(rel, right));
                bool inLineCorridor = along >= -0.6f && along <= maxLen + 0.5f && perp <= halfW;
                bool inCloseCoffinAura = (target.Position - pos).LengthHorizontalSquared <= closeAuraSq;

                if (inLineCorridor || inCloseCoffinAura)
                {
                    FuneralVfxUtility.SpawnTargetButterflyBloom(map, target.DrawPos, isBlack: blackFirst, scale: 1.05f, flutterCount: 2);
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 4.5f, 0.35f, instigator: parent));
                    if (!target.Dead)
                    {
                        target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 4.5f, 0.35f, instigator: parent));
                    }
                }
            }
        }

        private void TryStartButterflySalvo(LC_FuneralOfDeadButterfliesPawn selfPawn)
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(map, pos, PropsFuneral.salvoRange);
            if (target == null || !GenSight.LineOfSight(pos, target.Position, map))
            {
                return;
            }

            selfPawn?.FaceCellHorizontal(target.Position);
            if (selfPawn?.pather?.Moving == true)
            {
                selfPawn.pather.StopDead();
            }

            attackAnimTicks = AttackAnimDuration;
            pendingSalvoFireTicks = AttackFireTickOffset;
            pendingSalvoTarget = target;
            selfPawn?.UpdateAnimation(force: true);
        }

        private void ExecutePendingSalvoImpact()
        {
            Map map = parent?.Map;
            if (map == null)
            {
                pendingSalvoTarget = null;
                return;
            }

            Pawn target = pendingSalvoTarget;
            pendingSalvoTarget = null;
            if (target == null || target.Destroyed || target.Dead || !target.Spawned || target.Map != map)
            {
                target = LCAbnormalitiesHelper.FindNearestHumanlike(map, parent.Position, PropsFuneral.salvoRange);
            }

            if (target == null || !GenSight.LineOfSight(parent.Position, target.Position, map))
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Attack, parent);
            Vector3 dir = (target.DrawPos - parent.DrawPos).Yto0();
            FuneralVfxUtility.SpawnMuzzleFlutter(map, parent.DrawPos + new Vector3(0f, 0f, 0.25f), dir, isBlack: false);
            FuneralVfxUtility.SpawnTargetButterflyBloom(map, target.DrawPos, isBlack: false, scale: 1.05f, flutterCount: 3);
            FuneralVfxUtility.SpawnTargetButterflyBloom(map, target.DrawPos, isBlack: true, scale: 0.90f, flutterCount: 2);

            target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 9f, 0.30f, instigator: parent));
            if (!target.Dead)
            {
                target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 9f, 0.30f, instigator: parent));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref coffinPhase, "fdbCoffinPhase", FuneralCoffinPhase.None);
            Scribe_Values.Look(ref coffinPhaseTicks, "fdbCoffinPhaseTicks", 0);
            Scribe_Values.Look(ref coffinStreamOrigin, "fdbCoffinStreamOrigin");
            Scribe_Values.Look(ref coffinStreamDir, "fdbCoffinStreamDir", Vector3.right);
            Scribe_Values.Look(ref coffinStreamActualLength, "fdbCoffinStreamLen", 13f);
            Scribe_Values.Look(ref burstCooldownTicks, "fdbBurstCooldownTicks", 240);
            Scribe_Values.Look(ref salvoCooldownTicks, "fdbSalvoCooldownTicks", 60);
            Scribe_Values.Look(ref attackAnimTicks, "fdbAttackAnimTicks", 0);
            Scribe_Values.Look(ref pendingSalvoFireTicks, "fdbPendingSalvoFireTicks", 0);
            Scribe_Values.Look(ref studyingGraceTicks, "fdbStudyingGraceTicks", 0);
            Scribe_References.Look(ref pendingSalvoTarget, "fdbPendingSalvoTarget");
        }
    }

    /// <summary>亡蝶葬仪实体 Pawn：管理多姿态动画状态切换与水平朝向锁定。</summary>
    public class LC_FuneralOfDeadButterfliesPawn : LC_StandardEntityPawn
    {
        private bool facingWest;

        public CompFuneralOfDeadButterflies FuneralComp => EntityComp as CompFuneralOfDeadButterflies;

        public bool IsFacingLeft => facingWest;

        public override string CurrentBodyTexPath =>
            FuneralComp?.IsCoffinOpen == true
                ? "Things/Pawn/FuneralOfDeadButterflies/FuneralOfDeadButterflies_Open"
                : "Things/Pawn/FuneralOfDeadButterflies/FuneralOfDeadButterflies";

        public override Vector2 CurrentBodyDrawSize => new Vector2(2.05f, 2.05f);

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

            CompFuneralOfDeadButterflies comp = FuneralComp;
            AnimationDef desired;

            if (comp == null || !comp.Escaped)
            {
                desired = (comp != null && comp.IsBeingStudiedNow)
                    ? FuneralAnimationDefOf.Funeral_Work
                    : FuneralAnimationDefOf.Funeral_Idle;
            }
            else if (comp.IsCoffinOpen)
            {
                switch (comp.CoffinPhase)
                {
                    case FuneralCoffinPhase.Opening:
                        desired = FuneralAnimationDefOf.Funeral_CoffinOpen;
                        break;
                    case FuneralCoffinPhase.Closing:
                        desired = FuneralAnimationDefOf.Funeral_CoffinClose;
                        break;
                    case FuneralCoffinPhase.Loop:
                    default:
                        desired = FuneralAnimationDefOf.Funeral_CoffinLoop;
                        break;
                }
            }
            else if (comp.IsPlayingAttackAnim)
            {
                desired = FuneralAnimationDefOf.Funeral_Attack;
            }
            else
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? FuneralAnimationDefOf.Funeral_Walk
                    : FuneralAnimationDefOf.Funeral_SideIdle;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override void TickHolded()
        {
            base.TickHolded();
            facingWest = false;
            UpdateAnimation();
        }

        protected override void Tick()
        {
            CompFuneralOfDeadButterflies comp = FuneralComp;
            if (comp != null && comp.Escaped && Spawned && !Dead)
            {
                UpdateHorizontalFacing();
            }
            else if (comp != null && !comp.Escaped)
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
            Scribe_Values.Look(ref facingWest, "fdbFacingWest", defaultValue: false);
        }
    }

    public class JobGiver_ButterflyCombat : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompFuneralOfDeadButterflies comp = pawn.TryGetComp<CompFuneralOfDeadButterflies>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsCoffinOpen || comp.IsPlayingAttackAnim)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            float dist = pawn.Position.DistanceTo(target.Position);
            if (dist <= 9f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 45);
            }

            Verse.AI.Job approach = JobMaker.MakeJob(JobDefOf.Goto, target);
            approach.expiryInterval = 60;
            approach.checkOverrideOnExpire = true;
            approach.canBashDoors = true;
            return approach;
        }
    }

    /// <summary>圣宣 WAW 级双枪专属伤害处理器：同时结算白伤与黑伤双重判定。</summary>
    public class DamageWorker_SolemnLament : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (victim != null && !victim.Destroyed && !(victim is Pawn vp && vp.Dead))
            {
                float secondary = Mathf.Max(4f, dinfo.Amount);
                DamageInfo blackHit = new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                    secondary,
                    dinfo.ArmorPenetrationInt,
                    dinfo.Angle,
                    dinfo.Instigator,
                    weapon: dinfo.Weapon);
                victim.TakeDamage(blackHit);
            }

            return result;
        }
    }
}
