using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.FragmentOfTheUniverse
{
    [DefOf]
    public static class FragmentOfTheUniverseDefOf
    {
        public static ThingDef FragmentOfTheUniverseEgg;
        public static ThingDef LC_CosmosSongWaveFX;
        public static ThingDef LC_CosmosSpearSkillRunnerFX;

        public static DamageDef LC_Cosmos_SongWhite;
        public static HediffDef LC_CosmicEnlightenment;

        public static SoundDef LC_Cosmos_Default;
        public static SoundDef LC_Cosmos_Hit;
        public static SoundDef LC_Cosmos_Sing;
        public static SoundDef LC_Cosmos_SpearHit;

        public static AnimationDef Cosmos_Contained_Idle;
        public static AnimationDef Cosmos_Transform;
        public static AnimationDef Cosmos_Breached_Idle;
        public static AnimationDef Cosmos_Walk;
        public static AnimationDef Cosmos_Attack;
        public static AnimationDef Cosmos_Sing_Start;
        public static AnimationDef Cosmos_Sing;

        static FragmentOfTheUniverseDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FragmentOfTheUniverseDefOf));
        }
    }

    public class CompProperties_FragmentOfTheUniverse : CompProperties_LCAbnormality
    {
        public float normalDropChance = 0.50f;
        public float badDropChance = 0.70f;
        public int transformTicks = 90;
        public int attackAnimTicks = 44;
        public int singStartTicks = 42;
        public int singDurationTicks = 132;
        public int singCooldownTicks = 240;
        public float singRadius = 7.5f;
        public float singWhiteDamageMin = 4f;
        public float singWhiteDamageMax = 6f;

        public CompProperties_FragmentOfTheUniverse()
        {
            compClass = typeof(CompFragmentOfTheUniverse);
        }
    }

    /// <summary>
    /// O-03-60 宇宙碎片核心逻辑：
    /// 1. 作业结果为“一般”时有 50% 概率降低 1 点逆卡巴拉计数器（上限 2）；
    /// 2. 作业结果为“差”时有 70% 概率降低计数器（优不恢复）；
    /// 3. 作业期间若员工陷入恐慌 / 精神濒危（≥98% 精神伤害或 InMentalState），立即触发计数器 -1；
    /// 4. 计数器归 0 出逃后蜕变为无面巨口异怪形态，近战造成黑伤（11~15）并周期性高歌发射彩色音波造成范围白伤；
    /// 5. 专属 E.G.O. 长枪在谨慎 < V 级员工手中命中时有 10% 概率赋予“宇宙悟性”精神护盾。
    /// </summary>
    public class CompFragmentOfTheUniverse : CompLCAbnormality
    {
        private Pawn activeStudier;
        private bool panicDroppedThisStudy;
        private bool suppressMeltdown;
        private int transformTicksLeft;
        private int attackTicksLeft;
        private int singStartTicksLeft;
        private int singTicksLeft;
        private int singCooldown;
        private int nextSingPulseTick;

        public CompProperties_FragmentOfTheUniverse PropsCosmos => (CompProperties_FragmentOfTheUniverse)props;

        public bool IsTransforming => Escaped && transformTicksLeft > 0;
        public bool IsBreachedVisual => Escaped && transformTicksLeft <= 0;
        public bool IsAttacking => Escaped && attackTicksLeft > 0;
        public bool IsSingStarting => Escaped && singStartTicksLeft > 0;
        public bool IsSinging => Escaped && singTicksLeft > 0;
        public bool IsSingLocked => IsTransforming || IsSingStarting || IsSinging;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref activeStudier, "cosmosActiveStudier");
            Scribe_Values.Look(ref panicDroppedThisStudy, "cosmosPanicDropped", false);
            Scribe_Values.Look(ref transformTicksLeft, "cosmosTransformTicks", 0);
            Scribe_Values.Look(ref attackTicksLeft, "cosmosAttackTicks", 0);
            Scribe_Values.Look(ref singStartTicksLeft, "cosmosSingStartTicks", 0);
            Scribe_Values.Look(ref singTicksLeft, "cosmosSingTicks", 0);
            Scribe_Values.Look(ref singCooldown, "cosmosSingCooldown", 0);
            Scribe_Values.Look(ref nextSingPulseTick, "cosmosNextSingPulseTick", 0);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            activeStudier = null;
            panicDroppedThisStudy = false;
            suppressMeltdown = false;
            transformTicksLeft = 0;
            attackTicksLeft = 0;
            singStartTicksLeft = 0;
            singTicksLeft = 0;
            singCooldown = 160;
            (parent as LC_FragmentOfTheUniversePawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            activeStudier = studier;
            panicDroppedThisStudy = false;
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            if (studier != null)
            {
                activeStudier = studier;
                CheckWorkerPanicDrop(studier);
            }
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            bool ok = base.Notify_StudyInterval(studier, workType);
            if (studier?.parent is Pawn p)
            {
                activeStudier = p;
                CheckWorkerPanicDrop(p);
            }
            return ok;
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            if (studier != null) CheckWorkerPanicDrop(studier);
            activeStudier = null;
            panicDroppedThisStudy = false;
            base.Notify_Studied(studier, interrupted);
            (parent as LC_FragmentOfTheUniversePawn)?.UpdateAnimation(force: true);
        }

        private void CheckWorkerPanicDrop(Pawn worker)
        {
            if (Escaped || panicDroppedThisStudy || worker == null || worker.Dead) return;
            bool panicked = worker.InMentalState;
            if (!panicked && LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage != null && worker.health?.hediffSet != null)
            {
                Hediff md = worker.health.hediffSet.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
                if (md != null && md.Severity >= 0.98f) panicked = true;
            }

            if (panicked)
            {
                panicDroppedThisStudy = true;
                if (parent?.MapHeld != null && parent.PositionHeld.InBounds(parent.MapHeld))
                {
                    MoteMaker.ThrowText(parent.DrawPos + new Vector3(0f, 0f, 0.55f), parent.MapHeld,
                        "LC_Cosmos_PanicDropFloat".Translate(), new Color(0.84f, 0.45f, 1.0f), 2.2f);
                }
                QliphothCountCurrent--;
            }
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

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (!Escaped && Rand.Chance(PropsCosmos.normalDropChance))
            {
                QliphothCountCurrent--;
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            if (!Rand.Chance(PropsCosmos.badDropChance))
            {
                suppressMeltdown = true;
                int prev = QliphothCountCurrent;
                try
                {
                    base.StudyEvent_Bad(studier);
                }
                finally
                {
                    suppressMeltdown = false;
                }
                if (prev > 0 && QliphothCountCurrent < prev)
                {
                    QliphothCountCurrent = prev;
                }
                return;
            }

            base.StudyEvent_Bad(studier);
        }

        protected override void QliphothMeltdown()
        {
            if (suppressMeltdown) return;
            base.QliphothMeltdown();
        }

        public override void Notify_Escaped()
        {
            activeStudier = null;
            panicDroppedThisStudy = false;
            transformTicksLeft = PropsCosmos.transformTicks;
            attackTicksLeft = 0;
            singStartTicksLeft = 0;
            singTicksLeft = 0;
            singCooldown = 140;
            base.Notify_Escaped();
            LCAbnormalitiesHelper.PlaySoundAt(FragmentOfTheUniverseDefOf.LC_Cosmos_Default, parent);
            if (parent is Pawn self && self.Spawned && self.Map != null)
            {
                FragmentOfTheUniverseVfxUtility.SpawnSongWave(self.Map, self.DrawPos, 3.2f, 36);
            }
            (parent as LC_FragmentOfTheUniversePawn)?.UpdateAnimation(force: true);
        }

        public void TriggerTentacleAttack()
        {
            attackTicksLeft = PropsCosmos.attackAnimTicks;
            (parent as LC_FragmentOfTheUniversePawn)?.UpdateAnimation(force: true);
        }

        public bool TryStartSing(out int lockTicks)
        {
            lockTicks = 0;
            if (!Escaped || IsTransforming || IsSingStarting || IsSinging || singCooldown > 0) return false;
            singStartTicksLeft = PropsCosmos.singStartTicks;
            singTicksLeft = 0;
            singCooldown = PropsCosmos.singCooldownTicks;
            lockTicks = PropsCosmos.singStartTicks + PropsCosmos.singDurationTicks;
            (parent as LC_FragmentOfTheUniversePawn)?.UpdateAnimation(force: true);
            return true;
        }

        private void EmitSongPulse()
        {
            if (!(parent is Pawn self) || !self.Spawned || self.Map == null) return;
            LCAbnormalitiesHelper.PlaySoundAt(FragmentOfTheUniverseDefOf.LC_Cosmos_Sing, self);
            FragmentOfTheUniverseVfxUtility.SpawnSongWave(self.Map, self.DrawPos, PropsCosmos.singRadius * 0.9f, 44);

            if (FragmentOfTheUniverseDefOf.LC_Cosmos_SongWhite == null) return;
            IReadOnlyList<Pawn> all = self.Map.mapPawns.AllPawnsSpawned;
            float rSq = PropsCosmos.singRadius * PropsCosmos.singRadius;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Pawn target = all[i];
                if (target == null || target == self || target.Dead || !target.Spawned) continue;
                if (!(target.RaceProps?.Humanlike ?? false)) continue;
                if ((target.Position - self.Position).LengthHorizontalSquared > rSq) continue;

                float dmg = Rand.Range(PropsCosmos.singWhiteDamageMin, PropsCosmos.singWhiteDamageMax);
                target.TakeDamage(new DamageInfo(FragmentOfTheUniverseDefOf.LC_Cosmos_SongWhite, dmg, 0.25f, instigator: self));
            }
        }

        public override void TickContained()
        {
            base.TickContained();
            if (activeStudier != null && !Escaped)
            {
                CheckWorkerPanicDrop(activeStudier);
            }
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (transformTicksLeft > 0) transformTicksLeft--;
            if (attackTicksLeft > 0) attackTicksLeft--;
            if (singCooldown > 0) singCooldown--;

            if (singStartTicksLeft > 0)
            {
                singStartTicksLeft--;
                if (singStartTicksLeft == 0)
                {
                    singTicksLeft = PropsCosmos.singDurationTicks;
                    nextSingPulseTick = 1;
                }
            }
            else if (singTicksLeft > 0)
            {
                nextSingPulseTick--;
                if (nextSingPulseTick <= 0)
                {
                    EmitSongPulse();
                    nextSingPulseTick = 44;
                }
                singTicksLeft--;
            }
        }
    }

    public class LC_FragmentOfTheUniversePawn : LC_StandardEntityPawn
    {
        public CompFragmentOfTheUniverse CosmosComp => EntityComp as CompFragmentOfTheUniverse;

        public bool IsHeldOnPlatform => ParentHolder is Building_AbnormalityHoldingPlatform;

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null) return;
            CompFragmentOfTheUniverse comp = CosmosComp;
            AnimationDef desired;
            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Contained_Idle;
            }
            else if (comp != null && comp.IsTransforming)
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Transform;
            }
            else if (comp != null && comp.IsSingStarting)
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Sing_Start;
            }
            else if (comp != null && comp.IsSinging)
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Sing;
            }
            else if (comp != null && comp.IsAttacking)
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Attack;
            }
            else if (pather != null && pather.Moving)
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Walk;
            }
            else
            {
                desired = FragmentOfTheUniverseDefOf.Cosmos_Breached_Idle;
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
            CompFragmentOfTheUniverse comp = CosmosComp;
            if (comp != null && comp.IsSingLocked && pather?.Moving == true)
            {
                pather.StopDead();
            }
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }
    }

    public class JobGiver_FragmentOfTheUniverseHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return null;
            CompFragmentOfTheUniverse comp = pawn.TryGetComp<CompFragmentOfTheUniverse>();
            if (comp != null && comp.IsSingLocked)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, 65f, p => p != pawn);
            if (target == null) return null;

            if (comp != null && pawn.Position.DistanceTo(target.Position) <= comp.PropsCosmos.singRadius - 1f
                && comp.TryStartSing(out int lockTicks))
            {
                return JobMaker.MakeJob(JobDefOf.Wait, Mathf.Max(30, lockTicks));
            }

            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.expiryInterval = 60;
            attack.checkOverrideOnExpire = true;
            attack.canBashDoors = true;
            return attack;
        }
    }

    public class DamageWorker_CosmosTentacle : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn attacker = dinfo.Instigator as Pawn;
            CompFragmentOfTheUniverse comp = attacker?.TryGetComp<CompFragmentOfTheUniverse>();
            if (comp != null)
            {
                dinfo.SetAmount(Rand.Range(11f, 15f));
                comp.TriggerTentacleAttack();
            }
            return base.Apply(dinfo, victim);
        }
    }

    /// <summary>E.G.O. 来自某处的碎片长枪专属伤害：慎重 < V 的持握者命中时有 10% 概率获得宇宙悟性精神护盾。</summary>
    public class DamageWorker_CosmosSpear : DamageWorker_FourColor
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            DamageResult result = base.Apply(dinfo, victim);
            if (result.totalDamageDealt <= 0f) return result;

            if (dinfo.Instigator is Pawn wielder && !wielder.Dead && FragmentOfTheUniverseDefOf.LC_CosmicEnlightenment != null)
            {
                CompPawnStatus status = wielder.GetComp<CompPawnStatus>();
                EPawnLevel prudence = status != null ? status.GetPawnStatusELevel(EPawnStatus.Prudence) : EPawnLevel.I;
                if (prudence < EPawnLevel.V && Rand.Chance(0.10f))
                {
                    GrantOrRefreshEnlightenment(wielder);
                }
            }
            return result;
        }

        public static void GrantOrRefreshEnlightenment(Pawn target)
        {
            if (target == null || target.Dead || FragmentOfTheUniverseDefOf.LC_CosmicEnlightenment == null) return;
            Hediff h = LCAbnormalitiesHelper.ApplyOrRefreshHediff(target, FragmentOfTheUniverseDefOf.LC_CosmicEnlightenment, 1f);
            HediffComp_CosmicEnlightenment comp = h?.TryGetComp<HediffComp_CosmicEnlightenment>();
            comp?.RefreshShield();
            if (target.Spawned && target.Map != null && target.DrawPos.ShouldSpawnMotesAt(target.Map))
            {
                FragmentOfTheUniverseVfxUtility.SpawnSongWave(target.Map, target.DrawPos, 1.8f, 22);
                MoteMaker.ThrowText(target.DrawPos + new Vector3(0f, 0f, 0.55f), target.Map,
                    "LC_Cosmos_EnlightenmentFloat".Translate(), new Color(0.76f, 0.56f, 1.0f), 2.0f);
            }
        }
    }

    public class HediffCompProperties_CosmicEnlightenment : HediffCompProperties
    {
        public float maxShield = 50f;

        public HediffCompProperties_CosmicEnlightenment()
        {
            compClass = typeof(HediffComp_CosmicEnlightenment);
        }
    }

    /// <summary>宇宙悟性护盾组件：吸收至多 50 点 WHITE / BLACK 精神侵蚀伤害。</summary>
    public class HediffComp_CosmicEnlightenment : HediffComp, ILCPawnDamageInterceptor
    {
        private float remainingShield = -1f;

        public HediffCompProperties_CosmicEnlightenment Props => (HediffCompProperties_CosmicEnlightenment)props;

        public float RemainingShield
        {
            get
            {
                if (remainingShield < 0f) remainingShield = Props.maxShield;
                return remainingShield;
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            remainingShield = Props.maxShield;
        }

        public void RefreshShield()
        {
            remainingShield = Props.maxShield;
            HediffComp_Disappears disappears = parent?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = disappears.Props.disappearsAfterTicks.max;
            }
        }

        public override string CompLabelInBracketsExtra => $"{Mathf.CeilToInt(RemainingShield)}/{Mathf.CeilToInt(Props.maxShield)}";

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref remainingShield, "lcCosmicEnlightenmentShield", Props?.maxShield ?? 50f);
        }

        public bool TryInterceptDamage(Pawn pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (pawn == null || dinfo.Amount <= 0f || RemainingShield <= 0f) return false;
            if (!MatchesCosmicColor(dinfo.Def)) return false;

            float incoming = dinfo.Amount;
            if (RemainingShield >= incoming)
            {
                remainingShield -= incoming;
                absorbed = true;
                PlayFeedback(pawn, remainingShield <= 0.01f);
                if (remainingShield <= 0.01f) pawn.health?.RemoveHediff(parent);
                return true;
            }

            float leftover = incoming - remainingShield;
            remainingShield = 0f;
            dinfo.SetAmount(leftover);
            PlayFeedback(pawn, true);
            pawn.health?.RemoveHediff(parent);
            return false;
        }

        private static bool MatchesCosmicColor(DamageDef damageDef)
        {
            if (damageDef == null) return false;
            FourColorDamageExtension ext = damageDef.GetModExtension<FourColorDamageExtension>();
            if (ext != null)
            {
                return ext.damageType == FourColorDamageType.White || ext.damageType == FourColorDamageType.Black;
            }
            return damageDef == RimWorld.DamageDefOf.Psychic
                || damageDef == LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage
                || damageDef == LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
        }

        private static void PlayFeedback(Pawn pawn, bool broken)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return;
            SoundDef snd = DefDatabase<SoundDef>.GetNamedSilentFail(broken ? "LC_Sound_ShieldBroken" : "LC_Sound_ShieldHit");
            snd?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
        }
    }
}
