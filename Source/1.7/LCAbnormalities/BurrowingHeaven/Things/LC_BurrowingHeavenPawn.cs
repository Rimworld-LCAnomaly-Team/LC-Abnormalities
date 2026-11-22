using BurrowingHeaven.Comp;
using LCAnomalyCore.Comp;
using RimWorld.Planet;
using Verse;

namespace BurrowingHeaven.Things
{
    public class LC_BurrowingHeavenPawn : LC_EntityBasePawn
    {
        public CompBurrowingHeaven SelfEntityComp => (CompBurrowingHeaven)EntityComp;

        public bool ShouldCheckInsight = false;
        public bool IsOutOfSight => isOutOfSight;
        protected bool isOutOfSight = false;

        private int attackAnimTicksRemaining = 0;

        public LC_BurrowingHeavenPawn()
        {
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (SelfEntityComp != null && SelfEntityComp.Escaped)
            {
                SetBreachAnimation();
            }
            else
            {
                SetIdleAnimation();
            }

            if (respawningAfterLoad && SelfEntityComp?.HangingHeadCount > 0)
            {
                Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        public bool IsOutsidePlayerCamera()
        {
            if (MapHeld == null || Find.CameraDriver == null || !PositionHeld.IsValid)
                return false;

            if (WorldRendererUtility.WorldSelected || Find.CurrentMap != MapHeld)
                return true;

            return !Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(PositionHeld);
        }

        public void SetOutOfSight(bool value)
        {
            isOutOfSight = value;
        }

        public void SetIdleAnimation()
        {
            attackAnimTicksRemaining = 0;
            SetAnimationIfNeeded(Def.AnimationDefOf.BurrowingHeaven_Idle);
        }

        public void SetWorkAnimation()
        {
            if (attackAnimTicksRemaining <= 0)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.BurrowingHeaven_Work);
            }
        }

        public void SetBreachAnimation()
        {
            if (attackAnimTicksRemaining <= 0)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.BurrowingHeaven_Breach);
            }
        }

        public void PlayAttackAnimation(int durationTicks = 36)
        {
            AnimationDef anim = Def.AnimationDefOf.BurrowingHeaven_Attack;
            if (anim != null && Drawer?.renderer != null)
            {
                attackAnimTicksRemaining = durationTicks;
                Drawer.renderer.SetAnimation(anim);
            }
        }

        public override void TickHolded()
        {
            base.TickHolded();
            SelfEntityComp?.TickContainmentSight();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            //收容状态下丢下就出逃
            if (Spawned && CarriedBy == null && SelfEntityComp != null && !SelfEntityComp.Escaped)
                SelfEntityComp.HoldingPlaformTargetComp?.Escape();

            SelfEntityComp?.TickBreachCombat();
            UpdateAnimation();
            base.Tick();
        }

        private void UpdateAnimation()
        {
            if (Drawer?.renderer == null)
                return;

            if (attackAnimTicksRemaining > 0)
            {
                attackAnimTicksRemaining--;
                if (attackAnimTicksRemaining == 0)
                {
                    ResumeBaselineAnimation();
                }
            }
            else if (Drawer.renderer.CurAnimation == null)
            {
                ResumeBaselineAnimation();
            }
        }

        private void ResumeBaselineAnimation()
        {
            if (SelfEntityComp != null && SelfEntityComp.Escaped)
            {
                SetBreachAnimation();
            }
            else if (SelfEntityComp != null && SelfEntityComp.IsBeingStudiedNow)
            {
                SetWorkAnimation();
            }
            else
            {
                SetIdleAnimation();
            }
        }

        private void SetAnimationIfNeeded(AnimationDef animation)
        {
            if (animation != null && Drawer?.renderer != null && Drawer.renderer.CurAnimation != animation)
            {
                Drawer.renderer.SetAnimation(animation);
            }
        }
    }
}
