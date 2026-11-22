using Laetitia.Comp;
using Laetitia.Effect;
using LCAnomalyCore.Comp;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Laetitia.Things
{
    public class LC_LaetitiaMonsterPawn : LC_EntityBasePawn
    {
        private bool isMoving => pather.Moving;

        private bool isSpawning = false;
        private int spawnTicksRemaining = 0;

        private bool isAttacking = false;
        private bool attackHitApplied = false;
        private bool attackIsRight = false;
        private int attackTicksElapsed = 0;

        private bool isDying = false;
        private bool shouldDie = false;
        private int deathTicksRemaining = 0;

        private int lifespanTicksRemaining = -1;

        public Pawn CurTarget;

        /// <summary>
        /// 处于出生、攻击、死亡动画期间暂停 AI 接管。
        /// </summary>
        public bool IsBusyForAI => isSpawning || isAttacking || isDying || shouldDie;

        protected CompAbilityEffect_MonsterAttack CompAbilityAttack
        {
            get
            {
                if (compAbilityAttack == null)
                {
                    compAbilityAttack = this.abilities?.GetAbility(Def.AbilityDefOf.LaetitiaMonster_Attack)
                        ?.CompOfType<CompAbilityEffect_MonsterAttack>();
                }
                return compAbilityAttack;
            }
        }

        private CompAbilityEffect_MonsterAttack compAbilityAttack;

        public LC_LaetitiaMonsterPawn()
        {
        }

        public void SetTemporaryLifespan(int ticks)
        {
            lifespanTicksRemaining = ticks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isSpawning, "isSpawning", false);
            Scribe_Values.Look(ref spawnTicksRemaining, "spawnTicksRemaining", 0);
            Scribe_Values.Look(ref isAttacking, "isAttacking", false);
            Scribe_Values.Look(ref attackHitApplied, "attackHitApplied", false);
            Scribe_Values.Look(ref attackIsRight, "attackIsRight", false);
            Scribe_Values.Look(ref attackTicksElapsed, "attackTicksElapsed", 0);
            Scribe_Values.Look(ref isDying, "isDying", false);
            Scribe_Values.Look(ref shouldDie, "shouldDie", false);
            Scribe_Values.Look(ref deathTicksRemaining, "deathTicksRemaining", 0);
            Scribe_Values.Look(ref lifespanTicksRemaining, "lifespanTicksRemaining", -1);
            Scribe_References.Look(ref CurTarget, "curTarget");
        }

        protected override void Tick()
        {
            if (isSpawning)
            {
                spawnTicksRemaining--;
                if (spawnTicksRemaining <= 0)
                {
                    isSpawning = false;
                    spawnTicksRemaining = 0;
                    SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Idle);
                }
                return;
            }

            if (isDying)
            {
                deathTicksRemaining--;
                if (deathTicksRemaining <= 0)
                {
                    Map map = MapHeld;
                    IntVec3 pos = PositionHeld;
                    if (map != null && pos.InBounds(map))
                    {
                        var fx = GenSpawn.Spawn(Def.ThingDefOf.DyingLaetitiaMonster, pos, map) as DyingLaetitiaMonster;
                        fx?.InitWith(this);
                    }

                    isDying = false;
                    deathTicksRemaining = 0;
                    shouldDie = true;
                    Kill(null);
                }
                return;
            }

            base.Tick();
            if (Dead || Destroyed)
            {
                return;
            }

            if (lifespanTicksRemaining > 0)
            {
                lifespanTicksRemaining--;
                if (lifespanTicksRemaining == 0)
                {
                    Kill(null);
                    return;
                }
            }

            if (isAttacking)
            {
                attackTicksElapsed++;
                int hitTick = attackIsRight ? 23 : 26;
                if (!attackHitApplied && attackTicksElapsed >= hitTick)
                {
                    attackHitApplied = true;
                    AttackDirect(CurTarget);
                }

                AnimationDef attackAnim = attackIsRight
                    ? Def.AnimationDefOf.LaetitiaMonster_AttackR
                    : Def.AnimationDefOf.LaetitiaMonster_AttackL;
                int totalTicks = attackAnim?.durationTicks ?? 40;
                if (attackTicksElapsed >= totalTicks)
                {
                    isAttacking = false;
                    attackHitApplied = false;
                    attackTicksElapsed = 0;
                    CurTarget = null;
                }
            }
            else if (isMoving)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Walk);
            }
            else
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Idle);
            }
        }

        public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            if (isDying)
                return;

            if (!shouldDie)
            {
                isDying = true;
                isSpawning = false;
                isAttacking = false;
                deathTicksRemaining = Def.AnimationDefOf.LaetitiaMonster_Dead?.durationTicks ?? 45;
                pather?.StopDead();
                jobs?.StopAll();
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.LaetitiaMonster_Dead);
            }
            else
            {
                base.Kill(dinfo, exactCulprit);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                isSpawning = true;
                spawnTicksRemaining = Def.AnimationDefOf.LaetitiaMonster_Spawn?.durationTicks ?? 25;
                if (MapHeld != null)
                {
                    Def.SoundDefOf.LaetitiaMonster_Spawn.PlayOneShotOnCamera(MapHeld);
                }
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.LaetitiaMonster_Spawn);
            }
            else if (isDying)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Dead);
            }
            else if (isSpawning)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Spawn);
            }
            else if (isAttacking)
            {
                SetAnimationIfNeeded(attackIsRight
                    ? Def.AnimationDefOf.LaetitiaMonster_AttackR
                    : Def.AnimationDefOf.LaetitiaMonster_AttackL);
            }
            else
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.LaetitiaMonster_Idle);
            }
        }

        public void SetTargetAndDir(Pawn target)
        {
            if (target == null || isSpawning || isDying || shouldDie)
            {
                return;
            }

            isAttacking = true;
            attackHitApplied = false;
            attackTicksElapsed = 0;
            CurTarget = target;

            attackIsRight = (target.Position - this.Position).x >= 0;
            Drawer.renderer.SetAnimation(attackIsRight
                ? Def.AnimationDefOf.LaetitiaMonster_AttackR
                : Def.AnimationDefOf.LaetitiaMonster_AttackL);
        }

        private void AttackDirect(Pawn target)
        {
            if (target == null || target.Dead || target.Destroyed || !target.Spawned || target.MapHeld != MapHeld)
            {
                return;
            }

            // 仅在近战打击有效距离内结算伤害，防止目标跑远仍隔空命中
            if (Position.DistanceToSquared(target.Position) > 6.25f)
            {
                return;
            }

            CompAbilityEffect_MonsterAttack comp = CompAbilityAttack;
            if (comp == null)
            {
                return;
            }

            LCAnomalyCore.Util.AbnormalityUtility.Burst(target.PositionHeld, target.MapHeld);
            target.TakeDamage(comp.CreateStunDamageInfo());
            target.TakeDamage(comp.CreateAttackDamageInfo());
        }

        private void SetAnimationIfNeeded(AnimationDef animation)
        {
            if (animation != null && Drawer.renderer.CurAnimation != animation)
            {
                Drawer.renderer.SetAnimation(animation);
            }
        }
    }

}
