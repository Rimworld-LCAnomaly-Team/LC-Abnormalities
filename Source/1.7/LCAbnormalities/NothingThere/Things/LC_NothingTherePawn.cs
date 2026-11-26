using LCAnomalyCore.Comp;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using NothingThere.Comp;
using RimWorld;
using UnityEngine;
using Verse;

namespace NothingThere.Things
{
    /// <summary>
    /// 一无所有（O-06-20）实体 Pawn：实现三阶段四色抗性与二/三阶段红伤完全免疫。
    /// </summary>
    public class LC_NothingTherePawn : LC_EntityBasePawn, ILCFourColorDynamicResistance
    {
        public CompNothingThere SelfEntityComp => (CompNothingThere)EntityComp;

        private int lastImmuneMoteTick = -999;
        private bool facingWest = false;

        /// <summary>一无所有当前是否应渲染为面向左侧（防止斜向南北移动时朝向突变）。</summary>
        public bool IsFacingLeft => facingWest;

        public LC_NothingTherePawn()
        {
        }

        /// <summary>转向指定目标格子，并同步锁定左右水平朝向。</summary>
        public void FaceCellHorizontal(IntVec3 cell)
        {
            rotationTracker?.FaceCell(cell);
            if (Spawned && cell.IsValid)
            {
                if (cell.x < Position.x)
                {
                    facingWest = true;
                }
                else if (cell.x > Position.x)
                {
                    facingWest = false;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref facingWest, "facingWest", defaultValue: false);
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0f)
            {
                SelfEntityComp?.Notify_TookDamage();
            }
        }

        public bool TryGetDynamicFourColorResistance(FourColorDamageType damageType, out float resistance)
        {
            NothingThereForm form = SelfEntityComp?.CurrentForm ?? NothingThereForm.Contained;
            switch (form)
            {
                case NothingThereForm.Form2_Cocoon:
                    switch (damageType)
                    {
                        case FourColorDamageType.Red: resistance = 0.0f; return true;
                        case FourColorDamageType.White: resistance = 0.6f; return true;
                        case FourColorDamageType.Black: resistance = 0.6f; return true;
                        case FourColorDamageType.Pale: resistance = 1.0f; return true;
                    }
                    break;
                case NothingThereForm.Form3_TrueForm:
                    switch (damageType)
                    {
                        case FourColorDamageType.Red: resistance = 0.0f; return true;
                        case FourColorDamageType.White: resistance = 0.4f; return true;
                        case FourColorDamageType.Black: resistance = 0.4f; return true;
                        case FourColorDamageType.Pale: resistance = 0.8f; return true;
                    }
                    break;
                case NothingThereForm.Contained:
                case NothingThereForm.Form1_Disguised:
                default:
                    switch (damageType)
                    {
                        case FourColorDamageType.Red: resistance = 0.3f; return true;
                        case FourColorDamageType.White: resistance = 0.8f; return true;
                        case FourColorDamageType.Black: resistance = 0.8f; return true;
                        case FourColorDamageType.Pale: resistance = 1.2f; return true;
                    }
                    break;
            }

            resistance = 1f;
            return false;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);
            FourColorDamageExtension ext = dinfo.Def?.GetModExtension<FourColorDamageExtension>();
            if (absorbed || dinfo.Def == null || (ext == null && !dinfo.Def.harmsHealth))
            {
                return;
            }

            bool isRedOrPhysical = (ext != null && ext.damageType == FourColorDamageType.Red)
                || (ext == null
                    && dinfo.Def != RimWorld.DamageDefOf.ExecutionCut
                    && dinfo.Def != RimWorld.DamageDefOf.SurgicalCut
                    && dinfo.Def != RimWorld.DamageDefOf.Stun
                    && dinfo.Def != RimWorld.DamageDefOf.EMP);

            NothingThereForm form = SelfEntityComp?.CurrentForm ?? NothingThereForm.Contained;

            if (form == NothingThereForm.Form2_Cocoon || form == NothingThereForm.Form3_TrueForm)
            {
                // 二阶段肉茧与三阶段完全体完全免疫物理/红伤 (R=0.0)
                if (isRedOrPhysical)
                {
                    absorbed = true;
                    EmitImmuneFeedback();
                }
                return;
            }

            // Form1 与收容态下的原版物理伤害沿用 R=0.3；四色伤害已由 DamageUtils 通过 ILCFourColorDynamicResistance 统一计算
            if (ext == null && isRedOrPhysical)
            {
                dinfo.SetAmount(dinfo.Amount * 0.3f);
            }
        }

        private void EmitImmuneFeedback()
        {
            if (MapHeld == null || !Spawned)
            {
                return;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            if (ticksGame - lastImmuneMoteTick < 25)
            {
                return;
            }

            lastImmuneMoteTick = ticksGame;
            FleckMaker.ThrowMicroSparks(DrawPos, MapHeld);
            DamageUtils.SpawnFourColorHitIndicator(FourColorDamageType.Red, DrawPos, MapHeld, 0f, 0f);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad && SelfEntityComp != null && SelfEntityComp.Escaped)
            {
                SelfEntityComp.SyncFormStateAfterLoad();
                Drawer?.renderer?.SetAllGraphicsDirty();
            }

            UpdateAnimation(force: true);
        }

        public void PlayDogAttackAnimation() => PlayDogAttackAnimation(variant2: false);

        public void PlayDogAttackAnimation(bool variant2)
        {
            AnimationDef anim = (variant2 && Def.AnimationDefOf.NothingThere_DogAttack2 != null)
                ? Def.AnimationDefOf.NothingThere_DogAttack2
                : Def.AnimationDefOf.NothingThere_DogAttack;
            if (anim != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(anim);
            }
        }

        public void PlayMeleeAnimation()
        {
            if (Def.AnimationDefOf.NothingThere_TrueMelee != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.NothingThere_TrueMelee);
            }
        }

        public void PlayThrowAnimation()
        {
            if (Def.AnimationDefOf.NothingThere_TrueThrow != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.NothingThere_TrueThrow);
            }
        }

        public void PlayGoodbyeAnimation()
        {
            if (Def.AnimationDefOf.NothingThere_TrueGoodbye != null && Drawer?.renderer != null)
            {
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.NothingThere_TrueGoodbye);
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompNothingThere comp = SelfEntityComp;
            NothingThereForm form = comp?.CurrentForm ?? NothingThereForm.Contained;
            bool isMoving = Spawned && pather?.Moving == true;
            AnimationDef desired;

            switch (form)
            {
                case NothingThereForm.Form1_Disguised:
                    if (comp != null && comp.HasDisguise)
                    {
                        desired = isMoving
                            ? Def.AnimationDefOf.NothingThere_DisguiseWalk
                            : Def.AnimationDefOf.NothingThere_DisguiseIdle;
                    }
                    else if (comp != null && comp.AttackAnimTicks > 0)
                    {
                        desired = (comp.UseDogAttackVariant2 && Def.AnimationDefOf.NothingThere_DogAttack2 != null)
                            ? Def.AnimationDefOf.NothingThere_DogAttack2
                            : Def.AnimationDefOf.NothingThere_DogAttack;
                    }
                    else
                    {
                        desired = isMoving
                            ? Def.AnimationDefOf.NothingThere_DogWalk
                            : Def.AnimationDefOf.NothingThere_DogIdle;
                    }
                    break;

                case NothingThereForm.Form2_Cocoon:
                    desired = Def.AnimationDefOf.NothingThere_CocoonIdle;
                    break;

                case NothingThereForm.Form3_TrueForm:
                    if (comp != null && comp.IsPerformingGoodbye)
                    {
                        desired = Def.AnimationDefOf.NothingThere_TrueGoodbye;
                    }
                    else if (comp != null && comp.IsThrowingBoneSpike)
                    {
                        desired = Def.AnimationDefOf.NothingThere_TrueThrow;
                    }
                    else if (comp != null && comp.AttackAnimTicks > 0)
                    {
                        desired = Def.AnimationDefOf.NothingThere_TrueMelee;
                    }
                    else
                    {
                        desired = isMoving
                            ? Def.AnimationDefOf.NothingThere_TrueWalk
                            : Def.AnimationDefOf.NothingThere_TrueIdle;
                    }
                    break;

                case NothingThereForm.Contained:
                default:
                    if (comp != null && comp.HasDisguise)
                    {
                        desired = Def.AnimationDefOf.NothingThere_DisguiseIdle;
                    }
                    else
                    {
                        desired = (comp != null && comp.IsBeingStudiedNow)
                            ? Def.AnimationDefOf.NothingThere_DogWork
                            : Def.AnimationDefOf.NothingThere_DogIdle;
                    }
                    break;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (phase == DrawPhase.EnsureInitialized && SelfEntityComp != null && SelfEntityComp.HasDisguise)
            {
                SelfEntityComp.EnsureDisguiseMaterialInitialized();
            }

            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        public override void TickHolded()
        {
            base.TickHolded();
            facingWest = false;
            SelfEntityComp?.TickContained();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            if (IsContainedInRoom)
            {
                TickHolded();
                if (Spawned && !Dead && pather?.Moving == true)
                {
                    UpdateHorizontalFacing();
                }
                base.Tick();
                return;
            }

            // 若在未正式出逃状态下离开收容平台，立即转入出逃状态
            if (Spawned && CarriedBy == null && SelfEntityComp != null && !SelfEntityComp.Escaped)
            {
                SelfEntityComp.HoldingPlaformTargetComp?.Escape();
            }

            if (SelfEntityComp != null)
            {
                if ((SelfEntityComp.CurrentForm == NothingThereForm.Form2_Cocoon
                        || SelfEntityComp.IsPerformingGoodbye
                        || SelfEntityComp.IsThrowingBoneSpike)
                    && pather?.Moving == true)
                {
                    pather.StopDead();
                }

                if (SelfEntityComp.Escaped && Spawned && !Dead)
                {
                    UpdateHorizontalFacing();
                }
                else if (!SelfEntityComp.Escaped)
                {
                    facingWest = false;
                }
            }

            SelfEntityComp?.TickBreachCombat();
            UpdateAnimation();
            base.Tick();
        }

        private void UpdateHorizontalFacing()
        {
            if (pather != null && pather.Moving && pather.nextCell.IsValid)
            {
                if (pather.nextCell.x < Position.x)
                {
                    facingWest = true;
                    return;
                }
                if (pather.nextCell.x > Position.x)
                {
                    facingWest = false;
                    return;
                }
            }

            if (Rotation == Rot4.West)
            {
                facingWest = true;
            }
            else if (Rotation == Rot4.East)
            {
                facingWest = false;
            }
        }
    }
}
