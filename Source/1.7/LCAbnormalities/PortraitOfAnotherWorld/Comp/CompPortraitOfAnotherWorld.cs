using System.Collections.Generic;
using System.Text;
using LCAbnormalities.Settings;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Patch;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using PortraitHediffDefOf = PortraitOfAnotherWorld.Def.HediffDefOf;

namespace PortraitOfAnotherWorld.Comp
{
    public class CompProperties_PortraitOfAnotherWorld : CompProperties_AbnormalityTool
    {
        public float defaultTransferMultiplier = 1.15f;
        public float switchShockDamage = 10f;
        public float unbindShockDamage = 12f;
        public float substituteDeathWhiteShock = 20f;
        public float bondCollapseBlackDamage = 35f;

        public CompProperties_PortraitOfAnotherWorld()
        {
            compClass = typeof(CompPortraitOfAnotherWorld);
        }
    }

    /// <summary>
    /// O-09-91 异界肖像核心逻辑：绑定受庇护者与随机替死替身，拦截转嫁伤害，并在替身死亡时染血换替身或反噬。
    /// </summary>
    public class CompPortraitOfAnotherWorld : CompAbnormalityTool
    {
        private static readonly int ShaderPropColor = Shader.PropertyToID("_Color");
        private static readonly int ShaderPropAgeSecs = Shader.PropertyToID("_AgeSecs");
        private static readonly int ShaderPropCrimsonAmount = Shader.PropertyToID("_CrimsonAmount");
        private static readonly int ShaderPropRevealAmount = Shader.PropertyToID("_RevealAmount");

        private static Mesh cachedCanvasOverlayMesh;
        private static Shader cachedPortraitShader;

        private Pawn protectedPawn;
        private Pawn substitutePawn;
        private bool crimsonStained;
        private int totalDamageRedirected;
        private Material portraitOverlayMat;

        public new CompProperties_PortraitOfAnotherWorld Props =>
            (CompProperties_PortraitOfAnotherWorld)props;

        public Pawn ProtectedPawn => protectedPawn;

        public Pawn SubstitutePawn => substitutePawn;

        public bool IsBound =>
            protectedPawn != null && !protectedPawn.Dead && !protectedPawn.Destroyed
            && substitutePawn != null && !substitutePawn.Destroyed;

        public bool CrimsonStained => crimsonStained;

        public int TotalDamageRedirected => totalDamageRedirected;

        public bool IsBoundAs(Pawn pawn, bool asProtected)
        {
            if (pawn == null)
            {
                return false;
            }

            return asProtected ? pawn == protectedPawn : pawn == substitutePawn;
        }

        public override AcceptanceReport CanUseTool(Pawn pawn)
        {
            AcceptanceReport baseReport = base.CanUseTool(pawn);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            if (pawn == protectedPawn)
            {
                return "LC_Portrait_AlreadyBound".Translate();
            }

            if (FindEligibleSubstitutes(pawn).Count == 0)
            {
                return "LC_Portrait_NoSubstitute".Translate();
            }

            return true;
        }

        public List<Pawn> FindEligibleSubstitutes(Pawn excludePawn)
        {
            List<Pawn> result = new List<Pawn>();
            Map map = ActiveMap;
            if (map?.mapPawns == null)
            {
                return result;
            }

            bool allowNonColonists = LCAbnormalitiesMod.Settings?.PortraitAllowNonColonistSubstitute ?? false;
            IReadOnlyList<Pawn> candidates = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn p = candidates[i];
                if (p == null || p == excludePawn || p.Dead || p.Destroyed || !p.Spawned)
                {
                    continue;
                }

                if (!p.RaceProps.Humanlike || p.def is ThingDef_AbnormalityBase || p.TryGetComp<CompAbnormality>() != null)
                {
                    continue;
                }

                bool isPlayerMember = p.Faction == Faction.OfPlayer || p.IsColonist || p.IsSlaveOfColony;
                bool isFriendlyOrPrisoner = allowNonColonists && (p.IsPrisonerOfColony || !p.HostileTo(Faction.OfPlayer));
                if (isPlayerMember || isFriendlyOrPrisoner)
                {
                    result.Add(p);
                }
            }

            return result;
        }

        public override void OnToolUsed(Pawn user)
        {
            if (user == null)
            {
                return;
            }

            List<Pawn> substitutes = FindEligibleSubstitutes(user);
            if (substitutes.Count == 0)
            {
                return;
            }

            if (protectedPawn != null && protectedPawn != user && !protectedPawn.Dead && !protectedPawn.Destroyed)
            {
                ApplyWhiteMentalShock(protectedPawn, Props.switchShockDamage);
            }

            RemovePortraitHediffs(protectedPawn);
            RemovePortraitHediffs(substitutePawn);

            Pawn chosen = substitutes.RandomElement();
            protectedPawn = user;
            substitutePawn = chosen;
            crimsonStained = false;

            AttachLinkHediff(protectedPawn, PortraitHediffDefOf.LC_PortraitProtected);
            AttachLinkHediff(substitutePawn, PortraitHediffDefOf.LC_PortraitSubstitute);

            RecordToolUse(user, 1, 0);

            if (ActiveMap != null)
            {
                Messages.Message(
                    "LC_Portrait_BoundMessage".Translate(protectedPawn.LabelShortCap, substitutePawn.LabelShortCap),
                    new LookTargets(protectedPawn, substitutePawn),
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public bool TryRedirectDamage(Pawn victim, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (victim == null || victim != protectedPawn)
            {
                return false;
            }

            if (substitutePawn == null || substitutePawn.Dead || substitutePawn.Destroyed || !substitutePawn.Spawned)
            {
                HandleSubstituteLost(substitutePawn, triggerBacklash: false);
                if (substitutePawn == null || substitutePawn.Dead || substitutePawn.Destroyed || !substitutePawn.Spawned)
                {
                    return false;
                }
            }

            float transferMult = Mathf.Clamp(
                LCAbnormalitiesMod.Settings?.PortraitDamageTransferMultiplier ?? Props.defaultTransferMultiplier,
                0.5f,
                2.5f);
            float scaledAmount = Mathf.Max(1f, dinfo.Amount * transferMult);

            DamageInfo transferred = new DamageInfo(dinfo);
            transferred.SetAmount(scaledAmount);
            transferred.SetHitPart(null);

            absorbed = true;
            totalDamageRedirected += Mathf.RoundToInt(scaledAmount);

            Pawn currentSub = substitutePawn;
            Patch_Pawn_PreApplyDamage.RunWithRedirectGuard(delegate
            {
                if (currentSub != null && !currentSub.Dead && !currentSub.Destroyed)
                {
                    currentSub.TakeDamage(transferred);
                }
            });

            if (victim.Spawned && victim.Map != null)
            {
                MoteMaker.ThrowText(
                    victim.DrawPos,
                    victim.Map,
                    "LC_Portrait_RedirectedFloating".Translate(Mathf.RoundToInt(scaledAmount)),
                    new Color(1f, 0.85f, 0.2f),
                    2.0f);
                FleckMaker.ThrowMicroSparks(victim.DrawPos, victim.Map);
            }

            if (currentSub != null && currentSub.Spawned && !currentSub.Dead && currentSub.Map != null)
            {
                FleckMaker.ThrowMicroSparks(currentSub.DrawPos, currentSub.Map);
            }

            return true;
        }

        public void Notify_SubstituteDied(Pawn deadSub)
        {
            if (deadSub == null || deadSub != substitutePawn)
            {
                return;
            }

            HandleSubstituteLost(deadSub, triggerBacklash: true);
        }

        public void Notify_ProtectedDied(Pawn deadProtected)
        {
            if (deadProtected != null && deadProtected == protectedPawn)
            {
                ClearBond(notify: false);
            }
        }

        private void HandleSubstituteLost(Pawn deadSub, bool triggerBacklash)
        {
            RemovePortraitHediffs(deadSub);
            substitutePawn = null;
            crimsonStained = true;

            Pawn ward = protectedPawn;
            if (ward == null || ward.Dead || ward.Destroyed)
            {
                ClearBond(notify: false);
                return;
            }

            if (triggerBacklash)
            {
                ApplyWhiteMentalShock(ward, Props.substituteDeathWhiteShock);
            }

            List<Pawn> candidates = FindEligibleSubstitutes(ward);
            if (candidates.Count > 0)
            {
                Pawn replacement = candidates.RandomElement();
                substitutePawn = replacement;
                AttachLinkHediff(replacement, PortraitHediffDefOf.LC_PortraitSubstitute);

                if (ActiveMap != null)
                {
                    Messages.Message(
                        "LC_Portrait_SubstituteReplaced".Translate(
                            deadSub?.LabelShortCap ?? "None".Translate().ToString(),
                            replacement.LabelShortCap),
                        new LookTargets(ward, replacement),
                        MessageTypeDefOf.NegativeEvent);
                }
                return;
            }

            ClearBond(notify: false);

            if (triggerBacklash && Props.bondCollapseBlackDamage > 0f && !ward.Dead && !ward.Destroyed && ward.Spawned)
            {
                DamageDef blackDamage = LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
                if (blackDamage != null)
                {
                    Patch_Pawn_PreApplyDamage.RunWithRedirectGuard(delegate
                    {
                        ward.TakeDamage(new DamageInfo(blackDamage, Props.bondCollapseBlackDamage, instigator: InteractionTarget ?? parent));
                    });
                }
            }

            if (ActiveMap != null)
            {
                Messages.Message(
                    "LC_Portrait_BondCollapsed".Translate(
                        deadSub?.LabelShortCap ?? "None".Translate().ToString(),
                        ward.LabelShortCap),
                    ward,
                    MessageTypeDefOf.ThreatBig);
            }
        }

        public void ClearBond(bool notify, bool applyUnbindShock = false)
        {
            Pawn previousProtected = protectedPawn;
            Pawn previousSub = substitutePawn;

            protectedPawn = null;
            substitutePawn = null;
            crimsonStained = false;

            RemovePortraitHediffs(previousProtected);
            RemovePortraitHediffs(previousSub);

            if (applyUnbindShock && previousProtected != null && !previousProtected.Dead && !previousProtected.Destroyed)
            {
                ApplyWhiteMentalShock(previousProtected, Props.unbindShockDamage);
            }

            if (notify && ActiveMap != null && previousProtected != null)
            {
                Messages.Message(
                    "LC_Portrait_UnboundMessage".Translate(previousProtected.LabelShortCap),
                    InteractionTarget ?? parent,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        private void AttachLinkHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health == null || def == null)
            {
                return;
            }

            RemovePortraitHediffs(pawn);
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            if (hediff is HediffWithComps hwc)
            {
                HediffComp_PortraitLink link = hwc.TryGetComp<HediffComp_PortraitLink>();
                if (link != null)
                {
                    link.portraitThing = parent;
                }
            }
            pawn.health.AddHediff(hediff);
        }

        private void RemovePortraitHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
            {
                return;
            }

            List<Hediff> toRemove = null;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h == null)
                {
                    continue;
                }

                if (h.def != PortraitHediffDefOf.LC_PortraitProtected && h.def != PortraitHediffDefOf.LC_PortraitSubstitute)
                {
                    continue;
                }

                if (h is HediffWithComps hwc)
                {
                    HediffComp_PortraitLink link = hwc.TryGetComp<HediffComp_PortraitLink>();
                    if (link != null && link.portraitThing != null && link.portraitThing != parent)
                    {
                        continue;
                    }
                }

                toRemove ??= new List<Hediff>();
                toRemove.Add(h);
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    pawn.health.RemoveHediff(toRemove[i]);
                }
            }
        }

        private void ApplyWhiteMentalShock(Pawn pawn, float amount)
        {
            if (pawn == null || amount <= 0f || pawn.Dead || pawn.Destroyed)
            {
                return;
            }

            if (!DamageUtils.CanTakeMentalDamage(pawn))
            {
                return;
            }

            DamageDef whiteDamage = LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage;
            DamageInfo dinfo = whiteDamage != null
                ? new DamageInfo(whiteDamage, amount, instigator: parent)
                : new DamageInfo(RimWorld.DamageDefOf.Stun, amount, instigator: parent);
            DamageUtils.ApplyMentalDamage(pawn, amount, dinfo);
        }

        public override void OnLeftPlatform()
        {
            base.OnLeftPlatform();
            DestroyPortraitMaterial();
            ClearBond(notify: false);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            DestroyPortraitMaterial();
            base.PostDestroy(mode, previousMap);
        }

        protected override Color GetHeldOverrideColor()
        {
            if (IsBound && crimsonStained)
            {
                return new Color(1.0f, 0.84f, 0.82f);
            }

            return Color.white;
        }

        public override bool TryDrawHeldOverride(DrawPhase phase, Vector3 drawLoc)
        {
            if (!IsBound)
            {
                return false;
            }

            if (phase != DrawPhase.Draw)
            {
                return true;
            }

            Pawn pawn = parent as Pawn;
            List<PawnKindLifeStage> lifeStages = pawn?.kindDef?.lifeStages;
            GraphicData bodyGraphic = (lifeStages != null && lifeStages.Count > 0) ? lifeStages[0].bodyGraphicData : null;
            string texPath = GetHeldOverrideTexPath() ?? bodyGraphic?.texPath;
            if (texPath.NullOrEmpty())
            {
                return false;
            }

            Vector2 drawSize = bodyGraphic?.drawSize ?? new Vector2(2.1f, 2.1f);
            CompAbnormalityHoldingPlatformTarget targetComp = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (targetComp != null && targetComp.Props.heldDrawScale > 0f && Mathf.Abs(targetComp.Props.heldDrawScale - 1f) > 0.0001f)
            {
                drawSize *= targetComp.Props.heldDrawScale;
            }

            float y = drawLoc.y;
            Vector3 adjustedLoc = new Vector3(drawLoc.x, y, drawLoc.z);
            Graphic baseGraphic = GraphicDatabase.Get<Graphic_Single>(texPath, ShaderDatabase.Cutout, drawSize, GetHeldOverrideColor());
            baseGraphic?.Draw(adjustedLoc, Rot4.North, parent, 0f);

            RenderTexture portraitTex = PortraitsCache.Get(
                protectedPawn,
                new Vector2(96f, 148f),
                Rot4.South,
                cameraOffset: new Vector3(0f, 0f, 0.34f),
                cameraZoom: 1.48f,
                supersample: false,
                compensateForUIScale: false,
                renderHeadgear: false,
                renderClothes: true,
                healthStateOverride: PawnHealthState.Mobile);

            Shader portraitShader = GetPortraitShader();
            if (portraitTex != null && portraitShader != null)
            {
                if (portraitOverlayMat == null || portraitOverlayMat.shader != portraitShader)
                {
                    DestroyPortraitMaterial();
                    portraitOverlayMat = new Material(portraitShader);
                }

                portraitOverlayMat.mainTexture = portraitTex;
                portraitOverlayMat.SetColor(ShaderPropColor, Color.white);
                portraitOverlayMat.SetFloat(ShaderPropAgeSecs, (Find.TickManager?.TicksGame ?? 0) / 60f);
                portraitOverlayMat.SetFloat(ShaderPropCrimsonAmount, crimsonStained ? 1f : 0f);
                portraitOverlayMat.SetFloat(ShaderPropRevealAmount, 1f);

                Matrix4x4 overlayMatrix = Matrix4x4.TRS(
                    new Vector3(adjustedLoc.x, adjustedLoc.y + 0.005f, adjustedLoc.z),
                    Quaternion.identity,
                    new Vector3(drawSize.x, 1f, drawSize.y));
                GenDraw.DrawMeshNowOrLater(GetCanvasOverlayMesh(), overlayMatrix, portraitOverlayMat, false);
            }

            return true;
        }

        private void DestroyPortraitMaterial()
        {
            if (portraitOverlayMat != null)
            {
                Object.Destroy(portraitOverlayMat);
                portraitOverlayMat = null;
            }
        }

        private static Shader GetPortraitShader()
        {
            if (cachedPortraitShader == null)
            {
                cachedPortraitShader = DefDatabase<ShaderDef>.GetNamedSilentFail("LC_PortraitOfAnotherWorld")?.Shader
                    ?? ShaderDatabase.LoadShader("Map/LC_PortraitOfAnotherWorld");
            }
            return cachedPortraitShader;
        }

        private static Mesh GetCanvasOverlayMesh()
        {
            if (cachedCanvasOverlayMesh != null)
            {
                return cachedCanvasOverlayMesh;
            }

            // Normalized to PortraitOfAnotherWorld.png interior canvas corners (-0.5..+0.5 unit quad).
            Vector3[] vertices =
            {
                new Vector3(-0.125f, 0f, -0.248f), // BL
                new Vector3(-0.205f, 0f, +0.295f), // TL
                new Vector3(+0.130f, 0f, +0.290f), // TR
                new Vector3(+0.228f, 0f, -0.248f)  // BR
            };
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            int[] triangles = { 0, 1, 2, 0, 2, 3 };

            Mesh mesh = new Mesh
            {
                name = "LC_PortraitCanvasOverlayMesh",
                vertices = vertices,
                uv = uvs
            };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cachedCanvasOverlayMesh = mesh;
            return cachedCanvasOverlayMesh;
        }

        public override void DrawExtraSelectionOverlaysForUnit(IAbnormalityContainmentUnit unit)
        {
            Map map = ActiveMap;
            if (!IsBound || map == null)
            {
                return;
            }

            Vector3 anchor = ActiveDrawPos;
            Pawn ward = protectedPawn;
            Pawn sub = substitutePawn;

            if (ward != null && ward.Spawned && ward.Map == map)
            {
                GenDraw.DrawLineBetween(anchor, ward.DrawPos, SimpleColor.Yellow);
            }

            if (sub != null && sub.Spawned && sub.Map == map)
            {
                GenDraw.DrawLineBetween(anchor, sub.DrawPos, SimpleColor.Red);
            }
        }

        public override IEnumerable<Gizmo> GetPlatformGizmos()
        {
            foreach (Gizmo gizmo in base.GetPlatformGizmos())
            {
                yield return gizmo;
            }

            if (IsOperational && IsBound)
            {
                yield return new Command_Action
                {
                    defaultLabel = "LC_Portrait_UnbindLabel".Translate(),
                    defaultDesc = "LC_Portrait_UnbindDesc".Translate(),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = delegate
                    {
                        ClearBond(notify: true, applyUnbindShock: true);
                    }
                };
            }
        }

        public override string GetToolStatusInspectString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsBound)
            {
                sb.AppendLine("LC_Portrait_InspectBound".Translate(protectedPawn.LabelShortCap, substitutePawn.LabelShortCap));
                if (crimsonStained)
                {
                    sb.AppendLine("LC_Portrait_InspectCrimson".Translate());
                }
                if (totalDamageRedirected > 0)
                {
                    sb.AppendLine("LC_Portrait_InspectRedirected".Translate(totalDamageRedirected));
                }
            }
            else
            {
                sb.AppendLine("LC_Portrait_InspectUnbound".Translate());
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref protectedPawn, "lcPortraitProtectedPawn");
            Scribe_References.Look(ref substitutePawn, "lcPortraitSubstitutePawn");
            Scribe_Values.Look(ref crimsonStained, "lcPortraitCrimsonStained", false);
            Scribe_Values.Look(ref totalDamageRedirected, "lcPortraitTotalRedirected", 0);
        }
    }
}
