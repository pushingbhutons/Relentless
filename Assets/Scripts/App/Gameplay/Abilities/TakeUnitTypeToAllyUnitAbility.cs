using System;
using System.Collections.Generic;
using System.Linq;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Random = UnityEngine.Random;
using UnityEngine;

namespace Loom.ZombieBattleground
{
    public class TakeUnitTypeToAllyUnitAbility : AbilityBase
    {
        public Enumerators.CardType UnitType;
        public Enumerators.Faction Faction;

        public int Cost { get; }

        public TakeUnitTypeToAllyUnitAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
            UnitType = ability.TargetUnitType;
            Faction = ability.Faction;
            Cost = ability.Cost;
        }

        public override void Activate()
        {
            base.Activate();

            InvokeUseAbilityEvent();

            if (AbilityTrigger != Enumerators.AbilityTrigger.ENTRY)
                return;

            Action();
        }

        protected override void UnitDiedHandler()
        {
            base.UnitDiedHandler();

            if (AbilityTrigger != Enumerators.AbilityTrigger.DEATH)
                return;

            Action();
        }

        public override void Action(object info = null)
        {
            base.Action(info);

            Enumerators.ActionEffectType effectType = Enumerators.ActionEffectType.None;

            if (UnitType == Enumerators.CardType.FERAL)
            {
                effectType = Enumerators.ActionEffectType.Feral;
            }
            else if (UnitType == Enumerators.CardType.HEAVY)
            {
                effectType = Enumerators.ActionEffectType.Heavy;
            }

            List<PastActionsPopup.TargetEffectParam> targetEffects = new List<PastActionsPopup.TargetEffectParam>();

            switch (AbilityData.SubTrigger)
            {
                case Enumerators.AbilitySubTrigger.RandomUnit:
                    {
                        List<CardModel> allies;

                        allies = PlayerCallerOfAbility.CardsOnBoard
                        .Where(unit => unit != AbilityUnitOwner && unit.InitialUnitType != UnitType && !unit.IsDead)
                        .ToList();

                        if (allies.Count > 0)
                        {
                            int random = MTwister.IRandom(0, allies.Count - 1);

                            TakeTypeToUnit(allies[random]);

                            targetEffects.Add(new PastActionsPopup.TargetEffectParam()
                            {
                                ActionEffectType = effectType,
                                Target = allies[random]
                            });
                        }
                    }
                    break;
                case Enumerators.AbilitySubTrigger.OnlyThisUnitInPlay:
                    if (PlayerCallerOfAbility.CardsOnBoard.Where(
                            unit => unit != AbilityUnitOwner &&
                                !unit.IsDead &&
                                unit.CurrentDefense > 0)
                        .Count() == 0)
                    {
                        targetEffects.Add(new PastActionsPopup.TargetEffectParam()
                        {
                            ActionEffectType = effectType,
                            Target = AbilityUnitOwner
                        });

                        TakeTypeToUnit(AbilityUnitOwner);
                    }
                    break;
                case Enumerators.AbilitySubTrigger.AllOtherAllyUnitsInPlay:
                    {
                        List<CardModel> allies = PlayerCallerOfAbility.CardsOnBoard
                           .Where(unit => unit != AbilityUnitOwner &&
                                   (unit.Card.Prototype.Faction == Faction || Faction == Enumerators.Faction.Undefined) &&
                                   unit.InitialUnitType != UnitType && !unit.IsDead)
                           .ToList();

                        foreach(CardModel unit in allies)
                        {
                            TakeTypeToUnit(unit);

                            targetEffects.Add(new PastActionsPopup.TargetEffectParam()
                            {
                                ActionEffectType = effectType,
                                Target = unit
                            });
                        }
                    }
                    break;
                case Enumerators.AbilitySubTrigger.AllyUnitsByFactionThatCost:
                    {
                        List<CardModel> allies = PlayerCallerOfAbility.CardsOnBoard
                               .Where(unit => unit != AbilityUnitOwner && unit.Card.Prototype.Faction == Faction &&
                                      unit.Card.InstanceCard.Cost <= Cost && unit.InitialUnitType != UnitType && !unit.IsDead)
                               .ToList();

                        foreach (CardModel unit in allies)
                        {
                            TakeTypeToUnit(unit);

                            targetEffects.Add(new PastActionsPopup.TargetEffectParam()
                            {
                                ActionEffectType = effectType,
                                Target = unit
                            });
                        }
                    }
                    break;
                case Enumerators.AbilitySubTrigger.AllAllyUnitsInPlay:
                    {
                        List<CardModel> allies = PlayerCallerOfAbility.CardsOnBoard.Where(
                                       unit => unit != AbilityUnitOwner &&
                                           !unit.IsDead &&
                                           unit.CurrentDefense > 0).ToList();

                        foreach (CardModel unit in allies)
                        {
                            TakeTypeToUnit(unit);

                            targetEffects.Add(new PastActionsPopup.TargetEffectParam()
                            {
                                ActionEffectType = effectType,
                                Target = unit
                            });
                        }
                    }
                    break;
            }


            if (targetEffects.Count > 0)
            {
                Enumerators.ActionType actionType = Enumerators.ActionType.CardAffectingMultipleCards;

                if (targetEffects.Count == 1)
                {
                    actionType = Enumerators.ActionType.CardAffectingCard;
                }

                ActionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
                {
                    ActionType = actionType,
                    Caller = AbilityUnitOwner,
                    TargetEffects = targetEffects
                });
            }
        }

        private void TakeTypeToUnit(CardModel unit)
        {
            if (unit == null)
                return;

            switch (UnitType)
            {
                case Enumerators.CardType.HEAVY:
                    unit.SetAsHeavyUnit();
                    break;
                case Enumerators.CardType.FERAL:
                    unit.SetAsFeralUnit();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(UnitType), UnitType, null);
            }
        }
    }
}
