using System.Collections.Generic;
using LCAbnormalities.Settings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Damage;
using LCAnomalyCore.GameComponent;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.OldBeliefAndPromise
{
    public enum OldBeliefVisualPhase
    {
        Idle = 0,
        Doing = 1,
        Success = 2,
        Fail = 3
    }

    /// <summary>T-09-97 古老的信念与承诺组件属性。</summary>
    public class CompProperties_OldBeliefAndPromise : CompProperties_AbnormalityTool
    {
        public float tier1Multiplier = 1.25f;
        public float tier2Multiplier = 1.40f;
        public float tier1BetrayalChance = 0.20f;
        public float tier2BetrayalChance = 0.45f;
        public float betrayalWhiteDamage = 22f;
        public float protectedEgoBacklashWhite = 32f;
        public float protectedEgoBacklashBlack = 18f;

        public CompProperties_OldBeliefAndPromise()
        {
            compClass = typeof(CompOldBeliefAndPromise);
        }
    }

    /// <summary>
    /// T-09-97 古老的信念与承诺核心逻辑与解包多部件悬吊钟摆动画：
    /// 单次交互可将使用者当前手持主武器永久强化（+25% -> +40% 伤害倍率，支持 E.G.O 与常规武器），
    /// 并存入 <c>GameComponent_LC</c> 在信息面板展示与随存档持久保存；但背叛概率会随贪婪阶梯攀升。
    /// 视觉上还原原作 Spine（1_Default / 2_Get_marble / 3_Doing 蓝光摇摆 / 4_Sucess 金芒赐福 / 4_Fail 暗紫蚀裂）多部件实时演出。
    /// </summary>
    public class CompOldBeliefAndPromise : CompAbnormalityTool
    {
        private const int ResultAnimDurationTicks = 210; // 3.5s
        private const float SpinePxToUnit = 1f / 460f;

        private int totalBlessings;
        private int totalBetrayals;
        private bool lastOutcomeBetrayal;

        // Transient visual animation state (not required in save)
        private Pawn activeChanneler;
        private int doingTicks;
        private int resultAnimTicks;
        private bool resultWasBetrayal;
        private MaterialPropertyBlock partProps;

        public CompProperties_OldBeliefAndPromise PropsPromise => (CompProperties_OldBeliefAndPromise)props;

        public int TotalBlessings => totalBlessings;

        public int TotalBetrayals => totalBetrayals;

        public bool LastOutcomeBetrayal => lastOutcomeBetrayal;

        public Pawn ActiveChanneler => activeChanneler;

        public OldBeliefVisualPhase VisualPhase
        {
            get
            {
                if (activeChanneler != null && !activeChanneler.Dead && !activeChanneler.Destroyed)
                {
                    return OldBeliefVisualPhase.Doing;
                }

                if (resultAnimTicks > 0)
                {
                    return resultWasBetrayal ? OldBeliefVisualPhase.Fail : OldBeliefVisualPhase.Success;
                }

                return OldBeliefVisualPhase.Idle;
            }
        }

        public bool IsVisualSessionActive => VisualPhase != OldBeliefVisualPhase.Idle;

        public float ResultFadeAlpha
        {
            get
            {
                if (resultAnimTicks <= 0)
                {
                    return 0f;
                }

                return Mathf.Clamp01(resultAnimTicks / 45f);
            }
        }

        public override AcceptanceReport CanUseTool(Pawn pawn)
        {
            AcceptanceReport baseReport = base.CanUseTool(pawn);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            ThingWithComps weapon = pawn?.equipment?.Primary;
            if (weapon == null)
            {
                return "LC_OldBelief_NoWeapon".Translate();
            }

            float curMult = GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(weapon) ?? 1f;
            if (curMult >= PropsPromise.tier2Multiplier - 0.005f)
            {
                return "LC_OldBelief_AlreadyMaxBlessed".Translate(weapon.LabelCap);
            }

            return true;
        }

        public override void TickWhileHeld()
        {
            base.TickWhileHeld();
            TickVisualAnimation();
        }

        public override void CompTick()
        {
            base.CompTick();
            TickVisualAnimation();
        }

        private void TickVisualAnimation()
        {
            if (resultAnimTicks > 0)
            {
                resultAnimTicks--;
            }

            Map map = ActiveMap;
            Thing target = InteractionTarget;
            if (map == null || target == null || !target.Spawned)
            {
                activeChanneler = null;
                doingTicks = 0;
                return;
            }

            Pawn prevChanneler = activeChanneler;
            Pawn detected = DetectActiveChanneler(map, target);
            activeChanneler = detected;

            if (detected != null)
            {
                doingTicks++;
                if (prevChanneler != detected)
                {
                    // 2_Get_marble: user begins offering weapon spirit orb to the crystal sphere
                    resultAnimTicks = 0;
                    if (ActivePosition.IsValid)
                    {
                        PromiseSoundDefOf.LC_Promise_Marble?.PlayOneShot(new TargetInfo(ActivePosition, map));
                        OldBeliefVfxUtility.SpawnOrbTransfer(
                            map,
                            detected.DrawPos + new Vector3(0f, 0f, 0.35f),
                            ActiveDrawPos + new Vector3(0f, 0f, 0.14f),
                            trackEndPawn: null,
                            goldenSuccess: false,
                            shatter: false,
                            duration: 28);
                        OldBeliefVfxUtility.EnsureOracleAura(parent, map, ActivePosition);
                    }
                }
                else if (doingTicks % 20 == 0 && ActivePosition.IsValid)
                {
                    OldBeliefVfxUtility.EnsureOracleAura(parent, map, ActivePosition);
                }
            }
            else
            {
                doingTicks = 0;
            }
        }

        private static Pawn DetectActiveChanneler(Map map, Thing target)
        {
            JobDef useDef = LCAnomalyCore.Defs.JobDefOf.LC_UseAbnormalityTool;
            if (useDef == null || map?.thingGrid == null || target == null)
            {
                return null;
            }

            foreach (IntVec3 cell in GenAdj.CellsAdjacent8WayAndInside(target))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn p
                        && !p.Dead
                        && !p.Downed
                        && p.CurJobDef == useDef
                        && p.CurJob?.targetA.Thing == target
                        && p.pather?.Moving != true)
                    {
                        return p;
                    }
                }
            }

            return null;
        }

        public override void OnToolUsed(Pawn user)
        {
            base.OnToolUsed(user);

            activeChanneler = null;
            doingTicks = 0;

            ThingWithComps weapon = user?.equipment?.Primary;
            GameComponent_LC gc = GameComponent_LC.Instance;
            if (weapon == null || gc == null)
            {
                return;
            }

            float curMult = gc.GetWeaponBlessingMultiplier(weapon);
            bool isUpgrade = curMult > 1.0001f;
            float betrayalChance = isUpgrade ? PropsPromise.tier2BetrayalChance : PropsPromise.tier1BetrayalChance;

            Map map = ActiveMap;
            IntVec3 pos = ActivePosition;
            Vector3 drawAnchor = ActiveDrawPos + new Vector3(0f, 0f, 0.14f);

            if (Rand.Chance(betrayalChance))
            {
                lastOutcomeBetrayal = true;
                totalBetrayals++;
                resultWasBetrayal = true;
                resultAnimTicks = ResultAnimDurationTicks;
                gc.RemoveWeaponBlessing(weapon);

                if (IsOperational && map != null && pos.IsValid)
                {
                    PromiseSoundDefOf.LC_Promise_Result?.PlayOneShot(new TargetInfo(pos, map));
                    PromiseSoundDefOf.LC_Promise_Fail?.PlayOneShot(new TargetInfo(pos, map));
                    OldBeliefVfxUtility.EnsureOracleAura(parent, map, pos);
                    OldBeliefVfxUtility.SpawnResultBurst(map, drawAnchor, betrayal: true, radius: 3.1f, duration: 48);

                    if (user != null && user.Spawned && user.Map == map)
                    {
                        OldBeliefVfxUtility.SpawnOrbTransfer(
                            map,
                            drawAnchor,
                            user.DrawPos + new Vector3(0f, 0f, 0.35f),
                            user,
                            goldenSuccess: false,
                            shatter: true,
                            duration: 28);
                        OldBeliefVfxUtility.SpawnResultBurst(map, user.DrawPos, betrayal: true, radius: 1.9f, duration: 36);
                    }
                }

                bool isAuthoredEgo = FourColorOverrides.TryGetWeaponProfile(weapon.def, out FourColorOverrides.ResolvedWeaponProfile wp)
                    && wp.isAuthoredEgo;
                bool canDestroyEgo = LCAbnormalitiesMod.Settings?.OldBeliefCanDestroyEgoWeapon ?? false;

                if (!isAuthoredEgo || canDestroyEgo)
                {
                    string weaponLabel = weapon.LabelCap;
                    user.equipment.DestroyEquipment(weapon);

                    if (user != null && !user.Dead && !user.Destroyed)
                    {
                        user.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                            PropsPromise.betrayalWhiteDamage,
                            armorPenetration: 1.5f,
                            instigator: parent));
                    }

                    Messages.Message(
                        "LC_OldBelief_BetrayalDestroyed".Translate(user.LabelShortCap, weaponLabel),
                        new LookTargets(user),
                        MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    if (user != null && !user.Dead && !user.Destroyed)
                    {
                        user.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                            PropsPromise.protectedEgoBacklashWhite,
                            armorPenetration: 1.5f,
                            instigator: parent));

                        if (!user.Dead && !user.Destroyed)
                        {
                            user.TakeDamage(new DamageInfo(
                                LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                                PropsPromise.protectedEgoBacklashBlack,
                                armorPenetration: 1.5f,
                                instigator: parent));
                        }
                    }

                    Messages.Message(
                        "LC_OldBelief_BetrayalBacklash".Translate(user.LabelShortCap, weapon.LabelCap),
                        new LookTargets(user),
                        MessageTypeDefOf.NegativeEvent);
                }

                return;
            }

            lastOutcomeBetrayal = false;
            totalBlessings++;
            resultWasBetrayal = false;
            resultAnimTicks = ResultAnimDurationTicks;

            float targetMult = isUpgrade ? PropsPromise.tier2Multiplier : PropsPromise.tier1Multiplier;
            gc.SetWeaponBlessing(weapon, targetMult);

            if (IsOperational && map != null && pos.IsValid)
            {
                PromiseSoundDefOf.LC_Promise_Result?.PlayOneShot(new TargetInfo(pos, map));
                PromiseSoundDefOf.LC_Promise_Success?.PlayOneShot(new TargetInfo(pos, map));
                OldBeliefVfxUtility.EnsureOracleAura(parent, map, pos);
                OldBeliefVfxUtility.SpawnResultBurst(map, drawAnchor, betrayal: false, radius: 3.0f, duration: 48);

                if (user != null && user.Spawned && user.Map == map)
                {
                    OldBeliefVfxUtility.SpawnOrbTransfer(
                        map,
                        drawAnchor,
                        user.DrawPos + new Vector3(0f, 0f, 0.35f),
                        user,
                        goldenSuccess: true,
                        shatter: false,
                        duration: 34);
                }
            }

            string pct = $"+{(targetMult - 1f) * 100f:0.#}%";
            if (user != null && user.Spawned && user.Map != null)
            {
                MoteMaker.ThrowText(user.DrawPos, user.Map, pct, new Color(1.0f, 0.85f, 0.25f), 2.8f);
            }

            string msgKey = isUpgrade ? "LC_OldBelief_BlessUpgraded" : "LC_OldBelief_BlessSuccess";
            Messages.Message(
                msgKey.Translate(weapon.LabelCap, pct),
                new LookTargets(user),
                MessageTypeDefOf.PositiveEvent);
        }

        public override bool TryDrawHeldOverride(DrawPhase phase, Vector3 drawLoc)
        {
            if (phase != DrawPhase.Draw)
            {
                return true;
            }

            Pawn pawn = parent as Pawn;
            List<PawnKindLifeStage> lifeStages = pawn?.kindDef?.lifeStages;
            GraphicData bodyGraphic = (lifeStages != null && lifeStages.Count > 0) ? lifeStages[0].bodyGraphicData : null;
            Vector2 drawSize = bodyGraphic?.drawSize ?? new Vector2(2.1f, 2.1f);
            CompAbnormalityHoldingPlatformTarget targetComp = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (targetComp != null && targetComp.Props.heldDrawScale > 0f && Mathf.Abs(targetComp.Props.heldDrawScale - 1f) > 0.0001f)
            {
                drawSize *= targetComp.Props.heldDrawScale;
            }

            float scale = drawSize.y * SpinePxToUnit;
            float baseY = drawLoc.y;
            float ageSecs = ((Find.TickManager?.TicksGame ?? 0) + (parent?.thingIDNumber ?? 0) * 17) / 60f;

            OldBeliefVisualPhase visualPhase = VisualPhase;
            float resultFade = ResultFadeAlpha;

            // Compute pendulum angle and micro-shudder matching Spine bone9..bone13
            float swayDeg;
            float verticalBob;
            Vector2 jitter = Vector2.zero;
            float lizardBreath;

            switch (visualPhase)
            {
                case OldBeliefVisualPhase.Doing:
                    swayDeg = Mathf.Sin(ageSecs * 5.2f) * 12.5f + Mathf.Sin(ageSecs * 10.4f + 0.7f) * 3.5f;
                    verticalBob = Mathf.Sin(ageSecs * 6.0f) * 0.04f;
                    lizardBreath = 0.025f * Mathf.Sin(ageSecs * 8.0f);
                    break;
                case OldBeliefVisualPhase.Success:
                    swayDeg = Mathf.Sin(ageSecs * 2.1f) * 4.5f;
                    verticalBob = 0.06f * resultFade + Mathf.Sin(ageSecs * 3.2f) * 0.025f;
                    lizardBreath = 0.020f * Mathf.Sin(ageSecs * 4.2f);
                    break;
                case OldBeliefVisualPhase.Fail:
                    swayDeg = Mathf.Sin(ageSecs * 1.9f) * 3.2f + Mathf.Sin(ageSecs * 28.0f) * 2.2f * resultFade;
                    verticalBob = -0.04f * resultFade;
                    jitter = new Vector2(
                        Mathf.Sin(ageSecs * 53.0f) * 0.032f * resultFade,
                        Mathf.Cos(ageSecs * 61.0f) * 0.022f * resultFade);
                    lizardBreath = -0.015f * resultFade;
                    break;
                default:
                    swayDeg = Mathf.Sin(ageSecs * (Mathf.PI * 2f / 4.0f)) * 3.2f;
                    verticalBob = Mathf.Sin(ageSecs * 1.55f) * 0.018f;
                    lizardBreath = 0.008f * Mathf.Sin(ageSecs * 1.9f);
                    break;
            }

            // Pivot sits at top of the 3 hanging ropes; sphere center hangs below pivot.
            float ropeLength = 220f * scale;
            Vector3 pivotWorld = new Vector3(drawLoc.x, baseY, drawLoc.z + 0.42f * drawSize.y);
            float swayRad = swayDeg * Mathf.Deg2Rad;
            Vector3 sphereCenter = new Vector3(
                pivotWorld.x + Mathf.Sin(swayRad) * ropeLength + jitter.x,
                baseY,
                pivotWorld.z - Mathf.Cos(swayRad) * ropeLength + verticalBob + jitter.y);
            // Unity Y-up left-handed: positive pendulum swing (+X) rotates counter-clockwise (-swayDeg around +Y).
            float bodyRotDeg = -swayDeg;

            partProps ??= new MaterialPropertyBlock();

            // 1. Hanging braided ropes (Line1, Line2, Line3) anchored at ceiling pivot and glued to upper lizard knobs
            DrawHangingRope(OldBeliefMaterials.Line1Mat, pivotWorld, sphereCenter, bodyRotDeg, new Vector2(-96f * scale, 118f * scale), 20f * scale, 196f * scale, baseY + 0.001f);
            DrawHangingRope(OldBeliefMaterials.Line2Mat, pivotWorld, sphereCenter, bodyRotDeg, new Vector2(-12f * scale, 136f * scale), 25f * scale, 185f * scale, baseY + 0.002f);
            DrawHangingRope(OldBeliefMaterials.Line3Mat, pivotWorld, sphereCenter, bodyRotDeg, new Vector2(42f * scale, 126f * scale), 24f * scale, 218f * scale, baseY + 0.003f);

            // 2. Additive luminous halo behind the marble (3_Doing cyan-blue OR 4_Sucess radiant gold)
            if (visualPhase == OldBeliefVisualPhase.Doing)
            {
                float haloPulse = 0.72f + 0.28f * Mathf.Sin(ageSecs * 11.0f);
                DrawRigPart(OldBeliefMaterials.BallDoingLightMat, sphereCenter, bodyRotDeg, new Vector2(-10f, 8f), 390f, 374f, scale * (0.96f + 0.08f * haloPulse), baseY + 0.004f, new Color(0.55f, 0.94f, 1.0f, 0.85f * haloPulse));
            }
            else if (visualPhase == OldBeliefVisualPhase.Success)
            {
                float haloPulse = 0.80f + 0.20f * Mathf.Sin(ageSecs * 6.5f);
                DrawRigPart(OldBeliefMaterials.BallSuccessLightMat, sphereCenter, bodyRotDeg, new Vector2(-10f, 10f), 390f, 374f, scale * (1.02f + 0.08f * haloPulse), baseY + 0.004f, new Color(1.0f, 0.92f, 0.56f, 0.92f * resultFade));
            }

            // 3. Central Crystal Marble (Default Obsidian -> Doing Cyan -> Success Gold)
            Material ballMat = visualPhase switch
            {
                OldBeliefVisualPhase.Doing => OldBeliefMaterials.BallDoingMat,
                OldBeliefVisualPhase.Success => OldBeliefMaterials.BallSuccessMat,
                _ => OldBeliefMaterials.BallDefaultMat
            };
            Color ballTint = visualPhase == OldBeliefVisualPhase.Fail
                ? Color.Lerp(Color.white, new Color(0.42f, 0.34f, 0.56f, 1f), resultFade)
                : (visualPhase == OldBeliefVisualPhase.Idle && totalBlessings > 0 && !lastOutcomeBetrayal
                    ? new Color(1.0f, 0.97f, 0.88f, 1f)
                    : Color.white);
            DrawRigPart(ballMat, sphereCenter, bodyRotDeg, new Vector2(5f, 2f), 211f, 210f, scale, baseY + 0.006f, ballTint);

            // 4. Inner swirling light reflections (Reflected_1 & Reflected_2)
            float refAlpha = visualPhase == OldBeliefVisualPhase.Fail
                ? 0.20f * (1f - resultFade * 0.7f)
                : (visualPhase == OldBeliefVisualPhase.Doing ? 0.72f : (visualPhase == OldBeliefVisualPhase.Success ? 0.85f : 0.45f));
            float refRot1 = bodyRotDeg + Mathf.Sin(ageSecs * (visualPhase == OldBeliefVisualPhase.Doing ? 4.8f : 1.57f)) * 10f;
            float refRot2 = bodyRotDeg - Mathf.Cos(ageSecs * (visualPhase == OldBeliefVisualPhase.Doing ? 5.6f : 1.35f)) * 12f;
            DrawRigPart(OldBeliefMaterials.Reflected1Mat, sphereCenter, refRot1, new Vector2(9f, 0f), 209f, 211f, scale, baseY + 0.008f, new Color(1f, 1f, 1f, refAlpha));
            DrawRigPart(OldBeliefMaterials.Reflected2Mat, sphereCenter, refRot2, new Vector2(10f, -1f), 209f, 211f, scale * (1f + 0.03f * Mathf.Sin(ageSecs * 3.0f)), baseY + 0.009f, new Color(1f, 1f, 1f, refAlpha * 0.8f));

            // 5. Betrayal Eclipse Darkness (Dark_1 & Dark_2) or Success Starbursts (Success_Light)
            if (visualPhase == OldBeliefVisualPhase.Fail)
            {
                DrawRigPart(OldBeliefMaterials.Dark1Mat, sphereCenter, bodyRotDeg + Mathf.Sin(ageSecs * 12f) * 8f, new Vector2(10f, -6f), 224f, 188f, scale * 1.04f, baseY + 0.011f, new Color(1f, 1f, 1f, 0.92f * resultFade));
                DrawRigPart(OldBeliefMaterials.Dark2Mat, sphereCenter, bodyRotDeg - ageSecs * 25f, new Vector2(5f, 3f), 209f, 211f, scale * 1.02f, baseY + 0.012f, new Color(1f, 1f, 1f, 0.85f * resultFade));
            }
            else if (visualPhase == OldBeliefVisualPhase.Success)
            {
                float sparkle = 0.65f + 0.35f * Mathf.Sin(ageSecs * 9.5f);
                DrawRigPart(OldBeliefMaterials.SuccessLightMat, sphereCenter, bodyRotDeg + ageSecs * 18f, new Vector2(4f, -4f), 211f, 210f, scale * 1.08f, baseY + 0.011f, new Color(1.0f, 0.96f, 0.68f, sparkle * resultFade));
            }

            // 6. Cosmic Distortion Vortex (Distortion_1..3 cycling on right edge during Doing or Fail)
            if ((visualPhase == OldBeliefVisualPhase.Doing || visualPhase == OldBeliefVisualPhase.Fail) && OldBeliefMaterials.DistortionMats != null && OldBeliefMaterials.DistortionMats.Length > 0)
            {
                int frame = Mathf.FloorToInt(ageSecs * 12f) % OldBeliefMaterials.DistortionMats.Length;
                float distAlpha = visualPhase == OldBeliefVisualPhase.Doing
                    ? (0.58f + 0.28f * Mathf.Sin(ageSecs * 9.0f))
                    : (0.88f * resultFade);
                DrawRigPart(OldBeliefMaterials.DistortionMats[frame], sphereCenter, bodyRotDeg + Mathf.Sin(ageSecs * 8f) * 8f, new Vector2(60f, 1f), 89f, 148f, scale * (visualPhase == OldBeliefVisualPhase.Fail ? 1.15f : 1.0f), baseY + 0.013f, new Color(1f, 1f, 1f, distAlpha));
            }

            // 7. Interlocking Puzzle Lizards (Default Silver -> Doing Cyan-Blue -> Success Radiant Gold)
            Material upLizardMat = visualPhase switch
            {
                OldBeliefVisualPhase.Doing => OldBeliefMaterials.LizardUpDoingMat,
                OldBeliefVisualPhase.Success => OldBeliefMaterials.LizardUpSuccessMat,
                _ => OldBeliefMaterials.LizardUpDefaultMat
            };
            Material downLizardMat = visualPhase switch
            {
                OldBeliefVisualPhase.Doing => OldBeliefMaterials.LizardDownDoingMat,
                OldBeliefVisualPhase.Success => OldBeliefMaterials.LizardDownSuccessMat,
                _ => OldBeliefMaterials.LizardDownDefaultMat
            };

            Color lizardTint = visualPhase == OldBeliefVisualPhase.Fail
                ? Color.Lerp(Color.white, new Color(0.55f, 0.50f, 0.66f, 1f), resultFade)
                : Color.white;

            float upOffsetY = 72f + lizardBreath * 180f;
            float downOffsetY = -58f - lizardBreath * 180f;

            DrawRigPart(downLizardMat, sphereCenter, bodyRotDeg - lizardBreath * 55f, new Vector2(-34f, downOffsetY), 256f, 158f, scale, baseY + 0.015f, lizardTint);
            DrawRigPart(OldBeliefMaterials.LizardDownEyeMat, sphereCenter, bodyRotDeg - lizardBreath * 55f, new Vector2(73f, downOffsetY - 8f), 22f, 23f, scale, baseY + 0.016f, lizardTint);

            DrawRigPart(upLizardMat, sphereCenter, bodyRotDeg + lizardBreath * 55f, new Vector2(-32f, upOffsetY), 258f, 179f, scale, baseY + 0.018f, lizardTint);
            DrawRigPart(OldBeliefMaterials.LizardUpEyeMat, sphereCenter, bodyRotDeg + lizardBreath * 55f, new Vector2(74f, upOffsetY + 2f), 10f, 18f, scale, baseY + 0.019f, lizardTint);

            return true;
        }

        private void DrawHangingRope(Material mat, Vector3 pivotWorld, Vector3 sphereCenter, float bodyRotDeg, Vector2 attachLocal, float widthPx, float heightPx, float yAlt)
        {
            if (mat == null)
            {
                return;
            }

            float rad = -bodyRotDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rx = attachLocal.x * cos - attachLocal.y * sin;
            float rz = attachLocal.x * sin + attachLocal.y * cos;

            Vector3 top = new Vector3(pivotWorld.x + attachLocal.x, yAlt, pivotWorld.z);
            Vector3 bottom = new Vector3(sphereCenter.x + rx, yAlt, sphereCenter.z + rz);
            Vector3 delta = bottom - top;
            float len = Mathf.Max(heightPx * 0.35f, delta.magnitude);
            Vector3 mid = (top + bottom) * 0.5f;
            float angleDeg = -Mathf.Atan2(delta.x, -delta.z) * Mathf.Rad2Deg;

            partProps.SetColor(OldBeliefMaterials.PropColor, Color.white);
            Matrix4x4 trs = Matrix4x4.TRS(
                new Vector3(mid.x, yAlt, mid.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(widthPx, 1f, len));
            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, partProps);
        }

        private void DrawRigPart(Material mat, Vector3 centerWorld, float rotDeg, Vector2 localOffsetPx, float widthPx, float heightPx, float scale, float yAlt, Color color)
        {
            if (mat == null || color.a <= 0.01f)
            {
                return;
            }

            float rad = -rotDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float ox = localOffsetPx.x * scale;
            float oz = localOffsetPx.y * scale;
            float rx = ox * cos - oz * sin;
            float rz = ox * sin + oz * cos;

            partProps.SetColor(OldBeliefMaterials.PropColor, color);
            Matrix4x4 trs = Matrix4x4.TRS(
                new Vector3(centerWorld.x + rx, yAlt, centerWorld.z + rz),
                Quaternion.AngleAxis(rotDeg, Vector3.up),
                new Vector3(widthPx * scale, 1f, heightPx * scale));
            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, partProps);
        }

        public override string GetToolStatusInspectString()
        {
            return "LC_OldBelief_InspectStats".Translate(totalBlessings, totalBetrayals);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref totalBlessings, "oldBeliefTotalBlessings", 0);
            Scribe_Values.Look(ref totalBetrayals, "oldBeliefTotalBetrayals", 0);
            Scribe_Values.Look(ref lastOutcomeBetrayal, "oldBeliefLastBetrayal", false);
        }
    }

    [DefOf]
    public static class PromiseSoundDefOf
    {
        public static SoundDef LC_Promise_Doing;
        public static SoundDef LC_Promise_Success;
        public static SoundDef LC_Promise_Fail;
        public static SoundDef LC_Promise_Marble;
        public static SoundDef LC_Promise_Result;

        static PromiseSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PromiseSoundDefOf));
        }
    }
}
