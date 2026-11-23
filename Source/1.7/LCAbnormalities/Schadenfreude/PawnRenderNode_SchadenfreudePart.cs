using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Schadenfreude
{
    /// <summary>O-05-76 幸灾乐祸分段骨骼渲染节点角色分类（对应解包 LookAtMe.txt 与 LookAtMe.png）。</summary>
    public enum SchadenfreudeRenderRole
    {
        Root,
        BodyBox,
        BodyMeat,
        BodyVeins,
        Eye,
        KeyholeMask,
        LensBig,
        LensSmall,
        BackLegLeftUpper,
        BackLegLeftLower,
        BackLegRightUpper,
        BackLegRightLower,
        FrontLegLeftUpper,
        FrontLegLeftLower,
        FrontLegRightUpper,
        FrontLegRightLower,
        SawArmLeft,
        SawBladeLeft,
        SawArmRight,
        SawBladeRight
    }

    /// <summary>幸灾乐祸多部件骨骼渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_SchadenfreudePart : PawnRenderNodeProperties
    {
        public SchadenfreudeRenderRole role = SchadenfreudeRenderRole.BodyBox;
        public float activeScale = 2.55f;
        public float boxScale = 2.20f;
        public float containedScale = 2.25f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;
        public List<string> alternateTexPaths;

        public bool IsGroupRoot => role == SchadenfreudeRenderRole.Root;

        public PawnRenderNodeProperties_SchadenfreudePart()
        {
            nodeClass = typeof(PawnRenderNode_SchadenfreudePart);
            workerClass = typeof(PawnRenderNodeWorker_SchadenfreudePart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsGroupRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>幸灾乐祸多部件骨骼渲染节点：支持锁孔眼自然眨眼/血红狂暴切换，以及铁盒/蛛腿/双电锯形态显隐。</summary>
    public class PawnRenderNode_SchadenfreudePart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_SchadenfreudePart PartProps => Props as PawnRenderNodeProperties_SchadenfreudePart;

        public PawnRenderNode_SchadenfreudePart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_SchadenfreudePawn sp && sp.IsHeldOnPlatform;
        }

        public static CompSchadenfreude GetSchadenComp(Pawn pawn)
        {
            return (pawn as LC_SchadenfreudePawn)?.SchadenComp ?? pawn?.TryGetComp<CompSchadenfreude>();
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            if (meshSet == null)
            {
                return null;
            }

            return meshSet.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.IsGroupRoot)
            {
                return null;
            }

            if (Props.overrideMeshSize.HasValue)
            {
                return base.MeshSetFor(pawn);
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetCachedGraphic(string path)
        {
            if (path.NullOrEmpty())
            {
                return null;
            }

            if (cachedGraphics.TryGetValue(path, out Graphic found) && found != null)
            {
                return found;
            }

            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            Shader shader = (PartProps != null && PartProps.useTransparentShader)
                ? ShaderDatabase.Transparent
                : ShaderDatabase.Cutout;

            Graphic created = GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, Color.white);
            if (created != null)
            {
                cachedGraphics[path] = created;
            }

            return created;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_SchadenfreudePart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_SchadenfreudePart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot)
            {
                yield break;
            }

            Graphic primary = GetCachedGraphic(partProps.texPath);
            if (primary != null)
            {
                yield return primary;
            }

            if (partProps.alternateTexPaths != null)
            {
                for (int i = 0; i < partProps.alternateTexPaths.Count; i++)
                {
                    Graphic g = GetCachedGraphic(partProps.alternateTexPaths[i]);
                    if (g != null)
                    {
                        yield return g;
                    }
                }
            }
        }

        public string ResolveDynamicTexPath(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_SchadenfreudePart partProps = PartProps;
            if (partProps == null)
            {
                return null;
            }

            if (partProps.role == SchadenfreudeRenderRole.Eye && !parms.Portrait && partProps.alternateTexPaths != null)
            {
                CompSchadenfreude comp = GetSchadenComp(parms.pawn);
                // alternateTexPaths[0] = Eye_Closed (blink), [1] = Eye_Alert (bloodshot crimson glare)
                if (comp != null)
                {
                    bool alertMode = comp.IsSawAttacking || comp.IsWatchedDuringStudy;
                    if (alertMode && partProps.alternateTexPaths.Count > 1)
                    {
                        return partProps.alternateTexPaths[1];
                    }
                }

                // Authentic LookAtMe periodic blink (~0.25s blink every ~2.6s)
                int ticks = (Find.TickManager?.TicksGame ?? 0) + (parms.pawn?.thingIDNumber ?? 0) * 19;
                int cycle = ticks % 156;
                if (cycle >= 134 && cycle <= 148 && partProps.alternateTexPaths.Count > 0)
                {
                    return partProps.alternateTexPaths[0];
                }
            }

            return partProps.texPath;
        }
    }

    /// <summary>
    /// 幸灾乐祸渲染节点工作者：
    /// 1) 收容期 / 突破但移出镜头（Dormant Box）：仅显示铁盒本体、双镜头、锁孔遮罩与窥视之眼；
    /// 2) 突破且处于玩家视野（Watched Active）：展开底部血肉、血管脉络与四条二段关节钻腿；
    /// 3) 双电锯必杀阶段（SawWindup / SawSpin / SawRetract）：展开左右二段机械臂与血腥电锯，并在锯切期施加高频锯齿抖动。
    /// </summary>
    public class PawnRenderNodeWorker_SchadenfreudePart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_SchadenfreudePart partProps = (node as PawnRenderNode_SchadenfreudePart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_SchadenfreudePart);
            if (partProps == null)
            {
                return false;
            }

            CompSchadenfreude comp = PawnRenderNode_SchadenfreudePart.GetSchadenComp(parms.pawn);
            bool activeSpider = !parms.Portrait && comp != null && comp.Escaped && comp.IsWatchedActive;
            bool sawsDeployed = activeSpider && comp.AreSawsVisible;

            switch (partProps.role)
            {
                case SchadenfreudeRenderRole.Root:
                case SchadenfreudeRenderRole.BodyBox:
                case SchadenfreudeRenderRole.Eye:
                case SchadenfreudeRenderRole.KeyholeMask:
                case SchadenfreudeRenderRole.LensBig:
                case SchadenfreudeRenderRole.LensSmall:
                    return true;

                case SchadenfreudeRenderRole.BodyMeat:
                case SchadenfreudeRenderRole.BodyVeins:
                case SchadenfreudeRenderRole.BackLegLeftUpper:
                case SchadenfreudeRenderRole.BackLegLeftLower:
                case SchadenfreudeRenderRole.BackLegRightUpper:
                case SchadenfreudeRenderRole.BackLegRightLower:
                case SchadenfreudeRenderRole.FrontLegLeftUpper:
                case SchadenfreudeRenderRole.FrontLegLeftLower:
                case SchadenfreudeRenderRole.FrontLegRightUpper:
                case SchadenfreudeRenderRole.FrontLegRightLower:
                    return activeSpider;

                case SchadenfreudeRenderRole.SawArmLeft:
                case SchadenfreudeRenderRole.SawBladeLeft:
                case SchadenfreudeRenderRole.SawArmRight:
                case SchadenfreudeRenderRole.SawBladeRight:
                    return sawsDeployed;

                default:
                    return true;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_SchadenfreudePart sNode = node as PawnRenderNode_SchadenfreudePart;
            if (sNode != null)
            {
                string resolvedPath = sNode.ResolveDynamicTexPath(parms);
                Graphic g = sNode.GetCachedGraphic(resolvedPath);
                if (g != null)
                {
                    PawnDrawParms matParms = parms;
                    matParms.facing = Rot4.South;
                    Material mat = g.NodeGetMat(matParms);
                    if (mat != null && !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
                    {
                        mat = InvisibilityMatPool.GetInvisibleMat(mat);
                    }
                    if (mat != null)
                    {
                        return mat;
                    }
                }
            }

            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNodeProperties_SchadenfreudePart partProps = (node as PawnRenderNode_SchadenfreudePart)?.PartProps;
            if (partProps == null)
            {
                return offset;
            }

            if (partProps.role == SchadenfreudeRenderRole.Root
                && !parms.Portrait
                && PawnRenderNode_SchadenfreudePart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
            }
            else if (!parms.Portrait && (partProps.role == SchadenfreudeRenderRole.SawBladeLeft || partProps.role == SchadenfreudeRenderRole.SawBladeRight))
            {
                CompSchadenfreude comp = PawnRenderNode_SchadenfreudePart.GetSchadenComp(parms.pawn);
                if (comp != null && comp.IsSawSpinning)
                {
                    int t = Find.TickManager?.TicksGame ?? 0;
                    float jitterX = Mathf.Sin(t * 2.3f + (partProps.role == SchadenfreudeRenderRole.SawBladeLeft ? 0f : 1.7f)) * 0.018f;
                    float jitterZ = Mathf.Cos(t * 2.9f) * 0.018f;
                    offset += new Vector3(jitterX, 0f, jitterZ);
                }
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float angle = node.DebugAngleOffset;
            if (node.Props.drawData != null)
            {
                angle += node.Props.drawData.RotationOffsetForRot(parms.facing);
            }
            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out float animRot))
            {
                angle += animRot;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_SchadenfreudePart partProps = (node as PawnRenderNode_SchadenfreudePart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if (partProps.IsGroupRoot)
            {
                CompSchadenfreude comp = PawnRenderNode_SchadenfreudePart.GetSchadenComp(parms.pawn);
                float targetScale;
                if (parms.Portrait)
                {
                    targetScale = partProps.boxScale;
                }
                else if (PawnRenderNode_SchadenfreudePart.IsHeldOnPlatform(parms.pawn))
                {
                    targetScale = partProps.containedScale;
                }
                else if (comp != null && comp.Escaped && comp.IsWatchedActive)
                {
                    targetScale = partProps.activeScale;
                }
                else
                {
                    targetScale = partProps.boxScale;
                }

                if (!Mathf.Approximately(targetScale, 1f))
                {
                    scale.x *= targetScale;
                    scale.z *= targetScale;
                }
            }

            return scale;
        }
    }
}
