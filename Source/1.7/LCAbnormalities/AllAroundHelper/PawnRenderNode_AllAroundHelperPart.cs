using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.AllAroundHelper
{
    /// <summary>T-05-41 小帮手分段骨骼渲染节点角色分类（对应解包 Helper.fbx 与 HelperSprite.png）。</summary>
    public enum AllAroundHelperRenderRole
    {
        Root,
        SawRotor,
        Body,
        Face,
        LegLeft,
        LegRight,
        ArmUL,
        BladeUL,
        ArmUR,
        BladeUR,
        ArmDL,
        BladeDL,
        ArmDR,
        BladeDR
    }

    /// <summary>小帮手多部件骨骼渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_AllAroundHelperPart : PawnRenderNodeProperties
    {
        public AllAroundHelperRenderRole role = AllAroundHelperRenderRole.Body;
        public float formScale = 1.95f;
        public float containedScale = 1.75f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;
        public List<string> alternateTexPaths;

        public bool IsGroupRoot => role == AllAroundHelperRenderRole.Root;

        public PawnRenderNodeProperties_AllAroundHelperPart()
        {
            nodeClass = typeof(PawnRenderNode_AllAroundHelperPart);
            workerClass = typeof(PawnRenderNodeWorker_AllAroundHelperPart);
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

    /// <summary>小帮手多部件骨骼渲染节点：支持 Lv1/Lv2/Lv3 形态切换、面部红眼警报/宕机熄灭切换与高速锯盘自转。</summary>
    public class PawnRenderNode_AllAroundHelperPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_AllAroundHelperPart PartProps => Props as PawnRenderNodeProperties_AllAroundHelperPart;

        public PawnRenderNode_AllAroundHelperPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_AllAroundHelperPawn hp && hp.IsHeldOnPlatform;
        }

        public static CompAllAroundHelper GetHelperComp(Pawn pawn)
        {
            return (pawn as LC_AllAroundHelperPawn)?.HelperComp ?? pawn?.TryGetComp<CompAllAroundHelper>();
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
            PawnRenderNodeProperties_AllAroundHelperPart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_AllAroundHelperPart partProps = PartProps;
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
            PawnRenderNodeProperties_AllAroundHelperPart partProps = PartProps;
            if (partProps == null)
            {
                return null;
            }

            if (partProps.role == AllAroundHelperRenderRole.Face && !parms.Portrait && partProps.alternateTexPaths != null)
            {
                CompAllAroundHelper comp = GetHelperComp(parms.pawn);
                if (comp != null)
                {
                    // alternateTexPaths[0] = Face_Alert (crimson glow), [1] = Face_Off (dimmed shutdown)
                    if (comp.IsShuttingDown && partProps.alternateTexPaths.Count > 1)
                    {
                        return partProps.alternateTexPaths[1];
                    }
                    if (comp.IsSawingActive && partProps.alternateTexPaths.Count > 0)
                    {
                        return partProps.alternateTexPaths[0];
                    }
                }
            }

            return partProps.texPath;
        }
    }

    /// <summary>
    /// 小帮手渲染节点工作者：
    /// 1) Lv1（收容且计数>=2）：仅显示圆润白壳、笑脸与双轮小短腿；
    /// 2) Lv2（收容且计数<=1）：上方两条血刃机械臂从壳后探出微颤；
    /// 3) Lv3（出逃常态/蓄力/宕机）：收起小腿、四条二段关节血镰臂全面展开；
    /// 4) Lv3_Spin（高速冲锋与蓄力末段）：激活 8 刃 SawRotor 锯盘高速自转！
    /// </summary>
    public class PawnRenderNodeWorker_AllAroundHelperPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_AllAroundHelperPart partProps = (node as PawnRenderNode_AllAroundHelperPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_AllAroundHelperPart);
            if (partProps == null)
            {
                return false;
            }

            CompAllAroundHelper comp = PawnRenderNode_AllAroundHelperPart.GetHelperComp(parms.pawn);
            bool breached = !parms.Portrait && comp != null && comp.Escaped;
            bool isAgitatedLv2 = !parms.Portrait && !breached && comp != null && comp.QliphothCountCurrent <= 1;
            bool isSpinning = breached && comp.IsDashing;
            bool isHighCharge = breached && comp.IsCharging && comp.ChargeProgress >= 0.68f;

            switch (partProps.role)
            {
                case AllAroundHelperRenderRole.Root:
                case AllAroundHelperRenderRole.Body:
                case AllAroundHelperRenderRole.Face:
                    return true;

                case AllAroundHelperRenderRole.LegLeft:
                case AllAroundHelperRenderRole.LegRight:
                    return !breached;

                case AllAroundHelperRenderRole.ArmUL:
                case AllAroundHelperRenderRole.BladeUL:
                case AllAroundHelperRenderRole.ArmUR:
                case AllAroundHelperRenderRole.BladeUR:
                    if (isSpinning) return false;
                    return breached || isAgitatedLv2;

                case AllAroundHelperRenderRole.ArmDL:
                case AllAroundHelperRenderRole.BladeDL:
                case AllAroundHelperRenderRole.ArmDR:
                case AllAroundHelperRenderRole.BladeDR:
                    if (isSpinning) return false;
                    return breached;

                case AllAroundHelperRenderRole.SawRotor:
                    return isSpinning || isHighCharge;

                default:
                    return true;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_AllAroundHelperPart hNode = node as PawnRenderNode_AllAroundHelperPart;
            if (hNode != null)
            {
                string resolvedPath = hNode.ResolveDynamicTexPath(parms);
                Graphic g = hNode.GetCachedGraphic(resolvedPath);
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
            PawnRenderNodeProperties_AllAroundHelperPart partProps = (node as PawnRenderNode_AllAroundHelperPart)?.PartProps;
            if (partProps != null
                && partProps.role == AllAroundHelperRenderRole.Root
                && !parms.Portrait
                && PawnRenderNode_AllAroundHelperPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNodeProperties_AllAroundHelperPart partProps = (node as PawnRenderNode_AllAroundHelperPart)?.PartProps;
            if (partProps != null && partProps.role == AllAroundHelperRenderRole.SawRotor && !parms.Portrait)
            {
                CompAllAroundHelper comp = PawnRenderNode_AllAroundHelperPart.GetHelperComp(parms.pawn);
                float degPerTick = (comp != null && comp.IsDashing) ? 34f : 18f;
                float spinAngle = ((Find.TickManager?.TicksGame ?? 0) * degPerTick) % 360f;
                return Quaternion.AngleAxis(spinAngle, Vector3.up);
            }

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
            PawnRenderNodeProperties_AllAroundHelperPart partProps = (node as PawnRenderNode_AllAroundHelperPart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if (partProps.IsGroupRoot)
            {
                float targetScale = partProps.formScale;
                if (!parms.Portrait
                    && PawnRenderNode_AllAroundHelperPart.IsHeldOnPlatform(parms.pawn)
                    && partProps.containedScale > 0f)
                {
                    targetScale = partProps.containedScale;
                }

                if (!Mathf.Approximately(targetScale, 1f))
                {
                    scale.x *= targetScale;
                    scale.z *= targetScale;
                }
            }
            else if (partProps.role == AllAroundHelperRenderRole.SawRotor && !parms.Portrait)
            {
                CompAllAroundHelper comp = PawnRenderNode_AllAroundHelperPart.GetHelperComp(parms.pawn);
                float rotorScale = (comp != null && comp.IsDashing)
                    ? 1.16f
                    : Mathf.Lerp(0.65f, 1.05f, comp != null ? Mathf.InverseLerp(0.68f, 1.0f, comp.ChargeProgress) : 0.5f);
                scale.x *= rotorScale;
                scale.z *= rotorScale;
            }

            return scale;
        }
    }
}
