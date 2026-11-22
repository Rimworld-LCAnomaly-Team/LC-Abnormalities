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
    /// 默认设置下进入后即无法主动脱离，直至彻底榨干并爆发出最终能量箱洪流。
    /// </summary>
    public class CompWeCanChangeAnything : CompAbnormalityTool
    {
        private int totalBoxesProduced;
        private int totalVictims;
        private bool stainedWithBlood;

        private int boxTickCounter;
        private bool victimProcessedThisSession;

        public CompProperties_WeCanChangeAnything PropsIron => (CompProperties_WeCanChangeAnything)props;

        public int TotalBoxesProduced => totalBoxesProduced;

        public int TotalVictims => totalVictims;

        public bool StainedWithBlood => stainedWithBlood;

        public override bool CanManuallyStopContinuous(Pawn user)
        {
            return LCAbnormalitiesMod.Settings?.WeCanChangeAnythingAllowCancel ?? false;
        }

        public override void OnContinuousStart(Pawn user)
        {
            base.OnContinuousStart(user);
            boxTickCounter = 0;
            victimProcessedThisSession = false;

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
            float dmg = PropsIron.stage1Damage;
            int boxes = (boxTickCounter % 2 == 0) ? 1 : 0;

            if (sec >= PropsIron.stage3StartSeconds)
            {
                dmg = PropsIron.stage3Damage;
                boxes = 2;
            }
            else if (sec >= PropsIron.stage2StartSeconds)
            {
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
                if (boxTickCounter % 2 == 1)
                {
                    IronMaidenSoundDefOf.LC_IronMaiden_Generate?.PlayOneShot(new TargetInfo(ActivePosition, map));
                }

                if (Rand.Chance(0.35f) && RimWorld.ThingDefOf.Filth_Blood != null)
                {
                    FilthMaker.TryMakeFilth(ActivePosition, map, RimWorld.ThingDefOf.Filth_Blood);
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

            if (map != null)
            {
                if (PropsIron.deathBurstBoxes > 0)
                {
                    SpawnEnkephalinBoxes(map, PropsIron.deathBurstBoxes);
                }

                if (IsOperational && ActivePosition.IsValid)
                {
                    IronMaidenSoundDefOf.LC_IronMaiden_End?.PlayOneShot(new TargetInfo(ActivePosition, map));
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

        protected override string GetHeldOverrideTexPath()
        {
            return IsActiveContinuous
                ? "Things/Building/WeCanChangeAnything/WeCanChangeAnything_Active"
                : null;
        }

        protected override Color GetHeldOverrideColor()
        {
            if (!IsActiveContinuous && stainedWithBlood)
            {
                return new Color(1.0f, 0.82f, 0.82f);
            }

            return Color.white;
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
}
