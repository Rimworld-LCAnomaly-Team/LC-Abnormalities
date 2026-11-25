using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.WarmHeartedWoodsman
{
    public class PawnRenderNodeProperties_WarmHeartedWoodsmanPart : PawnRenderNodeProperties
    {
        public string role = "Woodsman_Torso";
        public float containedScale = 2.05f;
        public float breachedScale = 2.35f;
        public float portraitScale = 2.15f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;
        public List<string> alternateTexPaths;

        public bool IsGroupRoot => role == "Root" || !useGraphic;

        public PawnRenderNodeProperties_WarmHeartedWoodsmanPart()
        {
            nodeClass = typeof(PawnRenderNode_WarmHeartedWoodsmanPart);
            workerClass = typeof(PawnRenderNodeWorker_WarmHeartedWoodsmanPart);
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

    public class PawnRenderNode_WarmHeartedWoodsmanPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_WarmHeartedWoodsmanPart PartProps => Props as PawnRenderNodeProperties_WarmHeartedWoodsmanPart;

        public PawnRenderNode_WarmHeartedWoodsmanPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_WarmHeartedWoodsmanPawn w && w.IsHeldOnPlatform;
        }

        public static CompWarmHeartedWoodsman GetWoodsmanComp(Pawn pawn)
        {
            return (pawn as LC_WarmHeartedWoodsmanPawn)?.WoodsmanComp ?? pawn?.TryGetComp<CompWarmHeartedWoodsman>();
        }

        // Woodsman Spine artwork faces West (-X), so mirror horizontally on East
        public static bool ShouldFlipHorizontal(PawnDrawParms parms)
        {
            return !parms.Portrait && !IsHeldOnPlatform(parms.pawn) && parms.facing == Rot4.East;
        }

        public override bool FlipGraphic(PawnDrawParms parms) => ShouldFlipHorizontal(parms);

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && PartProps.IsGroupRoot) return null;
            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            if (PartProps != null && PartProps.IsGroupRoot) return null;
            if (meshSet == null) return null;
            Mesh mesh = meshSet.MeshAt(Rot4.South);
            return ShouldFlipHorizontal(parms) ? MeshPool.GridPlaneFlip(mesh) : mesh;
        }

        public Graphic GetCachedGraphic(string path)
        {
            if (path.NullOrEmpty()) return null;
            if (cachedGraphics.TryGetValue(path, out Graphic found) && found != null) return found;
            if (!UnityData.IsInMainThread) return null;

            Shader shader = (PartProps != null && PartProps.useTransparentShader)
                ? ShaderDatabase.Transparent
                : ShaderDatabase.Cutout;

            Graphic created = GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, Color.white);
            if (created != null) cachedGraphics[path] = created;
            return created;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (PartProps == null || PartProps.IsGroupRoot || PartProps.texPath.NullOrEmpty()) return null;
            return GetCachedGraphic(PartProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (PartProps == null || PartProps.IsGroupRoot) yield break;
            Graphic primary = GetCachedGraphic(PartProps.texPath);
            if (primary != null) yield return primary;
        }
    }

    public class PawnRenderNodeWorker_WarmHeartedWoodsmanPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;

            PawnRenderNodeProperties_WarmHeartedWoodsmanPart p = (node as PawnRenderNode_WarmHeartedWoodsmanPart)?.PartProps;
            if (p == null) return false;
            CompWarmHeartedWoodsman comp = PawnRenderNode_WarmHeartedWoodsmanPart.GetWoodsmanComp(parms.pawn);

            if (p.role == "Woodsman_Guts")
            {
                return !parms.Portrait && comp != null && comp.Escaped && comp.IsEatingHeart;
            }
            if (p.role == "Woodsman_Heart")
            {
                if (parms.Portrait) return true;
                return comp != null && (comp.HasFreshHeart || comp.Escaped || comp.IsHeartTrapArmed);
            }
            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_WarmHeartedWoodsmanPart wNode && wNode.PartProps != null)
            {
                Graphic g = wNode.GetCachedGraphic(wNode.PartProps.texPath);
                if (g != null)
                {
                    PawnDrawParms matParms = parms;
                    matParms.facing = Rot4.South;
                    Material mat = g.NodeGetMat(matParms);
                    if (mat != null && !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
                    {
                        mat = InvisibilityMatPool.GetInvisibleMat(mat);
                    }
                    if (mat != null) return mat;
                }
            }
            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNodeProperties_WarmHeartedWoodsmanPart p = (node as PawnRenderNode_WarmHeartedWoodsmanPart)?.PartProps;
            if (p == null) return offset;

            if (p.IsGroupRoot && !parms.Portrait && PawnRenderNode_WarmHeartedWoodsmanPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += p.containedOffset;
            }
            else if (PawnRenderNode_WarmHeartedWoodsmanPart.ShouldFlipHorizontal(parms))
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
            if (PawnRenderNode_WarmHeartedWoodsmanPart.ShouldFlipHorizontal(parms))
            {
                angle = -angle;
            }
            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_WarmHeartedWoodsmanPart p = (node as PawnRenderNode_WarmHeartedWoodsmanPart)?.PartProps;
            if (p != null && p.IsGroupRoot)
            {
                CompWarmHeartedWoodsman comp = PawnRenderNode_WarmHeartedWoodsmanPart.GetWoodsmanComp(parms.pawn);
                float target;
                if (parms.Portrait) target = p.portraitScale;
                else if (!PawnRenderNode_WarmHeartedWoodsmanPart.IsHeldOnPlatform(parms.pawn) && comp != null && comp.Escaped) target = p.breachedScale;
                else target = p.containedScale;

                if (!Mathf.Approximately(target, 1f))
                {
                    scale.x *= target;
                    scale.z *= target;
                }
            }
            return scale;
        }
    }
}
