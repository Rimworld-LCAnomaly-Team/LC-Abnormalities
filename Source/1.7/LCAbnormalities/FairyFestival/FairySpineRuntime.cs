using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using LCAbnormalities.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    public enum FairySpineAnimKind : byte
    {
        Default = 0,
        GiveFairy = 1
    }

    /// <summary>
    /// 精灵盛宴（F-04-83）轻量级 Spine 3.6 实时骨骼蒙皮运行时与四批次零 GC 网格渲染器：
    /// 直接解析解包导出的 Fairy.json + Fairy.atlas.txt 与 PMA 原图（Fairy.png / Fairy2.png），
    /// 严格复刻 97 根骨骼、12 组加权蒙皮网格、4 片羽翼纵横拉伸、6 枚呼吸光球及 3 层叠加微光。
    /// </summary>
    public static class FairySpineAssets
    {
        private static readonly object LoadLock = new object();
        private static FairySpineData cachedData;
        private static bool loadAttempted;

        private static Material batch0PmaFairy1Mat;
        private static Material batch1PmaFairy2Mat;
        private static Material batch2PmaFairy1Mat;
        private static Material batch3AddFairy1Mat;
        private static bool materialsInitialized;

        private static FairySpineBatchRenderer portraitRenderer;

        public static FairySpineData Data
        {
            get
            {
                if (cachedData != null || loadAttempted)
                {
                    return cachedData;
                }

                lock (LoadLock)
                {
                    if (cachedData == null && !loadAttempted)
                    {
                        loadAttempted = true;
                        try
                        {
                            string jsonPath = ResolveContentFile("Spine/FairyFestival/Fairy.json");
                            string atlasPath = ResolveContentFile("Spine/FairyFestival/Fairy.atlas.txt");
                            if (!string.IsNullOrEmpty(jsonPath) && !string.IsNullOrEmpty(atlasPath))
                            {
                                FairySpineAtlas atlas = FairySpineAtlas.Load(atlasPath);
                                cachedData = FairySpineData.Load(jsonPath, atlas);
                            }
                            else
                            {
                                Log.Error("[LCAbnormalities] FairyFestival Spine assets missing (Spine/FairyFestival/Fairy.json or Fairy.atlas.txt).");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[LCAbnormalities] Failed to load FairyFestival Spine data: {ex}");
                        }
                    }
                }

                return cachedData;
            }
        }

        public static bool EnsureMaterialsInitialized()
        {
            if (materialsInitialized)
            {
                return batch0PmaFairy1Mat != null
                    && batch1PmaFairy2Mat != null
                    && batch2PmaFairy1Mat != null
                    && batch3AddFairy1Mat != null;
            }

            if (!UnityData.IsInMainThread)
            {
                return false;
            }

            materialsInitialized = true;

            Shader pmaShader = ShaderDatabase.LoadShader("Map/LC_FairySpinePMA") ?? ShaderDatabase.Transparent;
            Shader addShader = ShaderDatabase.LoadShader("Map/LC_FairySpineAdditive") ?? ShaderDatabase.MoteGlow;

            Texture2D fairy1Tex = ContentFinder<Texture2D>.Get("Things/Pawn/FairyFestival/Spine/Fairy", reportFailure: false);
            Texture2D fairy2Tex = ContentFinder<Texture2D>.Get("Things/Pawn/FairyFestival/Spine/Fairy2", reportFailure: false);

            if (fairy1Tex == null || fairy2Tex == null)
            {
                Log.Error("[LCAbnormalities] Failed to find FairyFestival Spine textures (Things/Pawn/FairyFestival/Spine/Fairy or Fairy2).");
                return false;
            }

            batch0PmaFairy1Mat = MaterialPool.MatFrom(new MaterialRequest(fairy1Tex, pmaShader, Color.white));
            batch1PmaFairy2Mat = MaterialPool.MatFrom(new MaterialRequest(fairy2Tex, pmaShader, Color.white));
            batch2PmaFairy1Mat = batch0PmaFairy1Mat;
            batch3AddFairy1Mat = MaterialPool.MatFrom(new MaterialRequest(fairy1Tex, addShader, Color.white));

            return batch0PmaFairy1Mat != null
                && batch1PmaFairy2Mat != null
                && batch2PmaFairy1Mat != null
                && batch3AddFairy1Mat != null;
        }

        public static Material GetBatchMaterial(int batchIndex)
        {
            switch (batchIndex)
            {
                case 0: return batch0PmaFairy1Mat;
                case 1: return batch1PmaFairy2Mat;
                case 2: return batch2PmaFairy1Mat;
                case 3: return batch3AddFairy1Mat;
                default: return null;
            }
        }

        public static FairySpineBatchRenderer GetPortraitRenderer()
        {
            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            FairySpineData data = Data;
            if (data == null || !EnsureMaterialsInitialized())
            {
                return null;
            }

            if (portraitRenderer == null)
            {
                portraitRenderer = new FairySpineBatchRenderer(data);
                // Representative full-wingspan hover pose in Default (t = 0.267s)
                portraitRenderer.EvaluateAndUpload(
                    FairySpineAnimKind.Default,
                    0.267f,
                    feastMode: false,
                    tint: Color.white,
                    includeAdditive: true,
                    frameStamp: -1);
            }

            return portraitRenderer;
        }

        private static string ResolveContentFile(string subPath)
        {
            string normalized = subPath.Replace('/', Path.DirectorySeparatorChar);
            string[] prefixes = { "Cont", "1.7", "" };

            string modRoot = LCAbnormalitiesMod.ModRootDir;
            if (!string.IsNullOrEmpty(modRoot))
            {
                for (int i = 0; i < prefixes.Length; i++)
                {
                    string candidate = string.IsNullOrEmpty(prefixes[i])
                        ? Path.Combine(modRoot, normalized)
                        : Path.Combine(modRoot, prefixes[i], normalized);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
            if (mods != null)
            {
                for (int m = 0; m < mods.Count; m++)
                {
                    ModContentPack mod = mods[m];
                    if (mod == null || !string.Equals(mod.PackageId, "darthcy.lc.abnormalities", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    for (int i = 0; i < prefixes.Length; i++)
                    {
                        string candidate = string.IsNullOrEmpty(prefixes[i])
                            ? Path.Combine(mod.RootDir, normalized)
                            : Path.Combine(mod.RootDir, prefixes[i], normalized);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return null;
        }
    }

    public sealed class FairySpineAtlasRegion
    {
        public int PageIndex;
        public string Name;
        public bool Rotated;
        public float U;
        public float V;
        public float U2;
        public float V2;

        public Vector2 MapUV(float ru, float rv)
        {
            if (Rotated)
            {
                return new Vector2(U + rv * (U2 - U), V2 - ru * (V2 - V));
            }

            return new Vector2(U + ru * (U2 - U), V + rv * (V2 - V));
        }
    }

    public sealed class FairySpineAtlas
    {
        public readonly Dictionary<string, FairySpineAtlasRegion> Regions = new Dictionary<string, FairySpineAtlasRegion>(StringComparer.Ordinal);

        public static FairySpineAtlas Load(string path)
        {
            FairySpineAtlas atlas = new FairySpineAtlas();
            string[] lines = File.ReadAllLines(path);

            int pageIndex = -1;
            float pageW = 1024f;
            float pageH = 1024f;
            bool expectingPage = true;
            FairySpineAtlasRegion currentRegion = null;
            int rx = 0, ry = 0, rw = 1, rh = 1;

            void FlushRegion()
            {
                if (currentRegion == null)
                {
                    return;
                }

                currentRegion.U = rx / pageW;
                currentRegion.V = ry / pageH;
                if (currentRegion.Rotated)
                {
                    currentRegion.U2 = (rx + rh) / pageW;
                    currentRegion.V2 = (ry + rw) / pageH;
                }
                else
                {
                    currentRegion.U2 = (rx + rw) / pageW;
                    currentRegion.V2 = (ry + rh) / pageH;
                }

                atlas.Regions[currentRegion.Name] = currentRegion;
                currentRegion = null;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();
                if (trimmed.Length == 0)
                {
                    FlushRegion();
                    expectingPage = true;
                    continue;
                }

                bool indented = char.IsWhiteSpace(raw[0]);
                int colon = trimmed.IndexOf(':');
                if (!indented && colon < 0)
                {
                    if (expectingPage)
                    {
                        FlushRegion();
                        pageIndex++;
                        pageW = 1024f;
                        pageH = 1024f;
                        expectingPage = false;
                    }
                    else
                    {
                        FlushRegion();
                        currentRegion = new FairySpineAtlasRegion
                        {
                            PageIndex = pageIndex,
                            Name = trimmed
                        };
                        rx = 0;
                        ry = 0;
                        rw = 1;
                        rh = 1;
                    }
                    continue;
                }

                if (colon <= 0)
                {
                    continue;
                }

                string key = trimmed.Substring(0, colon).Trim();
                string val = trimmed.Substring(colon + 1).Trim();

                if (currentRegion == null)
                {
                    if (key == "size")
                    {
                        ParsePair(val, out int pw, out int ph);
                        pageW = Mathf.Max(1, pw);
                        pageH = Mathf.Max(1, ph);
                    }
                }
                else
                {
                    switch (key)
                    {
                        case "rotate":
                            currentRegion.Rotated = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "xy":
                            ParsePair(val, out rx, out ry);
                            break;
                        case "size":
                            ParsePair(val, out rw, out rh);
                            break;
                    }
                }
            }

            FlushRegion();
            return atlas;
        }

        private static void ParsePair(string val, out int a, out int b)
        {
            int comma = val.IndexOf(',');
            if (comma < 0)
            {
                a = 0;
                b = 0;
                return;
            }

            int.TryParse(val.Substring(0, comma).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out a);
            int.TryParse(val.Substring(comma + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b);
        }
    }

    public struct SpineBoneSetup
    {
        public string Name;
        public int ParentIndex;
        public float X;
        public float Y;
        public float Rotation;
        public float ScaleX;
        public float ScaleY;
        public float ShearX;
        public float ShearY;
    }

    public struct SpineBoneState
    {
        public float X;
        public float Y;
        public float Rotation;
        public float ScaleX;
        public float ScaleY;
        public float ShearX;
        public float ShearY;
        // 2x3 world affine matrix
        public float A;
        public float B;
        public float C;
        public float D;
        public float WX;
        public float WY;
    }

    public struct SpineMeshWeight
    {
        public int BoneIndex;
        public float X;
        public float Y;
        public float Weight;
    }

    public struct SpineVertexSlice
    {
        public int Start;
        public int Count;
    }

    public sealed class SpineAttachmentData
    {
        public string Name;
        public bool IsMesh;
        public int PageIndex;
        public Color Color;
        public int VertexCount;
        public Vector2[] LocalVerts; // Only for Region (4 verts)
        public Vector2[] UVs;
        public int[] Triangles;
        public SpineVertexSlice[] WeightSlices; // Only for Mesh
        public SpineMeshWeight[] Weights;       // Only for Mesh
    }

    public sealed class SpineSlotSetup
    {
        public int Index;
        public string Name;
        public int BoneIndex;
        public string SetupAttachmentName;
        public Color SetupColor;
        public bool IsAdditive;
        public int BatchIndex;
        public int VertexOffset;
        public int VertexCount;
        public SpineAttachmentData Attachment;
    }

    public struct SpineFloatKeyframe
    {
        public float Time;
        public float V0;
        public float V1;
        public bool Stepped;
    }

    public struct SpineColorKeyframe
    {
        public float Time;
        public Color Color;
        public bool Stepped;
    }

    public struct SpineAttachmentKeyframe
    {
        public float Time;
        public string Name;
    }

    public sealed class SpineBoneTrack
    {
        public int BoneIndex;
        public SpineFloatKeyframe[] Rotate;
        public SpineFloatKeyframe[] Translate;
        public SpineFloatKeyframe[] Scale;
        public SpineFloatKeyframe[] Shear;
    }

    public sealed class SpineSlotTrack
    {
        public int SlotIndex;
        public SpineAttachmentKeyframe[] Attachment;
        public SpineColorKeyframe[] Color;
    }

    public sealed class SpineAnimationClip
    {
        public string Name;
        public float Duration;
        public SpineBoneTrack[] BoneTracks;
        public SpineSlotTrack[] SlotTracks;
    }

    public sealed class FairySpineData
    {
        public const float CenterX = 0.4f;
        public const float CenterY = 275.3f;
        public const float PixelsToWorld = 0.00268f;
        public const float SlotDepthStep = 0.00025f;
        public const int EyeSlotIndex = 15;
        public const int Mouth1SlotIndex = 16;
        public const int Mouth2SlotIndex = 17;
        public const int FirstAdditiveSlotIndex = 21;

        public SpineBoneSetup[] Bones;
        public SpineSlotSetup[] Slots;
        public SpineAnimationClip DefaultAnim;
        public SpineAnimationClip GiveFairyAnim;

        public readonly int[] BatchVertexCounts = new int[4];
        public readonly List<int>[] BatchTriangleLists = new List<int>[4];
        public readonly Vector2[][] BatchUVs = new Vector2[4][];
        public readonly int[][] BatchTriangles = new int[4][];

        public int EyeGlowVertexOffset;

        public static FairySpineData Load(string jsonPath, FairySpineAtlas atlas)
        {
            using FileStream stream = File.OpenRead(jsonPath);
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;

            FairySpineData data = new FairySpineData();
            for (int b = 0; b < 4; b++)
            {
                data.BatchTriangleLists[b] = new List<int>();
            }

            // 1. Bones
            JsonElement bonesEl = root.GetProperty("bones");
            int boneCount = bonesEl.GetArrayLength();
            data.Bones = new SpineBoneSetup[boneCount];
            Dictionary<string, int> boneMap = new Dictionary<string, int>(boneCount, StringComparer.Ordinal);
            for (int i = 0; i < boneCount; i++)
            {
                JsonElement be = bonesEl[i];
                string bName = be.GetProperty("name").GetString();
                string pName = be.TryGetProperty("parent", out JsonElement pEl) ? pEl.GetString() : null;
                int parentIdx = (!string.IsNullOrEmpty(pName) && boneMap.TryGetValue(pName, out int p)) ? p : -1;

                data.Bones[i] = new SpineBoneSetup
                {
                    Name = bName,
                    ParentIndex = parentIdx,
                    X = GetFloat(be, "x", 0f),
                    Y = GetFloat(be, "y", 0f),
                    Rotation = GetFloat(be, "rotation", 0f),
                    ScaleX = GetFloat(be, "scaleX", 1f),
                    ScaleY = GetFloat(be, "scaleY", 1f),
                    ShearX = GetFloat(be, "shearX", 0f),
                    ShearY = GetFloat(be, "shearY", 0f)
                };
                boneMap[bName] = i;
            }

            // 2. Slots + Skin Attachments
            JsonElement defaultSkin = root.GetProperty("skins").GetProperty("default");
            JsonElement slotsEl = root.GetProperty("slots");
            int slotCount = slotsEl.GetArrayLength();
            data.Slots = new SpineSlotSetup[slotCount];
            Dictionary<string, int> slotMap = new Dictionary<string, int>(slotCount, StringComparer.Ordinal);

            for (int i = 0; i < slotCount; i++)
            {
                JsonElement se = slotsEl[i];
                string sName = se.GetProperty("name").GetString();
                string bName = se.GetProperty("bone").GetString();
                string setupAtt = se.TryGetProperty("attachment", out JsonElement attProp) && attProp.ValueKind == JsonValueKind.String
                    ? attProp.GetString()
                    : null;
                Color setupCol = se.TryGetProperty("color", out JsonElement colProp)
                    ? ParseHexColor(colProp.GetString())
                    : Color.white;
                bool isAdditive = se.TryGetProperty("blend", out JsonElement blendProp)
                    && string.Equals(blendProp.GetString(), "additive", StringComparison.OrdinalIgnoreCase);

                SpineAttachmentData attData = null;
                if (defaultSkin.TryGetProperty(sName, out JsonElement skinSlotEl))
                {
                    foreach (JsonProperty attEntry in skinSlotEl.EnumerateObject())
                    {
                        attData = ParseAttachment(attEntry.Name, attEntry.Value, boneMap[bName], boneMap, atlas);
                        break;
                    }
                }

                int batchIdx;
                if (isAdditive)
                {
                    batchIdx = 3;
                }
                else if (attData != null && attData.PageIndex == 1)
                {
                    batchIdx = 1;
                }
                else if (i <= 17)
                {
                    batchIdx = 0;
                }
                else
                {
                    batchIdx = 2;
                }

                int vOffset = data.BatchVertexCounts[batchIdx];
                int vCount = attData?.VertexCount ?? 0;
                data.BatchVertexCounts[batchIdx] += vCount;

                if (attData != null)
                {
                    List<int> triList = data.BatchTriangleLists[batchIdx];
                    int[] tris = attData.Triangles;
                    for (int t = 0; t < tris.Length; t++)
                    {
                        triList.Add(vOffset + tris[t]);
                    }
                }

                data.Slots[i] = new SpineSlotSetup
                {
                    Index = i,
                    Name = sName,
                    BoneIndex = boneMap[bName],
                    SetupAttachmentName = setupAtt,
                    SetupColor = setupCol,
                    IsAdditive = isAdditive,
                    BatchIndex = batchIdx,
                    VertexOffset = vOffset,
                    VertexCount = vCount,
                    Attachment = attData
                };
                slotMap[sName] = i;
            }

            // Reserve 4 extra vertices at end of Batch 3 for additive Eye glow during Give_fairy / Feast
            SpineAttachmentData eyeAtt = data.Slots[EyeSlotIndex].Attachment;
            data.EyeGlowVertexOffset = data.BatchVertexCounts[3];
            if (eyeAtt != null && eyeAtt.VertexCount == 4)
            {
                data.BatchVertexCounts[3] += 4;
                List<int> addTris = data.BatchTriangleLists[3];
                for (int t = 0; t < eyeAtt.Triangles.Length; t++)
                {
                    addTris.Add(data.EyeGlowVertexOffset + eyeAtt.Triangles[t]);
                }
            }

            // Bake fixed UVs and triangles per batch
            for (int b = 0; b < 4; b++)
            {
                data.BatchUVs[b] = new Vector2[data.BatchVertexCounts[b]];
                data.BatchTriangles[b] = data.BatchTriangleLists[b].ToArray();
            }

            for (int i = 0; i < slotCount; i++)
            {
                SpineSlotSetup s = data.Slots[i];
                if (s.Attachment == null)
                {
                    continue;
                }

                Vector2[] dst = data.BatchUVs[s.BatchIndex];
                Array.Copy(s.Attachment.UVs, 0, dst, s.VertexOffset, s.VertexCount);
            }

            if (eyeAtt != null && eyeAtt.VertexCount == 4)
            {
                Array.Copy(eyeAtt.UVs, 0, data.BatchUVs[3], data.EyeGlowVertexOffset, 4);
            }

            // 3. Animations
            JsonElement animsEl = root.GetProperty("animations");
            if (animsEl.TryGetProperty("Default", out JsonElement defAnimEl))
            {
                data.DefaultAnim = ParseAnimation("Default", defAnimEl, boneMap, slotMap);
            }
            if (animsEl.TryGetProperty("Give_fairy", out JsonElement giveAnimEl))
            {
                data.GiveFairyAnim = ParseAnimation("Give_fairy", giveAnimEl, boneMap, slotMap);
            }

            return data;
        }

        private static SpineAttachmentData ParseAttachment(
            string keyName,
            JsonElement el,
            int defaultBoneIndex,
            Dictionary<string, int> boneMap,
            FairySpineAtlas atlas)
        {
            string regionName = GetString(el, "path") ?? GetString(el, "name") ?? keyName;
            if (!atlas.Regions.TryGetValue(regionName, out FairySpineAtlasRegion region))
            {
                Log.Warning($"[LCAbnormalities] Fairy Spine atlas region not found: {regionName}");
                return null;
            }

            Color attColor = el.TryGetProperty("color", out JsonElement colEl)
                ? ParseHexColor(colEl.GetString())
                : Color.white;
            string typeStr = GetString(el, "type") ?? "region";

            if (typeStr == "mesh")
            {
                float[] uvsRaw = ReadFloatArray(el.GetProperty("uvs"));
                int[] trisRaw = ReadIntArray(el.GetProperty("triangles"));
                float[] vertsRaw = ReadFloatArray(el.GetProperty("vertices"));

                int vCount = uvsRaw.Length / 2;
                Vector2[] mappedUvs = new Vector2[vCount];
                for (int k = 0; k < vCount; k++)
                {
                    mappedUvs[k] = region.MapUV(uvsRaw[k * 2], uvsRaw[k * 2 + 1]);
                }

                // Reverse winding so +Y faces camera in XZ plane
                int[] tris = new int[trisRaw.Length];
                for (int t = 0; t < trisRaw.Length; t += 3)
                {
                    tris[t] = trisRaw[t];
                    tris[t + 1] = trisRaw[t + 2];
                    tris[t + 2] = trisRaw[t + 1];
                }

                SpineVertexSlice[] slices = new SpineVertexSlice[vCount];
                List<SpineMeshWeight> weights = new List<SpineMeshWeight>(vCount * 2);

                if (vertsRaw.Length == uvsRaw.Length)
                {
                    for (int k = 0; k < vCount; k++)
                    {
                        slices[k] = new SpineVertexSlice { Start = k, Count = 1 };
                        weights.Add(new SpineMeshWeight
                        {
                            BoneIndex = defaultBoneIndex,
                            X = vertsRaw[k * 2],
                            Y = vertsRaw[k * 2 + 1],
                            Weight = 1f
                        });
                    }
                }
                else
                {
                    int idx = 0;
                    for (int k = 0; k < vCount; k++)
                    {
                        int n = Mathf.RoundToInt(vertsRaw[idx++]);
                        slices[k] = new SpineVertexSlice { Start = weights.Count, Count = n };
                        for (int w = 0; w < n; w++)
                        {
                            int bIdx = Mathf.RoundToInt(vertsRaw[idx++]);
                            float vx = vertsRaw[idx++];
                            float vy = vertsRaw[idx++];
                            float weight = vertsRaw[idx++];
                            weights.Add(new SpineMeshWeight
                            {
                                BoneIndex = bIdx,
                                X = vx,
                                Y = vy,
                                Weight = weight
                            });
                        }
                    }
                }

                return new SpineAttachmentData
                {
                    Name = keyName,
                    IsMesh = true,
                    PageIndex = region.PageIndex,
                    Color = attColor,
                    VertexCount = vCount,
                    UVs = mappedUvs,
                    Triangles = tris,
                    WeightSlices = slices,
                    Weights = weights.ToArray()
                };
            }
            else
            {
                float ax = GetFloat(el, "x", 0f);
                float ay = GetFloat(el, "y", 0f);
                float rot = GetFloat(el, "rotation", 0f) * Mathf.Deg2Rad;
                float sx = GetFloat(el, "scaleX", 1f);
                float sy = GetFloat(el, "scaleY", 1f);
                float hw = GetFloat(el, "width", 1f) * 0.5f * sx;
                float hh = GetFloat(el, "height", 1f) * 0.5f * sy;

                float cosR = Mathf.Cos(rot);
                float sinR = Mathf.Sin(rot);

                Vector2[] localVerts = new Vector2[4];
                localVerts[0] = new Vector2(ax + (-hw * cosR - (-hh) * sinR), ay + (-hw * sinR + (-hh) * cosR)); // BL
                localVerts[1] = new Vector2(ax + (-hw * cosR - hh * sinR), ay + (-hw * sinR + hh * cosR));       // TL
                localVerts[2] = new Vector2(ax + (hw * cosR - hh * sinR), ay + (hw * sinR + hh * cosR));         // TR
                localVerts[3] = new Vector2(ax + (hw * cosR - (-hh) * sinR), ay + (hw * sinR + (-hh) * cosR));   // BR

                Vector2[] uvs = new Vector2[4];
                uvs[0] = region.MapUV(0f, 1f); // BL
                uvs[1] = region.MapUV(0f, 0f); // TL
                uvs[2] = region.MapUV(1f, 0f); // TR
                uvs[3] = region.MapUV(1f, 1f); // BR

                return new SpineAttachmentData
                {
                    Name = keyName,
                    IsMesh = false,
                    PageIndex = region.PageIndex,
                    Color = attColor,
                    VertexCount = 4,
                    LocalVerts = localVerts,
                    UVs = uvs,
                    Triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };
            }
        }

        private static SpineAnimationClip ParseAnimation(
            string name,
            JsonElement animEl,
            Dictionary<string, int> boneMap,
            Dictionary<string, int> slotMap)
        {
            float maxTime = 0f;
            List<SpineBoneTrack> boneTracks = new List<SpineBoneTrack>();
            List<SpineSlotTrack> slotTracks = new List<SpineSlotTrack>();

            if (animEl.TryGetProperty("bones", out JsonElement bonesEl))
            {
                foreach (JsonProperty bProp in bonesEl.EnumerateObject())
                {
                    if (!boneMap.TryGetValue(bProp.Name, out int boneIdx))
                    {
                        continue;
                    }

                    SpineBoneTrack tr = new SpineBoneTrack { BoneIndex = boneIdx };
                    JsonElement bObj = bProp.Value;

                    if (bObj.TryGetProperty("rotate", out JsonElement rotEl))
                    {
                        tr.Rotate = ParseFloatTrack(rotEl, "angle", null, 0f, ref maxTime);
                    }
                    if (bObj.TryGetProperty("translate", out JsonElement trEl))
                    {
                        tr.Translate = ParseFloatTrack(trEl, "x", "y", 0f, ref maxTime);
                    }
                    if (bObj.TryGetProperty("scale", out JsonElement scEl))
                    {
                        tr.Scale = ParseFloatTrack(scEl, "x", "y", 1f, ref maxTime);
                    }
                    if (bObj.TryGetProperty("shear", out JsonElement shEl))
                    {
                        tr.Shear = ParseFloatTrack(shEl, "x", "y", 0f, ref maxTime);
                    }

                    boneTracks.Add(tr);
                }
            }

            if (animEl.TryGetProperty("slots", out JsonElement slotsEl))
            {
                foreach (JsonProperty sProp in slotsEl.EnumerateObject())
                {
                    if (!slotMap.TryGetValue(sProp.Name, out int slotIdx))
                    {
                        continue;
                    }

                    SpineSlotTrack tr = new SpineSlotTrack { SlotIndex = slotIdx };
                    JsonElement sObj = sProp.Value;

                    if (sObj.TryGetProperty("attachment", out JsonElement attEl))
                    {
                        int len = attEl.GetArrayLength();
                        tr.Attachment = new SpineAttachmentKeyframe[len];
                        for (int k = 0; k < len; k++)
                        {
                            JsonElement kf = attEl[k];
                            float t = GetFloat(kf, "time", 0f);
                            if (t > maxTime) maxTime = t;
                            string attName = kf.TryGetProperty("name", out JsonElement nEl) && nEl.ValueKind == JsonValueKind.String
                                ? nEl.GetString()
                                : null;
                            tr.Attachment[k] = new SpineAttachmentKeyframe { Time = t, Name = attName };
                        }
                    }

                    if (sObj.TryGetProperty("color", out JsonElement colEl))
                    {
                        int len = colEl.GetArrayLength();
                        tr.Color = new SpineColorKeyframe[len];
                        for (int k = 0; k < len; k++)
                        {
                            JsonElement kf = colEl[k];
                            float t = GetFloat(kf, "time", 0f);
                            if (t > maxTime) maxTime = t;
                            Color c = ParseHexColor(GetString(kf, "color"));
                            bool stepped = kf.TryGetProperty("curve", out JsonElement cEl)
                                && cEl.ValueKind == JsonValueKind.String
                                && cEl.GetString() == "stepped";
                            tr.Color[k] = new SpineColorKeyframe { Time = t, Color = c, Stepped = stepped };
                        }
                    }

                    slotTracks.Add(tr);
                }
            }

            return new SpineAnimationClip
            {
                Name = name,
                Duration = Mathf.Max(0.01f, maxTime),
                BoneTracks = boneTracks.ToArray(),
                SlotTracks = slotTracks.ToArray()
            };
        }

        private static SpineFloatKeyframe[] ParseFloatTrack(
            JsonElement arrEl,
            string prop0,
            string prop1,
            float defaultVal,
            ref float maxTime)
        {
            int len = arrEl.GetArrayLength();
            SpineFloatKeyframe[] arr = new SpineFloatKeyframe[len];
            for (int k = 0; k < len; k++)
            {
                JsonElement kf = arrEl[k];
                float t = GetFloat(kf, "time", 0f);
                if (t > maxTime) maxTime = t;
                bool stepped = kf.TryGetProperty("curve", out JsonElement cEl)
                    && cEl.ValueKind == JsonValueKind.String
                    && cEl.GetString() == "stepped";
                arr[k] = new SpineFloatKeyframe
                {
                    Time = t,
                    V0 = GetFloat(kf, prop0, defaultVal),
                    V1 = prop1 != null ? GetFloat(kf, prop1, defaultVal) : 0f,
                    Stepped = stepped
                };
            }
            return arr;
        }

        public static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 6)
            {
                return Color.white;
            }

            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte a = hex.Length >= 8
                ? byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : (byte)255;
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static float GetFloat(JsonElement el, string prop, float def)
        {
            return el.TryGetProperty(prop, out JsonElement val) && val.ValueKind == JsonValueKind.Number
                ? (float)val.GetDouble()
                : def;
        }

        private static string GetString(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out JsonElement val) && val.ValueKind == JsonValueKind.String
                ? val.GetString()
                : null;
        }

        private static float[] ReadFloatArray(JsonElement arr)
        {
            int len = arr.GetArrayLength();
            float[] result = new float[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = (float)arr[i].GetDouble();
            }
            return result;
        }

        private static int[] ReadIntArray(JsonElement arr)
        {
            int len = arr.GetArrayLength();
            int[] result = new int[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = arr[i].GetInt32();
            }
            return result;
        }
    }

    /// <summary>
    /// 持有 4 个固定拓扑 UnityEngine.Mesh 与零分配工作缓存的 Spine 蒙皮批量渲染器。
    /// </summary>
    public sealed class FairySpineBatchRenderer
    {
        private readonly FairySpineData data;
        private readonly SpineBoneState[] boneStates;
        private readonly string[] slotAttachments;
        private readonly Color[] slotColors;

        private readonly Mesh[] meshes = new Mesh[4];
        private readonly Vector3[][] batchVerts = new Vector3[4][];
        private readonly Color32[][] batchColors = new Color32[4][];

        private int lastFrameStamp = int.MinValue;
        private FairySpineAnimKind lastAnimKind;
        private float lastAnimTime = -1f;
        private bool lastFeastMode;
        private Color lastTint;
        private bool lastIncludeAdditive;

        public FairySpineBatchRenderer(FairySpineData spineData)
        {
            data = spineData;
            boneStates = new SpineBoneState[spineData.Bones.Length];
            slotAttachments = new string[spineData.Slots.Length];
            slotColors = new Color[spineData.Slots.Length];

            Bounds fixedBounds = new Bounds(Vector3.zero, new Vector3(4.5f, 1f, 4.5f));
            for (int b = 0; b < 4; b++)
            {
                int vCount = spineData.BatchVertexCounts[b];
                batchVerts[b] = new Vector3[vCount];
                batchColors[b] = new Color32[vCount];

                Mesh mesh = new Mesh
                {
                    name = $"FairyFestival_SpineBatch{b}"
                };
                mesh.vertices = batchVerts[b];
                mesh.uv = spineData.BatchUVs[b];
                mesh.colors32 = batchColors[b];
                mesh.SetTriangles(spineData.BatchTriangles[b], 0);
                mesh.bounds = fixedBounds;
                meshes[b] = mesh;
            }
        }

        public Mesh GetMesh(int batchIndex) => meshes[batchIndex];

        public void EvaluateAndUpload(
            FairySpineAnimKind animKind,
            float animTime,
            bool feastMode,
            Color tint,
            bool includeAdditive,
            int frameStamp)
        {
            if (frameStamp >= 0
                && frameStamp == lastFrameStamp
                && animKind == lastAnimKind
                && Mathf.Abs(animTime - lastAnimTime) < 0.0001f
                && feastMode == lastFeastMode
                && tint == lastTint
                && includeAdditive == lastIncludeAdditive)
            {
                return;
            }

            lastFrameStamp = frameStamp;
            lastAnimKind = animKind;
            lastAnimTime = animTime;
            lastFeastMode = feastMode;
            lastTint = tint;
            lastIncludeAdditive = includeAdditive;

            SpineAnimationClip clip = animKind == FairySpineAnimKind.GiveFairy && data.GiveFairyAnim != null
                ? data.GiveFairyAnim
                : data.DefaultAnim;

            // 1. Reset to setup pose
            for (int i = 0; i < data.Bones.Length; i++)
            {
                ref SpineBoneSetup setup = ref data.Bones[i];
                boneStates[i].X = setup.X;
                boneStates[i].Y = setup.Y;
                boneStates[i].Rotation = setup.Rotation;
                boneStates[i].ScaleX = setup.ScaleX;
                boneStates[i].ScaleY = setup.ScaleY;
                boneStates[i].ShearX = setup.ShearX;
                boneStates[i].ShearY = setup.ShearY;
            }

            for (int i = 0; i < data.Slots.Length; i++)
            {
                SpineSlotSetup s = data.Slots[i];
                slotAttachments[i] = s.SetupAttachmentName;
                slotColors[i] = s.SetupColor;
            }

            // 2. Apply animation timelines
            float evalT = clip != null ? Mathf.Clamp(animTime, 0f, clip.Duration) : 0f;
            if (clip != null)
            {
                SpineBoneTrack[] bTracks = clip.BoneTracks;
                for (int i = 0; i < bTracks.Length; i++)
                {
                    SpineBoneTrack bt = bTracks[i];
                    ref SpineBoneSetup setup = ref data.Bones[bt.BoneIndex];
                    ref SpineBoneState state = ref boneStates[bt.BoneIndex];

                    if (bt.Rotate != null)
                    {
                        state.Rotation = setup.Rotation + SampleAngle(bt.Rotate, evalT);
                    }
                    if (bt.Translate != null)
                    {
                        SampleVec2(bt.Translate, evalT, out float tx, out float ty);
                        state.X = setup.X + tx;
                        state.Y = setup.Y + ty;
                    }
                    if (bt.Scale != null)
                    {
                        SampleVec2(bt.Scale, evalT, out float sx, out float sy);
                        state.ScaleX = setup.ScaleX * sx;
                        state.ScaleY = setup.ScaleY * sy;
                    }
                    if (bt.Shear != null)
                    {
                        SampleVec2(bt.Shear, evalT, out float shx, out float shy);
                        state.ShearX = setup.ShearX + shx;
                        state.ShearY = setup.ShearY + shy;
                    }
                }

                SpineSlotTrack[] sTracks = clip.SlotTracks;
                for (int i = 0; i < sTracks.Length; i++)
                {
                    SpineSlotTrack st = sTracks[i];
                    if (st.Attachment != null)
                    {
                        slotAttachments[st.SlotIndex] = SampleAttachment(st.Attachment, evalT);
                    }
                    if (st.Color != null)
                    {
                        slotColors[st.SlotIndex] = SampleColor(st.Color, evalT);
                    }
                }
            }

            // 3. Apply Feast predatory visual overrides (crimson eyes, gnashing mouth, crimson glow)
            if (feastMode)
            {
                slotColors[FairySpineData.EyeSlotIndex] = new Color(1.0f, 0.14f, 0.18f, 1.0f);
                bool showSmile = (((int)(animTime * 8.0f)) % 2) == 1;
                slotAttachments[FairySpineData.Mouth1SlotIndex] = showSmile ? null : "mouth1";
                slotAttachments[FairySpineData.Mouth2SlotIndex] = showSmile ? "mouth2" : null;

                for (int i = FairySpineData.FirstAdditiveSlotIndex; i < data.Slots.Length; i++)
                {
                    Color c = slotColors[i];
                    slotColors[i] = new Color(c.r * 1.0f, c.g * 0.16f, c.b * 0.20f, c.a);
                }
            }

            // 4. Compute Spine 3.6 world affine transforms (parent -> child)
            for (int i = 0; i < data.Bones.Length; i++)
            {
                ref SpineBoneSetup setup = ref data.Bones[i];
                ref SpineBoneState st = ref boneStates[i];

                float rx = (st.Rotation + st.ShearX) * Mathf.Deg2Rad;
                float ry = (st.Rotation + st.ShearY) * Mathf.Deg2Rad;
                float la = Mathf.Cos(rx) * st.ScaleX;
                float lb = -Mathf.Sin(ry) * st.ScaleY;
                float lc = Mathf.Sin(rx) * st.ScaleX;
                float ld = Mathf.Cos(ry) * st.ScaleY;

                int pIdx = setup.ParentIndex;
                if (pIdx < 0)
                {
                    st.A = la;
                    st.B = lb;
                    st.C = lc;
                    st.D = ld;
                    st.WX = st.X;
                    st.WY = st.Y;
                }
                else
                {
                    ref SpineBoneState p = ref boneStates[pIdx];
                    st.A = p.A * la + p.B * lc;
                    st.B = p.A * lb + p.B * ld;
                    st.C = p.C * la + p.D * lc;
                    st.D = p.C * lb + p.D * ld;
                    st.WX = p.A * st.X + p.B * st.Y + p.WX;
                    st.WY = p.C * st.X + p.D * st.Y + p.WY;
                }
            }

            // 5. Skin vertices & pack premultiplied colors into all 4 batches
            for (int i = 0; i < data.Slots.Length; i++)
            {
                SpineSlotSetup slot = data.Slots[i];
                SpineAttachmentData att = slot.Attachment;
                if (att == null || slot.VertexCount <= 0)
                {
                    continue;
                }

                int bIdx = slot.BatchIndex;
                if (bIdx == 3 && !includeAdditive)
                {
                    CollapseSlot(batchVerts[3], batchColors[3], slot.VertexOffset, slot.VertexCount);
                    continue;
                }

                string activeAttName = slotAttachments[i];
                Color sCol = slotColors[i];
                Color aCol = att.Color;

                float r = Mathf.Clamp01(sCol.r * aCol.r * tint.r);
                float g = Mathf.Clamp01(sCol.g * aCol.g * tint.g);
                float bl = Mathf.Clamp01(sCol.b * aCol.b * tint.b);
                float a = Mathf.Clamp01(sCol.a * aCol.a * tint.a);

                if (string.IsNullOrEmpty(activeAttName) || a <= 0.001f)
                {
                    CollapseSlot(batchVerts[bIdx], batchColors[bIdx], slot.VertexOffset, slot.VertexCount);
                    continue;
                }

                Color32 pmaColor = ToPremultipliedColor32(r, g, bl, a);
                Vector3[] dstVerts = batchVerts[bIdx];
                Color32[] dstCols = batchColors[bIdx];
                float worldY = i * FairySpineData.SlotDepthStep;

                if (att.IsMesh)
                {
                    SpineVertexSlice[] slices = att.WeightSlices;
                    SpineMeshWeight[] weights = att.Weights;
                    for (int k = 0; k < att.VertexCount; k++)
                    {
                        SpineVertexSlice slice = slices[k];
                        float sx = 0f;
                        float sy = 0f;
                        for (int w = 0; w < slice.Count; w++)
                        {
                            SpineMeshWeight mw = weights[slice.Start + w];
                            ref SpineBoneState bone = ref boneStates[mw.BoneIndex];
                            sx += (bone.A * mw.X + bone.B * mw.Y + bone.WX) * mw.Weight;
                            sy += (bone.C * mw.X + bone.D * mw.Y + bone.WY) * mw.Weight;
                        }

                        int dstIdx = slot.VertexOffset + k;
                        dstVerts[dstIdx] = new Vector3(
                            (sx - FairySpineData.CenterX) * FairySpineData.PixelsToWorld,
                            worldY,
                            (sy - FairySpineData.CenterY) * FairySpineData.PixelsToWorld);
                        dstCols[dstIdx] = pmaColor;
                    }
                }
                else
                {
                    ref SpineBoneState bone = ref boneStates[slot.BoneIndex];
                    Vector2[] locals = att.LocalVerts;
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 lv = locals[k];
                        float sx = bone.A * lv.x + bone.B * lv.y + bone.WX;
                        float sy = bone.C * lv.x + bone.D * lv.y + bone.WY;

                        int dstIdx = slot.VertexOffset + k;
                        dstVerts[dstIdx] = new Vector3(
                            (sx - FairySpineData.CenterX) * FairySpineData.PixelsToWorld,
                            worldY,
                            (sy - FairySpineData.CenterY) * FairySpineData.PixelsToWorld);
                        dstCols[dstIdx] = pmaColor;
                    }
                }
            }

            // 6. Populate or collapse the extra additive Eye glow quad at the end of Batch 3
            if (data.Slots[FairySpineData.EyeSlotIndex].VertexCount == 4)
            {
                float glowIntensity = 0f;
                Color glowTint = Color.white;
                if (includeAdditive && feastMode)
                {
                    glowIntensity = 0.85f * tint.a;
                    glowTint = new Color(1.0f, 0.14f, 0.18f, 1f);
                }
                else if (includeAdditive && animKind == FairySpineAnimKind.GiveFairy)
                {
                    Color ec = slotColors[FairySpineData.EyeSlotIndex];
                    float emeraldDelta = Mathf.Clamp01((ec.g - ec.r) * 1.15f);
                    if (emeraldDelta > 0.02f)
                    {
                        glowIntensity = emeraldDelta * 0.80f * tint.a;
                        glowTint = new Color(0.0f, 1.0f, 0.51f, 1f);
                    }
                }

                if (glowIntensity > 0.01f)
                {
                    int srcOffset = data.Slots[FairySpineData.EyeSlotIndex].VertexOffset;
                    int dstOffset = data.EyeGlowVertexOffset;
                    float glowY = data.Slots.Length * FairySpineData.SlotDepthStep;
                    Color32 glowCol = ToPremultipliedColor32(glowTint.r * tint.r, glowTint.g * tint.g, glowTint.b * tint.b, glowIntensity);
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 p = batchVerts[0][srcOffset + k];
                        batchVerts[3][dstOffset + k] = new Vector3(p.x, glowY, p.z);
                        batchColors[3][dstOffset + k] = glowCol;
                    }
                }
                else
                {
                    CollapseSlot(batchVerts[3], batchColors[3], data.EyeGlowVertexOffset, 4);
                }
            }

            // 7. Upload mutated vertices and colors to GPU meshes
            for (int b = 0; b < 4; b++)
            {
                if (b == 3 && !includeAdditive)
                {
                    continue;
                }
                meshes[b].vertices = batchVerts[b];
                meshes[b].colors32 = batchColors[b];
            }
        }

        private static void CollapseSlot(Vector3[] verts, Color32[] cols, int offset, int count)
        {
            Color32 zeroCol = new Color32(0, 0, 0, 0);
            for (int k = 0; k < count; k++)
            {
                verts[offset + k] = Vector3.zero;
                cols[offset + k] = zeroCol;
            }
        }

        private static Color32 ToPremultipliedColor32(float r, float g, float b, float a)
        {
            byte pa = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            byte pr = (byte)Mathf.Clamp(Mathf.RoundToInt(r * a * 255f), 0, 255);
            byte pg = (byte)Mathf.Clamp(Mathf.RoundToInt(g * a * 255f), 0, 255);
            byte pb = (byte)Mathf.Clamp(Mathf.RoundToInt(b * a * 255f), 0, 255);
            return new Color32(pr, pg, pb, pa);
        }

        private static float SampleAngle(SpineFloatKeyframe[] kfs, float t)
        {
            int len = kfs.Length;
            if (len == 0) return 0f;
            if (t <= kfs[0].Time) return kfs[0].V0;
            if (t >= kfs[len - 1].Time) return kfs[len - 1].V0;

            int idx = FindKeyframeIndex(kfs, t);
            SpineFloatKeyframe k0 = kfs[idx];
            if (k0.Stepped || idx + 1 >= len)
            {
                return k0.V0;
            }

            SpineFloatKeyframe k1 = kfs[idx + 1];
            float span = k1.Time - k0.Time;
            if (span <= 0.00001f) return k1.V0;

            float percent = (t - k0.Time) / span;
            float diff = k1.V0 - k0.V0;
            while (diff > 180f) diff -= 360f;
            while (diff < -180f) diff += 360f;
            return k0.V0 + diff * percent;
        }

        private static void SampleVec2(SpineFloatKeyframe[] kfs, float t, out float x, out float y)
        {
            int len = kfs.Length;
            if (len == 0)
            {
                x = 0f;
                y = 0f;
                return;
            }
            if (t <= kfs[0].Time)
            {
                x = kfs[0].V0;
                y = kfs[0].V1;
                return;
            }
            if (t >= kfs[len - 1].Time)
            {
                x = kfs[len - 1].V0;
                y = kfs[len - 1].V1;
                return;
            }

            int idx = FindKeyframeIndex(kfs, t);
            SpineFloatKeyframe k0 = kfs[idx];
            if (k0.Stepped || idx + 1 >= len)
            {
                x = k0.V0;
                y = k0.V1;
                return;
            }

            SpineFloatKeyframe k1 = kfs[idx + 1];
            float span = k1.Time - k0.Time;
            if (span <= 0.00001f)
            {
                x = k1.V0;
                y = k1.V1;
                return;
            }

            float percent = (t - k0.Time) / span;
            x = k0.V0 + (k1.V0 - k0.V0) * percent;
            y = k0.V1 + (k1.V1 - k0.V1) * percent;
        }

        private static int FindKeyframeIndex(SpineFloatKeyframe[] kfs, float t)
        {
            int lo = 0;
            int hi = kfs.Length - 2;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (t < kfs[mid].Time)
                {
                    hi = mid - 1;
                }
                else if (t >= kfs[mid + 1].Time)
                {
                    lo = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return Mathf.Clamp(lo, 0, kfs.Length - 1);
        }

        private static string SampleAttachment(SpineAttachmentKeyframe[] kfs, float t)
        {
            int len = kfs.Length;
            if (len == 0) return null;
            if (t < kfs[0].Time) return kfs[0].Name;
            if (t >= kfs[len - 1].Time) return kfs[len - 1].Name;

            for (int i = len - 1; i >= 0; i--)
            {
                if (t >= kfs[i].Time)
                {
                    return kfs[i].Name;
                }
            }
            return kfs[0].Name;
        }

        private static Color SampleColor(SpineColorKeyframe[] kfs, float t)
        {
            int len = kfs.Length;
            if (len == 0) return Color.white;
            if (t <= kfs[0].Time) return kfs[0].Color;
            if (t >= kfs[len - 1].Time) return kfs[len - 1].Color;

            for (int i = 0; i < len - 1; i++)
            {
                if (t < kfs[i + 1].Time)
                {
                    SpineColorKeyframe k0 = kfs[i];
                    if (k0.Stepped)
                    {
                        return k0.Color;
                    }

                    SpineColorKeyframe k1 = kfs[i + 1];
                    float span = k1.Time - k0.Time;
                    float pct = span > 0.00001f ? (t - k0.Time) / span : 1f;
                    return Color.LerpUnclamped(k0.Color, k1.Color, pct);
                }
            }

            return kfs[len - 1].Color;
        }
    }
}
