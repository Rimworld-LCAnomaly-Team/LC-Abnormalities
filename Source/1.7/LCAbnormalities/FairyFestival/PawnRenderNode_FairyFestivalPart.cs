using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    /// <summary>精灵盛宴（F-04-83）多部件骨骼渲染节点的角色分类。</summary>
    public enum FairyFestivalPartRole
    {
        Root,
        BodyGroup,
        Wing,
        Eyes,
        Mouth,
        AuraShimmer,
        StaticPart
    }

    /// <summary>精灵盛宴多部件渲染节点属性，支持透明发光羽翼、祝福翠绿瞳色切换与口型交替。</summary>
    public class PawnRenderNodeProperties_FairyFestivalPart : PawnRenderNodeProperties
    {
        public FairyFestivalPartRole role = FairyFestivalPartRole.StaticPart;
        public float formScale = 1f;
        public bool useTransparentShader = false;
        public bool useGlowShader = false;
        public string texPathAlt;

        public bool IsNonGraphicGroup => role == FairyFestivalPartRole.Root || role == FairyFestivalPartRole.BodyGroup;

        public PawnRenderNodeProperties_FairyFestivalPart()
        {
            nodeClass = typeof(PawnRenderNode_FairyFestivalPart);
            workerClass = typeof(PawnRenderNodeWorker_FairyFestivalPart);
            pawnType = RenderNodePawnType.Any;
            drawSize = Vector2.one;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsNonGraphicGroup)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>
    /// 精灵盛宴多部件渲染节点：使用 1x1 归一化网格与 512x512 统一画布 UV 枢轴精确对齐，
    /// 并在主线程预热基础、翠绿发光瞳孔、绯红噬血瞳孔与微笑口型材质。
    /// </summary>
    public class PawnRenderNode_FairyFestivalPart : PawnRenderNode
    {
        private Graphic cachedPrimary;
        private Graphic cachedAlt;
        private Graphic cachedEyesBlessing;
        private Graphic cachedEyesFeast;

        public PawnRenderNodeProperties_FairyFestivalPart PartProps => Props as PawnRenderNodeProperties_FairyFestivalPart;

        public PawnRenderNode_FairyFestivalPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
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
            if (PartProps != null && PartProps.IsNonGraphicGroup)
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        private Shader ResolveShader()
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p != null)
            {
                if (p.useGlowShader)
                {
                    return ShaderDatabase.MoteGlow;
                }
                if (p.useTransparentShader)
                {
                    return ShaderDatabase.Transparent;
                }
            }

            return ShaderDatabase.Cutout;
        }

        private Graphic GetOrLoad(ref Graphic slot, string path, Color color)
        {
            if (slot != null)
            {
                return slot;
            }

            if (path.NullOrEmpty() || !UnityData.IsInMainThread)
            {
                return null;
            }

            slot = GraphicDatabase.Get<Graphic_Single>(path, ResolveShader(), Vector2.one, color);
            return slot;
        }

        public Graphic GetPrimaryGraphic()
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p == null || p.IsNonGraphicGroup)
            {
                return null;
            }

            Color col = p.role == FairyFestivalPartRole.AuraShimmer
                ? new Color(0.72f, 1.0f, 0.88f, 0.42f)
                : Color.white;
            return GetOrLoad(ref cachedPrimary, p.texPath, col);
        }

        public Graphic GetAltGraphic()
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p == null || p.texPathAlt.NullOrEmpty())
            {
                return GetPrimaryGraphic();
            }

            return GetOrLoad(ref cachedAlt, p.texPathAlt, Color.white);
        }

        public Graphic GetEyesBlessingGraphic()
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p == null || p.texPath.NullOrEmpty())
            {
                return null;
            }

            // Exact Spine Give_fairy eye glow tint: #00FF82
            return GetOrLoad(ref cachedEyesBlessing, p.texPath, new Color(0.0f, 1.0f, 0.51f, 1.0f));
        }

        public Graphic GetEyesFeastGraphic()
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p == null || p.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetOrLoad(ref cachedEyesFeast, p.texPath, new Color(1.0f, 0.16f, 0.18f, 1.0f));
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            return GetPrimaryGraphic();
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_FairyFestivalPart p = PartProps;
            if (p == null || p.IsNonGraphicGroup)
            {
                yield break;
            }

            Graphic primary = GetPrimaryGraphic();
            if (primary != null) yield return primary;

            if (!p.texPathAlt.NullOrEmpty())
            {
                Graphic alt = GetAltGraphic();
                if (alt != null) yield return alt;
            }

            if (p.role == FairyFestivalPartRole.Eyes)
            {
                Graphic bless = GetEyesBlessingGraphic();
                if (bless != null) yield return bless;

                Graphic feast = GetEyesFeastGraphic();
                if (feast != null) yield return feast;
            }
        }
    }

    /// <summary>
    /// 精灵盛宴渲染工作者：
    /// 1) 驱动 Root 整体缩放；
    /// 2) 还原 Spine Give_fairy 期间眼眸泛起翠绿冷光（#00FF82）与 Feast 期间泛红；
    /// 3) 还原 Spine Default / Give_fairy 中 mouth1（微张）与 mouth2（猫嘴微笑）周期交替。
    /// </summary>
    public class PawnRenderNodeWorker_FairyFestivalPart : PawnRenderNodeWorker
    {
        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_FairyFestivalPart fairyNode = node as PawnRenderNode_FairyFestivalPart;
            PawnRenderNodeProperties_FairyFestivalPart p = fairyNode?.PartProps;
            if (fairyNode != null && p != null && !parms.Portrait)
            {
                LC_FairyFestivalPawn fairyPawn = parms.pawn as LC_FairyFestivalPawn;
                Graphic selected = null;

                if (p.role == FairyFestivalPartRole.Eyes && fairyPawn != null)
                {
                    if (fairyPawn.IsFeastingNow)
                    {
                        selected = fairyNode.GetEyesFeastGraphic();
                    }
                    else if (fairyPawn.IsGivingFairyNow)
                    {
                        selected = fairyNode.GetEyesBlessingGraphic();
                    }
                }
                else if (p.role == FairyFestivalPartRole.Mouth)
                {
                    bool useSmile;
                    if (fairyPawn != null && fairyPawn.IsFeastingNow)
                    {
                        // Rapid chomping alternation during Feast
                        int tick = Find.TickManager?.TicksGame ?? 0;
                        useSmile = ((tick / 8) % 2) == 1;
                    }
                    else if (fairyPawn != null && fairyPawn.IsGivingFairyNow)
                    {
                        // Matches Spine Give_fairy: mouth2 at start/end, mouth1 open in middle
                        useSmile = fairyPawn.ShouldShowSmileMouth;
                    }
                    else
                    {
                        // Matches Spine Default (3.0s = 180t): mouth1 0..96t, mouth2 96..168t, mouth1 168..180t
                        int phase = ((Find.TickManager?.TicksGame ?? 0) + ((parms.pawn?.thingIDNumber ?? 0) * 19)) % 180;
                        useSmile = phase >= 96 && phase < 168;
                    }

                    selected = useSmile ? fairyNode.GetAltGraphic() : fairyNode.GetPrimaryGraphic();
                }

                if (selected != null)
                {
                    PawnDrawParms matParms = parms;
                    matParms.facing = Rot4.South;
                    Material mat = selected.NodeGetMat(matParms);
                    if (mat != null && parms.flags.FlagSet(PawnRenderFlags.Invisible))
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
            PawnRenderNodeProperties_FairyFestivalPart p = (node as PawnRenderNode_FairyFestivalPart)?.PartProps;
            if (p != null && p.role == FairyFestivalPartRole.Root && !Mathf.Approximately(p.formScale, 1f))
            {
                scale.x *= p.formScale;
                scale.z *= p.formScale;
            }
            return scale;
        }
    }
}
