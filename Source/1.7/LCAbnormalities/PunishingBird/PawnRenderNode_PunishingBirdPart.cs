using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.PunishingBird
{
    /// <summary>惩戒鸟分段骨骼渲染节点角色分类，用于控制收容栖木、振翅、表情切换与暴怒巨口显隐。</summary>
    public enum PunishingBirdRenderRole
    {
        PerchRoot,
        PerchTrunk,
        PerchChain,
        PerchChainBack,
        PerchBlade,
        BirdRoot,
        BirdBody,
        BirdFeet,
        BirdFoldedWing,
        BirdFace,
        BirdFlyWingFront,
        BirdFlyWingBack,
        JawRoot,
        JawPart
    }

    /// <summary>惩戒鸟多部件渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_PunishingBirdPart : PawnRenderNodeProperties
    {
        public PunishingBirdRenderRole role = PunishingBirdRenderRole.BirdBody;
        public float formScale = 1f;
        public float containedScale = 1f;
        public Vector3 containedOffset = Vector3.zero;
        public bool flipOnWest = true;
        public bool useTransparentShader = false;
        public string texPathLeft;
        public List<string> alternateTexPaths;
        public List<string> alternateTexPathsLeft;

        public bool IsGroupRoot =>
            role == PunishingBirdRenderRole.PerchRoot
            || role == PunishingBirdRenderRole.BirdRoot
            || role == PunishingBirdRenderRole.JawRoot;

        public PawnRenderNodeProperties_PunishingBirdPart()
        {
            nodeClass = typeof(PawnRenderNode_PunishingBirdPart);
            workerClass = typeof(PawnRenderNodeWorker_PunishingBirdPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            // PawnRenderNode.AppendRequests skips children if useGraphic is true without any emitted request.
            if (IsGroupRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>惩戒鸟多部件渲染节点：支持主线程预加载多帧翅膀、表情、红体暴怒态与独立左向贴图。</summary>
    public class PawnRenderNode_PunishingBirdPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_PunishingBirdPart PartProps => Props as PawnRenderNodeProperties_PunishingBirdPart;

        public PawnRenderNode_PunishingBirdPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <summary>禁用引擎默认 West 网格翻转，统一使用独立左向贴图与枢轴/角度镜像。</summary>
        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_PunishingBirdPawn pb && pb.IsHeldOnPlatform;
        }

        public static bool IsEnraged(Pawn pawn)
        {
            return (pawn as LC_PunishingBirdPawn)?.BirdComp?.IsEnraged == true;
        }

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_PunishingBirdPart partProps = PartProps;
            if (partProps == null || !partProps.flipOnWest)
            {
                return false;
            }

            if (parms.Portrait || IsHeldOnPlatform(parms.pawn))
            {
                return false;
            }

            if (parms.facing == Rot4.West)
            {
                return true;
            }

            if (parms.facing == Rot4.East)
            {
                return false;
            }

            if (parms.pawn is LC_PunishingBirdPawn pb && pb.BirdComp?.Escaped == true)
            {
                return pb.IsFacingLeft;
            }

            return false;
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
            PawnRenderNodeProperties_PunishingBirdPart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_PunishingBirdPart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot)
            {
                yield break;
            }

            Graphic primary = GetCachedGraphic(partProps.texPath);
            if (primary != null)
            {
                yield return primary;
            }

            if (!partProps.texPathLeft.NullOrEmpty())
            {
                Graphic left = GetCachedGraphic(partProps.texPathLeft);
                if (left != null)
                {
                    yield return left;
                }
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

            if (partProps.alternateTexPathsLeft != null)
            {
                for (int i = 0; i < partProps.alternateTexPathsLeft.Count; i++)
                {
                    Graphic g = GetCachedGraphic(partProps.alternateTexPathsLeft[i]);
                    if (g != null)
                    {
                        yield return g;
                    }
                }
            }
        }

        public string ResolveDynamicTexPath(PawnDrawParms parms, bool leftFacing)
        {
            PawnRenderNodeProperties_PunishingBirdPart partProps = PartProps;
            if (partProps == null)
            {
                return null;
            }

            List<string> alts = leftFacing ? partProps.alternateTexPathsLeft : partProps.alternateTexPaths;
            string basePath = leftFacing && !partProps.texPathLeft.NullOrEmpty()
                ? partProps.texPathLeft
                : partProps.texPath;

            switch (partProps.role)
            {
                case PunishingBirdRenderRole.BirdBody:
                    // alternateTexPaths[0] = Body_Attack
                    if (!parms.Portrait && IsEnraged(parms.pawn) && alts != null && alts.Count > 0)
                    {
                        return alts[0];
                    }
                    return basePath;

                case PunishingBirdRenderRole.BirdFace:
                    // basePath = Face_Idle, alts[0] = Face_Look, alts[1] = Face_Fly
                    if (parms.Portrait)
                    {
                        return basePath;
                    }

                    if (!IsHeldOnPlatform(parms.pawn))
                    {
                        if (alts != null && alts.Count > 1)
                        {
                            return alts[1];
                        }
                        return basePath;
                    }

                    if (alts != null && alts.Count > 0)
                    {
                        int tick = tree?.AnimationTick ?? (Find.TickManager?.TicksGame ?? 0);
                        bool isWork = (parms.pawn as LC_PunishingBirdPawn)?.BirdComp?.IsBeingStudiedNow == true;
                        bool showLook = isWork
                            ? (tick >= 18 && tick <= 40)
                            : (tick >= 40 && tick <= 78);
                        if (showLook)
                        {
                            return alts[0];
                        }
                    }
                    return basePath;

                case PunishingBirdRenderRole.BirdFlyWingFront:
                case PunishingBirdRenderRole.BirdFlyWingBack:
                    // basePath = frame 1, alts[0..3] = frames 2..5
                    if (alts == null || alts.Count == 0)
                    {
                        return basePath;
                    }

                    int totalFrames = 1 + alts.Count;
                    int animTick = tree?.AnimationTick ?? (Find.TickManager?.TicksGame ?? 0);
                    LC_PunishingBirdPawn pb = parms.pawn as LC_PunishingBirdPawn;
                    int ticksPerFrame = 4;
                    if (pb?.BirdComp?.IsEnraged == true)
                    {
                        ticksPerFrame = 3;
                    }
                    else if (pb != null && pb.Spawned && pb.pather?.Moving != true && pb.BirdComp?.IsPecking != true)
                    {
                        ticksPerFrame = 6;
                    }

                    int frameIdx = (Mathf.Abs(animTick) / ticksPerFrame) % totalFrames;
                    return frameIdx == 0 ? basePath : alts[frameIdx - 1];

                default:
                    return basePath;
            }
        }
    }

    /// <summary>惩戒鸟多部件渲染节点工作者：处理栖木显隐、多帧振翅贴图切换、左向镜像及暴怒巨口层级缩放。</summary>
    public class PawnRenderNodeWorker_PunishingBirdPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_PunishingBirdPart partProps = (node as PawnRenderNode_PunishingBirdPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_PunishingBirdPart);
            if (partProps == null)
            {
                return false;
            }

            bool held = !parms.Portrait && PawnRenderNode_PunishingBirdPart.IsHeldOnPlatform(parms.pawn);
            bool enraged = !parms.Portrait && PawnRenderNode_PunishingBirdPart.IsEnraged(parms.pawn);

            switch (partProps.role)
            {
                case PunishingBirdRenderRole.PerchRoot:
                case PunishingBirdRenderRole.PerchTrunk:
                case PunishingBirdRenderRole.PerchChain:
                case PunishingBirdRenderRole.PerchChainBack:
                case PunishingBirdRenderRole.PerchBlade:
                    return held;

                case PunishingBirdRenderRole.BirdFoldedWing:
                    return parms.Portrait || held;

                case PunishingBirdRenderRole.BirdFlyWingFront:
                case PunishingBirdRenderRole.BirdFlyWingBack:
                    return !parms.Portrait && !held;

                case PunishingBirdRenderRole.JawRoot:
                case PunishingBirdRenderRole.JawPart:
                    return enraged;

                case PunishingBirdRenderRole.BirdRoot:
                case PunishingBirdRenderRole.BirdBody:
                case PunishingBirdRenderRole.BirdFeet:
                case PunishingBirdRenderRole.BirdFace:
                    return true;

                default:
                    return false;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_PunishingBirdPart pbNode = node as PawnRenderNode_PunishingBirdPart;
            if (pbNode != null)
            {
                bool left = pbNode.ShouldUseLeftFacing(parms);
                string resolvedPath = pbNode.ResolveDynamicTexPath(parms, left);
                Graphic g = pbNode.GetCachedGraphic(resolvedPath);
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
            PawnRenderNode_PunishingBirdPart pbNode = node as PawnRenderNode_PunishingBirdPart;
            PawnRenderNodeProperties_PunishingBirdPart partProps = pbNode?.PartProps;

            if (partProps != null
                && partProps.role == PunishingBirdRenderRole.BirdRoot
                && !parms.Portrait
                && PawnRenderNode_PunishingBirdPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
            }

            if (pbNode != null && pbNode.ShouldUseLeftFacing(parms))
            {
                offset.x = -offset.x;
                pivot.x = -pivot.x;
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float num = node.DebugAngleOffset;
            if (node.Props.drawData != null)
            {
                num += node.Props.drawData.RotationOffsetForRot(parms.facing);
            }
            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out var offset))
            {
                num += offset;
            }

            PawnRenderNode_PunishingBirdPart pbNode = node as PawnRenderNode_PunishingBirdPart;
            if (pbNode != null && pbNode.ShouldUseLeftFacing(parms))
            {
                num = -num;
            }

            return Quaternion.AngleAxis(num, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_PunishingBirdPart partProps = (node as PawnRenderNode_PunishingBirdPart)?.PartProps;
            if (partProps != null && partProps.IsGroupRoot)
            {
                float targetScale = partProps.formScale;
                if (partProps.role == PunishingBirdRenderRole.BirdRoot
                    && !parms.Portrait
                    && PawnRenderNode_PunishingBirdPart.IsHeldOnPlatform(parms.pawn)
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

            return scale;
        }
    }
}
