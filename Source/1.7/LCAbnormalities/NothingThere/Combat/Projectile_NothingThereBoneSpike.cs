using System.Collections.Generic;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace NothingThere.Combat
{
    /// <summary>一无所有三阶段远程射出的骨颚鱼叉弹体：散发暗红轨迹，命中时对直线沿途敌对单位造成穿透红伤。</summary>
    public class Projectile_NothingThereBoneSpike : Bullet
    {
        private static readonly Color CrimsonTrailColor = new Color(0.78f, 0.08f, 0.10f, 0.85f);
        private const float PierceHalfWidth = 0.85f;

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && !Destroyed && Map != null && this.IsHashIntervalTick(2))
            {
                FleckMaker.ThrowDustPuffThick(ExactPosition, Map, 0.5f, CrimsonTrailColor);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            Vector3 endPos = ExactPosition;
            Vector3 startPos = origin;

            if (map != null && pos.InBounds(map) && !blockedByShield)
            {
                AbnormalityUtility.Burst(pos, map, 0.7f);
                ApplyLinePierceDamage(map, startPos, endPos, hitThing);
            }

            base.Impact(hitThing, blockedByShield);
        }

        private void ApplyLinePierceDamage(Map map, Vector3 startPos, Vector3 endPos, Thing primaryHit)
        {
            Vector3 seg = endPos - startPos;
            seg.y = 0f;
            float segLenSq = seg.sqrMagnitude;
            if (segLenSq < 0.25f)
            {
                return;
            }

            Vector3 dir = seg / Mathf.Sqrt(segLenSq);
            float segLen = Mathf.Sqrt(segLenSq);
            DamageDef spikeDef = Def.DamageDefOf.LC_NothingThere_BoneSpike ?? DamageDef;
            List<Pawn> candidates = new List<Pawn>();
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p == null || p == launcher || p == primaryHit || p.Dead || !p.Spawned)
                {
                    continue;
                }

                if (launcher != null && p.Faction == launcher.Faction)
                {
                    continue;
                }

                Vector3 rel = p.DrawPos - startPos;
                rel.y = 0f;
                float proj = Vector3.Dot(rel, dir);
                if (proj < 0.25f || proj > segLen + 0.5f)
                {
                    continue;
                }

                Vector3 closest = dir * proj;
                float perpSq = (rel - closest).sqrMagnitude;
                if (perpSq <= PierceHalfWidth * PierceHalfWidth)
                {
                    candidates.Add(p);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                if (!target.Dead)
                {
                    target.TakeDamage(new DamageInfo(spikeDef, Rand.Range(50f, 60f), 0.65f, instigator: launcher));
                }
            }
        }
    }
}
