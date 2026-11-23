using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.ScorchedGirl
{
    /// <summary>焦化少女分段骨骼渲染节点角色分类。</summary>
    public enum ScorchedGirlRenderRole
    {
        Root,
        GroupRoot,
        StaticPart,
        BoilPart,
        Eye,
        Mouth,
        Flame
    }

    /// <summary>焦化少女分段骨骼渲染节点属性定义（支持蜡笔沸腾帧、攻防眼部切换与动态火苗缩放）。</summary>
    public class PawnRenderNodeProperties_ScorchedGirlPart : PawnRenderNodeProperties
    {
        public ScorchedGirlRenderRole role = ScorchedGirlRenderRole.StaticPart;
        public float formScale = 1f;
        public bool useTransparentShader = false;
        public bool flipOnWest = true;
        public string texPathLeft;
        public List<string> frameTexPaths;
        public List<string> frameTexPathsLeft;
        public string closingEyeTexPath;
        public string closingEyeTexPathLeft;
        public string attackEyeTexPath;
        public string attackEyeTexPathLeft;
        public int boilFrameTicks = 7;
        public Vector2 flamePivotUV = new Vector2(0.320f, 0.524f);

        public bool IsContainerOnly =>
            role == ScorchedGirlRenderRole.Root || role == ScorchedGirlRenderRole.GroupRoot;

        public PawnRenderNodeProperties_ScorchedGirlPart()
        {
            nodeClass = typeof(PawnRenderNode_ScorchedGirlPart);
            workerClass = typeof(PawnRenderNodeWorker_ScorchedGirlPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsContainerOnly)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>焦化少女分段骨骼渲染节点：预加载全部蜡笔帧、表情贴图及左向镜像变体，确保多线程渲染安全。</summary>
    public class PawnRenderNode_ScorchedGirlPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_ScorchedGirlPart PartProps =>
            Props as PawnRenderNodeProperties_ScorchedGirlPart;

        public PawnRenderNode_ScorchedGirlPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <summary>禁用引擎默认的 West 网格反转，由独立左向镜像贴图与枢轴/旋转镜像统一处理。</summary>
        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_ScorchedGirlPart partProps = PartProps;
            if (partProps == null || !partProps.flipOnWest)
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

            if (!parms.Portrait && parms.pawn is LC_ScorchedGirlPawn sg && sg.ScorchedComp?.Escaped == true)
            {
                return sg.IsFacingLeft;
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
            if (PartProps != null && PartProps.IsContainerOnly)
            {
                return null;
            }

            if (Props.overrideMeshSize.HasValue)
            {
                return base.MeshSetFor(pawn);
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetOrLoadGraphic(string path)
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
            Graphic g = GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, Color.white);
            if (g != null)
            {
                cachedGraphics[path] = g;
            }

            return g;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_ScorchedGirlPart partProps = PartProps;
            if (partProps == null || partProps.IsContainerOnly)
            {
                return null;
            }

            return GetOrLoadGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_ScorchedGirlPart partProps = PartProps;
            if (partProps == null || partProps.IsContainerOnly)
            {
                yield break;
            }

            HashSet<string> yielded = new HashSet<string>();

            IEnumerable<string> AllPaths()
            {
                yield return partProps.texPath;
                yield return partProps.texPathLeft;
                yield return partProps.closingEyeTexPath;
                yield return partProps.closingEyeTexPathLeft;
                yield return partProps.attackEyeTexPath;
                yield return partProps.attackEyeTexPathLeft;

                if (partProps.frameTexPaths != null)
                {
                    for (int i = 0; i < partProps.frameTexPaths.Count; i++)
                    {
                        yield return partProps.frameTexPaths[i];
                    }
                }

                if (partProps.frameTexPathsLeft != null)
                {
                    for (int i = 0; i < partProps.frameTexPathsLeft.Count; i++)
                    {
                        yield return partProps.frameTexPathsLeft[i];
                    }
                }
            }

            foreach (string p in AllPaths())
            {
                if (p.NullOrEmpty() || !yielded.Add(p))
                {
                    continue;
                }

                Graphic g = GetOrLoadGraphic(p);
                if (g != null)
                {
                    yield return g;
                }
            }
        }
    }

    /// <summary>焦化少女分段骨骼渲染器：驱动蜡笔沸腾切帧、自爆蓄力红眼切换、火苗分级膨胀与左向镜像。</summary>
    public class PawnRenderNodeWorker_ScorchedGirlPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_ScorchedGirlPart partProps =
                (node as PawnRenderNode_ScorchedGirlPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_ScorchedGirlPart);
            if (partProps == null)
            {
                return false;
            }

            // 原版 MatchGirl_Attack_Baked 蓄力引爆时隐藏嘴部，仅凸显血红竖瞳
            if (partProps.role == ScorchedGirlRenderRole.Mouth && !parms.Portrait)
            {
                CompScorchedGirl comp = ResolveComp(parms.pawn);
                if (comp != null && comp.IsPrimingDetonation)
                {
                    return false;
                }
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_ScorchedGirlPart sgNode = node as PawnRenderNode_ScorchedGirlPart;
            PawnRenderNodeProperties_ScorchedGirlPart partProps = sgNode?.PartProps;
            if (sgNode != null && partProps != null && !partProps.IsContainerOnly)
            {
                bool left = sgNode.ShouldUseLeftFacing(parms);
                string chosenPath = ResolveFrameTexPath(partProps, parms, left);
                if (!chosenPath.NullOrEmpty())
                {
                    Graphic g = sgNode.GetOrLoadGraphic(chosenPath);
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
            }

            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        private static string ResolveFrameTexPath(
            PawnRenderNodeProperties_ScorchedGirlPart partProps,
            PawnDrawParms parms,
            bool left)
        {
            CompScorchedGirl comp = ResolveComp(parms.pawn);

            if (partProps.role == ScorchedGirlRenderRole.Eye && !parms.Portrait && comp != null)
            {
                if (comp.IsPrimingDetonation)
                {
                    // 前 40% 蓄力：半闭渗红眼（공격눈감）；后 60% 蓄力：猛睁猩红竖瞳（성냥이 공격）
                    if (comp.DetonationPrimeProgress < 0.40f)
                    {
                        string closing = left && !partProps.closingEyeTexPathLeft.NullOrEmpty()
                            ? partProps.closingEyeTexPathLeft
                            : partProps.closingEyeTexPath;
                        if (!closing.NullOrEmpty())
                        {
                            return closing;
                        }
                    }
                    else
                    {
                        string atk = left && !partProps.attackEyeTexPathLeft.NullOrEmpty()
                            ? partProps.attackEyeTexPathLeft
                            : partProps.attackEyeTexPath;
                        if (!atk.NullOrEmpty())
                        {
                            return atk;
                        }
                    }
                }
                else if (comp.Escaped && comp.ShouldShowRedEyes)
                {
                    string atk = left && !partProps.attackEyeTexPathLeft.NullOrEmpty()
                        ? partProps.attackEyeTexPathLeft
                        : partProps.attackEyeTexPath;
                    if (!atk.NullOrEmpty())
                    {
                        return atk;
                    }
                }
            }

            List<string> frames = left && !partProps.frameTexPathsLeft.NullOrEmpty()
                ? partProps.frameTexPathsLeft
                : partProps.frameTexPaths;
            if (!frames.NullOrEmpty())
            {
                if (parms.Portrait || frames.Count == 1)
                {
                    return frames[0];
                }

                int step = Mathf.Max(3, partProps.boilFrameTicks);
                if (comp != null && (comp.IsPrimingDetonation || comp.Escaped))
                {
                    step = Mathf.Max(3, step - 2);
                }

                int ticks = Find.TickManager?.TicksGame ?? 0;
                int seed = (parms.pawn?.thingIDNumber ?? 0) * 7 + (partProps.debugLabel?.GetHashCode() ?? 0);
                int idx = Mathf.Abs((ticks + seed) / step) % frames.Count;
                return frames[idx];
            }

            if (left && !partProps.texPathLeft.NullOrEmpty())
            {
                return partProps.texPathLeft;
            }

            return partProps.texPath;
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_ScorchedGirlPart sgNode = node as PawnRenderNode_ScorchedGirlPart;
            PawnRenderNodeProperties_ScorchedGirlPart partProps = sgNode?.PartProps;

            if (partProps != null && partProps.role == ScorchedGirlRenderRole.Flame)
            {
                pivot = new Vector3(partProps.flamePivotUV.x - 0.5f, 0f, partProps.flamePivotUV.y - 0.5f);
            }

            if (sgNode != null && sgNode.ShouldUseLeftFacing(parms))
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

            PawnRenderNode_ScorchedGirlPart sgNode = node as PawnRenderNode_ScorchedGirlPart;
            PawnRenderNodeProperties_ScorchedGirlPart partProps = sgNode?.PartProps;
            if (partProps != null && partProps.role == ScorchedGirlRenderRole.Flame && !parms.Portrait)
            {
                int ticks = Find.TickManager?.TicksGame ?? 0;
                angle += Mathf.Sin(ticks * 0.24f) * 6.5f;
            }

            if (sgNode != null && sgNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_ScorchedGirlPart partProps =
                (node as PawnRenderNode_ScorchedGirlPart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if (partProps.role == ScorchedGirlRenderRole.Root && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }
            else if (partProps.role == ScorchedGirlRenderRole.Flame && !parms.Portrait)
            {
                float flameScale = EvaluateFlameScale(parms.pawn);
                scale.x *= flameScale;
                scale.z *= flameScale;
            }

            return scale;
        }

        private static float EvaluateFlameScale(Pawn pawn)
        {
            CompScorchedGirl comp = ResolveComp(pawn);
            int ticks = Find.TickManager?.TicksGame ?? 0;
            float pulse = Mathf.Sin(ticks * 0.28f + (pawn?.thingIDNumber ?? 0) * 0.7f);

            if (comp == null)
            {
                return 0.75f + 0.08f * pulse;
            }

            if (comp.IsPrimingDetonation)
            {
                float progress = comp.DetonationPrimeProgress;
                float flicker = Mathf.Sin(ticks * 0.75f) * 0.16f;
                return Mathf.Lerp(1.50f, 2.30f, progress) + flicker;
            }

            if (comp.Escaped)
            {
                return 1.32f + 0.14f * pulse;
            }

            // 收容室火柴燃大机制：满计数微火 -> 计数下降/工作中火势渐盛
            if (comp.QliphothCountCurrent <= 1 || comp.IsBeingStudiedNow)
            {
                return 0.96f + 0.12f * pulse;
            }

            return 0.54f + 0.06f * pulse;
        }

        private static CompScorchedGirl ResolveComp(Pawn pawn)
        {
            return (pawn as LC_ScorchedGirlPawn)?.ScorchedComp ?? pawn?.TryGetComp<CompScorchedGirl>();
        }
    }
}
