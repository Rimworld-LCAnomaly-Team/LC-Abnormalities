using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.WeCanChangeAnything
{
    /// <summary>T-09-85 我们可以改变一切分段骨骼渲染节点角色分类（对应解包 IronMaiden.txt 与 IronMaiden.png）。</summary>
    public enum WeCanChangeAnythingRenderRole
    {
        Root,
        Chassis,
        Body,
        BodyBlood,
        DoorLeft,
        HingeLeftUpper,
        HingeLeftLower,
        DoorRight,
        HingeRightUpper,
        HingeRightLower,
        Blood1,
        Blood2,
        Eyes,
        Heart
    }

    /// <summary>我们可以改变一切多部件骨骼渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_WeCanChangeAnythingPart : PawnRenderNodeProperties
    {
        public WeCanChangeAnythingRenderRole role = WeCanChangeAnythingRenderRole.Body;
        public float formScale = 2.2f;
        public float containedScale = 2.2f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;

        public bool IsGroupRoot => role == WeCanChangeAnythingRenderRole.Root;

        public bool IsVirtualContainer => role == WeCanChangeAnythingRenderRole.Root || role == WeCanChangeAnythingRenderRole.Chassis;

        public PawnRenderNodeProperties_WeCanChangeAnythingPart()
        {
            nodeClass = typeof(PawnRenderNode_WeCanChangeAnythingPart);
            workerClass = typeof(PawnRenderNodeWorker_WeCanChangeAnythingPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsVirtualContainer)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>
    /// 我们可以改变一切多部件骨骼渲染节点：
    /// 支持开合门、四连铰链伸缩、三阶段递增摇晃、门缝递进血渍、像素笑眼/红心闪烁与处决后的染血内腔展示。
    /// </summary>
    public class PawnRenderNode_WeCanChangeAnythingPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_WeCanChangeAnythingPart PartProps => Props as PawnRenderNodeProperties_WeCanChangeAnythingPart;

        public PawnRenderNode_WeCanChangeAnythingPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static CompWeCanChangeAnything GetIronComp(Pawn pawn)
        {
            return pawn?.TryGetComp<CompWeCanChangeAnything>();
        }

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return GetIronComp(pawn)?.HeldPlatform != null;
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
            if (PartProps != null && PartProps.IsVirtualContainer)
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
            PawnRenderNodeProperties_WeCanChangeAnythingPart partProps = PartProps;
            if (partProps == null || partProps.IsVirtualContainer || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_WeCanChangeAnythingPart partProps = PartProps;
            if (partProps == null || partProps.IsVirtualContainer)
            {
                yield break;
            }

            Graphic primary = GetCachedGraphic(partProps.texPath);
            if (primary != null)
            {
                yield return primary;
            }
        }
    }

    /// <summary>
    /// 我们可以改变一切渲染节点工作者：
    /// 严格对齐解包 IronMaiden.txt 与脑叶公司 Wiki 动画时序：
    /// - Default_First / Default_After：敞开待机（染血后展示血肉内腔 BodyBlood）；
    /// - Start_First / Start_After：门扇合拢后于 52t 点亮像素笑眼、58t 点亮像素红心；
    /// - Generate_01（一阶段）：基底枢轴规律摇晃，门外无血，红心随摇晃闪烁；
    /// - Generate_02（二阶段）：高频双连摇晃，门缝渗出 Blood2，红心高频闪烁；
    /// - Generate_03（三阶段）：持续剧烈摇晃，Blood1 + Blood2 迸溅，像素笑眼与红心同步疯狂闪烁；
    /// - End：显示熄灭，门扇重新敞开露出染血内腔。
    /// </summary>
    public class PawnRenderNodeWorker_WeCanChangeAnythingPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_WeCanChangeAnythingPart partProps =
                (node as PawnRenderNode_WeCanChangeAnythingPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_WeCanChangeAnythingPart);
            if (partProps == null)
            {
                return false;
            }

            switch (partProps.role)
            {
                case WeCanChangeAnythingRenderRole.Root:
                case WeCanChangeAnythingRenderRole.Chassis:
                case WeCanChangeAnythingRenderRole.Body:
                case WeCanChangeAnythingRenderRole.DoorLeft:
                case WeCanChangeAnythingRenderRole.HingeLeftUpper:
                case WeCanChangeAnythingRenderRole.HingeLeftLower:
                case WeCanChangeAnythingRenderRole.DoorRight:
                case WeCanChangeAnythingRenderRole.HingeRightUpper:
                case WeCanChangeAnythingRenderRole.HingeRightLower:
                    return true;
            }

            if (parms.Portrait)
            {
                return false;
            }

            CompWeCanChangeAnything comp = PawnRenderNode_WeCanChangeAnythingPart.GetIronComp(parms.pawn);
            AnimationDef curAnim = node.tree?.currentAnimation;

            bool isStart = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_First
                || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_After;
            bool isGen1 = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_01;
            bool isGen2 = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_02;
            bool isGen3 = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_03;
            bool isEnd = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_End;
            bool isGenerating = isGen1 || isGen2 || isGen3;

            switch (partProps.role)
            {
                case WeCanChangeAnythingRenderRole.BodyBlood:
                    if (isGenerating)
                    {
                        return false;
                    }
                    return (comp != null && comp.StainedWithBlood)
                        || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Default_After
                        || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_After;

                case WeCanChangeAnythingRenderRole.Blood2:
                    if (isEnd || isStart)
                    {
                        return false;
                    }
                    return isGen2 || isGen3 || (curAnim == null && comp != null && comp.CurrentWorkStage >= 2);

                case WeCanChangeAnythingRenderRole.Blood1:
                    if (isEnd || isStart)
                    {
                        return false;
                    }
                    return isGen3 || (curAnim == null && comp != null && comp.CurrentWorkStage >= 3);

                case WeCanChangeAnythingRenderRole.Eyes:
                case WeCanChangeAnythingRenderRole.Heart:
                    // Fine-grained per-tick onset & flicker are evaluated every frame in ScaleFor
                    // (since PawnRenderTree caches CanDrawNow between SetDirty() calls).
                    return isStart || isGenerating;

                default:
                    return true;
            }
        }

        private static bool IsPixelIndicatorVisibleNow(WeCanChangeAnythingRenderRole role, PawnRenderNode node, AnimationDef curAnim)
        {
            if (curAnim == null)
            {
                return false;
            }

            int animTick = GetCurrentAnimTick(node, curAnim);
            bool isStart = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_First
                || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_After;

            if (role == WeCanChangeAnythingRenderRole.Eyes)
            {
                if (isStart)
                {
                    return animTick >= 52;
                }
                if (curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_01
                    || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_02)
                {
                    return true;
                }
                if (curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_03)
                {
                    return IsStage3EyesVisible(animTick);
                }
                return false;
            }

            if (role == WeCanChangeAnythingRenderRole.Heart)
            {
                if (isStart)
                {
                    return animTick >= 58;
                }
                if (curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_01)
                {
                    return IsStage1HeartVisible(animTick);
                }
                if (curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_02)
                {
                    return IsStage2HeartVisible(animTick);
                }
                if (curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Generate_03)
                {
                    return IsStage3HeartVisible(animTick);
                }
                return false;
            }

            return true;
        }

        private static int GetCurrentAnimTick(PawnRenderNode node, AnimationDef curAnim)
        {
            if (node?.tree == null || curAnim == null || curAnim.durationTicks <= 0)
            {
                return 0;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int elapsed = now - node.tree.animationStartTick;

            bool isOneShot = curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_First
                || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_Start_After
                || curAnim == WeCanChangeAnythingAnimDefOf.WeCanChangeAnything_End;
            if (isOneShot)
            {
                return Mathf.Clamp(elapsed, 0, curAnim.durationTicks);
            }

            int dur = curAnim.durationTicks;
            return ((elapsed % dur) + dur) % dur;
        }

        // IronMaiden.txt Generate_01 (40t): heart off [18..22) and [24..28)
        private static bool IsStage1HeartVisible(int tick)
        {
            return !((tick >= 18 && tick < 22) || (tick >= 24 && tick < 28));
        }

        // IronMaiden.txt Generate_02 (60t): heart off [12..16), [18..22), [38..42), [46..50)
        private static bool IsStage2HeartVisible(int tick)
        {
            return !((tick >= 12 && tick < 16)
                || (tick >= 18 && tick < 22)
                || (tick >= 38 && tick < 42)
                || (tick >= 46 && tick < 50));
        }

        // IronMaiden.txt Generate_03 (38t): eyes flicker off [2..4), [6..10), [16..18), [24..28), [34..38)
        private static bool IsStage3EyesVisible(int tick)
        {
            return !((tick >= 2 && tick < 4)
                || (tick >= 6 && tick < 10)
                || (tick >= 16 && tick < 18)
                || (tick >= 24 && tick < 28)
                || (tick >= 34 && tick < 38));
        }

        // IronMaiden.txt Generate_03 (38t): heart flicker off [4..8), [10..14), [16..20), [24..28), [30..34)
        private static bool IsStage3HeartVisible(int tick)
        {
            return !((tick >= 4 && tick < 8)
                || (tick >= 10 && tick < 14)
                || (tick >= 16 && tick < 20)
                || (tick >= 24 && tick < 28)
                || (tick >= 30 && tick < 34));
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_WeCanChangeAnythingPart wNode = node as PawnRenderNode_WeCanChangeAnythingPart;
            if (wNode != null && wNode.PartProps != null && !wNode.PartProps.IsVirtualContainer)
            {
                Graphic g = wNode.GetCachedGraphic(wNode.PartProps.texPath);
                if (g != null)
                {
                    PawnDrawParms matParms = parms;
                    matParms.facing = Rot4.South;
                    Material mat = g.NodeGetMat(matParms);
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
            PawnRenderNodeProperties_WeCanChangeAnythingPart partProps =
                (node as PawnRenderNode_WeCanChangeAnythingPart)?.PartProps;
            if (partProps != null
                && partProps.IsGroupRoot
                && !parms.Portrait
                && PawnRenderNode_WeCanChangeAnythingPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
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

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_WeCanChangeAnythingPart partProps =
                (node as PawnRenderNode_WeCanChangeAnythingPart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if ((partProps.role == WeCanChangeAnythingRenderRole.Eyes || partProps.role == WeCanChangeAnythingRenderRole.Heart)
                && !parms.Portrait)
            {
                if (!IsPixelIndicatorVisibleNow(partProps.role, node, node.tree?.currentAnimation))
                {
                    return Vector3.zero;
                }
                return scale;
            }

            if (!partProps.IsGroupRoot)
            {
                return scale;
            }

            float targetScale = partProps.formScale;
            if (!parms.Portrait && PawnRenderNode_WeCanChangeAnythingPart.IsHeldOnPlatform(parms.pawn))
            {
                if (partProps.containedScale > 0f)
                {
                    targetScale = partProps.containedScale;
                }

                LCAnomalyCore.Comp.CompAbnormalityHoldingPlatformTarget targetComp =
                    parms.pawn?.TryGetComp<LCAnomalyCore.Comp.CompAbnormalityHoldingPlatformTarget>();
                if (targetComp != null && targetComp.Props.heldDrawScale > 0f && !Mathf.Approximately(targetComp.Props.heldDrawScale, 1f))
                {
                    targetScale *= targetComp.Props.heldDrawScale;
                }
            }

            if (!Mathf.Approximately(targetScale, 1f))
            {
                scale.x *= targetScale;
                scale.z *= targetScale;
            }

            return scale;
        }
    }
}
