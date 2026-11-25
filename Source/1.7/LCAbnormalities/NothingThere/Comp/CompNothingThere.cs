using System.Collections.Generic;
using System.Linq;
using System.Text;
using LCAbnormalities.Settings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Util;
using NothingThere.Things;
using NothingThere.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NothingThere.Comp
{
    /// <summary>
    /// O-06-20 一无所有核心逻辑组件：包含研究概率/秒杀窃皮机制、逆卡巴拉熔毁与三阶段出逃形态切换战斗。
    /// </summary>
    public class CompNothingThere : CompAbnormality
    {
        private static readonly string[] Form1VoiceKeys =
        {
            "LC_NothingThere_Voice_Hello",
            "LC_NothingThere_Voice_ILoveYou",
            "LC_NothingThere_Voice_Help",
            "LC_NothingThere_Voice_Manager"
        };

        private NothingThereForm currentForm = NothingThereForm.Contained;
        private DisguiseInfo disguiseInfo = new DisguiseInfo();
        private int formTicks = 0;
        private int cleaveCooldown = 0;
        private int boneSpikeCooldown = 0;
        private int goodbyeCooldown = 0;
        private int goodbyeWindupTicks = 0;
        private int goodbyeSlashTicks = 0;
        private Pawn goodbyeTarget = null;
        private int totalVictimsConsumed = 0;
        private bool lowStatWarningSent = false;
        private Pawn currentStudier = null;
        private int studyingGraceTicks = 0;
        private int attackAnimTicks = 0;
        private bool useDogAttackVariant2 = false;
        private int throwWindupTicks = 0;
        private int throwRecoverTicks = 0;
        private Pawn throwTarget = null;
        private float specialAttackChance = 0.40f;
        private int specialCheckCooldown = 150;
        private int lastDamagedTick = -99999;

        private Material disguiseMaterial = null;
        private Pawn disguiseMaterialPawn = null;

        public new CompProperties_NothingThere Props => (CompProperties_NothingThere)props;

        public LC_NothingTherePawn SelfPawn => (LC_NothingTherePawn)parent;

        public NothingThereForm CurrentForm => currentForm;

        public bool HasDisguise => disguiseInfo != null && disguiseInfo.hasDisguise;

        public DisguiseInfo DisguiseInfo => disguiseInfo;

        public int FormTicks => formTicks;

        public int GoodbyeWindupTicks => goodbyeWindupTicks;

        public int GoodbyeSlashTicks => goodbyeSlashTicks;

        public bool IsPerformingGoodbye => goodbyeWindupTicks > 0 || goodbyeSlashTicks > 0;

        public bool IsThrowingBoneSpike => throwWindupTicks > 0 || throwRecoverTicks > 0;

        public int ThrowWindupTicks => throwWindupTicks;

        public int AttackAnimTicks => attackAnimTicks;

        public bool UseDogAttackVariant2 => useDogAttackVariant2;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public int TotalVictimsConsumed => totalVictimsConsumed;

        public float SpecialAttackChance => specialAttackChance;

        public int LastDamagedTick
        {
            get => lastDamagedTick;
            set => lastDamagedTick = value;
        }

        public void Notify_TookDamage()
        {
            lastDamagedTick = Find.TickManager?.TicksGame ?? 0;
        }

        public int CocoonHatchTicksRequired
        {
            get
            {
                int sec = LCAbnormalitiesMod.Settings?.NothingThereCocoonHatchSeconds ?? 30;
                return Mathf.Clamp(sec, 15, 60) * 60;
            }
        }

        #region 生命周期与工作管理

        public override void Notify_Holded()
        {
            AbnormalityUtility.Discover(parent.def);
            ResetContainmentState();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            DestroyDisguiseMaterial();
            EffectUtil.OnNothingThereDeath(SelfPawn, prevMap);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            DestroyDisguiseMaterial();
            base.PostDestroy(mode, previousMap);
        }

        public override void Notify_Studying(Pawn studier)
        {
            currentStudier = studier;
            studyingGraceTicks = 20;
            if (lowStatWarningSent || studier == null)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status == null || !status.Enabled)
            {
                return;
            }

            EPawnLevel fort = status.GetPawnStatusELevel(EPawnStatus.Fortitude);
            EPawnLevel just = status.GetPawnStatusELevel(EPawnStatus.Justice);
            if (fort < EPawnLevel.IV || just < EPawnLevel.IV)
            {
                lowStatWarningSent = true;
                Messages.Message(
                    "LC_NothingThere_LowStatWarning".Translate(studier.LabelShort),
                    new LookTargets(parent.PositionHeld, parent.MapHeld),
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            bool success = base.Notify_StudyInterval(studier, workType);
            if (!success && studier?.parent is Pawn worker && !worker.Dead && worker.Spawned)
            {
                EPawnLevel fort = studier.GetPawnStatusELevel(EPawnStatus.Fortitude);
                EPawnLevel just = studier.GetPawnStatusELevel(EPawnStatus.Justice);

                // 原作管理须知 V/VI：已伪装状态下勇气 < IV 人员将遭受严重精神动摇并可能当场崩溃/被剥皮出逃
                if (HasDisguise && fort < EPawnLevel.IV && !worker.Dead && worker.Spawned)
                {
                    worker.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, Rand.Range(4f, 6f), instigator: SelfPawn));
                }

                if (!worker.Dead && worker.Spawned)
                {
                    float dmg = Props.workRedDamageRange.RandomInRange;
                    worker.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, dmg, instigator: SelfPawn));
                }

                if (worker.Dead || worker.InMentalState)
                {
                    if (HasDisguise)
                    {
                        KillAndStealSkinAndBreach(worker);
                    }
                    else if (just >= EPawnLevel.IV)
                    {
                        EnterContainedDisguise(worker);
                    }
                    else
                    {
                        QliphothCountCurrent = 0;
                    }
                }
            }

            return success;
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            lowStatWarningSent = false;
            currentStudier = null;
            studyingGraceTicks = 0;

            if (Escaped)
            {
                PeBoxProducedTemp = 0;
                NeBoxProducedTemp = 0;
                return;
            }

            CompPawnStatus status = studier?.TryGetComp<CompPawnStatus>();
            EPawnLevel fortLevel = (status != null && status.Enabled)
                ? status.GetPawnStatusELevel(EPawnStatus.Fortitude)
                : EPawnLevel.I;
            EPawnLevel justLevel = (status != null && status.Enabled)
                ? status.GetPawnStatusELevel(EPawnStatus.Justice)
                : EPawnLevel.I;

            // 若工作员在过程中已经死亡或崩溃，避免二次重复结算
            if (studier != null && studier.Dead)
            {
                PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
                PeBoxProducedTemp = 0;
                NeBoxProducedTemp = 0;
                if (!HasDisguise)
                {
                    if (justLevel >= EPawnLevel.IV)
                    {
                        EnterContainedDisguise(studier);
                    }
                    else
                    {
                        QliphothCountCurrent = 0;
                    }
                }
                return;
            }

            // 原作管理须知 V/VI：当在收容单元内已披皮伪装时，须知 II/III/IV 暂停；勇气 < IV 或恐慌崩溃者将被剥皮替换外壳并直接突破收容！
            if (HasDisguise)
            {
                if (!interrupted && studier != null && (fortLevel < EPawnLevel.IV || studier.InMentalState))
                {
                    PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
                    PeBoxProducedTemp = 0;
                    NeBoxProducedTemp = 0;
                    KillAndStealSkinAndBreach(studier);
                    return;
                }

                ResolveStudyOutcomeWithoutBreach(studier);
                return;
            }

            // 原作管理须知 II & 特殊能力：未披皮状态下，正义 < IV 的员工完成工作后不杀员工，直接清空计数器突破收容（犬形态）
            if (!interrupted && studier != null && justLevel < EPawnLevel.IV)
            {
                ResolveStudyOutcomeWithoutBreach(studier, countGoodQliphoth: false);
                QliphothCountCurrent = 0;
                return;
            }

            base.Notify_Studied(studier, interrupted);
        }

        private void ResolveStudyOutcomeWithoutBreach(Pawn studier, bool countGoodQliphoth = true)
        {
            if (PeBoxComp != null)
            {
                PeBoxComp.CheckSpawnPeBox(studier, PeBoxProducedTemp);
                if (PeBoxProducedTemp > PeBoxComp.Props.amountProdueRangeNormal.max)
                {
                    StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Good);
                    AccessoryableComp?.CheckGiveAccessory(studier);
                    if (countGoodQliphoth)
                    {
                        QliphothCountCurrent++;
                    }
                }
                else if (PeBoxProducedTemp < PeBoxComp.Props.amountProdueRangeNormal.min)
                {
                    StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Bad);
                    AccessoryableComp?.CheckGiveAccessory(studier);
                }
                else
                {
                    StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Normal);
                    AccessoryableComp?.CheckGiveAccessory(studier);
                }
            }

            PeBoxProducedTemp = 0;
            NeBoxProducedTemp = 0;
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
            StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Bad);
            AccessoryableComp?.CheckGiveAccessory(studier);

            // 原作管理须知 IV：未披皮且正义 >= IV 时，工作结果为差 -> 杀害工作员并披上外皮（留在收容单元内伪装）
            if (!HasDisguise && studier != null && !studier.Dead)
            {
                KillAndStealSkinContained(studier);
            }
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
        }

        protected override void QliphothMeltdown()
        {
            lowStatWarningSent = false;
            currentStudier = null;
            studyingGraceTicks = 0;
            base.QliphothMeltdown();
        }

        protected override float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            if (studier == null)
            {
                return 0f;
            }

            float baseRate = 0f;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    // NullThing_stat: Lv1=0, Lv2=0, Lv3=0.35, Lv4=0.40, Lv5=0.45
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Fortitude))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                            return 0f;
                        case EPawnLevel.III:
                            baseRate = 0.35f;
                            break;
                        case EPawnLevel.IV:
                            baseRate = 0.40f;
                            break;
                        default:
                            baseRate = 0.45f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Attachment:
                    // NullThing_stat: 沟通全等级 0.50
                    baseRate = 0.50f;
                    break;

                case EAnomalyWorkType.Insight:
                case EAnomalyWorkType.Repression:
                default:
                    // NullThing_stat: 洞察/压迫全等级 0%
                    return 0f;
            }

            // 原作管理须知 I 计算公式：基础成功率 × 勇气等级 × 0.2 + 观察/正义加成
            int fortOrdinal = Mathf.Clamp((int)studier.GetPawnStatusELevel(EPawnStatus.Fortitude), 1, 5);
            float scaledBase = baseRate * (fortOrdinal * 0.2f);
            float bonus = base.StudySuccessRateCalculate(studier, workType);
            return Mathf.Clamp(scaledBase + bonus, 0f, 0.92f);
        }

        public void TickContained()
        {
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }

            if (HasDisguise && disguiseMaterial == null && UnityData.IsInMainThread)
            {
                GetOrCreateDisguiseMaterial();
            }

            if (HasDisguise && Find.TickManager != null && Find.TickManager.TicksGame % 300 == 0)
            {
                ThrowMoteText("LC_NothingThere_Voice_ILoveYou".Translate(), new Color(0.95f, 0.3f, 0.3f));
                PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Form1Voice);
            }

            // 若在收容中误留战斗 buff，确保持续清理
            if (currentForm != NothingThereForm.Contained && !Escaped)
            {
                ResetContainmentState();
            }
        }

        private void ResetContainmentState()
        {
            currentForm = NothingThereForm.Contained;
            formTicks = 0;
            cleaveCooldown = 0;
            boneSpikeCooldown = 0;
            goodbyeCooldown = 0;
            goodbyeWindupTicks = 0;
            goodbyeSlashTicks = 0;
            goodbyeTarget = null;
            attackAnimTicks = 0;
            useDogAttackVariant2 = false;
            throwWindupTicks = 0;
            throwRecoverTicks = 0;
            throwTarget = null;
            specialAttackChance = Props.specialBaseChance;
            specialCheckCooldown = Props.specialCheckIntervalTicks;
            lastDamagedTick = -99999;
            studyingGraceTicks = 0;
            lowStatWarningSent = false;
            currentStudier = null;
            disguiseInfo?.Clear();
            DestroyDisguiseMaterial();
            SyncFormBuffs();
            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            SelfPawn?.UpdateAnimation(force: true);
        }

        #endregion 生命周期与工作管理

        #region 三阶段出逃与战斗逻辑

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            if (LCAbnormalitiesMod.Settings?.NothingThereAllowDisguiseForm ?? true)
            {
                TransitionToForm1();
            }
            else
            {
                TransitionToForm2();
            }
        }

        public void TransitionToForm1()
        {
            currentForm = NothingThereForm.Form1_Disguised;
            formTicks = 0;
            cleaveCooldown = 60;
            goodbyeWindupTicks = 0;
            goodbyeSlashTicks = 0;
            goodbyeTarget = null;
            attackAnimTicks = 0;
            useDogAttackVariant2 = false;
            throwWindupTicks = 0;
            throwRecoverTicks = 0;
            throwTarget = null;
            studyingGraceTicks = 0;
            SyncFormBuffs();
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Form1Meet ?? Def.SoundDefOf.NothingThere_Transform);
            ThrowMoteText("LC_NothingThere_Voice_Hello".Translate(), new Color(0.9f, 0.25f, 0.25f));
            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            SelfPawn?.UpdateAnimation(force: true);
        }

        public void TransitionToForm2()
        {
            currentForm = NothingThereForm.Form2_Cocoon;
            formTicks = 0;
            goodbyeWindupTicks = 0;
            goodbyeSlashTicks = 0;
            goodbyeTarget = null;
            attackAnimTicks = 0;
            useDogAttackVariant2 = false;
            throwWindupTicks = 0;
            throwRecoverTicks = 0;
            throwTarget = null;
            studyingGraceTicks = 0;
            DestroyDisguiseMaterial();
            SyncFormBuffs();

            HealAllInjuries();
            if (SelfPawn?.Spawned == true)
            {
                SelfPawn.pather?.StopDead();
                SelfPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Transform);
                AbnormalityUtility.Burst(SelfPawn.Position, SelfPawn.Map, 1.8f);
                ThrowMoteText("LC_NothingThere_Morphing".Translate(), new Color(0.95f, 0.2f, 0.2f));
            }

            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            SelfPawn?.UpdateAnimation(force: true);
        }

        public void TransitionToForm3()
        {
            currentForm = NothingThereForm.Form3_TrueForm;
            formTicks = 0;
            cleaveCooldown = 45;
            boneSpikeCooldown = 150;
            goodbyeCooldown = 240;
            goodbyeWindupTicks = 0;
            goodbyeSlashTicks = 0;
            goodbyeTarget = null;
            attackAnimTicks = 0;
            useDogAttackVariant2 = false;
            throwWindupTicks = 0;
            throwRecoverTicks = 0;
            throwTarget = null;
            specialAttackChance = Props.specialBaseChance;
            specialCheckCooldown = Props.specialCheckIntervalTicks;
            lastDamagedTick = -99999;
            studyingGraceTicks = 0;
            DestroyDisguiseMaterial();
            SyncFormBuffs();

            HealAllInjuries();
            if (SelfPawn?.Spawned == true)
            {
                SelfPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Hatch);
                AbnormalityUtility.Burst(SelfPawn.Position, SelfPawn.Map, 2.5f);
                ThrowMoteText("LC_NothingThere_Voice_ILoveYou".Translate(), new Color(0.95f, 0.1f, 0.1f));
                Find.LetterStack.ReceiveLetter(
                    "LC_NothingThere_HatchedLabel".Translate(),
                    "LC_NothingThere_HatchedText".Translate(),
                    LetterDefOf.ThreatBig,
                    SelfPawn);
            }

            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            SelfPawn?.UpdateAnimation(force: true);
        }

        public void SyncFormStateAfterLoad()
        {
            SyncFormBuffs();
            SelfPawn?.UpdateAnimation(force: true);
        }

        public void TickBreachCombat()
        {
            if (!Escaped || !parent.Spawned || SelfPawn == null || SelfPawn.Dead || SelfPawn.Downed)
            {
                return;
            }

            formTicks++;
            if (cleaveCooldown > 0) cleaveCooldown--;
            if (boneSpikeCooldown > 0) boneSpikeCooldown--;
            if (goodbyeCooldown > 0) goodbyeCooldown--;
            if (attackAnimTicks > 0) attackAnimTicks--;

            switch (currentForm)
            {
                case NothingThereForm.Form1_Disguised:
                    TickForm1();
                    break;
                case NothingThereForm.Form2_Cocoon:
                    TickForm2();
                    break;
                case NothingThereForm.Form3_TrueForm:
                    TickForm3();
                    break;
            }
        }

        private void TickForm1()
        {
            if (HasDisguise && disguiseMaterial == null && UnityData.IsInMainThread)
            {
                GetOrCreateDisguiseMaterial();
            }

            if (formTicks % 240 == 0)
            {
                string key = Form1VoiceKeys[Rand.Range(0, Form1VoiceKeys.Length)];
                ThrowMoteText(key.Translate(), new Color(0.95f, 0.35f, 0.35f));
                PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Form1Voice);
            }

            if (formTicks >= Props.form1DurationTicks
                || (Props.form1MorphHealthPct > 0f && SelfPawn.health.summaryHealth.SummaryHealthPercent < Props.form1MorphHealthPct))
            {
                TransitionToForm2();
                return;
            }

            if (cleaveCooldown <= 0)
            {
                Pawn target = FindNearestHostileTarget(Props.form1SwipeRadius);
                if (target != null)
                {
                    cleaveCooldown = 95;
                    SelfPawn?.FaceCellHorizontal(target.Position);
                    if (!HasDisguise)
                    {
                        attackAnimTicks = 37;
                        useDogAttackVariant2 = !useDogAttackVariant2;
                        SelfPawn?.PlayDogAttackAnimation(useDogAttackVariant2);
                    }

                    DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
                    ApplyAreaDamage(Props.form1SwipeRadius, slashDef, Props.form1SwipeDamageRange, 0.35f);
                    PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Attack);
                }
            }
        }

        private void TickForm2()
        {
            if (SelfPawn.pather?.Moving == true) SelfPawn.pather.StopDead();

            if (formTicks % 90 == 0)
            {
                PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_CocoonHeartbeat);
                if (SelfPawn.Map != null && SelfPawn.DrawPos.ShouldSpawnMotesAt(SelfPawn.Map))
                {
                    AbnormalityUtility.Burst(SelfPawn.Position, SelfPawn.Map, 0.75f);
                }
            }

            if (formTicks >= CocoonHatchTicksRequired)
            {
                TransitionToForm3();
            }
        }

        private void TickForm3()
        {
            // 原作：三阶段 10 秒（600 ticks）未受伤害时开始再生，低于 30% 血量时治疗速度翻倍
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            if (Props.form3RegenIntervalTicks > 0
                && formTicks % Props.form3RegenIntervalTicks == 0
                && (ticksGame - lastDamagedTick) >= Props.form3RegenDelayTicks)
            {
                float hpPct = SelfPawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                float healAmt = hpPct < 0.30f ? Props.form3RegenAmount * 2f : Props.form3RegenAmount;
                EffectUtil.HealPawnInjuries(SelfPawn, healAmt, showFeedback: false);
            }

            if (specialCheckCooldown > 0)
            {
                specialCheckCooldown--;
            }

            if (goodbyeWindupTicks > 0)
            {
                if (SelfPawn.pather?.Moving == true) SelfPawn.pather.StopDead();
                if (goodbyeTarget != null && goodbyeTarget.Spawned && !goodbyeTarget.Dead)
                {
                    SelfPawn.FaceCellHorizontal(goodbyeTarget.Position);
                }

                goodbyeWindupTicks--;
                if (goodbyeWindupTicks == 0)
                {
                    ExecuteGoodbyeSlash();
                }
                return;
            }

            if (goodbyeSlashTicks > 0)
            {
                if (SelfPawn.pather?.Moving == true) SelfPawn.pather.StopDead();
                goodbyeSlashTicks--;
                if (goodbyeSlashTicks == 0)
                {
                    SelfPawn.Drawer?.renderer?.SetAllGraphicsDirty();
                    SelfPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                }
                return;
            }

            if (throwWindupTicks > 0)
            {
                if (SelfPawn.pather?.Moving == true) SelfPawn.pather.StopDead();
                if (throwTarget != null && throwTarget.Spawned && !throwTarget.Dead)
                {
                    SelfPawn.FaceCellHorizontal(throwTarget.Position);
                }

                throwWindupTicks--;
                if (throwWindupTicks == 24)
                {
                    PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_BoneSpikeCharge);
                }
                if (throwWindupTicks == 0)
                {
                    LaunchBoneSpikeProjectile(throwTarget);
                    throwTarget = null;
                    if (throwRecoverTicks <= 0)
                    {
                        SelfPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
                return;
            }

            if (throwRecoverTicks > 0)
            {
                if (SelfPawn.pather?.Moving == true) SelfPawn.pather.StopDead();
                throwRecoverTicks--;
                if (throwRecoverTicks == 0)
                {
                    SelfPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                }
                return;
            }

            if (specialCheckCooldown <= 0)
            {
                Pawn clawTarget = boneSpikeCooldown <= 0 ? FindClawShotTarget(Props.boneSpikeMaxRange) : null;
                Pawn closeTarget = goodbyeCooldown <= 0 ? FindNearestHostileTarget(Props.goodbyeRadius) : null;
                if (clawTarget != null || closeTarget != null)
                {
                    specialCheckCooldown = Props.specialCheckIntervalTicks;
                    if (Rand.Chance(specialAttackChance))
                    {
                        bool useClaw = (clawTarget != null && closeTarget != null)
                            ? Rand.Chance(Props.specialBoneSpikeShare)
                            : (clawTarget != null);

                        specialAttackChance = Props.specialBaseChance;
                        if (useClaw)
                        {
                            StartBoneSpikeThrow(clawTarget);
                        }
                        else
                        {
                            StartGoodbyeWindup(closeTarget);
                        }
                        return;
                    }
                }
            }

            if (cleaveCooldown <= 0)
            {
                Pawn meleeTarget = FindNearestHostileTarget(Props.cleaveRadius);
                if (meleeTarget != null)
                {
                    ExecuteHeavyCleave(meleeTarget);
                }
            }
        }

        public void StartGoodbyeWindup(Pawn target)
        {
            throwWindupTicks = 0;
            throwRecoverTicks = 0;
            throwTarget = null;
            attackAnimTicks = 0;
            goodbyeTarget = target;
            goodbyeWindupTicks = Props.goodbyeWindupTicks;
            goodbyeCooldown = Props.goodbyeCooldownTicks;
            if (SelfPawn?.pather?.Moving == true) SelfPawn.pather.StopDead();
            SelfPawn?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            if (target != null)
            {
                SelfPawn?.FaceCellHorizontal(target.Position);
            }

            ThrowMoteText("LC_NothingThere_Goodbye".Translate(), new Color(0.95f, 0.1f, 0.12f));
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_GoodbyeCast);
            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            SelfPawn?.PlayGoodbyeAnimation();
        }

        public void ExecuteGoodbyeSlash()
        {
            Map map = SelfPawn?.MapHeld;
            IntVec3 pos = SelfPawn?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            if (SelfPawn?.pather?.Moving == true) SelfPawn.pather.StopDead();
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_GoodbyeFinish);
            AbnormalityUtility.Burst(pos, map, 2.2f);

            DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            int hitCount = ApplyAreaDamage(Props.goodbyeRadius, slashDef, new FloatRange(Props.goodbyeDamage, Props.goodbyeDamage), 0.85f);

            if (hitCount > 0 && Props.goodbyeSelfHeal > 0f)
            {
                EffectUtil.HealPawnInjuries(SelfPawn, Props.goodbyeSelfHeal, showFeedback: false);
            }

            goodbyeSlashTicks = Props.goodbyeSlashTicks;
            goodbyeTarget = null;
            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private void ExecuteHeavyCleave(Pawn target)
        {
            cleaveCooldown = Props.cleaveCooldownTicks;
            attackAnimTicks = 48;
            SelfPawn?.FaceCellHorizontal(target.Position);
            SelfPawn?.PlayMeleeAnimation();
            DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            ApplyAreaDamage(Props.cleaveRadius, slashDef, Props.cleaveDamageRange, 0.55f);
            specialAttackChance = Mathf.Min(1f, specialAttackChance + Props.specialChanceStep);
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Attack);
            if (target.MapHeld != null && target.PositionHeld.InBounds(target.MapHeld))
            {
                AbnormalityUtility.Burst(target.PositionHeld, target.MapHeld);
            }
        }

        public void StartBoneSpikeThrow(Pawn target)
        {
            boneSpikeCooldown = Props.boneSpikeCooldownTicks;
            throwWindupTicks = Props.throwWindupTicks;
            throwRecoverTicks = Props.throwRecoverTicks;
            throwTarget = target;
            if (SelfPawn?.pather?.Moving == true) SelfPawn.pather.StopDead();
            SelfPawn?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            if (target != null)
            {
                SelfPawn?.FaceCellHorizontal(target.Position);
            }

            ThrowMoteText("LC_NothingThere_Voice_Hello".Translate(), new Color(0.95f, 0.2f, 0.2f));
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_BoneSpikeCast);
            SelfPawn?.PlayThrowAnimation();
        }

        private int ApplyAreaDamage(float radius, DamageDef dmgDef, FloatRange damageRange, float armorPen)
        {
            Map map = SelfPawn?.MapHeld;
            IntVec3 pos = SelfPawn?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map))
            {
                return 0;
            }

            float radiusSq = radius * radius;
            List<Pawn> targets = new List<Pawn>();
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn candidate = spawned[i];
                if (IsValidHostileCandidate(candidate) && candidate.PositionHeld.DistanceToSquared(pos) <= radiusSq)
                {
                    targets.Add(candidate);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Pawn t = targets[i];
                if (!t.Dead)
                {
                    t.TakeDamage(new DamageInfo(dmgDef, damageRange.RandomInRange, armorPen, instigator: SelfPawn));
                }
            }

            return targets.Count;
        }

        public void LaunchBoneSpikeProjectile(Pawn target)
        {
            if (SelfPawn == null || !SelfPawn.Spawned || SelfPawn.Map == null)
            {
                return;
            }

            if (target == null || !target.Spawned || target.Dead || target.Map != SelfPawn.Map)
            {
                target = FindClawShotTarget(Props.boneSpikeMaxRange);
            }

            ThingDef projDef = Def.ThingDefOf.NothingThere_BoneSpikeProj;
            if (projDef == null || target == null)
            {
                return;
            }

            Vector3 dir = target.DrawPos - SelfPawn.DrawPos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
            }
            else
            {
                dir = SelfPawn.IsFacingLeft ? Vector3.left : Vector3.right;
            }

            IntVec3 farCell = ComputeClawPierceEndCell(SelfPawn.Map, SelfPawn.Position, target.Position, dir, Props.boneSpikeMaxRange);
            Vector3 launchPos = SelfPawn.DrawPos + dir * 0.35f + new Vector3(0f, 0f, 0.15f);
            AbnormalityUtility.Burst(SelfPawn.Position, SelfPawn.Map, 0.6f);
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_BoneSpikeLaunch);

            Projectile projectile = (Projectile)GenSpawn.Spawn(projDef, SelfPawn.Position, SelfPawn.Map);
            projectile?.Launch(
                SelfPawn,
                launchPos,
                new LocalTargetInfo(farCell),
                new LocalTargetInfo(farCell),
                ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld,
                preventFriendlyFire: false);
        }

        private static IntVec3 ComputeClawPierceEndCell(Map map, IntVec3 origin, IntVec3 targetCell, Vector3 dir, float maxRange)
        {
            Vector3 start = origin.ToVector3Shifted();
            IntVec3 bestCell = origin;
            int steps = Mathf.Max(1, Mathf.RoundToInt(maxRange));

            for (int d = 1; d <= steps; d++)
            {
                IntVec3 cell = (start + dir * d).ToIntVec3();
                if (!cell.InBounds(map))
                {
                    break;
                }

                bestCell = cell;
                Building edifice = cell.GetEdifice(map);
                if (cell.Impassable(map) || (edifice != null && edifice.def.Fillage == FillCategory.Full && !(edifice is Building_Door { Open: true })))
                {
                    break;
                }
            }

            if (bestCell == origin && targetCell.IsValid && targetCell.InBounds(map))
            {
                return targetCell;
            }

            return bestCell;
        }

        private void KillAndStealSkinContained(Pawn victim)
        {
            if (victim != null && !victim.Dead)
            {
                DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
                DamageInfo fatal = new DamageInfo(slashDef, 9999f, 1f, instigator: SelfPawn);
                victim.Kill(fatal);
            }

            EnterContainedDisguise(victim);
        }

        private void EnterContainedDisguise(Pawn victim)
        {
            if (victim == null || (HasDisguise && disguiseInfo?.victimPawn == victim))
            {
                return;
            }

            CaptureVictimDisguise(victim);
            ThrowMoteText("LC_NothingThere_Voice_ILoveYou".Translate(), new Color(0.95f, 0.25f, 0.25f));
            PlayOneShotIfSpawned(Def.SoundDefOf.NothingThere_Form1Voice);
            SelfPawn?.UpdateAnimation(force: true);

            Find.LetterStack.ReceiveLetter(
                "LC_NothingThere_SkinStolenContainedLabel".Translate(),
                "LC_NothingThere_SkinStolenContainedText".Translate(victim.LabelShort),
                LetterDefOf.ThreatSmall,
                SelfPawn);
        }

        private void KillAndStealSkinAndBreach(Pawn victim)
        {
            if (victim != null && !victim.Dead)
            {
                DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
                DamageInfo fatal = new DamageInfo(slashDef, 9999f, 1f, instigator: SelfPawn);
                victim.Kill(fatal);
            }

            if (victim != null)
            {
                CaptureVictimDisguise(victim);
                Find.LetterStack.ReceiveLetter(
                    "LC_NothingThere_SkinStolenLabel".Translate(),
                    "LC_NothingThere_SkinStolenText".Translate(victim.LabelShort),
                    LetterDefOf.ThreatBig,
                    SelfPawn);
            }

            QliphothCountCurrent = 0;
        }

        public void CaptureVictimDisguise(Pawn victim)
        {
            if (victim == null)
            {
                return;
            }

            disguiseInfo ??= new DisguiseInfo();
            disguiseInfo.victimPawn = victim;
            disguiseInfo.victimName = victim.LabelShortCap;
            disguiseInfo.hasDisguise = true;
            totalVictimsConsumed++;
            DestroyDisguiseMaterial();
            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        /// <summary>为一阶段伪装形态获取或刷新被窃皮殖民者的立绘材质（禁止使用 hideFlags）。</summary>
        public Material GetOrCreateDisguiseMaterial()
        {
            if (!HasDisguise || disguiseInfo?.victimPawn == null)
            {
                return null;
            }

            if (!UnityData.IsInMainThread)
            {
                return disguiseMaterial;
            }

            Pawn target = disguiseInfo.victimPawn;
            RenderTexture portraitTex = PortraitsCache.Get(
                target,
                new Vector2(128f, 128f),
                Rot4.South,
                cameraOffset: Vector3.zero,
                cameraZoom: 1.0f,
                supersample: false,
                compensateForUIScale: false,
                renderHeadgear: true,
                renderClothes: true,
                healthStateOverride: PawnHealthState.Mobile);

            if (portraitTex == null)
            {
                return null;
            }

            if (disguiseMaterial == null || disguiseMaterialPawn != target)
            {
                DestroyDisguiseMaterial();
                disguiseMaterial = new Material(ShaderDatabase.Transparent);
                disguiseMaterialPawn = target;
            }

            disguiseMaterial.mainTexture = portraitTex;
            disguiseMaterial.color = new Color(0.98f, 0.88f, 0.88f, 1f);
            InvisibilityMatPool.GetInvisibleMat(disguiseMaterial);
            return disguiseMaterial;
        }

        private void DestroyDisguiseMaterial()
        {
            if (disguiseMaterial != null)
            {
                Object.Destroy(disguiseMaterial);
                disguiseMaterial = null;
            }
            disguiseMaterialPawn = null;
        }

        private void HealAllInjuries()
        {
            EffectUtil.RestoreAllInjuriesAndMissingParts(SelfPawn);
        }

        private void SyncFormBuffs()
        {
            SyncSingleBuff(Def.HediffDefOf.LC_NothingThere_Form1Buff, currentForm == NothingThereForm.Form1_Disguised);
            SyncSingleBuff(Def.HediffDefOf.LC_NothingThere_TrueFormBuff, currentForm == NothingThereForm.Form3_TrueForm);
        }

        private void SyncSingleBuff(HediffDef buffDef, bool shouldHave)
        {
            if (buffDef == null || SelfPawn?.health?.hediffSet == null)
            {
                return;
            }

            Hediff existing = SelfPawn.health.hediffSet.GetFirstHediffOfDef(buffDef);
            if (shouldHave && existing == null)
            {
                SelfPawn.health.AddHediff(buffDef);
            }
            else if (!shouldHave && existing != null)
            {
                SelfPawn.health.RemoveHediff(existing);
            }
        }

        private bool IsValidHostileCandidate(Pawn candidate)
        {
            return candidate != null
                && candidate != SelfPawn
                && candidate.Spawned
                && !candidate.Dead
                && candidate.Faction != SelfPawn.Faction;
        }

        private Pawn FindNearestHostileTarget(float maxRadius)
        {
            Map map = SelfPawn?.MapHeld;
            IntVec3 pos = SelfPawn?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map))
            {
                return null;
            }

            float maxSq = maxRadius * maxRadius;
            float bestSq = float.MaxValue;
            Pawn best = null;
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn c = spawned[i];
                if (!IsValidHostileCandidate(c) || c.Downed)
                {
                    continue;
                }

                float dSq = c.PositionHeld.DistanceToSquared(pos);
                if (dSq <= maxSq && dSq < bestSq)
                {
                    bestSq = dSq;
                    best = c;
                }
            }

            return best;
        }

        private Pawn FindClawShotTarget(float maxRange)
        {
            Map map = SelfPawn?.MapHeld;
            IntVec3 pos = SelfPawn?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map))
            {
                return null;
            }

            float maxSq = maxRange * maxRange;
            float bestSq = float.MaxValue;
            Pawn best = null;
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn c = spawned[i];
                if (!IsValidHostileCandidate(c) || c.Downed)
                {
                    continue;
                }

                float dSq = c.PositionHeld.DistanceToSquared(pos);
                if (dSq <= maxSq && dSq < bestSq && (c.PositionHeld == pos || GenSight.LineOfSight(pos, c.PositionHeld, map)))
                {
                    bestSq = dSq;
                    best = c;
                }
            }

            return best;
        }

        private void PlayOneShotIfSpawned(SoundDef sound)
        {
            if (sound != null && SelfPawn != null && SelfPawn.Spawned && SelfPawn.Map != null)
            {
                sound.PlayOneShot(SoundInfo.InMap(SelfPawn));
            }
        }

        private void ThrowMoteText(string text, Color color)
        {
            if (SelfPawn?.Map != null && SelfPawn.DrawPos.ShouldSpawnMotesAt(SelfPawn.Map))
            {
                MoteMaker.ThrowText(SelfPawn.DrawPos + new Vector3(0f, 0f, 0.6f), SelfPawn.Map, text, color, 2.2f);
            }
        }

        #endregion 三阶段出逃与战斗逻辑

        #region 持久化与 UI

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref currentForm, "currentForm", NothingThereForm.Contained);
            Scribe_Deep.Look(ref disguiseInfo, "disguiseInfo");
            Scribe_Values.Look(ref formTicks, "formTicks", 0);
            Scribe_Values.Look(ref cleaveCooldown, "cleaveCooldown", 0);
            Scribe_Values.Look(ref boneSpikeCooldown, "boneSpikeCooldown", 0);
            Scribe_Values.Look(ref goodbyeCooldown, "goodbyeCooldown", 0);
            Scribe_Values.Look(ref goodbyeWindupTicks, "goodbyeWindupTicks", 0);
            Scribe_Values.Look(ref goodbyeSlashTicks, "goodbyeSlashTicks", 0);
            Scribe_References.Look(ref goodbyeTarget, "goodbyeTarget");
            Scribe_Values.Look(ref studyingGraceTicks, "studyingGraceTicks", 0);
            Scribe_Values.Look(ref attackAnimTicks, "attackAnimTicks", 0);
            Scribe_Values.Look(ref useDogAttackVariant2, "useDogAttackVariant2", false);
            Scribe_Values.Look(ref throwWindupTicks, "throwWindupTicks", 0);
            Scribe_Values.Look(ref throwRecoverTicks, "throwRecoverTicks", 0);
            Scribe_References.Look(ref throwTarget, "throwTarget");
            Scribe_Values.Look(ref specialAttackChance, "specialAttackChance", 0.40f);
            Scribe_Values.Look(ref specialCheckCooldown, "specialCheckCooldown", 150);
            Scribe_Values.Look(ref lastDamagedTick, "lastDamagedTick", -99999);
            Scribe_Values.Look(ref totalVictimsConsumed, "totalVictimsConsumed", 0);
            Scribe_Values.Look(ref lowStatWarningSent, "lowStatWarningSent", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && disguiseInfo == null)
            {
                disguiseInfo = new DisguiseInfo();
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("LC_NothingThere_Biosignature".Translate()).Append(": ").Append(BiosignatureName);

            string formKey = currentForm switch
            {
                NothingThereForm.Form1_Disguised => "LC_NothingThere_Inspect_Form1",
                NothingThereForm.Form2_Cocoon => "LC_NothingThere_Inspect_Form2",
                NothingThereForm.Form3_TrueForm => "LC_NothingThere_Inspect_Form3",
                _ => HasDisguise ? "LC_NothingThere_Inspect_ContainedDisguised" : "LC_NothingThere_Inspect_Contained"
            };
            sb.AppendLine().Append(formKey.Translate());

            string resistStr = currentForm switch
            {
                NothingThereForm.Form2_Cocoon => "R 0.0 | W 0.6 | B 0.6 | P 1.0",
                NothingThereForm.Form3_TrueForm => "R 0.0 | W 0.4 | B 0.4 | P 0.8",
                _ => "R 0.3 | W 0.8 | B 0.8 | P 1.2"
            };
            sb.AppendLine().Append(resistStr);

            if ((currentForm == NothingThereForm.Contained || currentForm == NothingThereForm.Form1_Disguised)
                && HasDisguise && !disguiseInfo.victimName.NullOrEmpty())
            {
                sb.AppendLine().Append("LC_NothingThere_Inspect_DisguiseVictim".Translate(disguiseInfo.victimName));
            }
            else if (currentForm == NothingThereForm.Form2_Cocoon)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(100f * formTicks / Mathf.Max(1, CocoonHatchTicksRequired)), 0, 100);
                sb.AppendLine().Append("LC_NothingThere_Inspect_HatchProgress".Translate(pct));
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Form 1",
                action = delegate
                {
                    if (!Escaped) QliphothCountCurrent = 0;
                    TransitionToForm1();
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "DEV: Form 2",
                action = delegate
                {
                    if (!Escaped) QliphothCountCurrent = 0;
                    TransitionToForm2();
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "DEV: Form 3",
                action = delegate
                {
                    if (!Escaped) QliphothCountCurrent = 0;
                    TransitionToForm3();
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "DEV: Goodbye Slash",
                action = delegate
                {
                    if (!Escaped) QliphothCountCurrent = 0;
                    if (currentForm != NothingThereForm.Form3_TrueForm) TransitionToForm3();
                    ExecuteGoodbyeSlash();
                }
            };
        }

        #endregion 持久化与 UI
    }
}
