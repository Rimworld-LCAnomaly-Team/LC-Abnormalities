using LCAbnormalities.Shared;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.PunishingBird
{
    [DefOf]
    public static class PunishingBirdDefOf
    {
        public static ThingDef PunishingBirdEgg;
        public static HediffDef LC_PunishingBirdEnragedBuff;
        public static SoundDef LC_PunishingBird_Hit;
        public static SoundDef LC_PunishingBird_Open;

        static PunishingBirdDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PunishingBirdDefOf));
        }
    }

    public class CompProperties_PunishingBird : CompProperties_LCAbnormality
    {
        public int calmRoamDurationTicks = 2700;
        public int peckIntervalTicks = 90;

        public CompProperties_PunishingBird()
        {
            compClass = typeof(CompPunishingBird);
        }
    }

    /// <summary>O-02-56 惩戒鸟核心逻辑：突破后温和啄击四周，若敢反击则立刻暴怒裂口追猎并用 75~100 红伤巨口处决后化蛋。</summary>
    public class CompPunishingBird : CompLCAbnormality
    {
        private bool isEnraged;
        private int breachTicks;
        private int lastPeckTick = -9999;
        private int postLossReturnTicks;
        private Pawn rageTarget;

        public CompProperties_PunishingBird PropsBird => (CompProperties_PunishingBird)props;

        public bool IsEnraged => isEnraged;

        public Pawn RageTarget => rageTarget;

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            isEnraged = false;
            breachTicks = 0;
            postLossReturnTicks = 0;
            rageTarget = null;
            RemoveEnrageBuff();
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            isEnraged = false;
            breachTicks = 0;
            postLossReturnTicks = 0;
            rageTarget = null;
            RemoveEnrageBuff();
        }

        public void TriggerEnrage(Pawn attacker)
        {
            if (parent == null || parent.Destroyed || !Escaped)
            {
                return;
            }

            if (!isEnraged)
            {
                isEnraged = true;
                postLossReturnTicks = 0;
                LCAbnormalitiesHelper.ApplyOrRefreshHediff(parent as Pawn, PunishingBirdDefOf.LC_PunishingBirdEnragedBuff, 1f);
                LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Open, parent);
                Messages.Message(
                    "LC_PunishingBird_EnragedMsg".Translate(),
                    parent,
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }

            if (attacker != null && !attacker.Dead && attacker.Spawned && attacker.Map == parent.Map)
            {
                rageTarget = attacker;
            }
            else if (rageTarget == null && parent.Map != null)
            {
                rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position);
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || parent == null || parent.Destroyed || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            breachTicks++;
            if (!isEnraged)
            {
                Pawn calmTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position, 1.6f);
                if (calmTarget != null && CanCalmPeckNow())
                {
                    ExecuteCalmPeck(calmTarget);
                }

                if (breachTicks >= PropsBird.calmRoamDurationTicks)
                {
                    ReturnQuietlyToEgg();
                }
                return;
            }

            if (rageTarget == null || rageTarget.Dead || !rageTarget.Spawned || rageTarget.Map != parent.Map)
            {
                rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(parent.Map, parent.Position);
                if (rageTarget == null)
                {
                    postLossReturnTicks++;
                    if (postLossReturnTicks >= 300)
                    {
                        ReturnQuietlyToEgg();
                    }
                    return;
                }
            }

            postLossReturnTicks = 0;
            if (parent.Position.DistanceTo(rageTarget.Position) <= 1.9f)
            {
                ExecuteMawPunishment(rageTarget);
            }
        }

        public bool CanCalmPeckNow()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return now - lastPeckTick >= PropsBird.peckIntervalTicks;
        }

        public void ExecuteCalmPeck(Pawn target)
        {
            if (target == null || target.Dead || parent == null)
            {
                return;
            }

            lastPeckTick = Find.TickManager?.TicksGame ?? 0;
            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Hit, parent);
            target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 2f, instigator: parent));
        }

        public void ExecuteMawPunishment(Pawn target)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(PunishingBirdDefOf.LC_PunishingBird_Open, parent);
            if (parent.Map != null && parent.Position.InBounds(parent.Map))
            {
                AbnormalityUtility.Burst(parent.Position, parent.Map, 1.8f);
            }

            if (target != null && !target.Dead)
            {
                int dmg = Rand.RangeInclusive(75, 100);
                target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, dmg, 0.9f, instigator: parent));
                Messages.Message(
                    "LC_PunishingBird_PunishMsg".Translate(target.LabelShortCap),
                    new LookTargets(target.PositionHeld, target.MapHeld),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }

            ReturnQuietlyToEgg();
        }

        public void ReturnQuietlyToEgg()
        {
            if (parent is Pawn p && !p.Destroyed)
            {
                if (p.MapHeld != null)
                {
                    Messages.Message(
                        "LC_PunishingBird_ReturnMsg".Translate(),
                        new LookTargets(p.PositionHeld, p.MapHeld),
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
                LCAbnormalitiesHelper.ReturnEntityToEgg(p, PunishingBirdDefOf.PunishingBirdEgg, biosignature);
            }
        }

        private void RemoveEnrageBuff()
        {
            if (parent is Pawn p && PunishingBirdDefOf.LC_PunishingBirdEnragedBuff != null)
            {
                Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(PunishingBirdDefOf.LC_PunishingBirdEnragedBuff);
                if (h != null)
                {
                    p.health.RemoveHediff(h);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isEnraged, "pbIsEnraged", false);
            Scribe_Values.Look(ref breachTicks, "pbBreachTicks", 0);
            Scribe_Values.Look(ref lastPeckTick, "pbLastPeckTick", -9999);
            Scribe_Values.Look(ref postLossReturnTicks, "pbPostLossReturnTicks", 0);
            Scribe_References.Look(ref rageTarget, "pbRageTarget");
        }
    }

    public class LC_PunishingBirdPawn : LC_StandardEntityPawn, ILCFourColorDynamicResistance
    {
        public CompPunishingBird BirdComp => EntityComp as CompPunishingBird;

        public override string CurrentBodyTexPath =>
            BirdComp?.IsEnraged == true
                ? "Things/Pawn/PunishingBird/PunishingBird_Enraged"
                : "Things/Pawn/PunishingBird/PunishingBird";

        public override Vector2 CurrentBodyDrawSize =>
            BirdComp?.IsEnraged == true
                ? new Vector2(2.2f, 2.2f)
                : new Vector2(1.25f, 1.25f);

        public bool TryGetDynamicFourColorResistance(FourColorDamageType damageType, out float resistance)
        {
            if (BirdComp?.IsEnraged == true)
            {
                resistance = 0.1f;
                return true;
            }

            resistance = 2.0f;
            return false;
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (!Dead && totalDamageDealt > 0f && BirdComp?.Escaped == true)
            {
                BirdComp.TriggerEnrage(dinfo.Instigator as Pawn);
            }
        }
    }

    /// <summary>惩戒鸟突破后的行为节点：暴怒时全速追逐目标并在贴身处发动巨口处决；温和时随机轻啄四周。</summary>
    public class JobGiver_PunishingBirdPeck : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompPunishingBird comp = (pawn as LC_PunishingBirdPawn)?.BirdComp ?? pawn.TryGetComp<CompPunishingBird>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsEnraged)
            {
                Pawn rageTarget = comp.RageTarget;
                if (rageTarget == null || rageTarget.Dead || !rageTarget.Spawned || rageTarget.Map != pawn.Map)
                {
                    rageTarget = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
                }

                if (rageTarget == null)
                {
                    return null;
                }

                if (pawn.Position.DistanceTo(rageTarget.Position) <= 1.9f)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait, 15);
                }

                Verse.AI.Job chase = JobMaker.MakeJob(JobDefOf.Goto, rageTarget);
                chase.expiryInterval = 30;
                chase.checkOverrideOnExpire = true;
                chase.canBashDoors = true;
                return chase;
            }

            Pawn calmTarget = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 18f);
            if (calmTarget == null)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(calmTarget.Position) <= 1.6f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 45);
            }

            Verse.AI.Job hop = JobMaker.MakeJob(JobDefOf.Goto, calmTarget);
            hop.expiryInterval = 90;
            hop.checkOverrideOnExpire = true;
            return hop;
        }
    }
}
