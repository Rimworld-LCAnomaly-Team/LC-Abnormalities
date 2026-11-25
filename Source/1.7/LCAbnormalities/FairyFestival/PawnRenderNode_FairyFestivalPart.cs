using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    /// <summary>精灵盛宴（F-04-83）骨骼渲染节点的角色分类。</summary>
    public enum FairyFestivalPartRole
    {
        SpineRoot,
        Root,
        BodyGroup,
        Wing,
        Eyes,
        Mouth,
        AuraShimmer,
        StaticPart
    }

    /// <summary>精灵盛宴 Spine 骨骼渲染节点属性。</summary>
    public class PawnRenderNodeProperties_FairyFestivalPart : PawnRenderNodeProperties
    {
        public FairyFestivalPartRole role = FairyFestivalPartRole.SpineRoot;
        public float formScale = 1f;
        public bool useTransparentShader = false;
        public bool useGlowShader = false;
        public string texPathAlt;

        public bool IsNonGraphicGroup =>
            role == FairyFestivalPartRole.SpineRoot
            || role == FairyFestivalPartRole.Root
            || role == FairyFestivalPartRole.BodyGroup;

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
    /// 精灵盛宴 Spine 3.6 实时蒙皮渲染节点：
    /// 持有独立的四批次 Live 网格实例（与全局静态 Portrait 烘焙实例严格隔离）。
    /// </summary>
    public class PawnRenderNode_FairyFestivalPart : PawnRenderNode
    {
        private FairySpineBatchRenderer liveRenderer;

        public PawnRenderNodeProperties_FairyFestivalPart PartProps => Props as PawnRenderNodeProperties_FairyFestivalPart;

        public PawnRenderNode_FairyFestivalPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public override Mesh GetMesh(PawnDrawParms parms) => null;

        public override GraphicMeshSet MeshSetFor(Pawn pawn) => null;

        public override Graphic GraphicFor(Pawn pawn) => null;

        public FairySpineBatchRenderer GetOrCreateLiveRenderer()
        {
            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            if (liveRenderer != null)
            {
                return FairySpineAssets.EnsureMaterialsInitialized() ? liveRenderer : null;
            }

            FairySpineData data = FairySpineAssets.Data;
            if (data == null || !FairySpineAssets.EnsureMaterialsInitialized())
            {
                return null;
            }

            liveRenderer = new FairySpineBatchRenderer(data);
            return liveRenderer;
        }
    }

    /// <summary>
    /// 精灵盛宴 Spine 3.6 渲染工作者：
    /// 在 ParallelPreDraw 阶段仅入队占位的 DrawRequest 计算矩阵，不在后台线程操作 Unity Mesh/Material；
    /// 在主线程 PostDraw 内求值 97 根骨骼与 12 个加权网格，并按原作绘制顺序提交 4 个 PMA/Additive 批次。
    /// </summary>
    public class PawnRenderNodeWorker_FairyFestivalPart : PawnRenderNodeWorker
    {
        public override void AppendDrawRequests(PawnRenderNode node, PawnDrawParms parms, List<PawnGraphicDrawRequest> requests)
        {
            PawnRenderNode_FairyFestivalPart fairyNode = node as PawnRenderNode_FairyFestivalPart;
            FairyFestivalPartRole role = fairyNode?.PartProps?.role ?? FairyFestivalPartRole.StaticPart;
            if (fairyNode != null && (role == FairyFestivalPartRole.SpineRoot || role == FairyFestivalPartRole.Root))
            {
                requests.Add(new PawnGraphicDrawRequest(node, null, null));
                return;
            }

            base.AppendDrawRequests(node, parms, requests);
        }

        public override void PostDraw(PawnRenderNode node, PawnDrawParms parms, Mesh mesh, Matrix4x4 matrix)
        {
            base.PostDraw(node, parms, mesh, matrix);

            PawnRenderNode_FairyFestivalPart fairyNode = node as PawnRenderNode_FairyFestivalPart;
            FairyFestivalPartRole role = fairyNode?.PartProps?.role ?? FairyFestivalPartRole.StaticPart;
            if (fairyNode == null || (role != FairyFestivalPartRole.SpineRoot && role != FairyFestivalPartRole.Root))
            {
                return;
            }

            if (!UnityData.IsInMainThread)
            {
                return;
            }

            bool isPortrait = parms.Portrait;
            bool isStatue = parms.Statue;
            bool isInvisible = !isPortrait && parms.flags.FlagSet(PawnRenderFlags.Invisible);

            Color tint = parms.tint;
            if (isStatue)
            {
                tint = parms.statueColor ?? new Color(0.55f, 0.55f, 0.55f, 1f);
            }
            else if (isInvisible)
            {
                tint = new Color(tint.r * 0.65f, tint.g * 0.90f, tint.b * 1.0f, tint.a * 0.35f);
            }

            bool includeAdditive = !isStatue;
            FairySpineBatchRenderer renderer;
            if (isPortrait)
            {
                renderer = FairySpineAssets.GetPortraitRenderer();
                if (renderer == null)
                {
                    return;
                }

                // Re-evaluate only if portrait has non-standard statue/tint overrides; otherwise reuse baked pose
                if (isStatue || tint != Color.white)
                {
                    renderer.EvaluateAndUpload(FairySpineAnimKind.Default, 0.267f, feastMode: false, tint, includeAdditive, frameStamp: -1);
                }
            }
            else
            {
                renderer = fairyNode.GetOrCreateLiveRenderer();
                if (renderer == null)
                {
                    return;
                }

                FairySpineAnimKind animKind = FairySpineAnimKind.Default;
                float animTime = 0.267f;
                bool feastMode = false;
                if (parms.pawn is LC_FairyFestivalPawn fairyPawn)
                {
                    fairyPawn.GetSpinePlaybackState(out animKind, out animTime, out feastMode);
                }

                renderer.EvaluateAndUpload(animKind, animTime, feastMode, tint, includeAdditive, Time.frameCount);
            }

            int batchCount = includeAdditive ? 4 : 3;
            for (int b = 0; b < batchCount; b++)
            {
                Mesh batchMesh = renderer.GetMesh(b);
                Material batchMat = FairySpineAssets.GetBatchMaterial(b);
                if (batchMesh != null && batchMat != null)
                {
                    Matrix4x4 batchMatrix = matrix;
                    if (b > 0)
                    {
                        batchMatrix.m13 += b * 0.0002f;
                    }
                    GenDraw.DrawMeshNowOrLater(batchMesh, batchMatrix, batchMat, parms.DrawNow);
                }
            }

            if (isPortrait && (isStatue || tint != Color.white))
            {
                // Restore default baked portrait colors after custom statue/tinted portrait draw
                renderer.EvaluateAndUpload(FairySpineAnimKind.Default, 0.267f, feastMode: false, Color.white, includeAdditive: true, frameStamp: -1);
            }
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_FairyFestivalPart p = (node as PawnRenderNode_FairyFestivalPart)?.PartProps;
            if (p != null
                && (p.role == FairyFestivalPartRole.SpineRoot || p.role == FairyFestivalPartRole.Root)
                && !Mathf.Approximately(p.formScale, 1f))
            {
                scale.x *= p.formScale;
                scale.z *= p.formScale;
            }
            return scale;
        }
    }
}
