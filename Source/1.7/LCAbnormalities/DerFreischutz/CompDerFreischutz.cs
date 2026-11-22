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
        public static SoundDef LC_DerFreischutz_Portal;
        public static SoundDef LC_DerFreischutz_Shoot;
        public static SoundDef LC_DerFreischutz_Bullet;

        static DerFreischutzDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DerFreischutzDefOf));
        }
    }

    public class CompProperties_DerFreischutz : CompProperties_LCAbnormality
    {
        public int requestShotCost = 10;
        public int aimingDurationTicks = 90;

        public CompProperties_DerFreischutz()
        {
            compClass = typeof(CompDerFreischutz);
        }
    }

    /// <summary>
    /// F-01-69 魔弹射手核心逻辑：从不越狱但可通过 10 PE-Box 请托其发射穿透全图敌我的魔弹；第 7 发魔弹必定扭曲瞄准随机殖民者，熔毁时亦会走火穿透设施。
    /// </summary>
    public class CompDerFreischutz : CompLCAbnormality
    {
        private int shotsFired;
        private int aimingTicksLeft;

        public CompProperties_DerFreischutz PropsFreischutz => (CompProperties_DerFreischutz)props;

        public int ShotsFired => shotsFired;

        public bool IsAiming => aimingTicksLeft > 0;

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
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
            if (aimingTicksLeft > 0)
            {
                aimingTicksLeft--;
            }
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

                aimingTicksLeft = PropsFreischutz.aimingDurationTicks;
                FirePiercingShot(cell);
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
            aimingTicksLeft = PropsFreischutz.aimingDurationTicks;

            IntVec3 actualTarget = targetCell;
            if (shotsFired >= 7)
            {
                shotsFired = 0;
                Pawn betrayedColonist = parent.MapHeld.mapPawns?.FreeColonistsSpawned?.RandomElementWithFallback();
                if (betrayedColonist != null && betrayedColonist.Spawned)
                {
                    actualTarget = betrayedColonist.Position;
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
            }

            FirePiercingShot(actualTarget);
            return true;
        }

        private void FirePiercingShot(IntVec3 targetCell)
        {
            Map map = parent?.MapHeld;
            IntVec3 originCell = parent?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !originCell.InBounds(map) || DerFreischutzDefOf.MagicBullet_Request == null)
            {
                return;
            }

            Vector3 start = originCell.ToVector3Shifted();
            Vector3 targetVec = targetCell.ToVector3Shifted();
            Vector3 dir = (targetVec - start).Yto0();
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            Vector3 edgePos = start;
            int maxSteps = map.Size.x + map.Size.z + 8;
            for (int i = 1; i <= maxSteps; i++)
            {
                Vector3 next = start + dir * i;
                IntVec3 c = next.ToIntVec3();
                if (!c.InBounds(map))
                {
                    break;
                }
                edgePos = next;
            }

            IntVec3 destCell = edgePos.ToIntVec3().ClampInsideMap(map);
            if (destCell == originCell)
            {
                destCell = targetCell.ClampInsideMap(map);
            }

            if (DerFreischutzDefOf.LC_DerFreischutz_Portal != null)
            {
                DerFreischutzDefOf.LC_DerFreischutz_Portal.PlayOneShot(new TargetInfo(originCell, map));
            }
            if (DerFreischutzDefOf.LC_DerFreischutz_Shoot != null)
            {
                DerFreischutzDefOf.LC_DerFreischutz_Shoot.PlayOneShot(new TargetInfo(originCell, map));
            }
            FleckMaker.ThrowDustPuffThick(start, map, 1.8f, new Color(0.25f, 0.45f, 0.95f));

            Projectile proj = (Projectile)GenSpawn.Spawn(DerFreischutzDefOf.MagicBullet_Request, originCell, map);
            proj.Launch(parent, start, destCell, destCell, ProjectileHitFlags.None);
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
                    TargetingParameters tp = new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetPawns = true,
                        canTargetBuildings = true,
                        canTargetSelf = false
                    };
                    Find.Targeter.BeginTargeting(tp, target => RequestMagicBulletAt(target.Cell));
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
            Scribe_Values.Look(ref aimingTicksLeft, "dfAimingTicksLeft", 0);
        }
    }

    public class LC_DerFreischutzPawn : LC_StandardEntityPawn
    {
        public CompDerFreischutz FreischutzComp => EntityComp as CompDerFreischutz;

        public override string CurrentBodyTexPath =>
            FreischutzComp?.IsAiming == true
                ? "Things/Pawn/DerFreischutz/DerFreischutz_Aiming"
                : "Things/Pawn/DerFreischutz/DerFreischutz";

        public override Vector2 CurrentBodyDrawSize => new Vector2(1.9f, 1.9f);
    }

    /// <summary>
    /// 魔弹通用直线全穿投射物基类：强制关闭单次命中拦截，沿 RimWorld 1.7 TickInterval 轨迹清扫沿途所有存活单位（敌我不分），仅在抵达轨迹终点或出界时销毁。
    /// </summary>
    public class Projectile_PiercingMagicBulletBase : Projectile
    {
        private readonly HashSet<int> hitIds = new HashSet<int>();

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
            base.Launch(launcher, origin, usedTarget, intendedTarget, ProjectileHitFlags.None, false, equipment, targetCoverDef);
        }

        protected override void TickInterval(int delta)
        {
            Vector3 prev = ExactPosition;
            Map map = Map;

            base.TickInterval(delta);

            if (map == null || !prev.InBounds(map))
            {
                return;
            }

            Vector3 curr = !Destroyed ? ExactPosition : destination;
            IntVec3 c0 = prev.ToIntVec3().ClampInsideMap(map);
            IntVec3 c1 = (curr.InBounds(map) ? curr : prev).ToIntVec3().ClampInsideMap(map);

            if (c0 == c1)
            {
                SweepCell(map, c0);
                return;
            }

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(c0, c1))
            {
                if (cell.InBounds(map))
                {
                    SweepCell(map, cell);
                }
            }
            SweepCell(map, c1);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            if (map != null && Position.InBounds(map) && !blockedByShield)
            {
                SweepCell(map, Position);
                FleckMaker.ThrowDustPuffThick(ExactPosition, map, 1.2f, new Color(0.25f, 0.45f, 0.95f));
            }
            base.Impact(hitThing, blockedByShield);
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

            DamageDef damDef = DamageDef ?? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            for (int i = 0; i < victims.Count; i++)
            {
                Pawn target = victims[i];
                if (target.Destroyed || target.Dead)
                {
                    continue;
                }

                hitIds.Add(target.thingIDNumber);
                if (DerFreischutzDefOf.LC_DerFreischutz_Bullet != null)
                {
                    DerFreischutzDefOf.LC_DerFreischutz_Bullet.PlayOneShot(new TargetInfo(cell, map));
                }
                target.TakeDamage(new DamageInfo(damDef, DamageAmount, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, weapon: equipmentDef));
            }
        }
    }

    public class Projectile_MagicBullet : Projectile_PiercingMagicBulletBase
    {
    }

    public class Projectile_EgoMagicBullet : Projectile_PiercingMagicBulletBase
    {
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
            LocalTargetInfo finalTarget = usedTarget;
            if (map != null)
            {
                Vector3 dir = (usedTarget.CenterVector3 - origin).Yto0();
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    Vector3 extended = origin;
                    int steps = Mathf.CeilToInt(equipmentDef?.Verbs?[0]?.range ?? 44f);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector3 next = origin + dir * i;
                        if (!next.InBounds(map))
                        {
                            break;
                        }
                        extended = next;
                    }
                    finalTarget = extended.ToIntVec3().ClampInsideMap(map);
                }
            }

            base.Launch(launcher, origin, finalTarget, finalTarget, ProjectileHitFlags.None, false, equipment, targetCoverDef);
        }
    }
}
