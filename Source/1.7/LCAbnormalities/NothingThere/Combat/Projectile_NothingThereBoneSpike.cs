using System.Collections.Generic;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NothingThere.Combat
{
    /// <summary>一无所有三阶段 Skill 1 射出的三重骨爪弹体：散发暗红轨迹，沿直线贯穿走廊路径上所有敌对单位并造成红伤。</summary>
    public class Projectile_NothingThereBoneSpike : Bullet
    {
        private static readonly Color CrimsonTrailColor = new Color(0.78f, 0.08f, 0.10f, 0.85f);
        private const float PierceHalfWidth = 0.85f;

        private Vector3 lastSweepPos;
        private bool sweepInitialized;
        private readonly HashSet<int> hitPawnIds = new HashSet<int>();

        protected override void TickInterval(int delta)
        {
            Vector3 startPos = sweepInitialized ? lastSweepPos : origin;
            base.TickInterval(delta);

            if (Spawned && !Destroyed && Map != null)
            {
                if (this.IsHashIntervalTick(2))
                {
                    FleckMaker.ThrowDustPuffThick(ExactPosition, Map, 0.5f, CrimsonTrailColor);
                }

                SweepPierceSegment(Map, startPos, ExactPosition);
                lastSweepPos = ExactPosition;
                sweepInitialized = true;
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            Vector3 endPos = ExactPosition;
            Vector3 startPos = sweepInitialized ? lastSweepPos : origin;

            if (map != null && pos.InBounds(map) && !blockedByShield)
            {
                AbnormalityUtility.Burst(pos, map, 0.7f);
                bool playedHit = SweepPierceSegment(map, startPos, endPos);

                if (hitThing is Pawn directPawn
                    && !hitPawnIds.Contains(directPawn.thingIDNumber)
                    && IsValidPierceTarget(directPawn))
                {
                    DamagePiercedPawn(map, directPawn, !playedHit);
                }
            }

            // 若 hitThing 是已被贯穿逻辑结算过的 Pawn，向基类传 null 避免重复扣血
            if (hitThing is Pawn hp && hitPawnIds.Contains(hp.thingIDNumber))
            {
                base.Impact(null, blockedByShield);
            }
            else
            {
                base.Impact(hitThing, blockedByShield);
            }
        }

        private bool SweepPierceSegment(Map map, Vector3 startPos, Vector3 endPos)
        {
            Vector3 seg = endPos - startPos;
            seg.y = 0f;
            float segLenSq = seg.sqrMagnitude;
            float halfWidthSq = PierceHalfWidth * PierceHalfWidth;
            bool shortSeg = segLenSq < 0.01f;
            float segLen = shortSeg ? 0f : Mathf.Sqrt(segLenSq);
            Vector3 dir = shortSeg ? Vector3.zero : (seg / segLen);

            List<Pawn> candidates = new List<Pawn>();
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (!IsValidPierceTarget(p) || hitPawnIds.Contains(p.thingIDNumber))
                {
                    continue;
                }

                if (shortSeg)
                {
                    Vector3 rel = p.DrawPos - endPos;
                    rel.y = 0f;
                    if (rel.sqrMagnitude <= halfWidthSq)
                    {
                        candidates.Add(p);
                    }
                    continue;
                }

                Vector3 relSeg = p.DrawPos - startPos;
                relSeg.y = 0f;
                float proj = Vector3.Dot(relSeg, dir);
                if (proj < -0.25f || proj > segLen + 0.5f)
                {
                    continue;
                }

                Vector3 closest = dir * Mathf.Clamp(proj, 0f, segLen);
                if ((relSeg - closest).sqrMagnitude <= halfWidthSq)
                {
                    candidates.Add(p);
                }
            }

            bool playedSound = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                if (!target.Dead)
                {
                    DamagePiercedPawn(map, target, !playedSound);
                    playedSound = true;
                }
            }

            return playedSound;
        }

        private void DamagePiercedPawn(Map map, Pawn target, bool playSound)
        {
            hitPawnIds.Add(target.thingIDNumber);
            DamageDef spikeDef = Def.DamageDefOf.LC_NothingThere_BoneSpike ?? DamageDef;
            target.TakeDamage(new DamageInfo(spikeDef, Rand.Range(50f, 60f), 0.65f, ExactRotation.eulerAngles.y, instigator: launcher));
            if (target.MapHeld == map && target.PositionHeld.InBounds(map))
            {
                AbnormalityUtility.Burst(target.PositionHeld, map, 0.55f);
            }

            if (playSound)
            {
                Def.SoundDefOf.NothingThere_BoneSpikeHit?.PlayOneShot(SoundInfo.InMap(target));
            }
        }

        private bool IsValidPierceTarget(Pawn p)
        {
            if (p == null || p == launcher || p.Dead || !p.Spawned)
            {
                return false;
            }

            return launcher == null || p.Faction != launcher.Faction;
        }
    }
}
