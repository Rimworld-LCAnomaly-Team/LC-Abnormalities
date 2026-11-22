using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    public struct ResolvedTrophyLayer
    {
        public Material material;
        public Mesh mesh;
        public Matrix4x4 localXZMatrix;
        public float subAltitude;
    }

    public static class TrophyHeadRenderHelper
    {
        public const string NeckBloodTexPath = "Things/Pawn/BurrowingHeaven/Attachments/hanging_blood_drip";
        public static readonly Color DeadSkinTargetTint = new Color(0.74f, 0.69f, 0.69f);
        public const float NeckBloodSyntheticRelativeY = 0.0030f;
        public const float CompositeLayerSpan = 0.16f;

        private const string HarBodyAddonNodeFullName = "AlienRace.AlienPawnRenderNode_BodyAddon";
        private const string HarWrapperFullName = "AlienRace.ExtendedGraphics.ExtendedGraphicsPawnWrapper";
        private const string HarConditionApparelFullName = "AlienRace.ExtendedGraphics.ConditionApparel";
        private const string HarConditionPostureFullName = "AlienRace.ExtendedGraphics.ConditionPosture";

        private static bool harReflectInitialized;
        private static bool harReflectAvailable;
        private static FieldInfo harNodePropsField;
        private static FieldInfo harPropsAddonField;
        private static FieldInfo harAddonConditionsField;
        private static FieldInfo harAddonResolveDataField;
        private static FieldInfo harAddonUseSkipFlagsField;
        private static PropertyInfo harAddonColorChannelProp;
        private static FieldInfo harAddonColorChannelField;
        private static ConstructorInfo harWrapperCtor;
        private static MethodInfo harConditionSatisfiedMethod;
        private static Type harConditionApparelType;
        private static Type harConditionPostureType;
        private static FieldInfo harPostureDrawnStandingField;

        private static bool shaderMapsInitialized;
        private static readonly Dictionary<Shader, string> shaderToPath = new Dictionary<Shader, string>();
        private static readonly Dictionary<Shader, string> shaderToDefName = new Dictionary<Shader, string>();
        private static readonly Dictionary<string, string> pathToDefName = new Dictionary<string, string>();
        private static readonly FieldInfo shaderTypeIntField =
            typeof(ShaderTypeDef).GetField("shaderInt", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo shaderDatabaseLookupField =
            typeof(ShaderDatabase).GetField("lookup", BindingFlags.Static | BindingFlags.NonPublic);

        public static List<HangingHeadLayerRecord> CaptureHeadLayers(Pawn victim)
        {
            var result = new List<HangingHeadLayerRecord>();
            PawnRenderTree tree = victim?.Drawer?.renderer?.renderTree;
            if (tree == null)
            {
                return result;
            }

            tree.EnsureInitialized(PawnRenderFlags.None);
            if (!tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out PawnRenderNode headNode) || headNode == null)
            {
                return result;
            }

            ulong tattoosMask = RenderSkipFlagDefOf.Tattoos;
            PawnDrawParms parms = new PawnDrawParms
            {
                pawn = victim,
                matrix = Matrix4x4.identity,
                facing = Rot4.South,
                rotDrawMode = RotDrawMode.Fresh,
                posture = PawnPosture.Standing,
                flags = PawnRenderFlags.None,
                tint = Color.white,
                skipFlags = (victim.genes != null && !victim.genes.TattoosVisible) ? tattoosMask : 0UL,
                flipHead = false,
                dead = false,
                crawling = false,
                swimming = false
            };

            if (!tree.TryGetMatrix(headNode, parms, out Matrix4x4 headMatrix))
            {
                return result;
            }

            Vector3 headPos = new Vector3(headMatrix.m03, headMatrix.m13, headMatrix.m23);
            float headAngleY = ExtractYRotation(headMatrix);
            Matrix4x4 headAnchorInv = Matrix4x4.TRS(headPos, Quaternion.AngleAxis(headAngleY, Vector3.up), Vector3.one).inverse;

            object harWrapperCache = null;
            int captureOrder = 0;
            CaptureNodeRecursive(
                headNode,
                victim,
                tree,
                parms,
                headMatrix,
                headAnchorInv,
                ref harWrapperCache,
                ref captureOrder,
                result);

            return result;
        }

        private static void CaptureNodeRecursive(
            PawnRenderNode node,
            Pawn victim,
            PawnRenderTree tree,
            PawnDrawParms parms,
            Matrix4x4 headMatrix,
            Matrix4x4 headAnchorInv,
            ref object harWrapperCache,
            ref int captureOrder,
            List<HangingHeadLayerRecord> output)
        {
            if (IsExcludedNode(node))
            {
                return;
            }

            if (!CanCaptureNode(node, parms, ref harWrapperCache, out bool isHarAddon, out string harColorChannel))
            {
                return;
            }

            if (node.Props.useGraphic)
            {
                node.EnsureInitialized(PawnRenderFlags.None);
                Graphic graphic = node.PrimaryGraphic ?? node.GraphicFor(victim);
                if (graphic != null && graphic != BaseContent.BadGraphic && !graphic.path.NullOrEmpty())
                {
                    Material mat = node.Worker.GetFinalizedMaterial(node, parms);
                    Mesh mesh = node.GetMesh(parms);
                    if (mat != null && mat != BaseContent.BadMat && mesh != null && tree.TryGetMatrix(node, parms, out Matrix4x4 nodeMatrix))
                    {
                        Matrix4x4 relMatrix = headAnchorInv * nodeMatrix;
                        DecomposeRelativeXZ(
                            relMatrix,
                            headMatrix,
                            nodeMatrix,
                            out Vector2 offsetXZ,
                            out float angleY,
                            out Vector2 scaleXZ,
                            out float relativeY);
                        ExtractMeshSizeAndFlip(node, parms, mesh, out Vector2 meshSize, out bool flipMesh);
                        Shader targetShader = mat.shader != null ? mat.shader : graphic.Shader;
                        ResolveShaderInfo(targetShader, out string shaderTypeDefName, out string shaderPath);

                        output.Add(new HangingHeadLayerRecord
                        {
                            texPath = graphic.path,
                            maskPath = graphic.maskPath,
                            isMulti = graphic is Graphic_Multi,
                            shaderTypeDefName = shaderTypeDefName,
                            shaderPath = shaderPath,
                            color = graphic.Color,
                            colorTwo = graphic.ColorTwo,
                            isSkinLayer = DetermineIsSkinLayer(node, graphic, isHarAddon, harColorChannel),
                            localOffsetXZ = offsetXZ,
                            localAngleY = angleY,
                            localScale = scaleXZ,
                            meshSize = meshSize,
                            flipMesh = flipMesh,
                            relativeY = relativeY,
                            captureOrder = captureOrder++
                        });
                    }
                }
            }

            if (!node.children.NullOrEmpty())
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    CaptureNodeRecursive(
                        node.children[i],
                        victim,
                        tree,
                        parms,
                        headMatrix,
                        headAnchorInv,
                        ref harWrapperCache,
                        ref captureOrder,
                        output);
                }
            }
        }

        private static bool IsExcludedNode(PawnRenderNode node)
        {
            if (node == null || !node.DebugEnabled)
            {
                return true;
            }

            PawnRenderNodeProperties props = node.Props;
            if (props != null && (props.tagDef == PawnRenderNodeTagDefOf.ApparelHead || props is PawnRenderNodeProperties_Overlay))
            {
                return true;
            }

            return node.apparel != null
                || node.Worker is PawnRenderNodeWorker_Overlay
                || node is PawnRenderNode_Stump;
        }

        private static bool CanCaptureNode(
            PawnRenderNode node,
            PawnDrawParms parms,
            ref object harWrapperCache,
            out bool isHarAddon,
            out string harColorChannel)
        {
            isHarAddon = node.GetType().FullName == HarBodyAddonNodeFullName;
            harColorChannel = null;

            if (isHarAddon)
            {
                return CanCaptureHarBodyAddon(node, parms, ref harWrapperCache, out harColorChannel);
            }

            return node.Worker.CanDrawNow(node, parms) && CanDrawSubWorkers(node, parms);
        }

        private static bool CanDrawSubWorkers(PawnRenderNode node, PawnDrawParms parms)
        {
            List<PawnRenderSubWorker> subWorkers = node.Props?.SubWorkers;
            if (subWorkers == null)
            {
                return true;
            }

            for (int i = 0; i < subWorkers.Count; i++)
            {
                if (!subWorkers[i].CanDrawNowSub(node, parms))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CanCaptureHarBodyAddon(
            PawnRenderNode node,
            PawnDrawParms parms,
            ref object harWrapperCache,
            out string colorChannel)
        {
            colorChannel = null;
            EnsureHarReflectionInitialized(node.GetType());
            if (!harReflectAvailable)
            {
                return node.Worker.CanDrawNow(node, parms) && CanDrawSubWorkers(node, parms);
            }

            try
            {
                object nodeProps = harNodePropsField.GetValue(node);
                object addon = nodeProps != null ? harPropsAddonField.GetValue(nodeProps) : null;
                if (addon == null)
                {
                    return node.Worker.CanDrawNow(node, parms) && CanDrawSubWorkers(node, parms);
                }

                if (harAddonColorChannelProp != null)
                {
                    colorChannel = harAddonColorChannelProp.GetValue(addon) as string;
                }
                else if (harAddonColorChannelField != null)
                {
                    colorChannel = harAddonColorChannelField.GetValue(addon) as string;
                }

                if (harAddonUseSkipFlagsField?.GetValue(addon) is IEnumerable skipFlags)
                {
                    foreach (object flagObj in skipFlags)
                    {
                        if (flagObj is RenderSkipFlagDef skipDef && skipDef != RenderSkipFlagDefOf.None && parms.skipFlags.HasFlag(skipDef))
                        {
                            return false;
                        }
                    }
                }

                if (!CanDrawSubWorkers(node, parms))
                {
                    return false;
                }

                if (harAddonConditionsField?.GetValue(addon) is IList conditions && conditions.Count > 0)
                {
                    harWrapperCache ??= harWrapperCtor.Invoke(new object[] { parms.pawn });
                    object resolveData = harAddonResolveDataField.GetValue(addon);
                    object[] satisfyArgs = new object[2];

                    for (int i = 0; i < conditions.Count; i++)
                    {
                        object condition = conditions[i];
                        if (condition == null)
                        {
                            continue;
                        }

                        if (harConditionApparelType != null && harConditionApparelType.IsInstanceOfType(condition))
                        {
                            continue;
                        }

                        if (harConditionPostureType != null && harConditionPostureType.IsInstanceOfType(condition))
                        {
                            bool drawnStanding = harPostureDrawnStandingField != null
                                && (bool)harPostureDrawnStandingField.GetValue(condition);
                            if (!drawnStanding)
                            {
                                return false;
                            }
                            continue;
                        }

                        satisfyArgs[0] = harWrapperCache;
                        satisfyArgs[1] = resolveData;
                        bool satisfied = (bool)harConditionSatisfiedMethod.Invoke(condition, satisfyArgs);
                        resolveData = satisfyArgs[1];
                        if (!satisfied)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return node.Worker.CanDrawNow(node, parms) && CanDrawSubWorkers(node, parms);
            }
        }

        private static void EnsureHarReflectionInitialized(Type harNodeType)
        {
            if (harReflectInitialized)
            {
                return;
            }
            harReflectInitialized = true;

            try
            {
                Assembly harAsm = harNodeType.Assembly;
                harNodePropsField = FindField(harNodeType, "props");
                if (harNodePropsField == null)
                {
                    return;
                }

                harPropsAddonField = FindField(harNodePropsField.FieldType, "addon");
                if (harPropsAddonField == null)
                {
                    return;
                }

                Type addonType = harPropsAddonField.FieldType;
                harAddonConditionsField = FindField(addonType, "conditions");
                harAddonResolveDataField = FindField(addonType, "resolveData");
                harAddonUseSkipFlagsField = FindField(addonType, "useSkipFlags");
                harAddonColorChannelProp = addonType.GetProperty("ColorChannel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                harAddonColorChannelField = FindField(addonType, "colorChannel");

                Type wrapperType = harAsm.GetType(HarWrapperFullName);
                harWrapperCtor = wrapperType?.GetConstructor(new[] { typeof(Pawn) });

                Type resolveDataType = harAddonResolveDataField?.FieldType;
                Type condElemType = harAddonConditionsField?.FieldType.IsGenericType == true
                    ? harAddonConditionsField.FieldType.GetGenericArguments()[0]
                    : null;

                if (condElemType != null && resolveDataType != null && wrapperType != null)
                {
                    harConditionSatisfiedMethod = condElemType.GetMethod(
                        "Satisfied",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { wrapperType, resolveDataType.MakeByRefType() },
                        null);
                }

                harConditionApparelType = harAsm.GetType(HarConditionApparelFullName);
                harConditionPostureType = harAsm.GetType(HarConditionPostureFullName);
                harPostureDrawnStandingField = harConditionPostureType != null
                    ? FindField(harConditionPostureType, "drawnStanding")
                    : null;

                harReflectAvailable = harAddonConditionsField != null
                    && harAddonResolveDataField != null
                    && harWrapperCtor != null
                    && harConditionSatisfiedMethod != null;
            }
            catch
            {
                harReflectAvailable = false;
            }
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static void DecomposeRelativeXZ(
            Matrix4x4 relMatrix,
            Matrix4x4 headMatrix,
            Matrix4x4 nodeMatrix,
            out Vector2 localOffsetXZ,
            out float localAngleY,
            out Vector2 localScale,
            out float relativeY)
        {
            localOffsetXZ = new Vector2(relMatrix.m03, relMatrix.m23);

            float sx = Mathf.Sqrt(relMatrix.m00 * relMatrix.m00 + relMatrix.m20 * relMatrix.m20);
            float sz = Mathf.Sqrt(relMatrix.m02 * relMatrix.m02 + relMatrix.m22 * relMatrix.m22);
            if (sx <= 1e-5f) sx = 1f;
            if (sz <= 1e-5f) sz = 1f;

            float detXZ = relMatrix.m00 * relMatrix.m22 - relMatrix.m02 * relMatrix.m20;
            if (detXZ < 0f)
            {
                sx = -sx;
            }

            localScale = new Vector2(sx, sz);
            localAngleY = Mathf.Atan2(-relMatrix.m20 / sx, relMatrix.m00 / sx) * Mathf.Rad2Deg;
            relativeY = nodeMatrix.m13 - headMatrix.m13;
        }

        private static float ExtractYRotation(Matrix4x4 m)
        {
            float sx = Mathf.Sqrt(m.m00 * m.m00 + m.m20 * m.m20);
            if (sx <= 1e-5f)
            {
                return 0f;
            }
            return Mathf.Atan2(-m.m20 / sx, m.m00 / sx) * Mathf.Rad2Deg;
        }

        private static void ExtractMeshSizeAndFlip(
            PawnRenderNode node,
            PawnDrawParms parms,
            Mesh mesh,
            out Vector2 size,
            out bool flipMesh)
        {
            try
            {
                MeshPool.MeshMetaData meta = MeshPool.GetMetaData(mesh);
                size = meta.size;
                flipMesh = meta.flipped;
            }
            catch (KeyNotFoundException)
            {
                Bounds bounds = mesh.bounds;
                size = new Vector2(
                    bounds.size.x > 1e-4f ? bounds.size.x : 1f,
                    bounds.size.z > 1e-4f ? bounds.size.z : 1f);
                flipMesh = node.FlipGraphic(parms);
            }
        }

        private static bool DetermineIsSkinLayer(PawnRenderNode node, Graphic graphic, bool isHarAddon, string harColorChannel)
        {
            string skullPath = HeadTypeDefOf.Skull?.graphicPath;
            if (!skullPath.NullOrEmpty() && graphic.path == skullPath)
            {
                return false;
            }

            if (node is PawnRenderNode_Head || node.Props.useSkinShader)
            {
                return true;
            }

            if (node.Props.colorType == PawnRenderNodeProperties.AttachmentColorType.Skin)
            {
                return true;
            }

            return isHarAddon && string.Equals(harColorChannel, "skin", StringComparison.OrdinalIgnoreCase);
        }

        private static void RefreshShaderMaps(bool force = false)
        {
            if (shaderMapsInitialized && !force)
            {
                return;
            }
            shaderMapsInitialized = true;

            if (shaderDatabaseLookupField?.GetValue(null) is Dictionary<string, Shader> dbLookup)
            {
                foreach (KeyValuePair<string, Shader> kvp in dbLookup)
                {
                    if (kvp.Value != null && !kvp.Key.NullOrEmpty())
                    {
                        shaderToPath[kvp.Value] = kvp.Key;
                    }
                }
            }

            List<ShaderTypeDef> defs = DefDatabase<ShaderTypeDef>.AllDefsListForReading;
            if (defs != null)
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    ShaderTypeDef def = defs[i];
                    if (def == null)
                    {
                        continue;
                    }

                    if (!def.shaderPath.NullOrEmpty() && !pathToDefName.ContainsKey(def.shaderPath))
                    {
                        pathToDefName[def.shaderPath] = def.defName;
                    }

                    if (shaderTypeIntField?.GetValue(def) is Shader loaded && loaded != null)
                    {
                        shaderToDefName[loaded] = def.defName;
                        if (!def.shaderPath.NullOrEmpty())
                        {
                            shaderToPath[loaded] = def.shaderPath;
                        }
                    }
                }
            }
        }

        private static void ResolveShaderInfo(Shader shader, out string shaderTypeDefName, out string shaderPath)
        {
            shaderTypeDefName = null;
            shaderPath = null;
            if (shader == null)
            {
                return;
            }

            RefreshShaderMaps();
            if (!shaderToPath.ContainsKey(shader) && !shaderToDefName.ContainsKey(shader))
            {
                RefreshShaderMaps(force: true);
            }

            shaderToPath.TryGetValue(shader, out shaderPath);
            if (!shaderToDefName.TryGetValue(shader, out shaderTypeDefName) && !shaderPath.NullOrEmpty())
            {
                pathToDefName.TryGetValue(shaderPath, out shaderTypeDefName);
            }
        }

        private static Shader ResolveShader(string shaderTypeDefName, string shaderPath)
        {
            if (!shaderTypeDefName.NullOrEmpty())
            {
                ShaderTypeDef def = DefDatabase<ShaderTypeDef>.GetNamedSilentFail(shaderTypeDefName);
                if (def != null)
                {
                    return def.Shader;
                }
            }

            if (!shaderPath.NullOrEmpty())
            {
                return ShaderDatabase.LoadShader(shaderPath);
            }

            return ShaderDatabase.Cutout;
        }

        private static bool TextureExists(string texPath, bool isMulti)
        {
            if (texPath.NullOrEmpty())
            {
                return false;
            }

            if (!isMulti)
            {
                return ContentFinder<Texture2D>.Get(texPath, reportFailure: false) != null;
            }

            return ContentFinder<Texture2D>.Get(texPath + Graphic_Multi.SouthSuffix, reportFailure: false) != null
                || ContentFinder<Texture2D>.Get(texPath + Graphic_Multi.NorthSuffix, reportFailure: false) != null
                || ContentFinder<Texture2D>.Get(texPath + Graphic_Multi.EastSuffix, reportFailure: false) != null
                || ContentFinder<Texture2D>.Get(texPath + Graphic_Multi.WestSuffix, reportFailure: false) != null
                || ContentFinder<Texture2D>.Get(texPath, reportFailure: false) != null;
        }

        private struct SortableCompositeLayer
        {
            public float relativeY;
            public int captureOrder;
            public Material material;
            public Mesh mesh;
            public Matrix4x4 localXZMatrix;
        }

        public static List<ResolvedTrophyLayer> BuildCompositeLayers(HangingHeadRecord record)
        {
            var result = new List<ResolvedTrophyLayer>();
            if (record?.capturedLayers == null || record.capturedLayers.Count == 0)
            {
                return result;
            }

            var pending = new List<SortableCompositeLayer>(record.capturedLayers.Count + 1);
            for (int i = 0; i < record.capturedLayers.Count; i++)
            {
                HangingHeadLayerRecord layer = record.capturedLayers[i];
                if (layer == null || !TextureExists(layer.texPath, layer.isMulti))
                {
                    continue;
                }

                Shader shader = ResolveShader(layer.shaderTypeDefName, layer.shaderPath);
                Color finalColor = layer.isSkinLayer
                    ? Color.Lerp(layer.color, DeadSkinTargetTint, 0.28f)
                    : layer.color;

                Graphic graphic = layer.isMulti
                    ? GraphicDatabase.Get<Graphic_Multi>(layer.texPath, shader, Vector2.one, finalColor, layer.colorTwo, null, layer.maskPath)
                    : GraphicDatabase.Get<Graphic_Single>(layer.texPath, shader, Vector2.one, finalColor, layer.colorTwo, null, layer.maskPath);

                Material mat = layer.isMulti ? graphic?.MatSouth : graphic?.MatSingle;
                if (mat == null || mat == BaseContent.BadMat)
                {
                    continue;
                }

                Vector2 validMeshSize = new Vector2(
                    Mathf.Abs(layer.meshSize.x) > 1e-4f ? layer.meshSize.x : 1f,
                    Mathf.Abs(layer.meshSize.y) > 1e-4f ? layer.meshSize.y : 1f);
                Mesh mesh = MeshPool.GridPlane(validMeshSize, layer.flipMesh);

                Vector3 validScale = new Vector3(
                    Mathf.Abs(layer.localScale.x) > 1e-5f ? layer.localScale.x : 1f,
                    1f,
                    Mathf.Abs(layer.localScale.y) > 1e-5f ? layer.localScale.y : 1f);

                Matrix4x4 localXZ = Matrix4x4.TRS(
                    new Vector3(layer.localOffsetXZ.x, 0f, layer.localOffsetXZ.y),
                    Quaternion.AngleAxis(layer.localAngleY, Vector3.up),
                    validScale);

                pending.Add(new SortableCompositeLayer
                {
                    relativeY = layer.relativeY,
                    captureOrder = layer.captureOrder,
                    material = mat,
                    mesh = mesh,
                    localXZMatrix = localXZ
                });
            }

            if (pending.Count == 0)
            {
                return result;
            }

            Graphic bloodGraphic = GraphicDatabase.Get<Graphic_Single>(NeckBloodTexPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
            Material bloodMat = bloodGraphic?.MatSingle;
            if (bloodMat != null && bloodMat != BaseContent.BadMat)
            {
                pending.Add(new SortableCompositeLayer
                {
                    relativeY = NeckBloodSyntheticRelativeY,
                    captureOrder = int.MaxValue / 2,
                    material = bloodMat,
                    mesh = MeshPool.GridPlane(Vector2.one, false),
                    localXZMatrix = Matrix4x4.TRS(new Vector3(0f, 0f, -0.24f), Quaternion.identity, new Vector3(0.62f, 1f, 0.62f))
                });
            }

            pending.Sort((a, b) =>
            {
                int cmp = a.relativeY.CompareTo(b.relativeY);
                return cmp != 0 ? cmp : a.captureOrder.CompareTo(b.captureOrder);
            });

            int count = pending.Count;
            for (int i = 0; i < count; i++)
            {
                float subLayerOffset = count <= 1 ? 0f : (float)i / (count - 1) * CompositeLayerSpan;
                result.Add(new ResolvedTrophyLayer
                {
                    material = pending[i].material,
                    mesh = pending[i].mesh,
                    localXZMatrix = pending[i].localXZMatrix,
                    subAltitude = PawnRenderUtility.AltitudeForLayer(subLayerOffset)
                });
            }

            return result;
        }
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
            if (rec == null || rec.HasUsableCapturedLayers)
                return null;

            switch (Props.layerKind)
            {
                case BurrowingHeavenTrophyLayerKind.Head:
                {
                    HeadTypeDef ht = rec.headTypeDef ?? HeadTypeDefOf.Skull;
                    if (ht.graphicPath.NullOrEmpty())
                        return null;

                    Color deadSkin = Color.Lerp(rec.skinColor, TrophyHeadRenderHelper.DeadSkinTargetTint, 0.28f);
                    Shader shader = ht == HeadTypeDefOf.Skull ? ShaderDatabase.Cutout : ShaderDatabase.CutoutSkin;
                    return GraphicDatabase.Get<Graphic_Multi>(ht.graphicPath, shader, Vector2.one, deadSkin);
                }
                case BurrowingHeavenTrophyLayerKind.NeckBlood:
                    return GraphicDatabase.Get<Graphic_Single>(TrophyHeadRenderHelper.NeckBloodTexPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
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

            HangingHeadRecord rec = trophyNode.GetRecord(parms.pawn);
            if (rec == null)
                return false;

            if (rec.HasUsableCapturedLayers)
                return trophyNode.Props.layerKind == BurrowingHeavenTrophyLayerKind.Head;

            return trophyNode.PrimaryGraphic != null;
        }

        public override void AppendDrawRequests(PawnRenderNode node, PawnDrawParms parms, List<PawnGraphicDrawRequest> requests)
        {
            if (node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode
                && trophyNode.Props.layerKind == BurrowingHeavenTrophyLayerKind.Head)
            {
                HangingHeadRecord rec = trophyNode.GetRecord(parms.pawn);
                if (rec != null && rec.HasUsableCapturedLayers)
                {
                    requests.Add(new PawnGraphicDrawRequest(node));
                    return;
                }
            }

            base.AppendDrawRequests(node, parms, requests);
        }

        public override void PostDraw(PawnRenderNode node, PawnDrawParms parms, Mesh mesh, Matrix4x4 matrix)
        {
            base.PostDraw(node, parms, mesh, matrix);

            if (!(node is PawnRenderNode_BurrowingHeavenTrophyHead trophyNode)
                || trophyNode.Props.layerKind != BurrowingHeavenTrophyLayerKind.Head)
                return;

            HangingHeadRecord rec = trophyNode.GetRecord(parms.pawn);
            if (rec == null || !rec.HasUsableCapturedLayers)
                return;

            List<ResolvedTrophyLayer> layers = rec.GetCompositeLayers();
            if (layers == null || layers.Count == 0)
                return;

            bool drawInvisible = !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible);
            MaterialPropertyBlock block = node.MatPropBlock;

            for (int i = 0; i < layers.Count; i++)
            {
                ResolvedTrophyLayer layer = layers[i];
                if (layer.material == null || layer.mesh == null)
                    continue;

                Material mat = drawInvisible ? InvisibilityMatPool.GetInvisibleMat(layer.material) : layer.material;
                if (mat == null)
                    continue;

                Matrix4x4 layerMatrix = matrix * layer.localXZMatrix;
                layerMatrix.m13 += layer.subAltitude;

                Color tintColor = parms.Statue && parms.statueColor.HasValue
                    ? parms.statueColor.Value
                    : parms.tint * mat.color;

                if (block != null)
                {
                    block.Clear();
                    block.SetColor(ShaderPropertyIDs.Color, tintColor);
                    GenDraw.DrawMeshNowOrLater(layer.mesh, layerMatrix, mat, parms.DrawNow, block);
                    block.Clear();
                }
                else
                {
                    GenDraw.DrawMeshNowOrLater(layer.mesh, layerMatrix, mat, parms.DrawNow);
                }
            }
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
