using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FuneralOfDeadButterflies
{
    /// <summary>亡蝶葬仪渲染树节点的姿态与角色分类。</summary>
    public enum FuneralRenderRole
    {
        FrontRoot,
        FrontPart,
        SideRoot,
        SidePart,
        SideArmRest,
        SideArmGun,
        OpenRoot,
        OpenPart
    }

    /// <summary>亡蝶葬仪多部件骨骼渲染节点属性。</summary>
    public class PawnRenderNodeProperties_FuneralPart : PawnRenderNodeProperties
    {
        public FuneralRenderRole role = FuneralRenderRole.FrontPart;
        public float formScale = 2.05f;
        public bool flipOnWest = true;
        public string texPathLeft;

        public bool IsRigRoot =>
            role == FuneralRenderRole.FrontRoot
            || role == FuneralRenderRole.SideRoot
            || role == FuneralRenderRole.OpenRoot;

        public PawnRenderNodeProperties_FuneralPart()
        {
            nodeClass = typeof(PawnRenderNode_FuneralPart);
            workerClass = typeof(PawnRenderNodeWorker_FuneralPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsRigRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>亡蝶葬仪多部件骨骼渲染节点：支持正面收容态、侧面移动/攻击态、开棺爆发态及独立左向贴图。</summary>
    public class PawnRenderNode_FuneralPart : PawnRenderNode
    {
        private Graphic cachedGraphic;
        private Graphic cachedGraphicLeft;

        public PawnRenderNodeProperties_FuneralPart PartProps => Props as PawnRenderNodeProperties_FuneralPart;

        public PawnRenderNode_FuneralPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_FuneralPart partProps = PartProps;
            if (partProps == null || !partProps.flipOnWest)
            {
                return false;
            }

            if (parms.Portrait)
            {
                return false;
            }

            if (parms.pawn is LC_FuneralOfDeadButterfliesPawn fp && fp.FuneralComp?.Escaped == true)
            {
                return fp.IsFacingLeft;
            }

            return parms.facing == Rot4.West;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.IsRigRoot)
            {
                return null;
            }

            if (Props.overrideMeshSize.HasValue)
            {
                return base.MeshSetFor(pawn);
            }

            // 1x1 单位网格，使所有 UV 枢轴 (u-0.5, v-0.5) 与关键帧旋转/缩放严丝合缝
            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GraphicLeftFor(Pawn pawn)
        {
            PawnRenderNodeProperties_FuneralPart partProps = PartProps;
            if (partProps == null || partProps.IsRigRoot || partProps.texPathLeft.NullOrEmpty())
            {
                return null;
            }

            if (cachedGraphicLeft != null)
            {
                return cachedGraphicLeft;
            }

            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            cachedGraphicLeft = GraphicDatabase.Get<Graphic_Single>(partProps.texPathLeft, ShaderDatabase.Cutout, Vector2.one, Color.white);
            return cachedGraphicLeft;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_FuneralPart partProps = PartProps;
            if (partProps == null || partProps.IsRigRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            if (cachedGraphic != null)
            {
                return cachedGraphic;
            }

            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            cachedGraphic = GraphicDatabase.Get<Graphic_Single>(partProps.texPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
            return cachedGraphic;
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            Graphic g = GraphicFor(pawn);
            if (g != null)
            {
                yield return g;
            }

            Graphic left = GraphicLeftFor(pawn);
            if (left != null)
            {
                yield return left;
            }
        }
    }

    /// <summary>亡蝶葬仪渲染节点工作者：执行形态门控、左向贴图/枢轴/旋转镜像与根缩放。</summary>
    public class PawnRenderNodeWorker_FuneralPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_FuneralPart partProps = (node as PawnRenderNode_FuneralPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_FuneralPart);
            if (partProps == null)
            {
                return false;
            }

            CompFuneralOfDeadButterflies comp = (parms.pawn as LC_FuneralOfDeadButterfliesPawn)?.FuneralComp
                ?? parms.pawn?.TryGetComp<CompFuneralOfDeadButterflies>();

            bool useFront = parms.Portrait || comp == null || !comp.Escaped;
            bool useOpen = !useFront && comp.IsCoffinOpen;
            bool useSide = !useFront && !useOpen;
            bool isAttacking = useSide && comp.IsPlayingAttackAnim;

            switch (partProps.role)
            {
                case FuneralRenderRole.FrontRoot:
                case FuneralRenderRole.FrontPart:
                    return useFront;

                case FuneralRenderRole.SideRoot:
                case FuneralRenderRole.SidePart:
                    return useSide;

                case FuneralRenderRole.SideArmRest:
                    return useSide && !isAttacking;

                case FuneralRenderRole.SideArmGun:
                    return useSide && isAttacking;

                case FuneralRenderRole.OpenRoot:
                case FuneralRenderRole.OpenPart:
                    return useOpen;

                default:
                    return false;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_FuneralPart fNode = node as PawnRenderNode_FuneralPart;
            if (fNode != null && fNode.ShouldUseLeftFacing(parms))
            {
                Graphic leftGraphic = fNode.GraphicLeftFor(parms.pawn);
                if (leftGraphic != null)
                {
                    PawnDrawParms leftParms = parms;
                    leftParms.facing = Rot4.South;
                    Material leftMat = leftGraphic.NodeGetMat(leftParms);
                    if (leftMat != null && !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
                    {
                        leftMat = InvisibilityMatPool.GetInvisibleMat(leftMat);
                    }
                    if (leftMat != null)
                    {
                        return leftMat;
                    }
                }
            }

            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_FuneralPart fNode = node as PawnRenderNode_FuneralPart;
            if (fNode != null && fNode.ShouldUseLeftFacing(parms))
            {
                offset.x = -offset.x;
                pivot.x = -pivot.x;
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
            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out float animAngle))
            {
                angle += animAngle;
            }
            PawnRenderNode_FuneralPart fNode = node as PawnRenderNode_FuneralPart;
            if (fNode != null && fNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }
            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_FuneralPart partProps = (node as PawnRenderNode_FuneralPart)?.PartProps;
            if (partProps != null && partProps.IsRigRoot && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }
            return scale;
        }
    }
}
