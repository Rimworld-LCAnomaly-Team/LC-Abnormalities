using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.VoidDream
{
    /// <summary>虚空之梦双形态多部件渲染节点角色分类（绵羊形态 + 羊毛云变身序列 + 公鸡形态）。</summary>
    public enum VoidDreamRenderRole
    {
        SheepRoot,
        SheepShadow,
        SheepBody,
        SheepFeet,
        SheepTail,
        SheepEyesWhite,
        SheepEyePupils,
        SheepHead,
        SheepEar,
        SheepZ1,
        SheepZ2,

        TransformCloud,

        RoosterRoot,
        RoosterShadow,
        RoosterBackThigh,
        RoosterBackShin,
        RoosterFrontThigh,
        RoosterFrontShin,
        RoosterBody,
        RoosterBodyPupils,
        RoosterSheepHead,
        RoosterSheepHorn,
        RoosterEyeTail,
        RoosterNeck,
        RoosterNeckPupils,
        RoosterHead,
        RoosterRealEye,
        RoosterUpperBeak,
        RoosterLowerBeak
    }

    /// <summary>虚空之梦多部件渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_VoidDreamPart : PawnRenderNodeProperties
    {
        public VoidDreamRenderRole role = VoidDreamRenderRole.SheepBody;
        public float formScale = 1f;
        public float containedScale = 1f;
        public Vector3 containedOffset = Vector3.zero;
        public bool flipOnWest = true;
        public bool useTransparentShader = false;
        public string texPathLeft;
        public List<string> alternateTexPaths;
        public List<string> alternateTexPathsLeft;

        public bool IsGroupRoot =>
            role == VoidDreamRenderRole.SheepRoot
            || role == VoidDreamRenderRole.RoosterRoot;

        public bool IsSheepPart =>
            role >= VoidDreamRenderRole.SheepRoot && role <= VoidDreamRenderRole.SheepZ2;

        public bool IsRoosterPart =>
            role >= VoidDreamRenderRole.RoosterRoot && role <= VoidDreamRenderRole.RoosterLowerBeak;

        public PawnRenderNodeProperties_VoidDreamPart()
        {
            nodeClass = typeof(PawnRenderNode_VoidDreamPart);
            workerClass = typeof(PawnRenderNodeWorker_VoidDreamPart);
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

    /// <summary>
    /// 虚空之梦双形态骨骼渲染节点：
    /// 1) 绵羊形态：悬浮漂浮、双瞳游弋、耳尾轻颤与飘升 Zzz 催眠符；
    /// 2) 变身瞬间：5 帧手绘羊毛云爆发遮掩绵羊→公鸡切换；
    /// 3) 公鸡形态：双腿交替步态、垂落羊头与黑丝眼尾摆动、长颈四瞳游弋、主眼眨眼/警觉切换与张喙啼鸣。
    /// </summary>
    public class PawnRenderNode_VoidDreamPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_VoidDreamPart PartProps => Props as PawnRenderNodeProperties_VoidDreamPart;

        public PawnRenderNode_VoidDreamPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <summary>禁用引擎默认 West 网格翻转，统一使用独立 Left 贴图与枢轴/角度镜像。</summary>
        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_VoidDreamPawn vd && vd.IsHeldOnPlatform;
        }

        public static CompVoidDream GetVoidDreamComp(Pawn pawn)
        {
            return (pawn as LC_VoidDreamPawn)?.DreamComp ?? pawn?.TryGetComp<CompVoidDream>();
        }

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_VoidDreamPart partProps = PartProps;
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

            if (parms.pawn is LC_VoidDreamPawn vd && vd.DreamComp?.Escaped == true)
            {
                return vd.IsFacingLeft;
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
            PawnRenderNodeProperties_VoidDreamPart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_VoidDreamPart partProps = PartProps;
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

        /// <summary>公鸡主眼眨眼阶段：0=睁眼，1=半闭，2=全闭；啼鸣时圆睁不眨眼。</summary>
        public static int GetRoosterBlinkStage(Pawn pawn)
        {
            CompVoidDream comp = GetVoidDreamComp(pawn);
            if (comp != null && (comp.IsCrowing || comp.IsTransforming))
            {
                return 0;
            }

            int ticks = (Find.TickManager?.TicksGame ?? 0) + ((pawn?.thingIDNumber ?? 0) * 41);
            int cycle = Mathf.Abs(ticks) % 195;
            if (cycle >= 182 && cycle <= 184) return 1;
            if (cycle >= 185 && cycle <= 189) return 2;
            if (cycle >= 190 && cycle <= 192) return 1;
            return 0;
        }

        public string ResolveDynamicTexPath(PawnDrawParms parms, bool leftFacing)
        {
            PawnRenderNodeProperties_VoidDreamPart partProps = PartProps;
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
                case VoidDreamRenderRole.RoosterRealEye:
                    // basePath = Open, alts[0] = Alert, alts[1] = Half, alts[2] = Closed
                    if (!parms.Portrait && alts != null)
                    {
                        CompVoidDream comp = GetVoidDreamComp(parms.pawn);
                        if (comp != null && comp.IsCrowing && alts.Count > 0)
                        {
                            return alts[0];
                        }

                        int blink = GetRoosterBlinkStage(parms.pawn);
                        if (blink == 2 && alts.Count > 2) return alts[2];
                        if (blink == 1 && alts.Count > 1) return alts[1];
                    }
                    return basePath;

                case VoidDreamRenderRole.TransformCloud:
                    // basePath = Cloud1, alts[0..3] = Cloud2..5 mapped across Transform cloud window (0..1)
                    if (alts != null && alts.Count >= 4)
                    {
                        CompVoidDream comp = GetVoidDreamComp(parms.pawn);
                        float p = comp != null ? comp.TransformCloudProgress : 0f;
                        int frame = Mathf.Clamp(Mathf.FloorToInt(p * 5f), 0, 4);
                        return frame == 0 ? basePath : alts[frame - 1];
                    }
                    return basePath;

                default:
                    return basePath;
            }
        }
    }

    /// <summary>虚空之梦多部件渲染节点工作者：管理绵羊/公鸡双形态切换、羊毛变身云显隐、左向镜像与收容缩放。</summary>
    public class PawnRenderNodeWorker_VoidDreamPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_VoidDreamPart partProps = (node as PawnRenderNode_VoidDreamPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_VoidDreamPart);
            if (partProps == null)
            {
                return false;
            }

            if (parms.Portrait)
            {
                if (partProps.role == VoidDreamRenderRole.SheepZ1 || partProps.role == VoidDreamRenderRole.SheepZ2)
                {
                    return false;
                }
                return partProps.IsSheepPart;
            }

            CompVoidDream comp = PawnRenderNode_VoidDreamPart.GetVoidDreamComp(parms.pawn);
            bool showRooster = comp != null && comp.ShowRoosterRig;

            if (partProps.role == VoidDreamRenderRole.TransformCloud)
            {
                return comp != null && comp.IsShowingTransformCloud;
            }

            if (partProps.IsSheepPart)
            {
                if (showRooster)
                {
                    return false;
                }
                if ((partProps.role == VoidDreamRenderRole.SheepZ1 || partProps.role == VoidDreamRenderRole.SheepZ2)
                    && comp != null && comp.IsTransforming)
                {
                    return false;
                }
                return true;
            }

            if (partProps.IsRoosterPart)
            {
                return showRooster;
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_VoidDreamPart vdNode = node as PawnRenderNode_VoidDreamPart;
            if (vdNode != null)
            {
                bool left = vdNode.ShouldUseLeftFacing(parms);
                string resolvedPath = vdNode.ResolveDynamicTexPath(parms, left);
                Graphic g = vdNode.GetCachedGraphic(resolvedPath);
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
            PawnRenderNode_VoidDreamPart vdNode = node as PawnRenderNode_VoidDreamPart;
            PawnRenderNodeProperties_VoidDreamPart partProps = vdNode?.PartProps;

            if (partProps != null
                && partProps.role == VoidDreamRenderRole.SheepRoot
                && !parms.Portrait
                && PawnRenderNode_VoidDreamPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
            }

            if (vdNode != null && vdNode.ShouldUseLeftFacing(parms))
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
            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out float animRot))
            {
                num += animRot;
            }

            PawnRenderNode_VoidDreamPart vdNode = node as PawnRenderNode_VoidDreamPart;
            if (vdNode != null && vdNode.ShouldUseLeftFacing(parms))
            {
                num = -num;
            }

            return Quaternion.AngleAxis(num, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_VoidDreamPart partProps = (node as PawnRenderNode_VoidDreamPart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if (partProps.IsGroupRoot || partProps.role == VoidDreamRenderRole.TransformCloud)
            {
                float targetScale = partProps.formScale;
                if (!parms.Portrait
                    && PawnRenderNode_VoidDreamPart.IsHeldOnPlatform(parms.pawn)
                    && partProps.containedScale > 0f)
                {
                    targetScale = partProps.containedScale;
                }

                if (partProps.role == VoidDreamRenderRole.TransformCloud)
                {
                    CompVoidDream comp = PawnRenderNode_VoidDreamPart.GetVoidDreamComp(parms.pawn);
                    float p = comp != null ? comp.TransformCloudProgress : 0f;
                    // Swell rapidly outward then billow softly
                    float burst = 0.72f + 0.58f * Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);
                    targetScale *= burst;
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
