using Laetitia.Effect;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Laetitia.Comp
{
    public class CompAbilityEffect_SendTheGift : CompAbilityEffect
    {
        private new CompProperties_AbilitySendTheGift Props => (CompProperties_AbilitySendTheGift)props;

        private Pawn Pawn => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = Pawn;
            Pawn victim = target.Pawn;
            if (caster == null || caster.Dead || !caster.Spawned || victim == null || victim.Dead || victim.Destroyed)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 anchorPos = victim.Spawned ? victim.DrawPos.Yto0() : victim.PositionHeld.ToVector3Shifted().Yto0();
            Vector3 dir = (anchorPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == LaetitiaWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, LaetitiaWeaponAnimHandler.ClipSkillGift, 74, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(LaetitiaWeaponAnimHandler.WeaponDefName);
            LaetitiaVfxUtility.SpawnRibbonTracer(map, cPos + dir * 0.65f, anchorPos, empowered: true);
            LaetitiaVfxUtility.SpawnHeartBloom(map, cPos + dir * 0.68f, scale: 1.05f, lifeTicks: 12);
            LaetitiaVfxUtility.SpawnHeartBloom(map, anchorPos, scale: 1.55f, lifeTicks: 16);

            // 若目标已携带攻击性礼物，首拍直接引爆现有碎心黑蕾丝再进入第 2 拍再挂印！
            Hediff existingGift = Def.HediffDefOf.LaetitiaGiftAggresive != null
                ? victim.health?.hediffSet?.GetFirstHediffOfDef(Def.HediffDefOf.LaetitiaGiftAggresive)
                : null;
            if (existingGift != null)
            {
                HediffComp_LaetitiaGiftAggresive giftComp = existingGift.TryGetComp<HediffComp_LaetitiaGiftAggresive>();
                if (giftComp != null)
                {
                    giftComp.MarkFromEgoWeapon(caster);
                    giftComp.Notify_LaetitiaRifleHit(forceDetonate: true);
                }
            }

            if (!victim.Dead && !victim.Destroyed)
            {
                float beat1Dmg = Props.beat1DamageRange.RandomInRange;
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, beat1Dmg, 0.40f, yaw, caster, weapon: weaponDef));
            }

            if (!victim.Dead && !victim.Destroyed && Def.HediffDefOf.LaetitiaGiftAggresive != null)
            {
                Hediff hediff = victim.health?.GetOrAddHediff(Def.HediffDefOf.LaetitiaGiftAggresive);
                if (hediff != null)
                {
                    hediff.Severity = 1f;
                    hediff.TryGetComp<HediffComp_LaetitiaGiftAggresive>()?.MarkFromEgoWeapon(caster);
                }
            }

            if (Laetitia.Def.ThingDefOf.LC_LaetitiaSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(Laetitia.Def.ThingDefOf.LC_LaetitiaSkillRunnerFX, caster.PositionHeld, map) is Thing_LaetitiaSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, weaponDef);
                }
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("LC_EgoSkill_TargetMustBeAlive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (Pawn != null && pawn.Faction != null && pawn.Faction == Pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("Laetitia_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && Pawn != null && target.Pawn.HostileTo(Pawn);
        }
    }
}
