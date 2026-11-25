using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAbnormalities.SnowQueen
{
    /// <summary>
    /// F-01-37 冰雪女皇多部件 Spine 骨骼渲染节点属性。
    /// </summary>
    public class PawnRenderNodeProperties_SnowQueenPart : PawnRenderNodeProperties
    {
        public bool isRoot;
        public bool isEmptyPivot;
        public float formScale = 2.15f;

        public bool NoGraphic => isRoot || isEmptyPivot;

        public PawnRenderNodeProperties_SnowQueenPart()
        {
            nodeClass = typeof(PawnRenderNode_SnowQueenPart);
            workerClass = typeof(PawnRenderNodeWorker_SnowQueenPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (NoGraphic)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>
    /// 冰雪女皇 Spine 部件渲染节点：使用 1x1 标准化单位网格确保烘焙 UV 枢轴与关键帧旋转严格对齐。
    /// </summary>
    public class PawnRenderNode_SnowQueenPart : PawnRenderNode
    {
        private Graphic cachedGraphic;

        public PawnRenderNodeProperties_SnowQueenPart PartProps => Props as PawnRenderNodeProperties_SnowQueenPart;

        public PawnRenderNode_SnowQueenPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.NoGraphic)
            {
                return null;
            }

            if (Props.overrideMeshSize.HasValue)
            {
                return base.MeshSetFor(pawn);
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_SnowQueenPart partProps = PartProps;
            if (partProps == null || partProps.NoGraphic || partProps.texPath.NullOrEmpty())
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

            // 采用 Transparent 着色器以保留冰晶发梢与霜衣半透明边缘的抗锯齿过渡
            cachedGraphic = GraphicDatabase.Get<Graphic_Single>(
                partProps.texPath,
                ShaderDatabase.Transparent,
                Vector2.one,
                Color.white);
            return cachedGraphic;
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            Graphic g = GraphicFor(pawn);
            if (g != null)
            {
                yield return g;
            }
        }
    }

    /// <summary>
    /// 冰雪女皇 Spine 渲染节点工作者：固定南向贴图采样并仅对根节点施加整体形态缩放。
    /// </summary>
    public class PawnRenderNodeWorker_SnowQueenPart : PawnRenderNodeWorker
    {
        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_SnowQueenPart partProps = (node as PawnRenderNode_SnowQueenPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_SnowQueenPart);
            if (partProps != null && partProps.isRoot && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }

            return scale;
        }
    }
}
