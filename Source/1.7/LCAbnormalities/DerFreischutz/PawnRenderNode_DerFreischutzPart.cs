using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.DerFreischutz
{
    /// <summary>魔弹射手分段骨骼渲染节点属性定义（对应脑叶公司 Spine matan.txt 骨骼层级）。</summary>
    public class PawnRenderNodeProperties_DerFreischutzPart : PawnRenderNodeProperties
    {
        public bool isRoot = false;
        public float formScale = 2.2f;
        public string texPathLeft;

        public PawnRenderNodeProperties_DerFreischutzPart()
        {
            nodeClass = typeof(PawnRenderNode_DerFreischutzPart);
            workerClass = typeof(PawnRenderNodeWorker_DerFreischutzPart);
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

    /// <summary>魔弹射手分段骨骼渲染节点：提供 1x1 单位网格、精确枢轴对齐与独立左向贴图预加载。</summary>
    public class PawnRenderNode_DerFreischutzPart : PawnRenderNode
    {
        private Graphic cachedGraphic;
        private Graphic cachedGraphicLeft;

        public PawnRenderNodeProperties_DerFreischutzPart PartProps => Props as PawnRenderNodeProperties_DerFreischutzPart;

        public PawnRenderNode_DerFreischutzPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
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

            if (parms.pawn is LC_DerFreischutzPawn df)
            {
                return df.IsFacingLeft;
            }

            return parms.facing == Rot4.West;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.isRoot)
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GraphicLeftFor(Pawn pawn)
        {
            PawnRenderNodeProperties_DerFreischutzPart partProps = PartProps;
            if (partProps == null || partProps.isRoot || partProps.texPathLeft.NullOrEmpty())
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
            PawnRenderNodeProperties_DerFreischutzPart partProps = PartProps;
            if (partProps == null || partProps.isRoot || partProps.texPath.NullOrEmpty())
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

    /// <summary>魔弹射手分段骨骼渲染节点工作者：同步左右镜像偏移、枢轴与关键帧旋转。</summary>
    public class PawnRenderNodeWorker_DerFreischutzPart : PawnRenderNodeWorker
    {
        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_DerFreischutzPart dfNode = node as PawnRenderNode_DerFreischutzPart;
            if (dfNode != null && dfNode.ShouldUseLeftFacing(parms))
            {
                Graphic leftGraphic = dfNode.GraphicLeftFor(parms.pawn);
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
            PawnRenderNode_DerFreischutzPart dfNode = node as PawnRenderNode_DerFreischutzPart;
            if (dfNode != null && dfNode.ShouldUseLeftFacing(parms))
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

            PawnRenderNode_DerFreischutzPart dfNode = node as PawnRenderNode_DerFreischutzPart;
            if (dfNode != null && dfNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_DerFreischutzPart partProps = (node as PawnRenderNode_DerFreischutzPart)?.PartProps;
            if (partProps != null && partProps.isRoot && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }

            return scale;
        }
    }
}
