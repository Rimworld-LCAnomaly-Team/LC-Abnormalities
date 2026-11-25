using System.Collections.Generic;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Shared
{
    /// <summary>支持按战斗或收容状态动态切换身体贴图与尺寸的异想体接口。</summary>
    public interface ILCDynamicSpritePawn
    {
        string CurrentBodyTexPath { get; }
        Vector2 CurrentBodyDrawSize { get; }
        bool ShouldFlipWest { get; }
    }

    /// <summary>支持动态贴图切换的异想体渲染节点属性。</summary>
    public class PawnRenderNodeProperties_DynamicEntity : PawnRenderNodeProperties
    {
        public Vector2 baseDrawSize = new Vector2(1.5f, 1.5f);
        public bool flipWest = true;
        public List<string> alternateTexPaths;

        public PawnRenderNodeProperties_DynamicEntity()
        {
            nodeClass = typeof(PawnRenderNode_DynamicEntity);
            workerClass = typeof(PawnRenderNodeWorker_DynamicEntity);
            drawSize = Vector2.one;
            pawnType = RenderNodePawnType.Any;
        }
    }

    /// <summary>支持动态贴图切换与西向水平镜像的异想体渲染节点。</summary>
    public class PawnRenderNode_DynamicEntity : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_DynamicEntity DynProps => (PawnRenderNodeProperties_DynamicEntity)props;

        public PawnRenderNode_DynamicEntity(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms)
        {
            bool allowFlip = (parms.pawn as ILCDynamicSpritePawn)?.ShouldFlipWest ?? (DynProps?.flipWest ?? true);
            return allowFlip && parms.facing == Rot4.West;
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetCachedGraphic(string texPath)
        {
            if (texPath.NullOrEmpty())
            {
                return PrimaryGraphic;
            }

            if (cachedGraphics.TryGetValue(texPath, out Graphic found) && found != null)
            {
                return found;
            }

            if (UnityData.IsInMainThread)
            {
                Graphic g = GraphicDatabase.Get<Graphic_Single>(texPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
                if (g != null)
                {
                    cachedGraphics[texPath] = g;
                    return g;
                }
            }

            return PrimaryGraphic;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string tex = (pawn as ILCDynamicSpritePawn)?.CurrentBodyTexPath ?? Props.texPath;
            if (tex.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(tex);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            Graphic primary = GraphicFor(pawn);
            if (primary != null)
            {
                yield return primary;
            }

            if (!Props.texPath.NullOrEmpty() && Props.texPath != ((pawn as ILCDynamicSpritePawn)?.CurrentBodyTexPath))
            {
                Graphic baseG = GetCachedGraphic(Props.texPath);
                if (baseG != null && baseG != primary)
                {
                    yield return baseG;
                }
            }

            List<string> alts = DynProps?.alternateTexPaths;
            if (alts != null)
            {
                for (int i = 0; i < alts.Count; i++)
                {
                    string altTex = alts[i];
                    if (altTex.NullOrEmpty())
                    {
                        continue;
                    }

                    Graphic altGraphic = GetCachedGraphic(altTex);
                    if (altGraphic != null)
                    {
                        yield return altGraphic;
                    }
                }
            }
        }
    }

    /// <summary>支持动态尺寸缩放与动态贴图切换的异想体渲染器。</summary>
    public class PawnRenderNodeWorker_DynamicEntity : PawnRenderNodeWorker
    {
        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_DynamicEntity dynNode = node as PawnRenderNode_DynamicEntity;
            if (dynNode != null && parms.pawn is ILCDynamicSpritePawn dynPawn)
            {
                string texPath = dynPawn.CurrentBodyTexPath;
                if (!texPath.NullOrEmpty())
                {
                    Graphic g = dynNode.GetCachedGraphic(texPath);
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
            }

            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_DynamicEntity dynProps = node.Props as PawnRenderNodeProperties_DynamicEntity;
            Vector2 targetSize = (parms.pawn as ILCDynamicSpritePawn)?.CurrentBodyDrawSize
                ?? (dynProps != null ? dynProps.baseDrawSize : new Vector2(1.5f, 1.5f));
            if (targetSize.x <= 0f) targetSize.x = 1.5f;
            if (targetSize.y <= 0f) targetSize.y = 1.5f;
            scale.x *= targetSize.x;
            scale.z *= targetSize.y;
            return scale;
        }
    }

    /// <summary>配置死亡演出销毁后应生成的异想体蛋定义与血污特效。</summary>
    public class EggReturnExtension : DefModExtension
    {
        public ThingDef eggDef;
        public float burstScale = 1.6f;
        public bool spawnFilth = true;
    }

    /// <summary>通用异想体死亡化蛋演出对象，通过 <see cref="EggReturnExtension"/> 驱动。</summary>
    public class DyingAbnormality : LC_FX_Standard
    {
        public override void InitWith(Pawn targetPawn)
        {
            CompAbnormality comp = targetPawn?.TryGetComp<CompAbnormality>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            EggReturnExtension ext = def.GetModExtension<EggReturnExtension>();
            float scale = ext != null ? ext.burstScale : 1.6f;
            if (Map != null && Position.InBounds(Map))
            {
                AbnormalityUtility.Burst(Position, Map, scale);
            }

            completeTick = TickSpawned + 60;
            hasInited = true;
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            EggReturnExtension ext = def.GetModExtension<EggReturnExtension>();
            if ((ext == null || ext.spawnFilth) && RimWorld.ThingDefOf.Filth_Blood != null && Map != null && PositionHeld.InBounds(Map))
            {
                FilthMaker.TryMakeFilth(PositionHeld, Map, RimWorld.ThingDefOf.Filth_Blood);
            }

            if (ext?.eggDef != null && MapHeld != null && PositionHeld.InBounds(MapHeld))
            {
                Thing egg = ThingMaker.MakeThing(ext.eggDef);
                AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, PositionHeld, MapHeld);
            }

            Destroy();
        }
    }

    /// <summary>可配置研究工作概率、NE-Box 伤害与死亡化蛋信息的异想体组件属性。</summary>
    public class CompProperties_LCAbnormality : CompProperties_Abnormality
    {
        public ThingDef dyingFxDef;
        public DamageDef workDamageDef;
        public IntRange workDamageRange = new IntRange(1, 2);
        public List<float> instinctProbs;
        public List<float> insightProbs;
        public List<float> attachmentProbs;
        public List<float> repressionProbs;
        public string escapedLetterLabelKey;
        public string escapedLetterTextKey;
        public string killedLetterLabelKey;
        public string killedLetterTextKey;
        public bool allowRoamingOutsideContainment = false;

        public CompProperties_LCAbnormality()
        {
            compClass = typeof(CompLCAbnormality);
        }
    }

    /// <summary>通用脑叶异想体核心组件基类，统一处理四维工作概率、工作伤害、逃脱信封与死亡化蛋。</summary>
    public class CompLCAbnormality : CompAbnormality
    {
        public new CompProperties_LCAbnormality Props => (CompProperties_LCAbnormality)props;

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            escaped = false;
            if (parent != null)
            {
                AbnormalityUtility.Discover(parent.def);
            }
        }

        public override void Notify_Escaped()
        {
            if (!Props.allowRoamingOutsideContainment)
            {
                parent?.Kill();
                return;
            }

            bool wasEscaped = escaped;
            base.Notify_Escaped();
            if (!wasEscaped && !Props.escapedLetterLabelKey.NullOrEmpty() && !Props.escapedLetterTextKey.NullOrEmpty())
            {
                Find.LetterStack.ReceiveLetter(
                    Props.escapedLetterLabelKey.Translate(),
                    Props.escapedLetterTextKey.Translate(parent.LabelCap),
                    LetterDefOf.ThreatBig,
                    parent);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            SpawnDyingFx(prevMap);
        }

        protected virtual void SpawnDyingFx(Map prevMap)
        {
            Map targetMap = prevMap ?? parent?.MapHeld;
            IntVec3 targetPos = parent?.PositionHeld ?? IntVec3.Invalid;
            if (Props.dyingFxDef == null || targetMap == null || !targetPos.InBounds(targetMap))
            {
                return;
            }

            if (!Props.killedLetterLabelKey.NullOrEmpty() && !Props.killedLetterTextKey.NullOrEmpty() && Escaped)
            {
                Find.LetterStack.ReceiveLetter(
                    Props.killedLetterLabelKey.Translate(),
                    Props.killedLetterTextKey.Translate(parent.LabelCap),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(targetPos, targetMap));
            }

            if (GenSpawn.Spawn(Props.dyingFxDef, targetPos, targetMap) is LC_FX_Standard fx)
            {
                fx.InitWith((Pawn)parent);
            }
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            bool success = base.Notify_StudyInterval(studier, workType);
            if (!success && Props.workDamageDef != null && studier?.parent is Pawn worker && !worker.Dead && worker.Spawned)
            {
                float dmg = Props.workDamageRange.RandomInRange;
                if (dmg > 0f)
                {
                    worker.TakeDamage(new DamageInfo(Props.workDamageDef, dmg, instigator: parent));
                }
            }

            return success;
        }

        /// <summary>供收容平台内的 Tick 调用。</summary>
        public virtual void TickContained()
        {
        }

        /// <summary>供出逃期间的 Tick 调用。</summary>
        public virtual void TickBreached()
        {
        }

        protected override float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            float baseRate = base.StudySuccessRateCalculate(studier, workType);
            float typeRate = LookupProb(studier, workType);
            if (typeRate <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(baseRate + typeRate, 0f, 0.95f);
        }

        private float LookupProb(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            List<float> table = null;
            EPawnStatus stat = EPawnStatus.Fortitude;
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    table = Props.instinctProbs;
                    stat = EPawnStatus.Fortitude;
                    break;
                case EAnomalyWorkType.Insight:
                    table = Props.insightProbs;
                    stat = EPawnStatus.Prudence;
                    break;
                case EAnomalyWorkType.Attachment:
                    table = Props.attachmentProbs;
                    stat = EPawnStatus.Temperance;
                    break;
                case EAnomalyWorkType.Repression:
                    table = Props.repressionProbs;
                    stat = EPawnStatus.Justice;
                    break;
                default:
                    return 0f;
            }

            if (table == null || table.Count == 0)
            {
                return 0.5f;
            }

            int levelIdx = studier != null
                ? Mathf.Clamp((int)studier.GetPawnStatusELevel(stat) - 1, 0, table.Count - 1)
                : 0;
            return table[levelIdx];
        }
    }

    /// <summary>按研究解锁等级配置工作速度与成功率加成的属性。</summary>
    public class CompProperties_Studiable_LCBonus : CompProperties_AbnormalityStudiable
    {
        public List<float> speedOffsetsByProgress;
        public List<float> successOffsetsByProgress;

        public CompProperties_Studiable_LCBonus()
        {
            compClass = typeof(CompStudiable_LCBonus);
        }
    }

    /// <summary>基于研究进度提供工作速度与成功率加成的通用 Studiable 组件。</summary>
    public class CompStudiable_LCBonus : CompAbnormalityStudiable
    {
        public new CompProperties_Studiable_LCBonus Props => (CompProperties_Studiable_LCBonus)props;

        public override float GetWorkSpeedOffset()
        {
            return ResolveOffset(Props.speedOffsetsByProgress);
        }

        public override float GetWorkSuccessRateOffset()
        {
            return ResolveOffset(Props.successOffsetsByProgress);
        }

        private float ResolveOffset(List<float> offsets)
        {
            if (CompStudyUnlocks == null || offsets == null || offsets.Count == 0)
            {
                return 0f;
            }

            int idx = Mathf.Clamp(CompStudyUnlocks.Progress, 0, offsets.Count - 1);
            return offsets[idx];
        }
    }

    /// <summary>通用标准异想体 Pawn 基类：自动处理非出逃化蛋、出逃转移与动态贴图接口。</summary>
    public class LC_StandardEntityPawn : LC_EntityBasePawn, ILCDynamicSpritePawn
    {
        public CompLCAbnormality LCComp => EntityComp as CompLCAbnormality;

        public virtual string CurrentBodyTexPath =>
            kindDef?.lifeStages?.Count > 0 ? kindDef.lifeStages[0]?.bodyGraphicData?.texPath : null;

        public virtual Vector2 CurrentBodyDrawSize =>
            kindDef?.lifeStages?.Count > 0 && kindDef.lifeStages[0]?.bodyGraphicData != null
                ? kindDef.lifeStages[0].bodyGraphicData.drawSize
                : new Vector2(1.6f, 1.6f);

        public virtual bool ShouldFlipWest => true;

        protected override void Tick()
        {
            CompAbnormalityHoldingPlatformTarget holdTarget = GetComp<CompAbnormalityHoldingPlatformTarget>();
            if (holdTarget != null && holdTarget.CurrentlyContainedInRoom)
            {
                TickHolded();
                base.Tick();
                return;
            }

            CompLCAbnormality comp = LCComp;
            if (Spawned && CarriedBy == null && comp != null)
            {
                if (!comp.Props.allowRoamingOutsideContainment)
                {
                    comp.Notify_Escaped();
                    return;
                }

                if (!comp.Escaped)
                {
                    comp.HoldingPlaformTargetComp?.Escape();
                }
            }

            comp?.TickBreached();
            if (Destroyed)
            {
                return;
            }
            base.Tick();
        }

        public override void TickHolded()
        {
            base.TickHolded();
            LCComp?.TickContained();
        }
    }

    /// <summary>Phase 1–2 异想体共用的治疗、精神恢复、音频播报、索敌与化蛋工具方法。</summary>
    public static class LCAbnormalitiesHelper
    {
        public static void HealInjuries(Pawn pawn, float totalAmount)
        {
            if (pawn == null || pawn.Dead || totalAmount <= 0f || pawn.health?.hediffSet == null)
            {
                return;
            }

            float remaining = totalAmount;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0 && remaining > 0.01f; i--)
            {
                if (hediffs[i] is Hediff_Injury injury && injury.CanHealNaturally())
                {
                    float heal = Mathf.Min(remaining, injury.Severity);
                    injury.Heal(heal);
                    remaining -= heal;
                }
            }
        }

        public static void RestoreMentalHealth(Pawn pawn, float spFractionRestore, float moodBoost = 0f)
        {
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
            {
                return;
            }

            if (spFractionRestore > 0f && LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage != null)
            {
                Hediff mentalDamage = pawn.health.hediffSet.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
                if (mentalDamage != null)
                {
                    mentalDamage.Severity -= spFractionRestore;
                    if (mentalDamage.Severity <= 0.0001f)
                    {
                        pawn.health.RemoveHediff(mentalDamage);
                        if (pawn.InMentalState && pawn.MentalState != null)
                        {
                            pawn.MentalState.RecoverFromState();
                        }
                    }
                }
            }

            if (moodBoost > 0f && pawn.needs?.mood != null)
            {
                pawn.needs.mood.CurLevel = Mathf.Clamp01(pawn.needs.mood.CurLevel + moodBoost);
            }
        }

        public static Hediff ApplyOrRefreshHediff(Pawn pawn, HediffDef def, float severity = 1f)
        {
            if (pawn == null || pawn.Dead || def == null || pawn.health?.hediffSet == null)
            {
                return null;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                existing.Severity = severity;
                return existing;
            }

            Hediff created = HediffMaker.MakeHediff(def, pawn);
            created.Severity = severity;
            pawn.health.AddHediff(created);
            return created;
        }

        public static void PlaySoundAt(SoundDef sound, Thing source)
        {
            if (sound == null || source == null)
            {
                return;
            }

            Map map = source.MapHeld;
            IntVec3 pos = source.PositionHeld;
            if (map != null && pos.InBounds(map))
            {
                Verse.Sound.SoundStarter.PlayOneShot(sound, new TargetInfo(pos, map));
            }
        }

        public static Pawn FindNearestHumanlike(Map map, IntVec3 origin, float maxDist = 9999f, System.Predicate<Pawn> extra = null)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null || !origin.IsValid)
            {
                return null;
            }

            Pawn best = null;
            float bestDistSq = maxDist >= 9000f ? float.MaxValue : maxDist * maxDist;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate.Dead || candidate.Downed || !candidate.Spawned)
                {
                    continue;
                }

                if (!(candidate.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if (extra != null && !extra(candidate))
                {
                    continue;
                }

                float dSq = (candidate.Position - origin).LengthHorizontalSquared;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = candidate;
                }
            }

            return best;
        }

        public static void ReturnEntityToEgg(Pawn entity, ThingDef eggDef, int bioSignature)
        {
            if (entity == null || entity.Destroyed)
            {
                return;
            }

            Map map = entity.MapHeld;
            IntVec3 pos = entity.PositionHeld;
            if (eggDef != null && map != null && pos.InBounds(map))
            {
                Thing egg = ThingMaker.MakeThing(eggDef);
                AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, pos, map);
            }

            entity.Destroy(DestroyMode.Vanish);
        }

        public static void GiveOrDropApparel(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn == null || pawn.Dead || apparelDef == null)
            {
                return;
            }

            if (!(ThingMaker.MakeThing(apparelDef) is Apparel apparel))
            {
                return;
            }

            if (pawn.apparel != null && ApparelUtility.HasPartsToWear(pawn, apparelDef))
            {
                for (int i = pawn.apparel.WornApparel.Count - 1; i >= 0; i--)
                {
                    Apparel worn = pawn.apparel.WornApparel[i];
                    if (worn != null && !ApparelUtility.CanWearTogether(worn.def, apparelDef, pawn.RaceProps.body))
                    {
                        if (pawn.MapHeld != null)
                        {
                            GenPlace.TryPlaceThing(apparel, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
                        }
                        return;
                    }
                }

                pawn.apparel.Wear(apparel, dropReplacedApparel: false, locked: false);
                return;
            }

            if (pawn.MapHeld != null)
            {
                GenPlace.TryPlaceThing(apparel, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            }
        }
    }
}
