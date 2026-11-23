using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.BigBird
{
    /// <summary>大鸟分段骨骼渲染节点角色分类（严格对应解包 BigBird.fbx 骨骼层级）。</summary>
    public enum BigBirdRenderRole
    {
        Root,
        Body,
        EyePupils,
        Eyelids,
        MouthInterior,
        UpperBeak,
        LowerBeak,
        BackThigh,
        BackShinFoot,
        FrontThigh,
        FrontShinFoot,
        UpperArm,
        ForearmHand,
        Lantern,
        LanternFlame
    }

    /// <summary>大鸟多部件渲染节点属性定义。</summary>
    public class PawnRenderNodeProperties_BigBirdPart : PawnRenderNodeProperties
    {
        public BigBirdRenderRole role = BigBirdRenderRole.Body;
        public float formScale = 1f;
        public float containedScale = 1f;
        public Vector3 containedOffset = Vector3.zero;
        public bool flipOnWest = true;
        public bool useTransparentShader = false;
        public bool useGlowShader = false;
        public string texPathLeft;
        public List<string> alternateTexPaths;
        public List<string> alternateTexPathsLeft;

        public bool IsGroupRoot => role == BigBirdRenderRole.Root;

        public PawnRenderNodeProperties_BigBirdPart()
        {
            nodeClass = typeof(PawnRenderNode_BigBirdPart);
            workerClass = typeof(PawnRenderNodeWorker_BigBirdPart);
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

    /// <summary>大鸟多部件骨骼渲染节点：支持主线程预加载、金瞳/赤瞳变体切换、周期性眨眼、巨喙张开血口显隐与独立左向镜像。</summary>
    public class PawnRenderNode_BigBirdPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_BigBirdPart PartProps => Props as PawnRenderNodeProperties_BigBirdPart;

        public PawnRenderNode_BigBirdPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <summary>禁用引擎默认 West 翻转，统一使用独立 Left 贴图与枢轴/角度镜像。</summary>
        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static bool IsHeldOnPlatform(Pawn pawn)
        {
            return pawn is LC_BigBirdPawn bb && bb.IsHeldOnPlatform;
        }

        public static CompBigBird GetBigBirdComp(Pawn pawn)
        {
            return (pawn as LC_BigBirdPawn)?.BigBirdComp ?? pawn?.TryGetComp<CompBigBird>();
        }

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_BigBirdPart partProps = PartProps;
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

            if (parms.pawn is LC_BigBirdPawn bb && bb.BigBirdComp?.Escaped == true)
            {
                return bb.IsFacingLeft;
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

            Shader shader = ShaderDatabase.Cutout;
            if (PartProps != null)
            {
                if (PartProps.useGlowShader)
                {
                    shader = ShaderDatabase.MoteGlow;
                }
                else if (PartProps.useTransparentShader)
                {
                    shader = ShaderDatabase.Transparent;
                }
            }

            Graphic created = GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, Color.white);
            if (created != null)
            {
                cachedGraphics[path] = created;
            }

            return created;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_BigBirdPart partProps = PartProps;
            if (partProps == null || partProps.IsGroupRoot || partProps.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(partProps.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_BigBirdPart partProps = PartProps;
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

        /// <summary>返回当前眨眼阶段：0 = 睁眼，1 = 半闭，2 = 全闭。</summary>
        public static int GetBlinkStage(Pawn pawn)
        {
            CompBigBird comp = GetBigBirdComp(pawn);
            // 咬头处决与摇灯魅惑时众目圆睁，不眨眼
            if (comp != null && (comp.IsPerformingDeadshot || comp.IsPerformingAttract || comp.IsPerformingMindAttack))
            {
                return 0;
            }

            int ticks = (Find.TickManager?.TicksGame ?? 0) + ((pawn?.thingIDNumber ?? 0) * 37);
            int cycle = Mathf.Abs(ticks) % 210;
            if (cycle >= 196 && cycle <= 198) return 1;
            if (cycle >= 199 && cycle <= 203) return 2;
            if (cycle >= 204 && cycle <= 206) return 1;
            return 0;
        }

        public string ResolveDynamicTexPath(PawnDrawParms parms, bool leftFacing)
        {
            PawnRenderNodeProperties_BigBirdPart partProps = PartProps;
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
                case BigBirdRenderRole.Body:
                    // alts[0] = Body_Attract (bright golden), alts[1] = Body_Deadshot (crimson eyes)
                    if (!parms.Portrait && alts != null)
                    {
                        CompBigBird comp = GetBigBirdComp(parms.pawn);
                        if (comp != null)
                        {
                            if (comp.IsPerformingDeadshot && alts.Count > 1)
                            {
                                return alts[1];
                            }
                            if ((comp.IsPerformingAttract || comp.IsPerformingMindAttack) && alts.Count > 0)
                            {
                                return alts[0];
                            }
                        }
                    }
                    return basePath;

                case BigBirdRenderRole.Eyelids:
                    // basePath = Eyelids_Half, alts[0] = Eyelids_Closed
                    if (alts != null && alts.Count > 0 && GetBlinkStage(parms.pawn) == 2)
                    {
                        return alts[0];
                    }
                    return basePath;

                default:
                    return basePath;
            }
        }
    }

    /// <summary>大鸟多部件渲染节点工作者：管理眨眼/血口显隐、动态材质切换、左向镜像、火焰呼吸脉冲与收容台缩放。</summary>
    public class PawnRenderNodeWorker_BigBirdPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            PawnRenderNodeProperties_BigBirdPart partProps = (node as PawnRenderNode_BigBirdPart)?.PartProps
                ?? (node.Props as PawnRenderNodeProperties_BigBirdPart);
            if (partProps == null)
            {
                return false;
            }

            switch (partProps.role)
            {
                case BigBirdRenderRole.Eyelids:
                    return !parms.Portrait && PawnRenderNode_BigBirdPart.GetBlinkStage(parms.pawn) > 0;

                case BigBirdRenderRole.MouthInterior:
                    if (parms.Portrait)
                    {
                        return false;
                    }
                    CompBigBird comp = PawnRenderNode_BigBirdPart.GetBigBirdComp(parms.pawn);
                    return comp != null && comp.IsMouthOpenForDeadshot;

                default:
                    return true;
            }
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_BigBirdPart bbNode = node as PawnRenderNode_BigBirdPart;
            if (bbNode != null)
            {
                bool left = bbNode.ShouldUseLeftFacing(parms);
                string resolvedPath = bbNode.ResolveDynamicTexPath(parms, left);
                Graphic g = bbNode.GetCachedGraphic(resolvedPath);
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
            PawnRenderNode_BigBirdPart bbNode = node as PawnRenderNode_BigBirdPart;
            PawnRenderNodeProperties_BigBirdPart partProps = bbNode?.PartProps;

            if (partProps != null
                && partProps.role == BigBirdRenderRole.Root
                && !parms.Portrait
                && PawnRenderNode_BigBirdPart.IsHeldOnPlatform(parms.pawn))
            {
                offset += partProps.containedOffset;
            }

            if (bbNode != null && bbNode.ShouldUseLeftFacing(parms))
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

            PawnRenderNode_BigBirdPart bbNode = node as PawnRenderNode_BigBirdPart;
            if (bbNode != null && bbNode.ShouldUseLeftFacing(parms))
            {
                num = -num;
            }

            return Quaternion.AngleAxis(num, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_BigBirdPart partProps = (node as PawnRenderNode_BigBirdPart)?.PartProps;
            if (partProps == null)
            {
                return scale;
            }

            if (partProps.IsGroupRoot)
            {
                float targetScale = partProps.formScale;
                if (!parms.Portrait
                    && PawnRenderNode_BigBirdPart.IsHeldOnPlatform(parms.pawn)
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
            else if (partProps.role == BigBirdRenderRole.LanternFlame && !parms.Portrait)
            {
                // Live organic eternal lantern flame flicker (mirrors LampFlame Inner/Mid/Outter pulse)
                float t = (Find.TickManager?.TicksGame ?? 0) * 0.14f + (parms.pawn?.thingIDNumber ?? 0) * 0.37f;
                CompBigBird comp = PawnRenderNode_BigBirdPart.GetBigBirdComp(parms.pawn);
                float boost = (comp != null && (comp.IsPerformingAttract || comp.IsPerformingMindAttack)) ? 1.24f : 1.0f;
                float flicker = boost * (0.94f + 0.08f * Mathf.Sin(t * 2.1f) + 0.04f * Mathf.Sin(t * 5.3f));
                scale.x *= flicker;
                scale.z *= flicker;
            }

            return scale;
        }
    }
}
