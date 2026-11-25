using System.Collections.Generic;
using LCAbnormalities.Settings;
using LCAnomalyCore.Comp;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.WeCanChangeAnything
{
    /// <summary>T-09-85 我们可以改变一切组件属性。</summary>
    public class CompProperties_WeCanChangeAnything : CompProperties_AbnormalityTool
    {
        public float stage1Damage = 5f;
        public float stage2Damage = 9f;
        public float stage3Damage = 14f;
        public int stage2StartSeconds = 10;
        public int stage3StartSeconds = 25;
        public int deathBurstBoxes = 10;
        public float lethalExecuteHealthThreshold = 0.2f;

        public CompProperties_WeCanChangeAnything()
        {
            compClass = typeof(CompWeCanChangeAnything);
        }
    }

    /// <summary>
    /// T-09-85 我们可以改变一切核心逻辑：
    /// 使用者步入铁处女腔室后承受随时间递增的物理挤压，同时源源不断转化并吐出脑啡肽箱（EnkephalinBox）；
    /// 驱动完整的 Spine 对齐多部件动画状态机（待机呼吸 → 关门亮灯 → 1/2/3 阶段递增摇晃、渗血与像素闪烁 → 开门血雾爆发与染血内腔）。
    /// </summary>
    public class CompWeCanChangeAnything : CompAbnormalityTool
    {
        public const int StartAnimDurationTicks = 60;
        public const int EndAnimDurationTicks = 68;

        private int totalBoxesProduced;
        private int totalVictims;
        private bool stainedWithBlood;

        private int boxTickCounter;
        private bool victimProcessedThisSession;
        private int startAnimTicksRemaining;
        private int endAnimTicksRemaining;

        public CompProperties_WeCanChangeAnything PropsIron => (CompProperties_WeCanChangeAnything)props;

        public int TotalBoxesProduced => totalBoxesProduced;

        public int TotalVictims => totalVictims;

        public bool StainedWithBlood => stainedWithBlood;

        public bool IsPlayingStartAnim => startAnimTicksRemaining > 0;

        public bool IsPlayingEndAnim => endAnimTicksRemaining > 0;

        /// <summary>当前工作阶段：0=未工作，1=一阶段（慢摇无血），2=二阶段（双连摇晃+缝隙渗血），3=三阶段（剧烈狂摇+大量喷血+眼心狂闪）。</summary>
        public int CurrentWorkStage
        {
            get
            {
                if (!IsActiveContinuous)
                {
                    return 0;
                }

                float sec = CurrentSessionSeconds;
                if (sec >= PropsIron.stage3StartSeconds)
                {
                    return 3;
                }

                if (sec >= PropsIron.stage2StartSeconds)
                {
                    return 2;
                }

                return 1;
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            UpdateAnimation(force: true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public override void CompTick()
        {
            TickTransitionTimers();
            base.CompTick();
            UpdateAnimation();
        }

        public override void TickWhileHeld()
        {
            TickTransitionTimers();
            base.TickWhileHeld();
            UpdateAnimation();
        }

        private void TickTransitionTimers()
        {
            if (startAnimTicksRemaining > 0)
            {
                startAnimTicksRemaining--;
            }

            if (endAnimTicksRemaining > 0)
            {
                endAnimTicksRemaining--;
            }
        }

        public override bool CanManuallyStopContinuous(Pawn user)
        {
            return LCAbnormalitiesMod.Settings?.WeCanChangeAnythingAllowCancel ?? false;
        }

        public override void OnContinuousStart(Pawn user)
        {
            base.OnContinuousStart(user);
            boxTickCounter = 0;
            victimProcessedThisSession = false;
            startAnimTicksRemaining = StartAnimDurationTicks;
            endAnimTicksRemaining = 0;

            UpdateAnimation(force: true);

            if (IsOperational && ActiveMap != null && ActivePosition.IsValid)
            {
                IronMaidenSoundDefOf.LC_IronMaiden_Start?.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
            }
        }

        public override void OnContinuousTick(Pawn user, int intervalTicks, int sessionTicks)
        {
            base.OnContinuousTick(user, intervalTicks, sessionTicks);

            Map map = ActiveMap;
            if (map == null || victimProcessedThisSession)
            {
                return;
            }

            float sec = sessionTicks / 60f;
            int stage = 1;
            float dmg = PropsIron.stage1Damage;
            int boxes = (boxTickCounter % 2 == 0) ? 1 : 0;

            if (sec >= PropsIron.stage3StartSeconds)
            {
                stage = 3;
                dmg = PropsIron.stage3Damage;
                boxes = 2;
            }
            else if (sec >= PropsIron.stage2StartSeconds)
            {
                stage = 2;
                dmg = PropsIron.stage2Damage;
                boxes = 1;
            }

            boxTickCounter++;

            if (user != null && !user.Dead && !user.Destroyed)
            {
                user.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    dmg,
                    armorPenetration: 2f,
                    instigator: parent));

                if (!CanManuallyStopContinuous(user)
                    && !user.Dead
                    && !user.Destroyed
                    && (user.Downed || (user.health?.summaryHealth?.SummaryHealthPercent ?? 1f) <= PropsIron.lethalExecuteHealthThreshold))
                {
                    user.Kill(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        999f,
                        armorPenetration: 2f,
                        instigator: parent));
                }
            }

            if (boxes > 0)
            {
                SpawnEnkephalinBoxes(map, boxes);
            }

            if (IsOperational && ActivePosition.IsValid)
            {
                // Stage 1: every 2s digital crunch; Stage 2 & 3: accelerating pulse every tick interval
                if (stage >= 2 || boxTickCounter % 2 == 1)
                {
                    IronMaidenSoundDefOf.LC_IronMaiden_Generate?.PlayOneShot(new TargetInfo(ActivePosition, map));
                }

                float bloodChance = stage >= 3 ? 0.65f : (stage == 2 ? 0.45f : 0.25f);
                if (Rand.Chance(bloodChance) && RimWorld.ThingDefOf.Filth_Blood != null)
                {
                    FilthMaker.TryMakeFilth(ActivePosition, map, RimWorld.ThingDefOf.Filth_Blood);
                }

                if (stage >= 2)
                {
                    SpawnBloodMistPuffs(map, ActiveDrawPos, stage >= 3 ? 3 : 1, burst: false);
                }
            }

            if (user == null || user.Dead || user.Destroyed)
            {
                HandleVictimDeath(user, map);
            }
        }

        public override void OnContinuousEnd(Pawn user, int sessionTicks, bool completedNaturally)
        {
            if (sessionTicks > 0 && !victimProcessedThisSession && (user == null || user.Dead || user.Destroyed))
            {
                HandleVictimDeath(user, ActiveMap);
            }

            base.OnContinuousEnd(user, sessionTicks, completedNaturally);

            startAnimTicksRemaining = 0;
            if (sessionTicks > 0 && endAnimTicksRemaining <= 0)
            {
                endAnimTicksRemaining = EndAnimDurationTicks;
            }

            UpdateAnimation(force: true);
        }

        private void HandleVictimDeath(Pawn victim, Map map)
        {
            if (victimProcessedThisSession)
            {
                return;
            }

            victimProcessedThisSession = true;
            stainedWithBlood = true;
            totalVictims++;
            startAnimTicksRemaining = 0;
            endAnimTicksRemaining = EndAnimDurationTicks;

            if (map != null)
            {
                if (PropsIron.deathBurstBoxes > 0)
                {
                    SpawnEnkephalinBoxes(map, PropsIron.deathBurstBoxes);
                }

                if (IsOperational && ActivePosition.IsValid)
                {
                    IronMaidenSoundDefOf.LC_IronMaiden_End?.PlayOneShot(new TargetInfo(ActivePosition, map));
                    SpawnBloodMistPuffs(map, ActiveDrawPos, count: 7, burst: true);
                    if (RimWorld.ThingDefOf.Filth_Blood != null)
                    {
                        FilthMaker.TryMakeFilth(ActivePosition, map, RimWorld.ThingDefOf.Filth_Blood, count: 2);
                    }
                }
            }

            string victimLabel = victim != null ? victim.LabelShortCap : parent.LabelCap;
            Messages.Message(
                "LC_WeCanChangeAnything_VictimConverted".Translate(victimLabel, PropsIron.deathBurstBoxes),
                InteractionTarget ?? parent,
                MessageTypeDefOf.NeutralEvent);

            RequestStopContinuous();
        }

        private static void SpawnBloodMistPuffs(Map map, Vector3 center, int count, bool burst)
        {
            if (map == null || !center.ToIntVec3().InBounds(map))
            {
                return;
            }

            FleckDef puffDef = burst ? FleckDefOf.DustPuffThick : FleckDefOf.AirPuff;
            if (puffDef == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                FleckCreationData data = FleckMaker.GetDataStatic(
                    center + new Vector3(Rand.Range(-0.28f, 0.28f), 0f, Rand.Range(-0.18f, 0.32f)),
                    map,
                    puffDef,
                    burst ? Rand.Range(1.45f, 2.35f) : Rand.Range(0.75f, 1.25f));
                data.instanceColor = burst
                    ? new Color(0.76f, 0.06f, 0.08f, 0.88f)
                    : new Color(0.72f, 0.05f, 0.07f, 0.65f);
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = burst ? Rand.Range(0.65f, 1.55f) : Rand.Range(0.25f, 0.65f);
                data.rotationRate = Rand.Range(-45f, 45f);
                map.flecks.CreateFleck(data);
            }
        }

        /// <summary>根据当前收容/工作/阶段状态同步铁处女多部件骨骼动画。</summary>
        public void UpdateAnimation(bool force = false)
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.Drawer?.renderer == null)
            {
                return;
            }

            AnimationDef desired = ResolveDesiredAnimation();
            if (desired != null && (force || pawn.Drawer.renderer.CurAnimation != desired))
            {
                pawn.Drawer.renderer.SetAnimation(desired);
            }
        }

        public AnimationDef ResolveDesiredAnimation()
        {
            if (IsActiveContinuous)
            {
                if (startAnimTicksRemaining > 0)
                {
                    return stainedWithBlood
                        ? WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_After
                        : WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_First;
                }

                switch (CurrentWorkStage)
                {
                    case 3:
                        return WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_03;
                    case 2:
                        return WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_02;
                    default:
                        return WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_01;
                }
            }

            if (endAnimTicksRemaining > 0)
            {
                return WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_End;
            }

            return stainedWithBlood
                ? WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Default_After
                : WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Default_First;
        }

        private void SpawnEnkephalinBoxes(Map map, int count)
        {
            ThingDef boxDef = LCAnomalyCore.Defs.ThingDefOf.EnkephalinBox;
            IntVec3 dropPos = ActivePosition;
            if (boxDef == null || map == null || count <= 0 || !dropPos.IsValid)
            {
                return;
            }

            Thing boxes = ThingMaker.MakeThing(boxDef);
            boxes.stackCount = count;
            if (GenPlace.TryPlaceThing(boxes, dropPos, map, ThingPlaceMode.Near))
            {
                totalBoxesProduced += count;
            }
            else if (!boxes.Destroyed)
            {
                boxes.Destroy();
            }
        }

        public override string GetToolStatusInspectString()
        {
            return "LC_WeCanChangeAnything_InspectStats".Translate(totalBoxesProduced, totalVictims);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref totalBoxesProduced, "ironMaidenBoxesProduced", 0);
            Scribe_Values.Look(ref totalVictims, "ironMaidenVictims", 0);
            Scribe_Values.Look(ref stainedWithBlood, "ironMaidenBlood", false);
            Scribe_Values.Look(ref startAnimTicksRemaining, "ironMaidenStartAnimTicks", 0);
            Scribe_Values.Look(ref endAnimTicksRemaining, "ironMaidenEndAnimTicks", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                UpdateAnimation(force: true);
            }
        }
    }

    [DefOf]
    public static class IronMaidenSoundDefOf
    {
        public static SoundDef LC_IronMaiden_Start;
        public static SoundDef LC_IronMaiden_Generate;
        public static SoundDef LC_IronMaiden_End;

        static IronMaidenSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IronMaidenSoundDefOf));
        }
    }

    [DefOf]
    public static class WeCanChangeAnythingAnimDefOf
    {
        public static AnimationDef WeCanChangeAnything_Default_First;
        public static AnimationDef WeCanChangeAnything_Default_After;
        public static AnimationDef WeCanChangeAnything_Start_First;
        public static AnimationDef WeCanChangeAnything_Start_After;
        public static AnimationDef WeCanChangeAnything_Generate_01;
        public static AnimationDef WeCanChangeAnything_Generate_02;
        public static AnimationDef WeCanChangeAnything_Generate_03;
        public static AnimationDef WeCanChangeAnything_End;

        static WeCanChangeAnythingAnimDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WeCanChangeAnythingAnimDefOf));
        }
    }
}
