using System.Collections.Generic;
using BurrowingHeaven.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace BurrowingHeaven.Render
{
    public enum BurrowingHeavenTrophyLayerKind
    {
        Head,
        NeckBlood,
        Beard,
        Hair
    }

    public class PawnRenderNodeProperties_BurrowingHeavenTrophyHead : PawnRenderNodeProperties
    {
        public int slotIndex;
        public BurrowingHeavenTrophyLayerKind layerKind = BurrowingHeavenTrophyLayerKind.Head;
        public Vector3 localOffset = Vector3.zero;
        public float defaultTiltAngle = 0f;
        public float headScale = 0.80f;

        public PawnRenderNodeProperties_BurrowingHeavenTrophyHead()
        {
            nodeClass = typeof(PawnRenderNode_BurrowingHeavenTrophyHead);
            workerClass = typeof(PawnRenderNodeWorker_BurrowingHeavenTrophyHead);
        }
    }

    public class PawnRenderNode_BurrowingHeavenTrophyHead : PawnRenderNode
    {
        private const string NeckBloodTexPath = "Things/Pawn/BurrowingHeaven/Attachments/hanging_blood_drip";
        private static readonly Color DeadSkinTargetTint = new Color(0.74f, 0.69f, 0.69f);

        public new PawnRenderNodeProperties_BurrowingHeavenTrophyHead Props =>
            (PawnRenderNodeProperties_BurrowingHeavenTrophyHead)props;

        public PawnRenderNode_BurrowingHeavenTrophyHead(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public HangingHeadRecord GetRecord(Pawn pawn)
        {
            return (pawn as LC_BurrowingHeavenPawn)?.SelfEntityComp?.GetHangingHead(Props.slotIndex);
        }

        public override bool FlipGraphic(PawnDrawParms parms)
        {
            return false;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            HangingHeadRecord rec = GetRecord(pawn);
            HeadTypeDef ht = rec?.headTypeDef ?? HeadTypeDefOf.Skull;

            switch (Props.layerKind)
            {
                case BurrowingHeavenTrophyLayerKind.Head:
                    return MeshPool.GetMeshSetForSize(1.5f, 1.5f);
                case BurrowingHeavenTrophyLayerKind.NeckBlood:
                    return MeshPool.GetMeshSetForSize(1.0f, 1.0f);
                case BurrowingHeavenTrophyLayerKind.Hair:
                    return MeshPool.GetMeshSetForSize(ht.hairMeshSize.x, ht.hairMeshSize.y);
                case BurrowingHeavenTrophyLayerKind.Beard:
                    return MeshPool.GetMeshSetForSize(ht.beardMeshSize.x, ht.beardMeshSize.y);
                default:
                    return base.MeshSetFor(pawn);
            }
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (HasGraphic(pawn))
            {
                Graphic graphic = GraphicFor(pawn);
                if (graphic != null)
                {
                    yield return graphic;
                }
            }
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            HangingHeadRecord rec = GetRecord(pawn);
            if (rec == null)
                return null;

            switch (Props.layerKind)
            {
                case BurrowingHeavenTrophyLayerKind.Head:
                {
                    HeadTypeDef ht = rec.headTypeDef ?? HeadTypeDefOf.Skull;
                    if (ht.graphicPath.NullOrEmpty())
                        return null;

                    Color deadSkin = Color.Lerp(rec.skinColor, DeadSkinTargetTint, 0.28f);
                    Shader shader = ht == HeadTypeDefOf.Skull ? ShaderDatabase.Cutout : ShaderDatabase.CutoutSkin;
                    return GraphicDatabase.Get<Graphic_Multi>(ht.graphicPath, shader, Vector2.one, deadSkin);
                }
                case BurrowingHeavenTrophyLayerKind.NeckBlood:
                    return GraphicDatabase.Get<Graphic_Single>(NeckBloodTexPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
                case BurrowingHeavenTrophyLayerKind.Hair:
                {
                    if (rec.hairDef == null || rec.hairDef.noGraphic || rec.hairDef.texPath.NullOrEmpty())
                        return null;

                    Shader shader = rec.hairDef.overrideShaderTypeDef?.Shader ?? ShaderDatabase.CutoutHair;
                    return GraphicDatabase.Get<Graphic_Multi>(rec.hairDef.texPath, shader, Vector2.one, rec.hairColor);
                }
                case BurrowingHeavenTrophyLayerKind.Beard:
                {
                    if (rec.beardDef == null || rec.beardDef.noGraphic || rec.beardDef.texPath.NullOrEmpty())
                        return null;

                    Shader shader = rec.beardDef.overrideShaderTypeDef?.Shader ?? ShaderDatabase.CutoutHair;
                    return GraphicDatabase.Get<Graphic_Multi>(rec.beardDef.texPath, shader, Vector2.one, rec.hairColor);
                }
                default:
                    return null;
            }
        }
    }

    public class PawnRenderNodeWorker_BurrowingHeavenTrophyHead : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            if (!(node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode))
                return false;

            return trophyNode.GetRecord(parms.pawn) != null && trophyNode.PrimaryGraphic != null;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            pivot = Vector3.zero;
            if (!(node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode))
                return Vector3.zero;

            switch (trophyNode.Props.layerKind)
            {
                case BurrowingHeavenTrophyLayerKind.Head:
                    return trophyNode.Props.localOffset;
                case BurrowingHeavenTrophyLayerKind.NeckBlood:
                    return new Vector3(0f, 0f, -0.24f);
                case BurrowingHeavenTrophyLayerKind.Beard:
                {
                    HangingHeadRecord rec = trophyNode.GetRecord(parms.pawn);
                    HeadTypeDef ht = rec?.headTypeDef;
                    Vector3 off = Vector3.zero;
                    if (ht != null)
                    {
                        off += ht.beardOffset;
                        if (rec.beardDef != null)
                        {
                            off += rec.beardDef.GetOffset(ht, Rot4.South);
                        }
                    }
                    return off;
                }
                case BurrowingHeavenTrophyLayerKind.Hair:
                default:
                    return Vector3.zero;
            }
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!(node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode))
                return Quaternion.identity;

            if (trophyNode.Props.layerKind != BurrowingHeavenTrophyLayerKind.Head)
                return Quaternion.identity;

            HangingHeadRecord rec = trophyNode.GetRecord(parms.pawn);
            float angle = rec != null ? rec.tiltAngle : trophyNode.Props.defaultTiltAngle;
            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!(node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode))
                return Vector3.one;

            switch (trophyNode.Props.layerKind)
            {
                case BurrowingHeavenTrophyLayerKind.Head:
                {
                    float s = trophyNode.Props.headScale;
                    return new Vector3(s, 1f, s);
                }
                case BurrowingHeavenTrophyLayerKind.NeckBlood:
                    return new Vector3(0.62f, 1f, 0.62f);
                case BurrowingHeavenTrophyLayerKind.Beard:
                case BurrowingHeavenTrophyLayerKind.Hair:
                default:
                    return Vector3.one;
            }
        }
    }
}
