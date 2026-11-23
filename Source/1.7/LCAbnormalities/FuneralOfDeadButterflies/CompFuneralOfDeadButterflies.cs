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
        public static ThingDef LC_SolemnLamentButterflyBloom;
        public static ThingDef LC_SolemnLamentButterflyFlutter;

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
        public float salvoRange = 18f;

        public CompProperties_FuneralOfDeadButterflies()
        {
            compClass = typeof(CompFuneralOfDeadButterflies);
        }
    }

    /// <summary>
    /// T-01-68 亡蝶葬仪核心逻辑：勇气高于 III 或正义低于 III 工作必降计数；
    /// 突破后定期开棺（开盖 -> 群蝶持续喷涌 -> 合盖）释放白黑混合群蝶冲击，并穿插手枪手势远程齐射。
    /// </summary>
    public class CompFuneralOfDeadButterflies : CompLCAbnormality
    {
        private const int CoffinOpenDuration = 40;
        private const int CoffinLoopDuration = 80;
        private const int CoffinCloseDuration = 30;
        private const int AttackAnimDuration = 60;
        private const int AttackFireTickOffset = 20;

        private FuneralCoffinPhase coffinPhase = FuneralCoffinPhase.None;
        private int coffinPhaseTicks;
        private int burstCooldownTicks = 240;
        private int salvoCooldownTicks = 60;
        private int attackAnimTicks;
        private int pendingSalvoFireTicks;
        private Pawn pendingSalvoTarget;
        private int studyingGraceTicks;
        private Sustainer coffinSkillSustainer;

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
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            EndCoffinSkillSustainer();
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            EndCoffinSkillSustainer();
            base.PostDestroy(mode, previousMap);
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
            if (nearest != null)
            {
                selfPawn?.FaceCellHorizontal(nearest.Position);
            }

            if (selfPawn?.pather?.Moving == true)
            {
                selfPawn.pather.StopDead();
            }

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
                    TriggerCoffinBurstDamageAndVfx(selfPawn);
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
            else if (coffinPhase == FuneralCoffinPhase.Loop)
            {
                MaintainCoffinSkillSustainer();
                if (coffinPhaseTicks % 16 == 0 && parent.Map != null)
                {
                    FuneralVfxUtility.SpawnCoffinSwarmWave(
                        parent.Map,
                        parent.DrawPos,
                        selfPawn?.IsFacingLeft ?? false,
                        count: 4);
                }

                if (coffinPhaseTicks <= 0)
                {
                    EndCoffinSkillSustainer();
                    coffinPhase = FuneralCoffinPhase.Closing;
                    coffinPhaseTicks = CoffinCloseDuration;
                    LCAbnormalitiesHelper.PlaySoundAt(FuneralDefOf.LC_Funeral_Close, parent);
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
            else if (coffinPhase == FuneralCoffinPhase.Closing)
            {
                EndCoffinSkillSustainer();
                if (coffinPhaseTicks <= 0)
                {
                    coffinPhase = FuneralCoffinPhase.None;
                    coffinPhaseTicks = 0;
                    selfPawn?.UpdateAnimation(force: true);
                }
            }
        }

        private void TriggerCoffinBurstDamageAndVfx(LC_FuneralOfDeadButterfliesPawn selfPawn)
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            FuneralVfxUtility.SpawnCoffinSwarmWave(map, parent.DrawPos, selfPawn?.IsFacingLeft ?? false, count: 9);

            float radSq = PropsFuneral.burstRadius * PropsFuneral.burstRadius;
            List<Pawn> list = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < list.Count; i++)
            {
                Pawn target = list[i];
                if (target == null || target == parent || target.Dead || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if ((target.Position - pos).LengthHorizontalSquared <= radSq)
                {
                    FuneralVfxUtility.SpawnTargetButterflyBloom(map, target.DrawPos, isBlack: false, scale: 1.15f, flutterCount: 3);
                    FuneralVfxUtility.SpawnTargetButterflyBloom(map, target.DrawPos, isBlack: true, scale: 0.95f, flutterCount: 2);
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 14f, 0.35f, instigator: parent));
                    if (!target.Dead)
                    {
                        target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 14f, 0.35f, instigator: parent));
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
