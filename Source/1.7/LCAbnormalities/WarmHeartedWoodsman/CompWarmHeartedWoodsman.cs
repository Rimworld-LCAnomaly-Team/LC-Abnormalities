using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.DamageWorker;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.WarmHeartedWoodsman
{
    [DefOf]
    public static class WarmHeartedWoodsmanDefOf
    {
        public static ThingDef WarmHeartedWoodsmanEgg;
        public static ThingDef LC_WoodsmanShutterFX;
        public static ThingDef LC_WoodsmanFurnaceAuraFX;
        public static ThingDef LC_LoggingSkillRunnerFX;

        public static SoundDef LC_Woodsman_Default;
        public static SoundDef LC_Woodsman_Escape;
        public static SoundDef LC_Woodsman_Atk;
        public static SoundDef LC_Woodsman_Special;
        public static SoundDef LC_Woodsman_Dead;
        public static SoundDef LC_Woodsman_AxeHit;

        public static AnimationDef Woodsman_Room_Idle;
        public static AnimationDef Woodsman_Room_Awaken;
        public static AnimationDef Woodsman_Escape_Idle;
        public static AnimationDef Woodsman_Escape_Walk;
        public static AnimationDef Woodsman_Attack_Chop;
        public static AnimationDef Woodsman_Attack_Frenzy;
        public static AnimationDef Woodsman_EatHeart;
        public static AnimationDef Woodsman_Dead;

        static WarmHeartedWoodsmanDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WarmHeartedWoodsmanDefOf));
        }
    }

    public class CompProperties_WarmHeartedWoodsman : CompProperties_LCAbnormality
    {
        public float healOnEatCorpse = 95f;
        public float chopMinDmg = 20f;
        public float chopMaxDmg = 28f;
        public int chopAnimTicks = 56;
        public int eatHeartTicks = 110;
        public int awakenTicks = 40;

        public CompProperties_WarmHeartedWoodsman()
        {
            compClass = typeof(CompWarmHeartedWoodsman);
        }
    }

    /// <summary>
    /// F-05-32 热心的樵夫核心逻辑：
    /// 1. 作业结果为差时计数器 -1；自律 ≥ III 级的员工完成工作时计数器 -1（上限 1，优不恢复）；
    /// 2. 计数器归 0 时并不直接出逃，而是进入等待心脏陷阱（heartTrapArmed = true），下一次进入收容室工作的员工会被铁闸处决并将心脏塞入熔炉后破封逃脱；
    /// 3. 出逃后使用重斧造成高额红伤，并在受伤时寻找周围人类尸体“进食心脏”，每次回复约 95 点生命值。
    /// </summary>
    public class CompWarmHeartedWoodsman : CompLCAbnormality
    {
        private bool heartTrapArmed;
        private bool hasFreshHeart;
        private bool skipInterruptedStudy;
        private int awakenTicksLeft;
        private int chopTicksLeft;
        private int eatHeartTicksLeft;
        private bool alternateFrenzyAnim;

        public CompProperties_WarmHeartedWoodsman PropsWoodsman => (CompProperties_WarmHeartedWoodsman)props;

        public bool IsHeartTrapArmed => !Escaped && heartTrapArmed;
        public bool HasFreshHeart => hasFreshHeart;
        public bool IsAwakening => Escaped && awakenTicksLeft > 0;
        public bool IsChopping => Escaped && chopTicksLeft > 0;
        public bool IsEatingHeart => Escaped && eatHeartTicksLeft > 0;
        public bool UseFrenzyChopAnim => alternateFrenzyAnim;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heartTrapArmed, "woodsmanHeartTrapArmed", false);
            Scribe_Values.Look(ref hasFreshHeart, "woodsmanHasFreshHeart", false);
            Scribe_Values.Look(ref skipInterruptedStudy, "woodsmanSkipInterrupted", false);
            Scribe_Values.Look(ref awakenTicksLeft, "woodsmanAwakenTicks", 0);
            Scribe_Values.Look(ref chopTicksLeft, "woodsmanChopTicks", 0);
            Scribe_Values.Look(ref eatHeartTicksLeft, "woodsmanEatHeartTicks", 0);
            Scribe_Values.Look(ref alternateFrenzyAnim, "woodsmanAltFrenzy", false);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            heartTrapArmed = false;
            hasFreshHeart = false;
            skipInterruptedStudy = false;
            awakenTicksLeft = 0;
            chopTicksLeft = 0;
            eatHeartTicksLeft = 0;
            (parent as LC_WarmHeartedWoodsmanPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            skipInterruptedStudy = false;
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            if (!Escaped && heartTrapArmed && studier != null && !studier.Dead && studier.Spawned)
            {
                TriggerHeartTrapExecution(studier);
            }
        }

        private void TriggerHeartTrapExecution(Pawn victim)
        {
            heartTrapArmed = false;
            hasFreshHeart = true;
            skipInterruptedStudy = true;

            Map map = parent?.MapHeld ?? victim?.MapHeld;
            IntVec3 pos = victim?.PositionHeld ?? parent?.PositionHeld ?? IntVec3.Invalid;
            LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Special, parent);

            if (map != null && pos.InBounds(map))
            {
                WarmHeartedWoodsmanVfxUtility.SpawnShutterExecution(map, pos.ToVector3Shifted(), 2.8f, 34);
                WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(map, parent.DrawPos, 2.5f, 36);
                Messages.Message("LC_Woodsman_HeartTrapTriggered".Translate(parent.LabelCap, victim.LabelShortCap),
                    new LookTargets(pos, map), MessageTypeDefOf.ThreatBig);
            }

            if (victim != null && !victim.Dead)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 9999f, 1f, instigator: parent));
                if (!victim.Dead)
                {
                    victim.Kill(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 9999f, 1f, instigator: parent));
                }
            }

            HoldingPlaformTargetComp?.Escape();
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            if (skipInterruptedStudy)
            {
                skipInterruptedStudy = false;
                return;
            }

            base.Notify_Studied(studier, interrupted);
            if (!interrupted && !Escaped && studier != null && QliphothCountCurrent > 0)
            {
                CompPawnStatus status = studier.GetComp<CompPawnStatus>();
                if (status != null && status.GetPawnStatusELevel(EPawnStatus.Temperance) >= EPawnLevel.III)
                {
                    QliphothCountCurrent--;
                }
            }

            (parent as LC_WarmHeartedWoodsmanPawn)?.UpdateAnimation(force: true);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            int prev = QliphothCountCurrent;
            base.StudyEvent_Good(studier);
            if (prev > 0 && QliphothCountCurrent > prev)
            {
                QliphothCountCurrent = prev;
            }
        }

        protected override void QliphothMeltdown()
        {
            // Tip 3: Instead of escaping at Qliphoth 0, arm the heart trap unless already armed!
            if (!heartTrapArmed)
            {
                heartTrapArmed = true;
                LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Default, parent);
                if (parent?.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                {
                    WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(parent.MapHeld, parent.DrawPos, 2.2f, 32);
                    MoteMaker.ThrowText(parent.DrawPos + new Vector3(0f, 0f, 0.55f), parent.MapHeld,
                        "LC_Woodsman_TrapArmedFloat".Translate(), new Color(0.95f, 0.28f, 0.24f), 2.2f);
                    Messages.Message("LC_Woodsman_TrapArmedMessage".Translate(parent.LabelCap),
                        new LookTargets(parent.PositionHeld, parent.MapHeld), MessageTypeDefOf.NegativeEvent);
                }
                return;
            }

            base.QliphothMeltdown();
        }

        public override void Notify_Escaped()
        {
            heartTrapArmed = false;
            hasFreshHeart = true;
            awakenTicksLeft = PropsWoodsman.awakenTicks;
            chopTicksLeft = 0;
            eatHeartTicksLeft = 0;
            base.Notify_Escaped();
            LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Escape, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(self.Map, self.DrawPos, 2.7f, 34);
            }
            (parent as LC_WarmHeartedWoodsmanPawn)?.UpdateAnimation(force: true);
        }

        public void TriggerAxeChop(Pawn victim)
        {
            chopTicksLeft = PropsWoodsman.chopAnimTicks;
            alternateFrenzyAnim = !alternateFrenzyAnim;
            LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Atk, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(self.Map, self.DrawPos, 2.2f, 22);
            }
            (parent as LC_WarmHeartedWoodsmanPawn)?.UpdateAnimation(force: true);
        }

        public bool ShouldSeekCorpseToEat(out Corpse bestCorpse)
        {
            bestCorpse = null;
            if (!(parent is Pawn self) || !Escaped || IsEatingHeart || IsChopping || !self.Spawned || self.Map == null)
            {
                return false;
            }

            if (self.health.summaryHealth.SummaryHealthPercent >= 0.85f)
            {
                return false;
            }

            float bestDistSq = 28f * 28f;
            var corpses = self.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                if (corpses[i] is Corpse c && c.Spawned && !c.Destroyed && (c.InnerPawn?.RaceProps?.Humanlike ?? false))
                {
                    float dSq = (c.Position - self.Position).LengthHorizontalSquared;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestCorpse = c;
                    }
                }
            }
            return bestCorpse != null;
        }

        public void ConsumeCorpseHeart(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !(parent is Pawn self) || self.Dead) return;

            eatHeartTicksLeft = PropsWoodsman.eatHeartTicks;
            hasFreshHeart = true;
            LCAbnormalitiesHelper.PlaySoundAt(WarmHeartedWoodsmanDefOf.LC_Woodsman_Special, self);
            LCAbnormalitiesHelper.HealInjuries(self, PropsWoodsman.healOnEatCorpse);
            if (self.Map != null && self.Position.InBounds(self.Map))
            {
                WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(self.Map, self.DrawPos, 2.7f, 40);
                MoteMaker.ThrowText(self.DrawPos + new Vector3(0f, 0f, 0.55f), self.Map,
                    "LC_Woodsman_EatHeartFloat".Translate(PropsWoodsman.healOnEatCorpse.ToString("F0")),
                    new Color(0.95f, 0.35f, 0.25f), 2.3f);
            }
            corpse.Destroy(DestroyMode.Vanish);
            (parent as LC_WarmHeartedWoodsmanPawn)?.UpdateAnimation(force: true);
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (awakenTicksLeft > 0) awakenTicksLeft--;
            if (chopTicksLeft > 0) chopTicksLeft--;
            if (eatHeartTicksLeft > 0) eatHeartTicksLeft--;
        }

        public override string GetPlatformInspectString()
        {
            if (heartTrapArmed)
            {
                return "LC_Woodsman_TrapArmedInspect".Translate();
            }
            return base.GetPlatformInspectString();
        }
    }

    public class LC_WarmHeartedWoodsmanPawn : LC_StandardEntityPawn
    {
        public CompWarmHeartedWoodsman WoodsmanComp => EntityComp as CompWarmHeartedWoodsman;

        public bool IsHeldOnPlatform => IsHeldInContainment;

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null) return;
            CompWarmHeartedWoodsman comp = WoodsmanComp;
            AnimationDef desired;
            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = WarmHeartedWoodsmanDefOf.Woodsman_Room_Idle;
            }
            else if (comp != null && comp.IsEatingHeart)
            {
                desired = WarmHeartedWoodsmanDefOf.Woodsman_EatHeart;
            }
            else if (comp != null && comp.IsChopping)
            {
                desired = comp.UseFrenzyChopAnim
                    ? WarmHeartedWoodsmanDefOf.Woodsman_Attack_Frenzy
                    : WarmHeartedWoodsmanDefOf.Woodsman_Attack_Chop;
            }
            else if (comp != null && comp.IsAwakening)
            {
                desired = WarmHeartedWoodsmanDefOf.Woodsman_Room_Awaken;
            }
            else if (pather != null && pather.Moving)
            {
                desired = WarmHeartedWoodsmanDefOf.Woodsman_Escape_Walk;
            }
            else
            {
                desired = WarmHeartedWoodsmanDefOf.Woodsman_Escape_Idle;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public override void TickHolded()
        {
            base.TickHolded();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }
    }

    public class JobGiver_WarmHeartedWoodsmanHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return null;
            CompWarmHeartedWoodsman comp = pawn.TryGetComp<CompWarmHeartedWoodsman>();
            if (comp != null && comp.ShouldSeekCorpseToEat(out Corpse corpse))
            {
                if (pawn.Position.AdjacentTo8WayOrInside(corpse.Position))
                {
                    comp.ConsumeCorpseHeart(corpse);
                    return JobMaker.MakeJob(JobDefOf.Wait, 40);
                }
                Job go = JobMaker.MakeJob(JobDefOf.Goto, corpse);
                go.expiryInterval = 50;
                go.checkOverrideOnExpire = true;
                go.canBashDoors = true;
                return go;
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 65f, p => p != pawn);
            if (target == null) return null;

            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.expiryInterval = 60;
            attack.checkOverrideOnExpire = true;
            attack.canBashDoors = true;
            return attack;
        }
    }

    public class DamageWorker_WoodsmanAxe : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn attacker = dinfo.Instigator as Pawn;
            CompWarmHeartedWoodsman comp = attacker?.TryGetComp<CompWarmHeartedWoodsman>();
            if (comp != null)
            {
                dinfo.SetAmount(Rand.Range(comp.PropsWoodsman.chopMinDmg, comp.PropsWoodsman.chopMaxDmg));
                comp.TriggerAxeChop(victim as Pawn);
            }
            return base.Apply(dinfo, victim);
        }
    }
}
