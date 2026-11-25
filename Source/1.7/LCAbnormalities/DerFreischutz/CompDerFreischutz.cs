using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.DerFreischutz
{
    [DefOf]
    public static class DerFreischutzDefOf
    {
        public static ThingDef MagicBullet_Request;
        public static ThingDef LC_MagicBulletBeamFX;
        public static ThingDef LC_MagicBulletPortalFX;
        public static ThingDef LC_MagicBulletHitFX;
        public static ThingDef LC_MagicBulletSkillRunnerFX;

        public static SoundDef LC_DerFreischutz_Reload;
        public static SoundDef LC_DerFreischutz_Cocking;
        public static SoundDef LC_DerFreischutz_Create;
        public static SoundDef LC_DerFreischutz_CursedCreate;
        public static SoundDef LC_DerFreischutz_WeaponCharge;
        public static SoundDef LC_DerFreischutz_Portal;
        public static SoundDef LC_DerFreischutz_Shoot;
        public static SoundDef LC_DerFreischutz_Shot;
        public static SoundDef LC_DerFreischutz_Bullet;
        public static SoundDef LC_DerFreischutz_PierceHit;

        public static AnimationDef DerFreischutz_Idle;
        public static AnimationDef DerFreischutz_Work;
        public static AnimationDef DerFreischutz_Ready;
        public static AnimationDef DerFreischutz_AimHold;
        public static AnimationDef DerFreischutz_Shoot;
        public static AnimationDef DerFreischutz_End;

        static DerFreischutzDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DerFreischutzDefOf));
        }
    }

    public enum DerFreischutzAnimPhase
    {
        Idle,
        Ready,
        Shoot,
        End
    }

    public class CompProperties_DerFreischutz : CompProperties_LCAbnormality
    {
        public int requestShotCost = 10;
        public int readyDurationTicks = 50;
        public int portalCreateTick = 32;
        public int shootDurationTicks = 40;
        public int shootFireTick = 6;
        public int endDurationTicks = 30;

        public CompProperties_DerFreischutz()
        {
            compClass = typeof(CompDerFreischutz);
        }
    }

    /// <summary>
    /// F-01-69 魔弹射手核心逻辑：从不越狱但可通过 10 PE-Box 请托其发射穿透全图敌我的魔弹；
    /// 完整复刻脑叶公司举枪（Ready）→ 枪口展开符文传送门（create）→ 后坐力击发贯穿全图魔弹（Shoot/Attack）→ 收枪（End）演出，
    /// 第 7 发魔弹必定扭曲瞄准随机殖民者，熔毁时亦会走火穿透设施。
    /// </summary>
    public class CompDerFreischutz : CompLCAbnormality
    {
        private int shotsFired;
        private DerFreischutzAnimPhase animPhase = DerFreischutzAnimPhase.Idle;
        private int phaseTicksElapsed;
        private IntVec3 pendingTargetCell = IntVec3.Invalid;
        private Thing pendingTargetThing;
        private bool pendingShotFired;
        private bool pendingPortalSpawned;
        private bool pendingIsCursedSeventh;
        private bool isBeingStudied;
        private bool aimingLeft;

        // Non-saved runtime portal reference during Ready -> Shoot
        private Thing_MagicBulletPortal activeMuzzlePortal;

        public CompProperties_DerFreischutz PropsFreischutz => (CompProperties_DerFreischutz)props;

        public int ShotsFired => shotsFired;

        public bool IsAiming => animPhase != DerFreischutzAnimPhase.Idle;

        public bool IsBeingStudied => isBeingStudied;

        public bool IsAimingLeft => IsAiming && aimingLeft;

        public DerFreischutzAnimPhase CurrentPhase => animPhase;

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            isBeingStudied = false;
            (parent as LC_DerFreischutzPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            isBeingStudied = true;
            (parent as LC_DerFreischutzPawn)?.UpdateAnimation();
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            isBeingStudied = false;
            (parent as LC_DerFreischutzPawn)?.UpdateAnimation();

            if (studier == null || parent == null || parent.Destroyed)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status != null && status.GetPawnStatusELevel(EPawnStatus.Justice) < EPawnLevel.III)
            {
                QliphothCountCurrent--;
                Messages.Message("LC_DerFreischutz_LowJusticeMsg".Translate(studier.LabelShortCap), parent, MessageTypeDefOf.NegativeEvent, historical: false);
            }
        }

        public override void TickContained()
        {
            base.TickContained();
            TickFiringSequence();
            (parent as LC_DerFreischutzPawn)?.UpdateAnimation();
        }

        private void TickFiringSequence()
        {
            if (animPhase == DerFreischutzAnimPhase.Idle)
            {
                return;
            }

            phaseTicksElapsed++;
            CompProperties_DerFreischutz p = PropsFreischutz;

            switch (animPhase)
            {
                case DerFreischutzAnimPhase.Ready:
                    // At t=0.5333s (tick 32), Spine "create" event opens the glowing blue runic muzzle portal!
                    if (!pendingPortalSpawned && phaseTicksElapsed >= p.portalCreateTick)
                    {
                        pendingPortalSpawned = true;
                        SpawnMuzzlePortalForPendingShot();
                    }

                    if (phaseTicksElapsed >= p.readyDurationTicks)
                    {
                        animPhase = DerFreischutzAnimPhase.Shoot;
                        phaseTicksElapsed = 0;
                        pendingShotFired = false;
                        (parent as LC_DerFreischutzPawn)?.UpdateAnimation(force: true);
                    }
                    break;

                case DerFreischutzAnimPhase.Shoot:
                    // At t=0.10s (tick 6), Spine "Attack" event unleashes the map-spanning Magic Bullet!
                    if (!pendingShotFired && phaseTicksElapsed >= p.shootFireTick)
                    {
                        pendingShotFired = true;
                        ExecutePendingPiercingShot();
                    }

                    if (phaseTicksElapsed >= p.shootDurationTicks)
                    {
                        animPhase = DerFreischutzAnimPhase.End;
                        phaseTicksElapsed = 0;
                        (parent as LC_DerFreischutzPawn)?.UpdateAnimation(force: true);
                    }
                    break;

                case DerFreischutzAnimPhase.End:
                    if (phaseTicksElapsed == 8 && DerFreischutzDefOf.LC_DerFreischutz_Cocking != null && parent?.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                    {
                        DerFreischutzDefOf.LC_DerFreischutz_Cocking.PlayOneShot(new TargetInfo(parent.PositionHeld, parent.MapHeld));
                    }

                    if (phaseTicksElapsed >= p.endDurationTicks)
                    {
                        ResetFiringSequence();
                    }
                    break;
            }
        }

        private void ResetFiringSequence()
        {
            animPhase = DerFreischutzAnimPhase.Idle;
            phaseTicksElapsed = 0;
            pendingTargetCell = IntVec3.Invalid;
            pendingTargetThing = null;
            pendingShotFired = false;
            pendingPortalSpawned = false;
            pendingIsCursedSeventh = false;
            aimingLeft = false;
            if (activeMuzzlePortal != null && !activeMuzzlePortal.Destroyed)
            {
                activeMuzzlePortal.CancelEarly();
            }
            activeMuzzlePortal = null;
            (parent as LC_DerFreischutzPawn)?.UpdateAnimation(force: true);
        }

        protected override void QliphothMeltdown()
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            Map map = parent.MapHeld;
            IntVec3 origin = parent.PositionHeld;
            if (map != null && origin.IsValid)
            {
                Pawn rogueTarget = map.mapPawns?.FreeColonistsSpawned?.RandomElementWithFallback();
                IntVec3 cell = rogueTarget != null && rogueTarget.Spawned ? rogueTarget.Position : map.Center;
                if (cell == origin)
                {
                    cell = origin + IntVec3.East;
                }

                BeginFiringSequence(cell, rogueTarget, cursedSeventh: true);
                Find.LetterStack.ReceiveLetter(
                    "LC_DerFreischutz_MeltdownLabel".Translate(),
                    "LC_DerFreischutz_MeltdownText".Translate(),
                    LetterDefOf.ThreatBig,
                    new LookTargets(origin, map));
            }

            QliphothCountCurrent = QliphothCountMax;
        }

        public bool RequestMagicBulletAt(IntVec3 targetCell)
        {
            return RequestMagicBulletAtTarget(new LocalTargetInfo(targetCell));
        }

        public bool RequestMagicBulletAtTarget(LocalTargetInfo target)
        {
            if (parent == null || parent.Destroyed || PeBoxComp == null || parent.MapHeld == null)
            {
                return false;
            }

            int cost = PropsFreischutz.requestShotCost;
            if (PeBoxComp.CurAmountIndiPebox < cost)
            {
                return false;
            }

            PeBoxComp.CurAmountIndiPebox -= cost;
            shotsFired++;

            IntVec3 actualTarget = target.Cell;
            Thing actualTargetThing = target.HasThing && target.Thing != parent ? target.Thing : null;
            bool isSeventh = false;
            if (shotsFired >= 7)
            {
                shotsFired = 0;
                isSeventh = true;
                Pawn betrayedColonist = parent.MapHeld.mapPawns?.FreeColonistsSpawned?.RandomElementWithFallback();
                if (betrayedColonist != null && betrayedColonist.Spawned)
                {
                    actualTarget = betrayedColonist.Position;
                    actualTargetThing = betrayedColonist;
                    Messages.Message(
                        "LC_DerFreischutz_SeventhShotMsg".Translate(betrayedColonist.LabelShortCap),
                        betrayedColonist,
                        MessageTypeDefOf.ThreatBig,
                        historical: false);
                }
            }

            if (actualTarget == parent.PositionHeld)
            {
                actualTarget = parent.PositionHeld + IntVec3.East;
                actualTargetThing = null;
            }

            BeginFiringSequence(actualTarget, actualTargetThing, isSeventh);
            return true;
        }

        private void BeginFiringSequence(IntVec3 targetCell, Thing targetThing, bool cursedSeventh)
        {
            if (activeMuzzlePortal != null && !activeMuzzlePortal.Destroyed)
            {
                activeMuzzlePortal.CancelEarly();
                activeMuzzlePortal = null;
            }

            IntVec3 origin = parent?.PositionHeld ?? IntVec3.Invalid;
            aimingLeft = origin.IsValid && targetCell.IsValid && targetCell.x < origin.x;

            pendingTargetCell = targetCell;
            pendingTargetThing = targetThing;
            pendingIsCursedSeventh = cursedSeventh;
            pendingShotFired = false;
            pendingPortalSpawned = false;
            animPhase = DerFreischutzAnimPhase.Ready;
            phaseTicksElapsed = 0;

            if (DerFreischutzDefOf.LC_DerFreischutz_Reload != null && parent?.MapHeld != null && origin.InBounds(parent.MapHeld))
            {
                DerFreischutzDefOf.LC_DerFreischutz_Reload.PlayOneShot(new TargetInfo(origin, parent.MapHeld));
            }

            (parent as LC_DerFreischutzPawn)?.UpdateAnimation(force: true);
        }

        private Vector3 ResolvePendingTargetWorldPos(Map map, IntVec3 originCell)
        {
            if (pendingTargetThing != null && !pendingTargetThing.Destroyed && pendingTargetThing.Spawned && pendingTargetThing.Map == map)
            {
                pendingTargetCell = pendingTargetThing.Position;
                return pendingTargetThing.DrawPos.Yto0();
            }

            IntVec3 fallbackCell = pendingTargetCell.IsValid ? pendingTargetCell : (originCell + IntVec3.East);
            return fallbackCell.ToVector3Shifted().Yto0();
        }

        private Vector3 ComputeMuzzleWorldPos(IntVec3 originCell, Vector3 targetVec, out Vector3 exactShotDir)
        {
            Vector3 center = originCell.ToVector3Shifted().Yto0();
            if (parent?.ParentHolder is LCAnomalyCore.Buildings.Building_AbnormalityHoldingPlatform platform)
            {
                center = (platform.DrawPos + platform.PawnDrawOffset).Yto0();
            }
            else if (parent?.ParentHolder is Thing holderThing)
            {
                center = holderThing.DrawPos.Yto0();
            }

            Vector3 roughDelta = (targetVec - center).Yto0();
            float roughDist = roughDelta.magnitude;
            Vector3 roughDir = roughDist > 0.01f
                ? roughDelta / roughDist
                : (aimingLeft ? Vector3.left : Vector3.right);

            // Align muzzle portal right at the outstretched rifle barrel tip (UV ~0.98, 0.61 at formScale 2.2)
            float signX = aimingLeft ? -1f : 1f;
            Vector3 rifleTip = center + new Vector3(signX * 1.05f, 0f, 0.24f);
            Vector3 muzzlePos = Vector3.Lerp(rifleTip, center + roughDir * 1.10f, 0.35f);

            // Keep muzzle strictly behind very close targets so the shot never reverses direction
            if (roughDist < 1.35f)
            {
                float shrink = Mathf.Clamp01((roughDist - 0.15f) / 1.20f);
                muzzlePos = Vector3.Lerp(center, muzzlePos, shrink);
            }

            // Aim directly from the actual muzzle position through targetVec to eliminate any parallel parallax offset
            Vector3 exactDelta = (targetVec - muzzlePos).Yto0();
            exactShotDir = exactDelta.sqrMagnitude > 0.01f ? exactDelta.normalized : roughDir;
            return muzzlePos;
        }

        private void SpawnMuzzlePortalForPendingShot()
        {
            Map map = parent?.MapHeld;
            IntVec3 originCell = parent?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !originCell.InBounds(map) || !pendingTargetCell.IsValid)
            {
                return;
            }

            Vector3 targetVec = ResolvePendingTargetWorldPos(map, originCell);
            aimingLeft = targetVec.x < originCell.ToVector3Shifted().x;

            Vector3 muzzlePos = ComputeMuzzleWorldPos(originCell, targetVec, out Vector3 dir);
            activeMuzzlePortal = MagicBulletVfxUtility.SpawnPortal(map, muzzlePos, dir, 1.15f, charging: true, cursedSeventh: pendingIsCursedSeventh);

            SoundDef createSound = pendingIsCursedSeventh
                ? (DerFreischutzDefOf.LC_DerFreischutz_CursedCreate ?? DerFreischutzDefOf.LC_DerFreischutz_Create)
                : DerFreischutzDefOf.LC_DerFreischutz_Create;
            if (createSound != null)
            {
                createSound.PlayOneShot(new TargetInfo(originCell, map));
            }
            if (DerFreischutzDefOf.LC_DerFreischutz_Portal != null)
            {
                DerFreischutzDefOf.LC_DerFreischutz_Portal.PlayOneShot(new TargetInfo(originCell, map));
            }
        }

        private void ExecutePendingPiercingShot()
        {
            Map map = parent?.MapHeld;
            IntVec3 originCell = parent?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !originCell.InBounds(map) || DerFreischutzDefOf.MagicBullet_Request == null)
            {
                return;
            }

            Vector3 targetVec = ResolvePendingTargetWorldPos(map, originCell);
            aimingLeft = targetVec.x < originCell.ToVector3Shifted().x;

            Vector3 start = ComputeMuzzleWorldPos(originCell, targetVec, out Vector3 dir);
            Vector3 edgePos = Projectile_PiercingMagicBulletBase.ComputeMapBoundedEndPoint(map, start, dir, maxRange: -1f);
            IntVec3 destCell = edgePos.ToIntVec3().ClampInsideMap(map);

            if (activeMuzzlePortal != null && !activeMuzzlePortal.Destroyed)
            {
                activeMuzzlePortal.TriggerFireBurst();
                activeMuzzlePortal = null;
            }
            else
            {
                MagicBulletVfxUtility.SpawnPortal(map, start, dir, 1.15f, charging: false, cursedSeventh: pendingIsCursedSeventh);
            }

            if (DerFreischutzDefOf.LC_DerFreischutz_Shoot != null)
            {
                DerFreischutzDefOf.LC_DerFreischutz_Shoot.PlayOneShot(new TargetInfo(originCell, map));
            }

            Color smokeCol = pendingIsCursedSeventh
                ? new Color(0.28f, 0.14f, 0.52f)
                : new Color(0.12f, 0.28f, 0.58f);
            FleckMaker.ThrowDustPuffThick(start, map, 0.85f, smokeCol);

            LocalTargetInfo intended = pendingTargetThing != null && !pendingTargetThing.Destroyed && pendingTargetThing.Spawned && pendingTargetThing.Map == map
                ? new LocalTargetInfo(pendingTargetThing)
                : new LocalTargetInfo(pendingTargetCell.IsValid ? pendingTargetCell : destCell);

            Projectile_MagicBullet proj = (Projectile_MagicBullet)GenSpawn.Spawn(DerFreischutzDefOf.MagicBullet_Request, originCell, map);
            proj.ConfigureShotVisuals(isCursedSeventh: pendingIsCursedSeventh, beamWidth: 0.72f, spawnOriginPortal: false);
            proj.SetExactDestination(edgePos);
            proj.Launch(parent, start, destCell, intended, ProjectileHitFlags.None);
        }

        public override IEnumerable<Gizmo> GetPlatformGizmos()
        {
            if (parent == null || parent.Destroyed || parent.MapHeld == null)
            {
                yield break;
            }

            int cost = PropsFreischutz.requestShotCost;
            int available = PeBoxComp?.CurAmountIndiPebox ?? 0;

            Command_Action reqCmd = new Command_Action
            {
                defaultLabel = "LC_DerFreischutz_RequestShotLabel".Translate(cost),
                defaultDesc = "LC_DerFreischutz_RequestShotDesc".Translate(cost, shotsFired),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/DerFreischutz", reportFailure: false) ?? BaseContent.BadTex,
                action = () =>
                {
                    if (DerFreischutzDefOf.LC_DerFreischutz_Cocking != null && parent.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                    {
                        DerFreischutzDefOf.LC_DerFreischutz_Cocking.PlayOneShot(new TargetInfo(parent.PositionHeld, parent.MapHeld));
                    }
                    TargetingParameters tp = new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetPawns = true,
                        canTargetBuildings = true,
                        canTargetSelf = false
                    };
                    Find.Targeter.BeginTargeting(tp, target => RequestMagicBulletAtTarget(target));
                }
            };

            if (available < cost)
            {
                reqCmd.Disable("LC_DerFreischutz_NotEnoughPeBox".Translate(cost, available));
            }

            yield return reqCmd;
        }

        public override string GetPlatformInspectString()
        {
            return "LC_DerFreischutz_InspectCounter".Translate(shotsFired);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shotsFired, "dfShotsFired", 0);
            Scribe_Values.Look(ref animPhase, "dfAnimPhase", DerFreischutzAnimPhase.Idle);
            Scribe_Values.Look(ref phaseTicksElapsed, "dfPhaseTicksElapsed", 0);
            Scribe_Values.Look(ref pendingTargetCell, "dfPendingTargetCell", IntVec3.Invalid);
            Scribe_References.Look(ref pendingTargetThing, "dfPendingTargetThing");
            Scribe_Values.Look(ref pendingShotFired, "dfPendingShotFired", false);
            Scribe_Values.Look(ref pendingPortalSpawned, "dfPendingPortalSpawned", false);
            Scribe_Values.Look(ref pendingIsCursedSeventh, "dfPendingIsCursedSeventh", false);
            Scribe_Values.Look(ref isBeingStudied, "dfIsBeingStudied", false);
            Scribe_Values.Look(ref aimingLeft, "dfAimingLeft", false);
        }
    }

    public class LC_DerFreischutzPawn : LC_StandardEntityPawn
    {
        public CompDerFreischutz FreischutzComp => EntityComp as CompDerFreischutz;

        public bool IsFacingLeft => FreischutzComp?.IsAimingLeft == true;

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

            CompDerFreischutz comp = FreischutzComp;
            AnimationDef desired = DerFreischutzDefOf.DerFreischutz_Idle;
            if (comp != null)
            {
                switch (comp.CurrentPhase)
                {
                    case DerFreischutzAnimPhase.Ready:
                        desired = DerFreischutzDefOf.DerFreischutz_Ready;
                        break;
                    case DerFreischutzAnimPhase.Shoot:
                        desired = DerFreischutzDefOf.DerFreischutz_Shoot;
                        break;
                    case DerFreischutzAnimPhase.End:
                        desired = DerFreischutzDefOf.DerFreischutz_End;
                        break;
                    case DerFreischutzAnimPhase.Idle:
                    default:
                        desired = comp.IsBeingStudied
                            ? DerFreischutzDefOf.DerFreischutz_Work
                            : DerFreischutzDefOf.DerFreischutz_Idle;
                        break;
                }
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }
    }

    /// <summary>
    /// 魔弹通用直线全穿投射物基类：强制关闭单次命中拦截，隐藏裸子弹贴图并由自定义 GLSL 贯穿光束与双层符文传送门驱动视觉表现，
    /// 沿 RimWorld 1.7 TickInterval 轨迹清扫沿途所有存活单位（敌我不分），命中单位时触发蓝黑贯穿爆裂特效。
    /// </summary>
    public class Projectile_PiercingMagicBulletBase : Projectile
    {
        private readonly HashSet<int> hitIds = new HashSet<int>();
        private Thing_MagicBulletBeam linkedBeam;
        private Vector3 shotDir = Vector3.right;
        private bool playedBulletResonance;
        private bool hasExactDestinationOverride;
        private Vector3 exactDestinationOverride;
        protected bool isCursedSeventhShot;
        protected float configuredBeamWidth = 0.55f;
        protected bool spawnOriginPortalOnLaunch = true;

        public void ConfigureShotVisuals(bool isCursedSeventh, float beamWidth, bool spawnOriginPortal)
        {
            isCursedSeventhShot = isCursedSeventh;
            configuredBeamWidth = beamWidth;
            spawnOriginPortalOnLaunch = spawnOriginPortal;
        }

        public void SetExactDestination(Vector3 exactDest)
        {
            exactDestinationOverride = exactDest.Yto0();
            hasExactDestinationOverride = true;
        }

        /// <summary>沿精确方向射线解析求取地图边界（或最大射程）内的无量化浮点终点。</summary>
        public static Vector3 ComputeMapBoundedEndPoint(Map map, Vector3 start, Vector3 dir, float maxRange)
        {
            Vector3 s = start.Yto0();
            Vector3 d = dir.Yto0();
            if (d.sqrMagnitude < 1e-6f)
            {
                d = Vector3.right;
            }
            else
            {
                d.Normalize();
            }

            float minX = 0.08f;
            float maxX = Mathf.Max(minX, map.Size.x - 0.08f);
            float minZ = 0.08f;
            float maxZ = Mathf.Max(minZ, map.Size.z - 0.08f);

            float tMax = maxRange > 0f ? maxRange : (map.Size.x + map.Size.z + 16f);
            if (d.x > 1e-5f)
            {
                tMax = Mathf.Min(tMax, (maxX - s.x) / d.x);
            }
            else if (d.x < -1e-5f)
            {
                tMax = Mathf.Min(tMax, (minX - s.x) / d.x);
            }

            if (d.z > 1e-5f)
            {
                tMax = Mathf.Min(tMax, (maxZ - s.z) / d.z);
            }
            else if (d.z < -1e-5f)
            {
                tMax = Mathf.Min(tMax, (minZ - s.z) / d.z);
            }

            tMax = Mathf.Max(0.25f, tMax);
            Vector3 end = s + d * tMax;
            end.x = Mathf.Clamp(end.x, minX, maxX);
            end.z = Mathf.Clamp(end.z, minZ, maxZ);
            end.y = 0f;
            return end;
        }

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
            hitIds.Clear();
            playedBulletResonance = false;
            base.Launch(launcher, origin, usedTarget, intendedTarget, ProjectileHitFlags.None, false, equipment, targetCoverDef);

            // Eliminate vanilla Projectile.Launch's Gen.RandomHorizontalVector(0.3f) scatter and IntVec3 truncation
            destination = hasExactDestinationOverride
                ? exactDestinationOverride
                : usedTarget.CenterVector3.Yto0();
            ticksToImpact = Mathf.Max(1, Mathf.CeilToInt(StartingTicksToImpact));
            lifetime = ticksToImpact;

            Map map = Map ?? launcher?.MapHeld;
            if (map != null)
            {
                Vector3 delta = (destination - this.origin).Yto0();
                shotDir = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.right;
                float speed = def?.projectile?.speed ?? 180f;

                if (spawnOriginPortalOnLaunch)
                {
                    MagicBulletVfxUtility.SpawnPortal(map, this.origin + shotDir * 0.40f, shotDir, configuredBeamWidth * 1.45f, charging: false, cursedSeventh: isCursedSeventhShot);
                }

                linkedBeam = MagicBulletVfxUtility.SpawnBeam(map, this.origin, destination, configuredBeamWidth, speed, isCursedSeventhShot);
            }
        }

        /// <summary>禁用默认裸子弹贴图绘制，改由自定义着色器贯穿光束与弹头锋芒统一呈现。</summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // Intentionally handled by Thing_MagicBulletBeam for authentic Lobotomy Corp piercing lance VFX.
        }

        protected override void TickInterval(int delta)
        {
            Vector3 prev = ExactPosition;
            Map map = Map;

            base.TickInterval(delta);

            if (linkedBeam != null && !linkedBeam.Destroyed)
            {
                linkedBeam.SyncHeadPosition(!Destroyed ? ExactPosition : destination);
            }

            if (map == null || !prev.InBounds(map))
            {
                return;
            }

            Vector3 curr = !Destroyed ? ExactPosition : destination;
            SweepSegment(map, prev, curr);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            if (linkedBeam != null && !linkedBeam.Destroyed)
            {
                linkedBeam.NotifyReachedEnd();
            }

            if (map != null && Position.InBounds(map) && !blockedByShield)
            {
                SweepCell(map, Position);
                Color dustCol = isCursedSeventhShot
                    ? new Color(0.28f, 0.14f, 0.52f)
                    : new Color(0.12f, 0.28f, 0.56f);
                FleckMaker.ThrowDustPuffThick(ExactPosition, map, 0.60f, dustCol);
            }
            base.Impact(hitThing, blockedByShield);
        }

        private void SweepSegment(Map map, Vector3 p0, Vector3 p1)
        {
            Vector3 a = p0.Yto0();
            Vector3 b = (p1.InBounds(map) ? p1 : p0).Yto0();
            Vector3 seg = b - a;
            float len = seg.magnitude;

            IntVec3 lastCell = IntVec3.Invalid;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.35f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                IntVec3 cell = (a + seg * t).ToIntVec3().ClampInsideMap(map);
                if (cell != lastCell)
                {
                    lastCell = cell;
                    SweepCell(map, cell);
                }
            }

            // Ensure a moving intended Pawn whose visual DrawPos crosses the exact beam never slips between Position & DrawPos
            if (intendedTarget.HasThing && intendedTarget.Thing is Pawn intendedPawn &&
                intendedPawn != launcher && intendedPawn.Spawned && !intendedPawn.Dead &&
                !hitIds.Contains(intendedPawn.thingIDNumber))
            {
                Vector3 pawnPos = intendedPawn.DrawPos.Yto0();
                float projT = len > 1e-4f ? Mathf.Clamp01(Vector3.Dot(pawnPos - a, seg) / (len * len)) : 0f;
                Vector3 closest = a + seg * projT;
                if ((pawnPos - closest).sqrMagnitude <= 0.65f * 0.65f)
                {
                    ApplyPiercingHit(map, intendedPawn, intendedPawn.Position.ClampInsideMap(map));
                }
            }
        }

        private void SweepCell(Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            if (things == null || things.Count == 0)
            {
                return;
            }

            List<Pawn> victims = null;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn p && p != launcher && !p.Dead && !hitIds.Contains(p.thingIDNumber))
                {
                    victims ??= new List<Pawn>();
                    victims.Add(p);
                }
            }

            if (victims == null)
            {
                return;
            }

            for (int i = 0; i < victims.Count; i++)
            {
                Pawn target = victims[i];
                if (target.Destroyed || target.Dead)
                {
                    continue;
                }

                ApplyPiercingHit(map, target, cell);
            }
        }

        private void ApplyPiercingHit(Map map, Pawn target, IntVec3 cell)
        {
            if (target == null || target.Destroyed || target.Dead || !hitIds.Add(target.thingIDNumber))
            {
                return;
            }

            DamageDef damDef = DamageDef ?? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            TargetInfo hitLoc = new TargetInfo(cell, map);
            if (DerFreischutzDefOf.LC_DerFreischutz_PierceHit != null)
            {
                DerFreischutzDefOf.LC_DerFreischutz_PierceHit.PlayOneShot(hitLoc);
            }
            if (!playedBulletResonance && DerFreischutzDefOf.LC_DerFreischutz_Bullet != null)
            {
                playedBulletResonance = true;
                DerFreischutzDefOf.LC_DerFreischutz_Bullet.PlayOneShot(hitLoc);
            }

            MagicBulletVfxUtility.SpawnHitBurst(map, target.DrawPos, shotDir, configuredBeamWidth * 0.92f, isCursedSeventhShot);
            target.TakeDamage(new DamageInfo(damDef, DamageAmount, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, weapon: equipmentDef));
        }
    }

    public class Projectile_MagicBullet : Projectile_PiercingMagicBulletBase
    {
        public Projectile_MagicBullet()
        {
            configuredBeamWidth = 0.72f;
        }
    }

    public class Projectile_EgoMagicBullet : Projectile_PiercingMagicBulletBase
    {
        public Projectile_EgoMagicBullet()
        {
            configuredBeamWidth = 0.55f;
            spawnOriginPortalOnLaunch = true;
        }

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
            Map map = launcher?.MapHeld;
            LocalTargetInfo aimTarget = intendedTarget.IsValid ? intendedTarget : usedTarget;
            LocalTargetInfo finalTarget = aimTarget;
            Vector3 launchOrigin = origin.Yto0();

            if (map != null)
            {
                Vector3 aimPos = (aimTarget.HasThing && aimTarget.Thing.Spawned
                    ? aimTarget.Thing.DrawPos
                    : aimTarget.CenterVector3).Yto0();
                Vector3 delta = aimPos - launchOrigin;
                float distToAim = delta.magnitude;
                if (distToAim > 0.01f)
                {
                    Vector3 dir = delta / distToAim;
                    // Start the piercing lance cleanly from the rifle muzzle while staying strictly behind close targets
                    float muzzleOffset = Mathf.Min(0.72f, distToAim * 0.45f);
                    launchOrigin += dir * muzzleOffset;

                    float maxRange = equipment?.def?.Verbs?[0]?.range ?? equipmentDef?.Verbs?[0]?.range ?? 44f;
                    Vector3 exactEnd = ComputeMapBoundedEndPoint(map, launchOrigin, dir, maxRange);
                    SetExactDestination(exactEnd);
                    finalTarget = exactEnd.ToIntVec3().ClampInsideMap(map);
                }
            }

            // Portal was already spawned and burst-triggered by Verb_ShootMagicBullet; spawn instant portal only if fired by another verb
            if (launcher is Pawn p && p.equipment?.PrimaryEq?.PrimaryVerb is Verb_ShootMagicBullet)
            {
                spawnOriginPortalOnLaunch = false;
            }

            base.Launch(launcher, launchOrigin, finalTarget, aimTarget, ProjectileHitFlags.None, false, equipment, targetCoverDef);
        }
    }
}
