using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.LittleRedRidingHoodedMercenary
{
    /// <summary>小红帽雇佣兵分段骨骼渲染节点角色分类（严格对应解包 RedHood / skeleton #223196.txt 骨骼层级）。</summary>
    public enum LittleRedRenderRole
    {
        Root,
        Pelvis,
        Limb,
        Torso,
        Cape,
        Head,
        HoodShadow,
        Gun,
        Sickle
    }

    /// <summary>小红帽雇佣兵分段骨骼渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_LittleRedPart : PawnRenderNodeProperties
    {
        public LittleRedRenderRole role = LittleRedRenderRole.Limb;
        public float formScale = 2.25f;
        public float containedScale = 2.05f;
        public string texPathLeft;
        public string enragedTexPath;
        public string enragedTexPathLeft;

        public bool IsContainer => role == LittleRedRenderRole.Root || role == LittleRedRenderRole.Pelvis;

        public PawnRenderNodeProperties_LittleRedPart()
        {
            nodeClass = typeof(PawnRenderNode_LittleRedPart);
            workerClass = typeof(PawnRenderNodeWorker_LittleRedPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (IsContainer)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>
    /// 小红帽雇佣兵多部件骨骼渲染节点：
    /// 支持主线程预加载左右镜像贴图、冷静黑影兜帽 / 暴走猩红疤面切换、
    /// 收容时收起双武器与兜帽阴影显隐、以及飞镰掷出期间右手弯镰动态隐藏。
    /// </summary>
    public class PawnRenderNode_LittleRedPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_LittleRedPart PartProps => Props as PawnRenderNodeProperties_LittleRedPart;

        public PawnRenderNode_LittleRedPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
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

            if (parms.pawn is LC_LittleRedPawn lr)
            {
                if (lr.IsHeldOnPlatform)
                {
                    return false;
                }

                return lr.IsFacingLeft;
            }

            return parms.facing == Rot4.West;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && (PartProps.IsContainer || !PartProps.useGraphic))
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetCachedGraphic(string path)
        {
            if (path.NullOrEmpty())
            {
                return null;
            }

            if (cachedGraphics.TryGetValue(path, out Graphic existing) && existing != null)
            {
                return existing;
            }

            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            Graphic created = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
            if (created != null)
            {
                cachedGraphics[path] = created;
            }

            return created;
        }

        public Graphic ResolveGraphic(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_LittleRedPart p = PartProps;
            if (p == null || p.IsContainer || !p.useGraphic)
            {
                return null;
            }

            bool left = ShouldUseLeftFacing(parms);
            bool enraged = false;
            if (!parms.Portrait && p.role == LittleRedRenderRole.Head && parms.pawn is LC_LittleRedPawn lr)
            {
                enraged = lr.RedComp?.IsEnragedVisual == true;
            }

            string path;
            if (enraged && (!p.enragedTexPath.NullOrEmpty() || !p.enragedTexPathLeft.NullOrEmpty()))
            {
                path = left && !p.enragedTexPathLeft.NullOrEmpty() ? p.enragedTexPathLeft : p.enragedTexPath;
            }
            else
            {
                path = left && !p.texPathLeft.NullOrEmpty() ? p.texPathLeft : p.texPath;
            }

            return GetCachedGraphic(path);
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_LittleRedPart p = PartProps;
            if (p == null || p.IsContainer || !p.useGraphic)
            {
                return null;
            }

            return GetCachedGraphic(p.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_LittleRedPart p = PartProps;
            if (p == null || p.IsContainer || !p.useGraphic)
            {
                yield break;
            }

            Graphic g1 = GetCachedGraphic(p.texPath);
            if (g1 != null) yield return g1;

            Graphic g2 = GetCachedGraphic(p.texPathLeft);
            if (g2 != null) yield return g2;

            Graphic g3 = GetCachedGraphic(p.enragedTexPath);
            if (g3 != null) yield return g3;

            Graphic g4 = GetCachedGraphic(p.enragedTexPathLeft);
            if (g4 != null) yield return g4;
        }
    }

    /// <summary>小红帽雇佣兵分段骨骼渲染节点工作者：驱动左右镜像、武器显隐、飞镰掷出隐藏与兜帽阴影切换。</summary>
    public class PawnRenderNodeWorker_LittleRedPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_LittleRedPart p = (node as PawnRenderNode_LittleRedPart)?.PartProps;
            if (p == null)
            {
                return true;
            }

            if (parms.Portrait)
            {
                // Portrait displays her iconic contained/calm silhouette with weapons drawn cleanly or stowed:
                // show hood shadow off & weapons visible so her mercenary identity reads immediately.
                return p.role != LittleRedRenderRole.HoodShadow;
            }

            LC_LittleRedPawn lr = parms.pawn as LC_LittleRedPawn;
            CompLittleRedRidingHoodedMercenary comp = lr?.RedComp;
            bool contained = lr != null && lr.IsHeldOnPlatform && (comp == null || !comp.Escaped);

            switch (p.role)
            {
                case LittleRedRenderRole.HoodShadow:
                    // Shadow drapes over hood interior while calm/contained; lifts once hunting or enraged
                    return contained && (comp == null || !comp.IsEnragedVisual);

                case LittleRedRenderRole.Gun:
                    // Hand cannon stowed while idling on holding platform; drawn when hunting/breached
                    return !contained;

                case LittleRedRenderRole.Sickle:
                    // Hooked sickle stowed while contained AND hidden in hand while boomerang-thrown in flight!
                    if (contained)
                    {
                        return false;
                    }
                    if (comp != null && comp.IsSickleInFlight)
                    {
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_LittleRedPart lrNode = node as PawnRenderNode_LittleRedPart;
            if (lrNode != null)
            {
                Graphic g = lrNode.ResolveGraphic(parms);
                if (g != null)
                {
                    PawnDrawParms drawParms = parms;
                    drawParms.facing = Rot4.South;
                    Material mat = g.NodeGetMat(drawParms);
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
            PawnRenderNode_LittleRedPart lrNode = node as PawnRenderNode_LittleRedPart;
            if (lrNode != null && lrNode.ShouldUseLeftFacing(parms))
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

            PawnRenderNode_LittleRedPart lrNode = node as PawnRenderNode_LittleRedPart;
            if (lrNode != null && lrNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_LittleRedPart p = (node as PawnRenderNode_LittleRedPart)?.PartProps;
            if (p != null && p.role == LittleRedRenderRole.Root)
            {
                bool contained = !parms.Portrait && parms.pawn is LC_LittleRedPawn lr && lr.IsHeldOnPlatform;
                float s = contained ? p.containedScale : p.formScale;
                if (!Mathf.Approximately(s, 1f))
                {
                    scale.x *= s;
                    scale.z *= s;
                }
            }

            return scale;
        }
    }
}
