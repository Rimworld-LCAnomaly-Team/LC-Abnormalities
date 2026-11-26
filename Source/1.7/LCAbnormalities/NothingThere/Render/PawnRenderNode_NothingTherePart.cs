using System.Collections.Generic;
using NothingThere.Comp;
using NothingThere.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace NothingThere.Render
{
    /// <summary>一无所有渲染树节点的角色分类，用于控制显隐、着色器与根节点缩放。</summary>
    public enum NothingThereRenderRole
    {
        FormRoot_Dog,
        DogPart,
        DogEye,
        FormRoot_Disguise,
        DisguiseBody,
        DisguiseSeam,
        FormRoot_Cocoon,
        CocoonPart,
        FormRoot_True,
        TruePart,
        TrueEye,
        TrueMace,
        TrueGoodbyeBlade
    }

    /// <summary>一无所有分段骨骼渲染节点的属性定义。</summary>
    public class PawnRenderNodeProperties_NothingTherePart : PawnRenderNodeProperties
    {
        public NothingThereRenderRole role = NothingThereRenderRole.DogPart;
        public float formScale = 1f;
        public bool useTransparentShader = false;
        public bool flipOnWest = true;
        public string texPathLeft;
        public Vector2 eyePivotUV = new Vector2(0.5f, 0.5f);
        public Vector2 eyeCenterOffsetUV = Vector2.zero;
        public Vector2 eyeOrbitRadius = Vector2.zero;
        public float eyeRotAmplitude = 0f;
        public int eyePeriodTicks = 97;
        public float eyePhaseOffset = 0f;

        public bool IsFormRoot =>
            role == NothingThereRenderRole.FormRoot_Dog
            || role == NothingThereRenderRole.FormRoot_Disguise
            || role == NothingThereRenderRole.FormRoot_Cocoon
            || role == NothingThereRenderRole.FormRoot_True;

        public PawnRenderNodeProperties_NothingTherePart()
        {
            nodeClass = typeof(PawnRenderNode_NothingTherePart);
            workerClass = typeof(PawnRenderNodeWorker_NothingTherePart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            // PawnRenderNode.AppendRequests skips children if useGraphic is true without any emitted request.
            if (IsFormRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>一无所有分段骨骼渲染节点：支持状态切换、独立左向贴图与动态伪装立绘。</summary>
    public class PawnRenderNode_NothingTherePart : PawnRenderNode
    {
        private Graphic cachedGraphic;
        private Graphic cachedGraphicLeft;

        public PawnRenderNodeProperties_NothingTherePart PartProps => Props as PawnRenderNodeProperties_NothingTherePart;

        public PawnRenderNode_NothingTherePart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <summary>禁用引擎默认的 West 网格反转，避免与人形 MeshSet 的西向翻转双重抵消。</summary>
        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_NothingTherePart partProps = PartProps;
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

            if (!parms.Portrait && parms.pawn is LC_NothingTherePawn nt && nt.SelfEntityComp?.Escaped == true)
            {
                return nt.IsFacingLeft;
            }

            return false;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            if (meshSet == null)
            {
                return null;
            }

            // 始终使用未预翻转的南向平面网格，左向改由独立左向贴图与枢轴/角度镜像实现
            return meshSet.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.IsFormRoot)
            {
                return null;
            }

            if (Props.overrideMeshSize.HasValue)
            {
                return base.MeshSetFor(pawn);
            }

            // 使用 1x1 单位网格，使所有 UV 枢轴 (u-0.5, v-0.5) 与眼球/骨骼偏移在本地坐标系 1:1 严格对齐
            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GraphicLeftFor(Pawn pawn)
        {
            PawnRenderNodeProperties_NothingTherePart partProps = PartProps;
            if (partProps == null || partProps.IsFormRoot || partProps.texPathLeft.NullOrEmpty())
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

            Shader shader = partProps.useTransparentShader ? ShaderDatabase.Transparent : ShaderDatabase.Cutout;
            cachedGraphicLeft = GraphicDatabase.Get<Graphic_Single>(partProps.texPathLeft, shader, Vector2.one, Color.white);
            return cachedGraphicLeft;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_NothingTherePart partProps = PartProps;
            if (partProps == null || partProps.IsFormRoot)
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

            string tex = partProps.texPath;
            if (tex.NullOrEmpty() && partProps.role == NothingThereRenderRole.DisguiseBody)
            {
                tex = "Things/Pawn/NothingThere/NothingThere_Form1";
            }

            if (tex.NullOrEmpty())
            {
                return null;
            }

            Shader shader = partProps.useTransparentShader ? ShaderDatabase.Transparent : ShaderDatabase.Cutout;
            cachedGraphic = GraphicDatabase.Get<Graphic_Single>(tex, shader, Vector2.one, Color.white);
            return cachedGraphic;
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            Graphic g = GraphicFor(pawn);
            if (g != null)
            {
                yield return g;
            }

            // 预加载左向独立贴图与隐身材质变体，避免后续多线程 ParallelPreDraw 触发离主线程加载错误
            Graphic left = GraphicLeftFor(pawn);
            if (left != null)
            {
                yield return left;
            }
        }

        protected override void EnsureMaterialVariantsInitialized(Graphic g)
        {
            base.EnsureMaterialVariantsInitialized(g);
            if (UnityData.IsInMainThread && PartProps?.role == NothingThereRenderRole.DisguiseBody)
            {
                (tree?.pawn as LC_NothingTherePawn)?.SelfEntityComp?.EnsureDisguiseMaterialInitialized();
            }
        }
    }

    /// <summary>一无所有分段骨骼渲染节点工作者：执行形态门控、左向贴图/枢轴/旋转镜像及伪装材质注入。</summary>
    public class PawnRenderNodeWorker_NothingTherePart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_NothingTherePart partProps = (node as PawnRenderNode_NothingTherePart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_NothingTherePart);
            if (partProps == null)
            {
                return false;
            }

            CompNothingThere comp = (parms.pawn as LC_NothingTherePawn)?.SelfEntityComp
                ?? parms.pawn?.TryGetComp<CompNothingThere>();
            NothingThereForm form = comp?.CurrentForm ?? NothingThereForm.Contained;
            bool hasDisguise = comp != null && comp.HasDisguise;

            switch (partProps.role)
            {
                case NothingThereRenderRole.FormRoot_Dog:
                case NothingThereRenderRole.DogPart:
                case NothingThereRenderRole.DogEye:
                    return (form == NothingThereForm.Contained || form == NothingThereForm.Form1_Disguised) && !hasDisguise;

                case NothingThereRenderRole.FormRoot_Disguise:
                case NothingThereRenderRole.DisguiseBody:
                    return (form == NothingThereForm.Contained || form == NothingThereForm.Form1_Disguised) && hasDisguise;

                case NothingThereRenderRole.DisguiseSeam:
                    return false;

                case NothingThereRenderRole.FormRoot_Cocoon:
                case NothingThereRenderRole.CocoonPart:
                    return form == NothingThereForm.Form2_Cocoon;

                case NothingThereRenderRole.FormRoot_True:
                case NothingThereRenderRole.TruePart:
                case NothingThereRenderRole.TrueEye:
                    return form == NothingThereForm.Form3_TrueForm;

                case NothingThereRenderRole.TrueMace:
                    return form == NothingThereForm.Form3_TrueForm && (comp == null || !comp.IsPerformingGoodbye);

                case NothingThereRenderRole.TrueGoodbyeBlade:
                    return form == NothingThereForm.Form3_TrueForm && comp != null && comp.IsPerformingGoodbye;

                default:
                    return false;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_NothingTherePart ntNode = node as PawnRenderNode_NothingTherePart;
            PawnRenderNodeProperties_NothingTherePart partProps = ntNode?.PartProps;
            if (partProps != null && partProps.role == NothingThereRenderRole.DisguiseBody)
            {
                CompNothingThere comp = (parms.pawn as LC_NothingTherePawn)?.SelfEntityComp
                    ?? parms.pawn?.TryGetComp<CompNothingThere>();
                if (comp != null
                    && (comp.CurrentForm == NothingThereForm.Contained || comp.CurrentForm == NothingThereForm.Form1_Disguised)
                    && comp.HasDisguise)
                {
                    Material disguiseMat = comp.CachedDisguiseMaterial;
                    if (disguiseMat != null)
                    {
                        if (!parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
                        {
                            return InvisibilityMatPool.GetInvisibleMat(disguiseMat);
                        }
                        return disguiseMat;
                    }
                }
            }

            if (ntNode != null && ntNode.ShouldUseLeftFacing(parms))
            {
                Graphic leftGraphic = ntNode.GraphicLeftFor(parms.pawn);
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

        private static bool EvaluateEyeMotion(
            PawnDrawParms parms,
            PawnRenderNodeProperties_NothingTherePart partProps,
            out Vector2 offsetXZ,
            out float rotDeg)
        {
            offsetXZ = Vector2.zero;
            rotDeg = 0f;
            if (partProps == null || parms.Portrait)
            {
                return false;
            }

            if (partProps.eyeCenterOffsetUV == Vector2.zero
                && partProps.eyeOrbitRadius == Vector2.zero
                && Mathf.Abs(partProps.eyeRotAmplitude) <= 0.01f)
            {
                return false;
            }

            int ticks = Find.TickManager?.TicksGame ?? 0;
            int seedShift = (parms.pawn?.thingIDNumber ?? 0) * 19;
            int period = Mathf.Max(24, partProps.eyePeriodTicks);
            float u = (((ticks + seedShift) % period) / (float)period + partProps.eyePhaseOffset / (Mathf.PI * 2f)) % 1f;
            if (u < 0f) u += 1f;

            // 四段“凝视停留 -> 快速扫视”轨迹，模拟脑叶公司眼珠不安分地四处乱转
            float scaled = u * 4f;
            int seg = Mathf.Clamp((int)scaled, 0, 3);
            float frac = scaled - seg;
            float moveT = frac < 0.62f ? 0f : Mathf.SmoothStep(0f, 1f, (frac - 0.62f) / 0.38f);

            Vector2 p0 = EyeWaypoint(seg, partProps.eyePhaseOffset);
            Vector2 p1 = EyeWaypoint((seg + 1) & 3, partProps.eyePhaseOffset);
            Vector2 dir = Vector2.Lerp(p0, p1, moveT);

            offsetXZ = new Vector2(
                partProps.eyeCenterOffsetUV.x + dir.x * partProps.eyeOrbitRadius.x,
                partProps.eyeCenterOffsetUV.y + dir.y * partProps.eyeOrbitRadius.y);
            rotDeg = Mathf.Sin(u * Mathf.PI * 2f + partProps.eyePhaseOffset) * partProps.eyeRotAmplitude;
            return true;
        }

        private static Vector2 EyeWaypoint(int index, float phase)
        {
            float baseX;
            float baseY;
            switch (index & 3)
            {
                case 0: baseX = 0.85f; baseY = 0.40f; break;
                case 1: baseX = -0.80f; baseY = 0.65f; break;
                case 2: baseX = 0.55f; baseY = -0.75f; break;
                default: baseX = -0.65f; baseY = -0.50f; break;
            }

            float c = Mathf.Cos(phase);
            float s = Mathf.Sin(phase);
            return new Vector2(
                Mathf.Clamp(baseX * c - baseY * s, -1f, 1f),
                Mathf.Clamp(baseX * s + baseY * c, -1f, 1f));
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_NothingTherePart ntNode = node as PawnRenderNode_NothingTherePart;
            PawnRenderNodeProperties_NothingTherePart partProps = ntNode?.PartProps;

            if (partProps != null && partProps.role == NothingThereRenderRole.DisguiseSeam && !parms.Portrait)
            {
                int tick = Find.TickManager?.TicksGame ?? 0;
                if (tick % 37 == 0) offset.x += 0.027f;
                if (tick % 53 == 0) offset.z -= 0.020f;
            }

            if (partProps != null && (partProps.role == NothingThereRenderRole.DogEye || partProps.role == NothingThereRenderRole.TrueEye))
            {
                pivot = new Vector3(partProps.eyePivotUV.x - 0.5f, 0f, partProps.eyePivotUV.y - 0.5f);
                if (EvaluateEyeMotion(parms, partProps, out Vector2 eyeOff, out _))
                {
                    offset.x += eyeOff.x;
                    offset.z += eyeOff.y;
                }
            }

            if (ntNode != null && ntNode.ShouldUseLeftFacing(parms))
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
            PawnRenderNode_NothingTherePart ntNode = node as PawnRenderNode_NothingTherePart;
            PawnRenderNodeProperties_NothingTherePart partProps = ntNode?.PartProps;
            if (partProps != null
                && (partProps.role == NothingThereRenderRole.DogEye || partProps.role == NothingThereRenderRole.TrueEye)
                && EvaluateEyeMotion(parms, partProps, out _, out float eyeRot))
            {
                num += eyeRot;
            }
            if (ntNode != null && ntNode.ShouldUseLeftFacing(parms))
            {
                num = -num;
            }
            return Quaternion.AngleAxis(num, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_NothingTherePart partProps = (node as PawnRenderNode_NothingTherePart)?.PartProps;
            if (partProps != null && partProps.IsFormRoot && !Mathf.Approximately(partProps.formScale, 1f))
            {
                scale.x *= partProps.formScale;
                scale.z *= partProps.formScale;
            }
            return scale;
        }
    }
}
