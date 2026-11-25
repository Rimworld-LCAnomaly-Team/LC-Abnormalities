using Laetitia.Comp;
using LCAnomalyCore.Comp;
using RimWorld;
using Verse;

namespace Laetitia.Things
{
    public class LC_LaetitiaPawn : LC_EntityBasePawn
    {
        private int giftAnimationEndTick = -1;

        public bool GiftAnimationPlaying => giftAnimationEndTick > Find.TickManager.TicksGame;

        public LC_LaetitiaPawn()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref giftAnimationEndTick, "giftAnimationEndTick", -1);
        }

        protected override void Tick()
        {
            if (IsContainedInRoom)
            {
                TickHolded();
                base.Tick();
                return;
            }

            //收容状态下丢下就出逃
            if (Spawned && CarriedBy == null)
            {
                GetComp<CompLaetitia>()?.Notify_Escaped();
                return;
            }

            base.Tick();
            UpdateAnimation();
        }

        public override void TickHolded()
        {
            base.TickHolded();
            UpdateAnimation();
        }

        public void SetIdleAnimation()
        {
            SetAnimationIfNeeded(Def.AnimationDefOf.Laetitia_Idle);
        }

        public void SetWorkAnimation()
        {
            if (!GiftAnimationPlaying)
            {
                SetAnimationIfNeeded(Def.AnimationDefOf.Laetitia_Work);
            }
        }

        public void PlayGiftAnimation()
        {
            AnimationDef animation = Def.AnimationDefOf.Laetitia_GiveGift;
            giftAnimationEndTick = Find.TickManager.TicksGame + animation.durationTicks;
            SetAnimationIfNeeded(animation);
        }

        private void UpdateAnimation()
        {
            if (giftAnimationEndTick >= 0 && !GiftAnimationPlaying)
            {
                giftAnimationEndTick = -1;
                SetIdleAnimation();
            }
            else if (Drawer.renderer.CurAnimation == null)
            {
                SetIdleAnimation();
            }
        }

        private void SetAnimationIfNeeded(AnimationDef animation)
        {
            if (Drawer.renderer.CurAnimation != animation)
            {
                Drawer.renderer.SetAnimation(animation);
            }
        }
    }
}
