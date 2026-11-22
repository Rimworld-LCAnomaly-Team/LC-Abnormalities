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
        public static SoundDef LC_ScorchedGirl_Escape;
        public static SoundDef LC_ScorchedGirl_Fuse;
        public static SoundDef LC_ScorchedGirl_Explode;
        public static SoundDef LC_ScorchedGirl_SmallExplode;

        static ScorchedGirlDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ScorchedGirlDefOf));
        }
    }

    public class CompProperties_ScorchedGirl : CompProperties_LCAbnormality
    {
        public int fuseDurationTicks = 1200;
        public float explosionRadius = 4.2f;
        public int explosionDamage = 38;
        public float detonateDistance = 1.8f;

        public CompProperties_ScorchedGirl()
        {
            compClass = typeof(CompScorchedGirl);
        }
    }

    /// <summary>
    /// F-01-02 焦化少女核心逻辑：良评估 50%/差评估 100% 降计数；突破后点燃导火索全速冲向人类，贴近或满 20 秒引爆高额红伤自毁化蛋；若在引爆前被击杀则静默化蛋。
    /// </summary>
    public class CompScorchedGirl : CompLCAbnormality
    {
        private int fuseTicksLeft;
        private bool detonated;
        private Sustainer fuseSustainer;

        public CompProperties_ScorchedGirl PropsScorched => (CompProperties_ScorchedGirl)props;

        public int FuseTicksLeft => fuseTicksLeft;

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (!Escaped && parent != null && !parent.Destroyed && Rand.Chance(0.5f))
            {
                QliphothCountCurrent--;
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            fuseTicksLeft = PropsScorched.fuseDurationTicks;
            detonated = false;
            LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Escape, parent);
            TryStartFuseSustainer();
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            EndFuseSustainer();
            fuseTicksLeft = 0;
            detonated = false;
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || detonated || parent == null || parent.Destroyed || !parent.Spawned)
            {
                EndFuseSustainer();
                return;
            }

            if (fuseSustainer == null || fuseSustainer.Ended)
            {
                TryStartFuseSustainer();
            }
            fuseSustainer?.Maintain();

            fuseTicksLeft--;
            Pawn closeTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position, PropsScorched.detonateDistance);
            if (fuseTicksLeft <= 0 || closeTarget != null)
            {
                DetonateAndReturnToEgg();
            }
        }

        public void DetonateAndReturnToEgg()
        {
            if (detonated || parent == null || parent.Destroyed)
            {
                return;
            }

            detonated = true;
            EndFuseSustainer();

            Map map = parent.MapHeld;
            IntVec3 pos = parent.PositionHeld;
            int sig = biosignature;

            if (map != null && pos.InBounds(map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(ScorchedGirlDefOf.LC_ScorchedGirl_Explode, parent);
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
            EndFuseSustainer();
            if (!detonated)
            {
                base.Notify_Killed(prevMap, dinfo);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            EndFuseSustainer();
            base.PostDeSpawn(map, mode);
        }

        private void TryStartFuseSustainer()
        {
            if (parent != null && parent.Spawned && parent.Map != null && ScorchedGirlDefOf.LC_ScorchedGirl_Fuse != null)
            {
                SoundInfo info = SoundInfo.InMap(parent, MaintenanceType.PerTick);
                fuseSustainer = ScorchedGirlDefOf.LC_ScorchedGirl_Fuse.TrySpawnSustainer(info);
            }
        }

        private void EndFuseSustainer()
        {
            if (fuseSustainer != null && !fuseSustainer.Ended)
            {
                fuseSustainer.End();
            }
            fuseSustainer = null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref fuseTicksLeft, "sgFuseTicksLeft", 0);
            Scribe_Values.Look(ref detonated, "sgDetonated", false);
        }
    }

    public class LC_ScorchedGirlPawn : LC_StandardEntityPawn
    {
        public override string CurrentBodyTexPath =>
            LCComp?.Escaped == true
                ? "Things/Pawn/ScorchedGirl/ScorchedGirl_Ignited"
                : "Things/Pawn/ScorchedGirl/ScorchedGirl";

        public override Vector2 CurrentBodyDrawSize => new Vector2(1.75f, 1.75f);
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

    /// <summary>第四把火 E.G.O 火炮投射物：撞击落点引爆 2.4 格红伤爆炸并小概率起火。</summary>
    public class Projectile_FourthMatchFlame : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            Thing inst = launcher;

            if (map != null && pos.InBounds(map) && !blockedByShield)
            {
                if (ScorchedGirlDefOf.LC_ScorchedGirl_SmallExplode != null)
                {
                    ScorchedGirlDefOf.LC_ScorchedGirl_SmallExplode.PlayOneShot(new TargetInfo(pos, map));
                }

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
