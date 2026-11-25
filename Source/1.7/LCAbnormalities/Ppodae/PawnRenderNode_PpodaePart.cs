using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Ppodae
{
    public class PawnRenderNodeProperties_PpodaePart : PawnRenderNodeProperties
    {
        public string role = "Ppodae_Puppy_Body";
        public string formGroup = "Puppy";
        public float containedScale = 1.45f;
        public float frenzyScale = 2.10f;
        public float portraitScale = 1.55f;
        public Vector3 containedOffset = Vector3.zero;
        public bool useTransparentShader = false;
        public List<string> alternateTexPaths;

        public bool IsGroupRoot => role == "Root" || !useGraphic;

        public PawnRenderNodeProperties_PpodaePart()
        {
            nodeClass = typeof(PawnRenderNode_PpodaePart);
            workerClass = typeof(PawnRenderNodeWorker_PpodaePart);
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

    public class PawnRenderNode_PpodaePart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_PpodaePart PartProps => Props as PawnRenderNodeProperties_PpodaePart;

        public PawnRenderNode_PpodaePart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_PpodaePawn p && p.IsHeldOnPlatform;
        }

        public static CompPpodae GetPpodaeComp(Pawn pawn)
        {
            return (pawn as LC_PpodaePawn)?.PpodaeComp ?? pawn?.TryGetComp<CompPpodae>();
        }

        // Ppodae Spine artwork faces East (+X), so mirror horizontally only when walking West outside containment
        public static bool ShouldFlipHorizontal(PawnDrawParms parms)
        {
            return !parms.Portrait && !IsHeldOnPlatform(parms.pawn) && parms.facing == Rot4.West;
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
            PawnRenderNodeProperties_PpodaePart p = PartProps;
            if (p == null) return null;

            CompPpodae comp = GetPpodaeComp(parms.pawn);
            if (p.alternateTexPaths != null && p.alternateTexPaths.Count > 0 && !parms.Portrait)
            {
                if (p.role == "Ppodae_Puppy_Mouth")
                {
                    if (comp != null && comp.IsEating && p.alternateTexPaths.Count > 1) return p.alternateTexPaths[1];
                    if (comp != null && comp.IsBarking) return p.alternateTexPaths[0];
                }
                else if (p.role == "Ppodae_Puppy_Twinkle")
                {
                    int cyc = ((Find.TickManager?.TicksGame ?? 0) + (parms.pawn?.thingIDNumber ?? 0) * 13) % 48;
                    if (cyc >= 24) return p.alternateTexPaths[0];
                }
                else if (p.role == "Ppodae_Frenzy_Mouth")
                {
                    if (comp != null && (comp.IsFrenzyAttacking || comp.ShouldReturnToEggSoon)) return p.alternateTexPaths[0];
                }
                else if (p.role == "Ppodae_Frenzy_Eye")
                {
                    int cyc = ((Find.TickManager?.TicksGame ?? 0) + (parms.pawn?.thingIDNumber ?? 0) * 17) % 140;
                    if (cyc >= 126 && cyc <= 136) return p.alternateTexPaths[0];
                }
            }

            return p.texPath;
        }
    }

    public class PawnRenderNodeWorker_PpodaePart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;

            PawnRenderNodeProperties_PpodaePart p = (node as PawnRenderNode_PpodaePart)?.PartProps;
            if (p == null) return false;
            if (p.IsGroupRoot) return true;

            CompPpodae comp = PawnRenderNode_PpodaePart.GetPpodaeComp(parms.pawn);
            bool frenzy = !parms.Portrait && !PawnRenderNode_PpodaePart.IsHeldOnPlatform(parms.pawn) && comp != null && comp.Escaped;

            if (p.formGroup == "Puppy" && frenzy) return false;
            if (p.formGroup == "Frenzy" && !frenzy) return false;

            if (p.role == "Ppodae_Frenzy_Twinkle")
            {
                int cyc = ((Find.TickManager?.TicksGame ?? 0) + (parms.pawn?.thingIDNumber ?? 0) * 17) % 140;
                if (cyc >= 126 && cyc <= 136) return false;
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_PpodaePart pNode)
            {
                string path = pNode.ResolveDynamicTexPath(parms);
                Graphic g = pNode.GetCachedGraphic(path);
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
            PawnRenderNodeProperties_PpodaePart p = (node as PawnRenderNode_PpodaePart)?.PartProps;
            if (p == null) return offset;

            if (p.IsGroupRoot && !parms.Portrait && PawnRenderNode_PpodaePart.IsHeldOnPlatform(parms.pawn))
            {
                offset += p.containedOffset;
            }
            else if (PawnRenderNode_PpodaePart.ShouldFlipHorizontal(parms))
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
            if (PawnRenderNode_PpodaePart.ShouldFlipHorizontal(parms))
            {
                angle = -angle;
            }
            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_PpodaePart p = (node as PawnRenderNode_PpodaePart)?.PartProps;
            if (p != null && p.IsGroupRoot)
            {
                CompPpodae comp = PawnRenderNode_PpodaePart.GetPpodaeComp(parms.pawn);
                float target;
                if (parms.Portrait) target = p.portraitScale;
                else if (!PawnRenderNode_PpodaePart.IsHeldOnPlatform(parms.pawn) && comp != null && comp.Escaped) target = p.frenzyScale;
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
