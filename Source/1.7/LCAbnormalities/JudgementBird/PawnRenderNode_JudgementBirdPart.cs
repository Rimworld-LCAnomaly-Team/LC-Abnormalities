using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.JudgementBird
{
    /// <summary>审判鸟分段骨骼渲染节点属性（对应脑叶公司 Spine 긴새.json.txt 骨骼层级）。</summary>
    public class PawnRenderNodeProperties_JudgementBirdPart : PawnRenderNodeProperties
    {
        public bool isRoot = false;
        public float formScale = 2.55f;
        public string texPathLeft;
        public bool onlyDuringBigScale = false;
        public bool hideDuringBigScale = false;

        public PawnRenderNodeProperties_JudgementBirdPart()
        {
            nodeClass = typeof(PawnRenderNode_JudgementBirdPart);
            workerClass = typeof(PawnRenderNodeWorker_JudgementBirdPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (isRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>审判鸟分段骨骼渲染节点：提供 1x1 单位网格、精确枢轴对齐与独立左向贴图预加载。</summary>
    public class PawnRenderNode_JudgementBirdPart : PawnRenderNode
    {
        private Graphic cachedGraphic;
        private Graphic cachedGraphicLeft;

        public PawnRenderNodeProperties_JudgementBirdPart PartProps => Props as PawnRenderNodeProperties_JudgementBirdPart;

        public PawnRenderNode_JudgementBirdPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            if (parms.Portrait)
            {
                return false;
            }

            if (parms.pawn is LC_JudgementBirdPawn jb)
            {
                return jb.IsFacingLeft;
            }

            return parms.facing == Rot4.West;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && (PartProps.isRoot || !PartProps.useGraphic))
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GraphicLeftFor(Pawn pawn)
        {
            PawnRenderNodeProperties_JudgementBirdPart partProps = PartProps;
            if (partProps == null || partProps.isRoot || !partProps.useGraphic || partProps.texPathLeft.NullOrEmpty())
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
            PawnRenderNodeProperties_JudgementBirdPart partProps = PartProps;
            if (partProps == null || partProps.isRoot || !partProps.useGraphic || partProps.texPath.NullOrEmpty())
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
            Graphic right = GraphicFor(pawn);
            if (right != null)
            {
                yield return right;
            }

            Graphic left = GraphicLeftFor(pawn);
            if (left != null)
            {
                yield return left;
            }
        }
    }

    /// <summary>审判鸟分段骨骼渲染节点工作者：同步左右镜像偏移、枢轴、旋转及 BigChoo 大天平显隐切换。</summary>
    public class PawnRenderNodeWorker_JudgementBirdPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_JudgementBirdPart partProps = (node as PawnRenderNode_JudgementBirdPart)?.PartProps;
            if (partProps != null)
            {
                bool showBigScale = !parms.Portrait && (parms.pawn as LC_JudgementBirdPawn)?.IsBigScaleVisible == true;
                if (partProps.onlyDuringBigScale && !showBigScale)
                {
                    return false;
                }

                if (partProps.hideDuringBigScale && showBigScale)
                {
                    return false;
                }
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_JudgementBirdPart jbNode = node as PawnRenderNode_JudgementBirdPart;
            if (jbNode != null && jbNode.ShouldUseLeftFacing(parms))
            {
                Graphic leftGraphic = jbNode.GraphicLeftFor(parms.pawn);
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
            PawnRenderNode_JudgementBirdPart jbNode = node as PawnRenderNode_JudgementBirdPart;
            if (jbNode != null && jbNode.ShouldUseLeftFacing(parms))
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

            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out float animRot))
            {
                angle += animRot;
            }

            PawnRenderNode_JudgementBirdPart jbNode = node as PawnRenderNode_JudgementBirdPart;
            if (jbNode != null && jbNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_JudgementBirdPart partProps = (node as PawnRenderNode_JudgementBirdPart)?.PartProps;
            if (partProps != null && partProps.isRoot && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }

            return scale;
        }
    }
}
