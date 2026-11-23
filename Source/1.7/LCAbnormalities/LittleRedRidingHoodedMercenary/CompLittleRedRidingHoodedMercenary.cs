using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.LittleRedRidingHoodedMercenary
{
    [DefOf]
    public static class LittleRedDefOf
    {
        public static ThingDef LittleRedRidingHoodedMercenaryEgg;
        public static ThingDef Bullet_EgoWeapon_LittleRedRidingHoodedMercenary;

        public static ThingDef LC_LittleRedSlashFX;
        public static ThingDef LC_LittleRedShotgunConeFX;
        public static ThingDef LC_LittleRedHuntMarkFX;
        public static ThingDef LC_LittleRedThrownSickleFX;
        public static ThingDef LC_LittleRedImpactFX;

        public static SoundDef LC_LittleRed_Atk1;
        public static SoundDef LC_LittleRed_Atk2;
        public static SoundDef LC_LittleRed_AtkPower;
        public static SoundDef LC_LittleRed_Change;
        public static SoundDef LC_LittleRed_Gun;
        public static SoundDef LC_LittleRed_Throw;

        public static AnimationDef LittleRed_ContainedIdle;
        public static AnimationDef LittleRed_ReadyOut;
        public static AnimationDef LittleRed_CombatIdle;
        public static AnimationDef LittleRed_Dash;
        public static AnimationDef LittleRed_DashAttack;
        public static AnimationDef LittleRed_AttackAxe;
        public static AnimationDef LittleRed_AttackAxePower;
        public static AnimationDef LittleRed_AttackAxeThrow;
        public static AnimationDef LittleRed_AttackGun;
        public static AnimationDef LittleRed_AttackGunMove;

        static LittleRedDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LittleRedDefOf));
        }
    }

    public enum LittleRedActionState
    {
        None,
        DashAttack,
        AttackAxe,
        AttackAxePower,
        AttackAxeThrow,
        AttackGun,
        AttackGunMove
    }

    public class CompProperties_LittleRedRidingHoodedMercenary : CompProperties_LCAbnormality
    {
        public int hireCostPeBox = 15;
        public int shotgunCooldownTicks = 135;
        public int meleeCooldownTicks = 75;
        public int axeThrowCooldownTicks = 260;
        public float shotgunRange = 14f;
        public float axeThrowMinRange = 4.5f;
        public float axeThrowMaxRange = 14.5f;
        public float shotgunDamage = 20f;
        public float meleeDamage = 26f;
        public float axeThrowDamage = 24f;
        public int hiredIdleReturnTicks = 300;

        public CompProperties_LittleRedRidingHoodedMercenary()
        {
            compClass = typeof(CompLittleRedRidingHoodedMercenary);
        }
    }

    /// <summary>
    /// F-01-57 小红帽雇佣兵核心逻辑：
    /// 逆卡巴拉计数器上限 3；工作结果差或其他异想体突破时计数器 -1，归零则敌对突破；
    /// 在收容状态下可通过平台按钮支付 15 PE-Box 雇佣她临时出击猎杀地图上的敌对目标，结束后可手动召回或自动返回平台。
    /// 完整驱动脑叶公司 Spine 全套骨骼动画（收容待机、出击待机、疾跑、冲刺斩、弯镰斩、强力二连重劈、跨房回旋飞镰、霰弹轰击、移动双连射）
    /// 与专属 GLSL 猩红刀光、霰弹火光锥、猎杀之眼标记特效。
    /// </summary>
    public class CompLittleRedRidingHoodedMercenary : CompLCAbnormality, ILCMapAbnormalityEscapeListener
    {
        // Exact Spine event ticks (60 ticks/sec) baked from RedHood / skeleton #223196.txt
        private const int DashAttackHitTick = 12;
        private const int DashAttackDur = 54;

        private const int AxeHitTick = 36;
        private const int AxeDur = 70;

        private const int AxePowerHit1Tick = 48;
        private const int AxePowerHit2Tick = 88;
        private const int AxePowerDur = 148;

        private const int AxeThrowLaunchTick = 32;
        private const int AxeThrowDur = 120;

        private const int GunFireTick = 42;
        private const int GunDur = 114;

        private const int GunMoveFire1Tick = 20;
        private const int GunMoveFire2Tick = 32;
        private const int GunMoveDur = 76;

        private bool isHired;
        private Pawn hiredTarget;
        private Building_AbnormalityHoldingPlatform homePlatform;
        private int shotgunCooldown;
        private int meleeCooldown;
        private int axeThrowCooldown;
        private int huntVisualTicks;
        private int hiredIdleTicks;

        private LittleRedActionState actionState = LittleRedActionState.None;
        private int actionElapsedTicks;
        private bool actionEvent1Fired;
        private bool actionEvent2Fired;
        private Pawn actionTarget;
        private bool sickleInFlight;

        // Non-saved runtime references
        private Thing_LittleRedHuntMark activeHuntMark;
        private Thing_LittleRedThrownSickle activeThrownSickle;

        public CompProperties_LittleRedRidingHoodedMercenary PropsRed => (CompProperties_LittleRedRidingHoodedMercenary)props;

        public bool IsHired => isHired;

        public Pawn HiredTarget => hiredTarget;

        public bool IsHuntingVisual => isHired || huntVisualTicks > 0;

        public LittleRedActionState CurrentAction => actionState;

        public bool IsBusyInAttackAnimation =>
            actionState == LittleRedActionState.AttackAxe ||
            actionState == LittleRedActionState.AttackAxePower ||
            actionState == LittleRedActionState.AttackAxeThrow ||
            actionState == LittleRedActionState.AttackGun ||
            actionState == LittleRedActionState.DashAttack;

        public bool IsSickleInFlight => sickleInFlight && activeThrownSickle != null && !activeThrownSickle.Destroyed;

        /// <summary>敌对暴走突破或半血以下狂暴时显露兜帽下血红疤面并升级猩红狂暴特效。</summary>
        public bool IsEnragedVisual
        {
            get
            {
                if (Escaped && !isHired)
                {
                    return true;
                }

                if (parent is Pawn p && !p.Dead && p.health?.summaryHealth != null && p.health.summaryHealth.SummaryHealthPercent < 0.50f)
                {
                    return true;
                }

                return false;
            }
        }

        public void NotifyThrownSickleReturned()
        {
            sickleInFlight = false;
            activeThrownSickle = null;
        }

        private void ClearActionState()
        {
            actionState = LittleRedActionState.None;
            actionElapsedTicks = 0;
            actionEvent1Fired = false;
            actionEvent2Fired = false;
            actionTarget = null;
        }

        private void ClearHuntMark()
        {
            if (activeHuntMark != null && !activeHuntMark.Destroyed)
            {
                activeHuntMark.BeginFadeOut();
            }

            activeHuntMark = null;
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            isHired = false;
            hiredTarget = null;
            hiredIdleTicks = 0;
            huntVisualTicks = 0;
            sickleInFlight = false;
            activeThrownSickle = null;
            ClearActionState();
            ClearHuntMark();

            if (parent is Pawn pawn && LCContainmentUtility.AbnormalityFaction != null && pawn.Faction != LCContainmentUtility.AbnormalityFaction)
            {
                pawn.SetFaction(LCContainmentUtility.AbnormalityFaction);
            }

            (parent as LC_LittleRedPawn)?.UpdateAnimation(force: true);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            QliphothCountCurrent--;
        }

        public void NotifyOtherAbnormalityEscaped(CompAbnormality escapedComp)
        {
            if (parent == null || parent.Destroyed || Escaped || isHired || escapedComp == null || escapedComp.parent == parent)
            {
                return;
            }

            if (escapedComp is CompLittleRedRidingHoodedMercenary otherRed && otherRed.IsHired)
            {
                return;
            }

            QliphothCountCurrent--;
            Messages.Message(
                "LC_LittleRed_EscapeCounterDropMsg".Translate(parent.LabelCap, QliphothCountCurrent),
                parent,
                MessageTypeDefOf.NegativeEvent,
                historical: false);
        }

        public override void Notify_Escaped()
        {
            if (isHired)
            {
                escaped = true;
                return;
            }

            base.Notify_Escaped();
            huntVisualTicks = 90;
            ClearActionState();
            LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Change, parent);
            (parent as LC_LittleRedPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            bool wasHired = isHired;
            isHired = false;
            hiredTarget = null;
            homePlatform = null;
            sickleInFlight = false;
            activeThrownSickle = null;
            ClearActionState();
            ClearHuntMark();
            if (wasHired)
            {
                escaped = false;
            }

            base.Notify_Killed(prevMap, dinfo);
        }

        public bool RequestMercenaryHunt(Pawn target)
        {
            if (!(parent is Pawn pawn) || pawn.Destroyed || pawn.Dead || PeBoxComp == null)
            {
                return false;
            }

            if (target == null || target.Dead || target.Destroyed || !target.Spawned || target == pawn)
            {
                return false;
            }

            int cost = PropsRed.hireCostPeBox;
            if (PeBoxComp.CurAmountIndiPebox < cost)
            {
                return false;
            }

            Building_AbnormalityHoldingPlatform plat = parent.ParentHolder as Building_AbnormalityHoldingPlatform;
            if (plat == null || !plat.Spawned || plat.Map == null)
            {
                return false;
            }

            PeBoxComp.CurAmountIndiPebox -= cost;
            homePlatform = plat;
            hiredTarget = target;
            isHired = true;
            escaped = true;
            hiredIdleTicks = 0;
            huntVisualTicks = 90;
            ClearActionState();

            plat.EjectContents();

            if (Faction.OfPlayer != null && pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            EnsureHuntMarkOn(pawn, target);
            LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Change, pawn);
            Messages.Message(
                "LC_LittleRed_HiredMsg".Translate(target.LabelShortCap),
                pawn,
                MessageTypeDefOf.PositiveEvent,
                historical: false);
            (pawn as LC_LittleRedPawn)?.UpdateAnimation(force: true);
            return true;
        }

        public bool RecallFromHunt()
        {
            if (!(parent is Pawn pawn) || pawn.Destroyed || pawn.Dead)
            {
                return false;
            }

            isHired = false;
            hiredTarget = null;
            hiredIdleTicks = 0;
            huntVisualTicks = 0;
            escaped = false;
            sickleInFlight = false;
            activeThrownSickle = null;
            ClearActionState();
            ClearHuntMark();

            if (LCContainmentUtility.AbnormalityFaction != null && pawn.Faction != LCContainmentUtility.AbnormalityFaction)
            {
                pawn.SetFaction(LCContainmentUtility.AbnormalityFaction);
            }

            pawn.jobs?.StopAll(ifLayingKeepLaying: false);

            Building_AbnormalityHoldingPlatform plat = homePlatform;
            homePlatform = null;

            if (plat != null && !plat.Destroyed && plat.Spawned && !plat.Occupied)
            {
                if (pawn.Spawned)
                {
                    pawn.DeSpawn(DestroyMode.Vanish);
                }

                ThingOwner owner = plat.GetDirectlyHeldThings();
                if (owner != null && owner.TryAdd(pawn))
                {
                    HoldingPlaformTargetComp?.Notify_HeldOnPlatform(owner);
                    Notify_Holded();
                    LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Change, plat);
                    return true;
                }
            }

            LCAbnormalitiesHelper.ReturnEntityToEgg(pawn, LittleRedDefOf.LittleRedRidingHoodedMercenaryEgg, biosignature);
            return true;
        }

        private void EnsureHuntMarkOn(Pawn self, Pawn target)
        {
            if (self == null || !self.Spawned || self.Map == null || target == null || target.Dead || target.Destroyed || !target.Spawned || target.Map != self.Map)
            {
                ClearHuntMark();
                return;
            }

            if (activeHuntMark != null && !activeHuntMark.Destroyed)
            {
                if (activeHuntMark.MarkedPrey != target)
                {
                    activeHuntMark.Retarget(target);
                }
                return;
            }

            activeHuntMark = LittleRedVfxUtility.SpawnHuntMark(self.Map, self, target);
        }

        public Pawn FindCombatTarget()
        {
            if (!(parent is Pawn pawn) || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }

            Map map = pawn.Map;
            if (isHired)
            {
                if (hiredTarget != null && !hiredTarget.Dead && !hiredTarget.Destroyed && hiredTarget.Spawned && hiredTarget.Map == map)
                {
                    return hiredTarget;
                }

                Pawn bestHostile = null;
                float bestDistSq = 45f * 45f;
                IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < allPawns.Count; i++)
                {
                    Pawn candidate = allPawns[i];
                    if (candidate == null || candidate == pawn || candidate.Dead || candidate.Downed || !candidate.Spawned)
                    {
                        continue;
                    }

                    if (!candidate.HostileTo(Faction.OfPlayer))
                    {
                        continue;
                    }

                    float dSq = (candidate.Position - pawn.Position).LengthHorizontalSquared;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestHostile = candidate;
                    }
                }

                hiredTarget = bestHostile;
                return bestHostile;
            }

            return LCAbnormalitiesHelper.FindNearestHumanlike(map, pawn.Position, 65f, p => p != pawn);
        }

        public override void TickContained()
        {
            base.TickContained();
            (parent as LC_LittleRedPawn)?.UpdateAnimation();
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!(parent is LC_LittleRedPawn pawn) || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (shotgunCooldown > 0) shotgunCooldown--;
            if (meleeCooldown > 0) meleeCooldown--;
            if (axeThrowCooldown > 0) axeThrowCooldown--;
            if (huntVisualTicks > 0) huntVisualTicks--;

            // Guard stale thrown sickle flag after save/load
            if (sickleInFlight && (activeThrownSickle == null || activeThrownSickle.Destroyed))
            {
                sickleInFlight = false;
                activeThrownSickle = null;
            }

            Pawn target = FindCombatTarget();
            if (isHired && target == null && actionState == LittleRedActionState.None)
            {
                ClearHuntMark();
                hiredIdleTicks++;
                if (hiredIdleTicks >= PropsRed.hiredIdleReturnTicks)
                {
                    RecallFromHunt();
                }
                else
                {
                    pawn.UpdateFacingFromMotionOrTarget(null);
                    pawn.UpdateAnimation();
                }
                return;
            }

            hiredIdleTicks = 0;
            if (target != null)
            {
                EnsureHuntMarkOn(pawn, target);
            }
            else
            {
                ClearHuntMark();
            }

            // Advance active attack animation & fire exact Spine Damage/Throw events
            if (actionState != LittleRedActionState.None)
            {
                TickActiveAction(pawn, target);
                pawn.UpdateFacingFromMotionOrTarget(actionTarget ?? target);
                pawn.UpdateAnimation();
                return;
            }

            if (target != null)
            {
                Map map = pawn.Map;
                float dist = pawn.Position.DistanceTo(target.Position);
                bool hasLoS = GenSight.LineOfSight(pawn.Position, target.Position, map);

                if (dist <= 2.6f && meleeCooldown <= 0 && !IsSickleInFlight)
                {
                    bool wasMoving = pawn.pather?.Moving == true;
                    LittleRedActionState chosen;
                    if (wasMoving && Rand.Chance(0.35f))
                    {
                        chosen = LittleRedActionState.DashAttack;
                    }
                    else if (Rand.Chance(IsEnragedVisual ? 0.42f : 0.28f))
                    {
                        chosen = LittleRedActionState.AttackAxePower;
                    }
                    else
                    {
                        chosen = LittleRedActionState.AttackAxe;
                    }

                    BeginAction(pawn, chosen, target);
                }
                else if (dist >= PropsRed.axeThrowMinRange && dist <= PropsRed.axeThrowMaxRange && axeThrowCooldown <= 0 && !IsSickleInFlight && hasLoS && Rand.Chance(0.55f))
                {
                    BeginAction(pawn, LittleRedActionState.AttackAxeThrow, target);
                }
                else if (dist > 2.6f && dist <= PropsRed.shotgunRange && shotgunCooldown <= 0 && hasLoS)
                {
                    LittleRedActionState gunState = (pawn.pather?.Moving == true && Rand.Chance(0.55f))
                        ? LittleRedActionState.AttackGunMove
                        : LittleRedActionState.AttackGun;
                    BeginAction(pawn, gunState, target);
                }
            }

            pawn.UpdateFacingFromMotionOrTarget(target);
            pawn.UpdateAnimation();
        }

        private void BeginAction(LC_LittleRedPawn self, LittleRedActionState state, Pawn target)
        {
            actionState = state;
            actionElapsedTicks = 0;
            actionEvent1Fired = false;
            actionEvent2Fired = false;
            actionTarget = target;
            huntVisualTicks = 60;

            switch (state)
            {
                case LittleRedActionState.DashAttack:
                    meleeCooldown = PropsRed.meleeCooldownTicks;
                    break;
                case LittleRedActionState.AttackAxe:
                    meleeCooldown = PropsRed.meleeCooldownTicks;
                    break;
                case LittleRedActionState.AttackAxePower:
                    meleeCooldown = PropsRed.meleeCooldownTicks + 45;
                    break;
                case LittleRedActionState.AttackAxeThrow:
                    axeThrowCooldown = PropsRed.axeThrowCooldownTicks;
                    break;
                case LittleRedActionState.AttackGun:
                    shotgunCooldown = PropsRed.shotgunCooldownTicks;
                    break;
                case LittleRedActionState.AttackGunMove:
                    shotgunCooldown = Mathf.Max(80, PropsRed.shotgunCooldownTicks - 25);
                    break;
            }

            self.UpdateFacingFromMotionOrTarget(target);
            self.UpdateAnimation(force: true);
        }

        private void TickActiveAction(LC_LittleRedPawn self, Pawn fallbackTarget)
        {
            actionElapsedTicks++;
            Pawn tgt = (actionTarget != null && !actionTarget.Dead && !actionTarget.Destroyed && actionTarget.Spawned && actionTarget.Map == self.Map)
                ? actionTarget
                : fallbackTarget;

            switch (actionState)
            {
                case LittleRedActionState.DashAttack:
                    if (!actionEvent1Fired && actionElapsedTicks >= DashAttackHitTick)
                    {
                        actionEvent1Fired = true;
                        ExecuteSickleStrike(self, tgt, PropsRed.meleeDamage * 1.05f, isPower: false, flipSweep: false);
                    }
                    if (actionElapsedTicks >= DashAttackDur)
                    {
                        ClearActionState();
                    }
                    break;

                case LittleRedActionState.AttackAxe:
                    if (!actionEvent1Fired && actionElapsedTicks >= AxeHitTick)
                    {
                        actionEvent1Fired = true;
                        ExecuteSickleStrike(self, tgt, PropsRed.meleeDamage, isPower: false, flipSweep: Rand.Bool);
                    }
                    if (actionElapsedTicks >= AxeDur)
                    {
                        ClearActionState();
                    }
                    break;

                case LittleRedActionState.AttackAxePower:
                    if (!actionEvent1Fired && actionElapsedTicks >= AxePowerHit1Tick)
                    {
                        actionEvent1Fired = true;
                        ExecuteSickleStrike(self, tgt, PropsRed.meleeDamage * 0.90f, isPower: true, flipSweep: false);
                    }
                    if (!actionEvent2Fired && actionElapsedTicks >= AxePowerHit2Tick)
                    {
                        actionEvent2Fired = true;
                        ExecuteSickleStrike(self, tgt, PropsRed.meleeDamage * 1.25f, isPower: true, flipSweep: true);
                    }
                    if (actionElapsedTicks >= AxePowerDur)
                    {
                        ClearActionState();
                    }
                    break;

                case LittleRedActionState.AttackAxeThrow:
                    if (!actionEvent1Fired && actionElapsedTicks >= AxeThrowLaunchTick)
                    {
                        actionEvent1Fired = true;
                        ExecuteSickleThrow(self, tgt);
                    }
                    if (actionElapsedTicks >= AxeThrowDur)
                    {
                        ClearActionState();
                    }
                    break;

                case LittleRedActionState.AttackGun:
                    if (!actionEvent1Fired && actionElapsedTicks >= GunFireTick)
                    {
                        actionEvent1Fired = true;
                        ExecuteShotgunBlast(self, tgt, heavyBlast: true);
                    }
                    if (actionElapsedTicks >= GunDur)
                    {
                        ClearActionState();
                    }
                    break;

                case LittleRedActionState.AttackGunMove:
                    if (!actionEvent1Fired && actionElapsedTicks >= GunMoveFire1Tick)
                    {
                        actionEvent1Fired = true;
                        ExecuteShotgunBlast(self, tgt, heavyBlast: false);
                    }
                    if (!actionEvent2Fired && actionElapsedTicks >= GunMoveFire2Tick)
                    {
                        actionEvent2Fired = true;
                        ExecuteShotgunBlast(self, tgt, heavyBlast: false);
                    }
                    if (actionElapsedTicks >= GunMoveDur)
                    {
                        ClearActionState();
                    }
                    break;
            }
        }

        private void ExecuteSickleStrike(LC_LittleRedPawn self, Pawn primary, float damage, bool isPower, bool flipSweep)
        {
            Map map = self.Map;
            if (map == null)
            {
                return;
            }

            SoundDef atkSound = isPower
                ? LittleRedDefOf.LC_LittleRed_AtkPower
                : (Rand.Bool ? LittleRedDefOf.LC_LittleRed_Atk1 : LittleRedDefOf.LC_LittleRed_Atk2);
            LCAbnormalitiesHelper.PlaySoundAt(atkSound, self);

            Vector3 selfPos = self.DrawPos.Yto0();
            Vector3 aimDir = (primary != null && primary.Spawned)
                ? (primary.DrawPos.Yto0() - selfPos)
                : (self.IsFacingLeft ? Vector3.left : Vector3.right);
            if (aimDir.sqrMagnitude < 0.001f)
            {
                aimDir = self.IsFacingLeft ? Vector3.left : Vector3.right;
            }
            aimDir.Normalize();

            float yaw = LittleRedVfxUtility.VectorToYawDeg(aimDir);
            bool berserk = isPower || IsEnragedVisual;
            Vector3 slashCenter = selfPos + aimDir * 0.72f;

            // Signature crimson-and-obsidian crescent sickle slash!
            LittleRedVfxUtility.SpawnSlash(
                map,
                slashCenter,
                yaw,
                scale: isPower ? 2.85f : 2.25f,
                flipSweep: flipSweep,
                berserk: berserk,
                lifeTicks: isPower ? 18 : 14,
                spanRad: isPower ? 3.05f : 2.55f);

            if (isPower)
            {
                LittleRedVfxUtility.SpawnSlash(
                    map,
                    slashCenter,
                    yaw + (flipSweep ? -24f : 24f),
                    scale: 2.35f,
                    flipSweep: !flipSweep,
                    berserk: true,
                    lifeTicks: 15,
                    spanRad: 2.65f);
            }

            float reach = isPower ? 3.2f : 2.75f;
            int maxTargets = isPower ? 3 : 2;
            int hitCount = 0;

            if (primary != null && !primary.Dead && !primary.Destroyed && primary.Spawned && primary.Position.DistanceTo(self.Position) <= reach)
            {
                primary.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    damage,
                    isPower ? 0.65f : 0.55f,
                    instigator: self));
                LittleRedVfxUtility.SpawnImpactBurst(map, primary.DrawPos.Yto0(), isPower ? 1.45f : 1.05f, berserk);
                hitCount++;
            }

            // Cleave nearby hostiles within sickle arc
            List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < nearby.Count && hitCount < maxTargets; i++)
            {
                Pawn other = nearby[i];
                if (other == null || other == self || other == primary || other.Dead || other.Destroyed || !other.Spawned)
                {
                    continue;
                }

                if (isHired ? !other.HostileTo(Faction.OfPlayer) : !(other.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if (other.Position.DistanceTo(self.Position) <= reach)
                {
                    other.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        damage * 0.72f,
                        0.45f,
                        instigator: self));
                    LittleRedVfxUtility.SpawnImpactBurst(map, other.DrawPos.Yto0(), 1.0f, berserk);
                    hitCount++;
                }
            }
        }

        private void ExecuteSickleThrow(LC_LittleRedPawn self, Pawn target)
        {
            Map map = self.Map;
            if (map == null || target == null || !target.Spawned)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Throw, self);
            Vector3 startPos = self.DrawPos.Yto0() + new Vector3(self.IsFacingLeft ? -0.45f : 0.45f, 0f, 0.15f);
            Vector3 targetPos = target.DrawPos.Yto0();

            activeThrownSickle = LittleRedVfxUtility.SpawnThrownSickle(map, self, startPos, targetPos, PropsRed.axeThrowDamage, isHired);
            if (activeThrownSickle != null)
            {
                sickleInFlight = true;
            }
        }

        private void ExecuteShotgunBlast(LC_LittleRedPawn self, Pawn target, bool heavyBlast)
        {
            Map map = self.Map;
            if (map == null)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(LittleRedDefOf.LC_LittleRed_Gun, self);

            Vector3 selfPos = self.DrawPos.Yto0();
            Vector3 aimPos = (target != null && target.Spawned)
                ? target.DrawPos.Yto0()
                : (selfPos + (self.IsFacingLeft ? Vector3.left : Vector3.right) * 6f);
            Vector3 dir = (aimPos - selfPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = self.IsFacingLeft ? Vector3.left : Vector3.right;
            }
            dir.Normalize();

            Vector3 muzzlePos = selfPos + dir * 0.85f + new Vector3(0f, 0f, 0.18f);
            bool berserk = heavyBlast || IsEnragedVisual;

            // Custom GLSL volumetric crimson blunderbuss muzzle flash cone & diverging pellet tracers!
            LittleRedVfxUtility.SpawnShotgunCone(
                map,
                muzzlePos,
                dir,
                length: heavyBlast ? 5.2f : 3.9f,
                width: heavyBlast ? 2.55f : 1.85f,
                spread: heavyBlast ? 0.68f : 0.50f,
                berserk: berserk,
                lifeTicks: heavyBlast ? 16 : 13);
            FleckMaker.ThrowSmoke(muzzlePos, map, heavyBlast ? 1.35f : 0.95f);

            if (target == null || target.Dead || target.Destroyed || !target.Spawned)
            {
                return;
            }

            if (LittleRedDefOf.Bullet_EgoWeapon_LittleRedRidingHoodedMercenary != null)
            {
                int pellets = heavyBlast ? 2 : 1;
                for (int i = 0; i < pellets; i++)
                {
                    Projectile proj = (Projectile)GenSpawn.Spawn(LittleRedDefOf.Bullet_EgoWeapon_LittleRedRidingHoodedMercenary, self.Position, map);
                    Vector3 jitter = i == 0 ? Vector3.zero : new Vector3(Rand.Range(-0.18f, 0.18f), 0f, Rand.Range(-0.18f, 0.18f));
                    proj.Launch(self, muzzlePos + jitter, target, target, ProjectileHitFlags.IntendedTarget);
                }
            }
            else
            {
                target.TakeDamage(new DamageInfo(
                    LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                    heavyBlast ? PropsRed.shotgunDamage * 1.15f : PropsRed.shotgunDamage * 0.80f,
                    0.45f,
                    instigator: self));
                LittleRedVfxUtility.SpawnImpactBurst(map, target.DrawPos.Yto0(), 1.2f, berserk);
            }
        }

        public override IEnumerable<Gizmo> GetPlatformGizmos()
        {
            if (parent == null || parent.Destroyed || parent.MapHeld == null)
            {
                yield break;
            }

            int cost = PropsRed.hireCostPeBox;
            int available = PeBoxComp?.CurAmountIndiPebox ?? 0;

            Command_Action hireCmd = new Command_Action
            {
                defaultLabel = "LC_LittleRed_HireLabel".Translate(cost),
                defaultDesc = "LC_LittleRed_HireDesc".Translate(cost),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/LittleRedRidingHoodedMercenary", reportFailure: false) ?? BaseContent.BadTex,
                action = () =>
                {
                    TargetingParameters tp = new TargetingParameters
                    {
                        canTargetPawns = true,
                        canTargetLocations = false,
                        canTargetBuildings = false,
                        canTargetSelf = false,
                        validator = target => target.Thing is Pawn p && !p.Dead && !p.Destroyed && p.HostileTo(Faction.OfPlayer)
                    };
                    Find.Targeter.BeginTargeting(tp, target =>
                    {
                        if (target.Thing is Pawn p)
                        {
                            RequestMercenaryHunt(p);
                        }
                    });
                }
            };

            if (available < cost)
            {
                hireCmd.Disable("LC_LittleRed_NotEnoughPeBox".Translate(cost, available));
            }

            yield return hireCmd;
        }

        public override string GetPlatformInspectString()
        {
            return "LC_LittleRed_InspectPlatform".Translate(PropsRed.hireCostPeBox);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isHired, "lrIsHired", false);
            Scribe_References.Look(ref hiredTarget, "lrHiredTarget");
            Scribe_References.Look(ref homePlatform, "lrHomePlatform");
            Scribe_Values.Look(ref shotgunCooldown, "lrShotgunCooldown", 0);
            Scribe_Values.Look(ref meleeCooldown, "lrMeleeCooldown", 0);
            Scribe_Values.Look(ref axeThrowCooldown, "lrAxeThrowCooldown", 0);
            Scribe_Values.Look(ref huntVisualTicks, "lrHuntVisualTicks", 0);
            Scribe_Values.Look(ref hiredIdleTicks, "lrHiredIdleTicks", 0);
            Scribe_Values.Look(ref actionState, "lrActionState", LittleRedActionState.None);
            Scribe_Values.Look(ref actionElapsedTicks, "lrActionElapsedTicks", 0);
            Scribe_Values.Look(ref actionEvent1Fired, "lrActionEvent1Fired", false);
            Scribe_Values.Look(ref actionEvent2Fired, "lrActionEvent2Fired", false);
            Scribe_References.Look(ref actionTarget, "lrActionTarget");
            Scribe_Values.Look(ref sickleInFlight, "lrSickleInFlight", false);
        }
    }

    public class LC_LittleRedPawn : LC_StandardEntityPawn
    {
        private bool facingLeft;

        public CompLittleRedRidingHoodedMercenary RedComp => EntityComp as CompLittleRedRidingHoodedMercenary;

        public bool IsFacingLeft => facingLeft;

        public bool IsHeldOnPlatform => ParentHolder is Building_AbnormalityHoldingPlatform;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public void UpdateFacingFromMotionOrTarget(Pawn target)
        {
            if (IsHeldOnPlatform)
            {
                facingLeft = false;
                return;
            }

            if (target != null && target.Spawned && target.Map == Map)
            {
                float dx = target.DrawPos.x - DrawPos.x;
                if (Mathf.Abs(dx) > 0.15f)
                {
                    facingLeft = dx < 0f;
                    return;
                }
            }

            if (pather != null && pather.Moving && pather.nextCell.IsValid)
            {
                int dxCell = pather.nextCell.x - Position.x;
                if (dxCell != 0)
                {
                    facingLeft = dxCell < 0;
                    return;
                }
            }

            if (Rotation == Rot4.West)
            {
                facingLeft = true;
            }
            else if (Rotation == Rot4.East)
            {
                facingLeft = false;
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompLittleRedRidingHoodedMercenary comp = RedComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = LittleRedDefOf.LittleRed_ContainedIdle;
            }
            else if (comp != null && comp.CurrentAction != LittleRedActionState.None)
            {
                switch (comp.CurrentAction)
                {
                    case LittleRedActionState.DashAttack:
                        desired = LittleRedDefOf.LittleRed_DashAttack;
                        break;
                    case LittleRedActionState.AttackAxe:
                        desired = LittleRedDefOf.LittleRed_AttackAxe;
                        break;
                    case LittleRedActionState.AttackAxePower:
                        desired = LittleRedDefOf.LittleRed_AttackAxePower;
                        break;
                    case LittleRedActionState.AttackAxeThrow:
                        desired = LittleRedDefOf.LittleRed_AttackAxeThrow;
                        break;
                    case LittleRedActionState.AttackGun:
                        desired = LittleRedDefOf.LittleRed_AttackGun;
                        break;
                    case LittleRedActionState.AttackGunMove:
                        desired = LittleRedDefOf.LittleRed_AttackGunMove;
                        break;
                    default:
                        desired = LittleRedDefOf.LittleRed_CombatIdle;
                        break;
                }
            }
            else
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? LittleRedDefOf.LittleRed_Dash
                    : LittleRedDefOf.LittleRed_CombatIdle;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            if (Spawned && !Dead && RedComp != null && RedComp.IsHired)
            {
                yield return new Command_Action
                {
                    defaultLabel = "LC_LittleRed_RecallLabel".Translate(),
                    defaultDesc = "LC_LittleRed_RecallDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/LittleRedRidingHoodedMercenary", reportFailure: false) ?? BaseContent.BadTex,
                    action = () => RedComp.RecallFromHunt()
                };
            }
        }
    }

    /// <summary>小红帽雇佣兵战斗 AI：仅负责追击移动或攻击驻足，不在此处销毁实体。</summary>
    public class JobGiver_LittleRedCombat : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }

            CompLittleRedRidingHoodedMercenary comp = pawn.TryGetComp<CompLittleRedRidingHoodedMercenary>();
            if (comp == null)
            {
                return null;
            }

            // Hold stance cleanly during heavy committed swing/throw/gun animations
            if (comp.IsBusyInAttackAnimation)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 20);
            }

            Pawn target = comp.FindCombatTarget();
            if (target == null)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 45);
            }

            if (pawn.Position.DistanceTo(target.Position) <= 1.9f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 25);
            }

            Job chase = JobMaker.MakeJob(JobDefOf.Goto, target);
            chase.expiryInterval = 60;
            chase.checkOverrideOnExpire = true;
            chase.canBashDoors = !comp.IsHired;
            return chase;
        }
    }

    /// <summary>
    /// 赤红疤痕（WAW 红伤武器）专属伤害处理器：
    /// 持握者血量越低伤害越高（最高 +50%）；近战弯镰挥砍触发专属 GLSL 猩红黑核月牙斩击特效与原版斩击音效，
    /// 半血以下狂暴状态触发强化双弧猩红斩并顺劈身旁 1 名敌对目标。
    /// </summary>
    public class DamageWorker_CrimsonScar : DamageWorker_FourColor
    {
        private static bool inCrimsonCleavePass;

        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            if (inCrimsonCleavePass)
            {
                return base.Apply(dinfo, victim);
            }

            bool berserk = false;
            if (dinfo.Instigator is Pawn attacker && !attacker.Dead && attacker.health?.summaryHealth != null)
            {
                float hpPct = attacker.health.summaryHealth.SummaryHealthPercent;
                berserk = hpPct < 0.55f;
                float missingFraction = Mathf.Clamp01(1f - hpPct);
                float bonusMult = 1f + Mathf.Clamp01(missingFraction / 0.6f) * 0.50f;
                if (bonusMult > 1.001f)
                {
                    dinfo.SetAmount(dinfo.Amount * bonusMult);
                }
            }

            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);

            // Detect melee crimson sickle strikes (non-projectile weapon strikes from Crimson Scar)
            if (!Projectile_CrimsonScarShot.IsApplyingImpact &&
                dinfo.Instigator is Pawn meleeAttacker && victim != null && victim.MapHeld != null &&
                dinfo.Weapon != null &&
                dinfo.Weapon.defName == "EgoWeapon_LittleRedRidingHoodedMercenary" &&
                meleeAttacker.Spawned && victim.Spawned)
            {
                {
                    Map map = victim.MapHeld;
                    Vector3 attPos = meleeAttacker.DrawPos.Yto0();
                    Vector3 vicPos = victim.DrawPos.Yto0();
                    Vector3 dir = (vicPos - attPos).Yto0();
                    float yaw = dir.sqrMagnitude > 0.001f ? LittleRedVfxUtility.VectorToYawDeg(dir) : dinfo.Angle;

                    SoundDef sfx = berserk
                        ? LittleRedDefOf.LC_LittleRed_AtkPower
                        : (Rand.Bool ? LittleRedDefOf.LC_LittleRed_Atk1 : LittleRedDefOf.LC_LittleRed_Atk2);
                    LCAbnormalitiesHelper.PlaySoundAt(sfx, victim);

                    LittleRedVfxUtility.SpawnSlash(
                        map,
                        Vector3.Lerp(attPos, vicPos, 0.62f),
                        yaw,
                        scale: berserk ? 2.35f : 1.85f,
                        flipSweep: Rand.Bool,
                        berserk: berserk,
                        lifeTicks: berserk ? 16 : 13,
                        spanRad: berserk ? 2.85f : 2.45f);

                    LittleRedVfxUtility.SpawnImpactBurst(map, vicPos, berserk ? 1.30f : 0.95f, berserk);

                    if (berserk)
                    {
                        // Berserk overdrive: second cross-slash + cleave 1 adjacent hostile (faithful to Lobotomy Corp <50% HP berserk!)
                        LittleRedVfxUtility.SpawnSlash(
                            map,
                            vicPos,
                            yaw + 32f,
                            scale: 2.05f,
                            flipSweep: true,
                            berserk: true,
                            lifeTicks: 14,
                            spanRad: 2.65f);

                        List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                        inCrimsonCleavePass = true;
                        try
                        {
                            for (int i = 0; i < nearby.Count; i++)
                            {
                                Pawn other = nearby[i];
                                if (other == null || other == victim || other == meleeAttacker || other.Dead || other.Destroyed || !other.Spawned)
                                {
                                    continue;
                                }

                                if (!other.HostileTo(meleeAttacker))
                                {
                                    continue;
                                }

                                if (other.Position.DistanceTo(victim.Position) <= 2.3f)
                                {
                                    other.TakeDamage(new DamageInfo(
                                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                                        Mathf.Max(3f, dinfo.Amount * 0.65f),
                                        0.45f,
                                        dinfo.Angle,
                                        meleeAttacker,
                                        weapon: dinfo.Weapon));
                                    LittleRedVfxUtility.SpawnImpactBurst(map, other.DrawPos.Yto0(), 1.0f, berserk: true);
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            inCrimsonCleavePass = false;
                        }
                    }
                }
            }

            return result;
        }
    }
}
