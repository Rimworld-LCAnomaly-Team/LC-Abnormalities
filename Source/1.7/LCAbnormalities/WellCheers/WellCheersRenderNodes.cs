using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.WellCheers
{
    /// <summary>韦尔奇乐牌汽水多部件渲染节点的角色分类。</summary>
    public enum WellCheersPartRole
    {
        Root,
        VendingMachine,
        DispensedCan,
        StaticPart
    }

    /// <summary>韦尔奇乐牌汽水多部件渲染节点属性，支持口味贴图动态切换与统一比例缩放。</summary>
    public class PawnRenderNodeProperties_WellCheersPart : PawnRenderNodeProperties
    {
        public WellCheersPartRole role = WellCheersPartRole.StaticPart;
        public float formScale = 1f;
        public string texPathPurple;
        public string texPathRed;
        public string texPathBlue;

        public PawnRenderNodeProperties_WellCheersPart()
        {
            nodeClass = typeof(PawnRenderNode_WellCheersPart);
            workerClass = typeof(PawnRenderNodeWorker_WellCheersPart);
            pawnType = RenderNodePawnType.Any;
            drawSize = Vector2.one;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (role == WellCheersPartRole.Root)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>韦尔奇乐牌汽水多部件渲染节点：使用 1x1 归一化网格以与 512x512 UV 枢轴严格对齐，并在主线程预热三色贴图。</summary>
    public class PawnRenderNode_WellCheersPart : PawnRenderNode
    {
        private Graphic cachedStatic;
        private Graphic cachedPurple;
        private Graphic cachedRed;
        private Graphic cachedBlue;

        public PawnRenderNodeProperties_WellCheersPart PartProps => Props as PawnRenderNodeProperties_WellCheersPart;

        public PawnRenderNode_WellCheersPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

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
            if (PartProps != null && PartProps.role == WellCheersPartRole.Root)
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetFlavorGraphic(WellCheersFlavor flavor)
        {
            PawnRenderNodeProperties_WellCheersPart p = PartProps;
            if (p == null || p.role == WellCheersPartRole.Root)
            {
                return null;
            }

            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    return GetOrLoad(ref cachedRed, p.texPathRed ?? p.texPath);
                case WellCheersFlavor.Blue:
                    return GetOrLoad(ref cachedBlue, p.texPathBlue ?? p.texPath);
                default:
                    return GetOrLoad(ref cachedPurple, p.texPathPurple ?? p.texPath);
            }
        }

        private static Graphic GetOrLoad(ref Graphic slot, string path)
        {
            if (slot != null)
            {
                return slot;
            }

            if (path.NullOrEmpty() || !UnityData.IsInMainThread)
            {
                return null;
            }

            slot = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
            return slot;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_WellCheersPart p = PartProps;
            if (p == null || p.role == WellCheersPartRole.Root)
            {
                return null;
            }

            if (p.role == WellCheersPartRole.VendingMachine || p.role == WellCheersPartRole.DispensedCan)
            {
                CompWellCheers comp = (pawn as LC_WellCheersPawn)?.WellCheersComp ?? pawn?.TryGetComp<CompWellCheers>();
                return GetFlavorGraphic(comp?.CurrentFlavor ?? WellCheersFlavor.Purple);
            }

            return GetOrLoad(ref cachedStatic, p.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_WellCheersPart p = PartProps;
            if (p == null || p.role == WellCheersPartRole.Root)
            {
                yield break;
            }

            if (p.role == WellCheersPartRole.VendingMachine || p.role == WellCheersPartRole.DispensedCan)
            {
                Graphic gp = GetFlavorGraphic(WellCheersFlavor.Purple);
                if (gp != null) yield return gp;
                Graphic gr = GetFlavorGraphic(WellCheersFlavor.Red);
                if (gr != null) yield return gr;
                Graphic gb = GetFlavorGraphic(WellCheersFlavor.Blue);
                if (gb != null) yield return gb;
            }
            else
            {
                Graphic g = GetOrLoad(ref cachedStatic, p.texPath);
                if (g != null) yield return g;
            }
        }
    }

    /// <summary>韦尔奇乐牌汽水多部件渲染工作者：按状态控制出货汽水罐显隐与三色贴图切换。</summary>
    public class PawnRenderNodeWorker_WellCheersPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_WellCheersPart p = (node as PawnRenderNode_WellCheersPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_WellCheersPart);
            if (p == null)
            {
                return false;
            }

            if (p.role == WellCheersPartRole.DispensedCan && !parms.Portrait)
            {
                CompWellCheers comp = (parms.pawn as LC_WellCheersPawn)?.WellCheersComp
                    ?? parms.pawn?.TryGetComp<CompWellCheers>();
                if (comp != null && !comp.ShouldShowDispensedCan)
                {
                    return false;
                }
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_WellCheersPart wcNode = node as PawnRenderNode_WellCheersPart;
            PawnRenderNodeProperties_WellCheersPart p = wcNode?.PartProps;
            if (wcNode != null && p != null && (p.role == WellCheersPartRole.VendingMachine || p.role == WellCheersPartRole.DispensedCan))
            {
                CompWellCheers comp = (parms.pawn as LC_WellCheersPawn)?.WellCheersComp
                    ?? parms.pawn?.TryGetComp<CompWellCheers>();
                WellCheersFlavor flavor = comp?.CurrentFlavor ?? WellCheersFlavor.Purple;
                Graphic g = wcNode.GetFlavorGraphic(flavor);
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

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_WellCheersPart p = (node as PawnRenderNode_WellCheersPart)?.PartProps;
            if (p != null && p.role == WellCheersPartRole.Root && !Mathf.Approximately(p.formScale, 1f))
            {
                scale.x *= p.formScale;
                scale.z *= p.formScale;
            }
            return scale;
        }
    }
}
