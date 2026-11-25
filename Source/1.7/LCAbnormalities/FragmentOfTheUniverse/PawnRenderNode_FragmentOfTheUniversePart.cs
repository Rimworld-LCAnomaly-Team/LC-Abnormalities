using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FragmentOfTheUniverse
{
    public class PawnRenderNodeProperties_FragmentOfTheUniversePart : PawnRenderNodeProperties
    {
        public string role = "Cosmos_Body";
        public float containedScale = 1.75f;
        public float breachedScale = 2.05f;
        public float portraitScale = 1.85f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;
        public List<string> alternateTexPaths;

        public bool IsGroupRoot => role == "Root" || !useGraphic;

        public PawnRenderNodeProperties_FragmentOfTheUniversePart()
        {
            nodeClass = typeof(PawnRenderNode_FragmentOfTheUniversePart);
            workerClass = typeof(PawnRenderNodeWorker_FragmentOfTheUniversePart);
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

    public class PawnRenderNode_FragmentOfTheUniversePart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_FragmentOfTheUniversePart PartProps => Props as PawnRenderNodeProperties_FragmentOfTheUniversePart;

        public PawnRenderNode_FragmentOfTheUniversePart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_FragmentOfTheUniversePawn p && p.IsHeldOnPlatform;
        }

        public static CompFragmentOfTheUniverse GetCosmosComp(Pawn pawn)
        {
            return (pawn as LC_FragmentOfTheUniversePawn)?.CosmosComp ?? pawn?.TryGetComp<CompFragmentOfTheUniverse>();
        }

        // Cosmos attacks toward West (-X), so mirror horizontally on East
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
            if (PartProps.alternateTexPaths != null)
            {
                for (int i = 0; i < PartProps.alternateTexPaths.Count; i++)
                {
                    Graphic g = GetCachedGraphic(PartProps.alternateTexPaths[i]);
                    if (g != null) yield return g;
                }
            }
        }

        public string ResolveDynamicTexPath(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_FragmentOfTheUniversePart p = PartProps;
            if (p == null) return null;

            if (p.alternateTexPaths != null && p.alternateTexPaths.Count > 0 && !parms.Portrait)
            {
                CompFragmentOfTheUniverse comp = GetCosmosComp(parms.pawn);
                if (comp != null && comp.IsBreachedVisual && !IsHeldOnPlatform(parms.pawn))
                {
                    return p.alternateTexPaths[0];
                }
            }
            return p.texPath;
        }
    }

    public class PawnRenderNodeWorker_FragmentOfTheUniversePart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;

            PawnRenderNodeProperties_FragmentOfTheUniversePart p = (node as PawnRenderNode_FragmentOfTheUniversePart)?.PartProps;
            if (p == null) return false;
            if (p.role == "Cosmos_Spikes")
            {
                CompFragmentOfTheUniverse comp = PawnRenderNode_FragmentOfTheUniversePart.GetCosmosComp(parms.pawn);
                return !parms.Portrait && !PawnRenderNode_FragmentOfTheUniversePart.IsHeldOnPlatform(parms.pawn)
                    && comp != null && comp.Escaped;
            }
            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_FragmentOfTheUniversePart cNode)
            {
                string path = cNode.ResolveDynamicTexPath(parms);
                Graphic g = cNode.GetCachedGraphic(path);
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
            PawnRenderNodeProperties_FragmentOfTheUniversePart p = (node as PawnRenderNode_FragmentOfTheUniversePart)?.PartProps;
            if (p == null) return offset;

            if (p.IsGroupRoot && !parms.Portrait && PawnRenderNode_FragmentOfTheUniversePart.IsHeldOnPlatform(parms.pawn))
            {
                offset += p.containedOffset;
            }
            else if (PawnRenderNode_FragmentOfTheUniversePart.ShouldFlipHorizontal(parms))
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
            if (PawnRenderNode_FragmentOfTheUniversePart.ShouldFlipHorizontal(parms))
            {
                angle = -angle;
            }
            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_FragmentOfTheUniversePart p = (node as PawnRenderNode_FragmentOfTheUniversePart)?.PartProps;
            if (p != null && p.IsGroupRoot)
            {
                CompFragmentOfTheUniverse comp = PawnRenderNode_FragmentOfTheUniversePart.GetCosmosComp(parms.pawn);
                float target;
                if (parms.Portrait) target = p.portraitScale;
                else if (!PawnRenderNode_FragmentOfTheUniversePart.IsHeldOnPlatform(parms.pawn) && comp != null && comp.Escaped) target = p.breachedScale;
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
