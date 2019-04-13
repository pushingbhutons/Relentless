using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using System.Collections.Generic;

namespace Loom.ZombieBattleground
{
    public class GiveBuffsToUnitAbility : AbilityBase
    {
        public GiveBuffsToUnitAbility(Enumerators.CardKind cardKind, AbilityData ability) : base(cardKind, ability)
        {
        }

        public override void Activate()
        {
            base.Activate();

            if (AbilityTrigger != Enumerators.AbilityTrigger.ENTRY || AbilityActivity != Enumerators.AbilityActivity.PASSIVE)
                return;

            CheckSubTriggers();
        }

        protected override void InputEndedHandler()
        {
            base.InputEndedHandler();

            if (IsAbilityResolved)
            {
                GiveBuffsToUnit(TargetUnit);
            }
        }

        private void CheckSubTriggers()
        {
            if (AbilityData.SubTrigger == Enumerators.AbilitySubTrigger.OnlyThisUnitInPlay)
            {
                if (PlayerCallerOfAbility.PlayerCardsController.CardsOnBoard.FindAll(card => card != AbilityUnitOwner).Count == 0)
                {
                    GiveBuffsToUnit(AbilityUnitOwner);
                }
            }
        }

        private void GiveBuffsToUnit(CardModel unit)
        {
            List<PastActionsPopup.TargetEffectParam> TargetEffects = new List<PastActionsPopup.TargetEffectParam>();
            Enumerators.ActionEffectType actionEffectType = Enumerators.ActionEffectType.None;
            foreach (Enumerators.GameMechanicDescription type in AbilityData.TargetGameMechanicDescriptions)
            {
                switch (type)
                {
                    case Enumerators.GameMechanicDescription.Guard:
                        unit.AddBuffShield();
                        actionEffectType = Enumerators.ActionEffectType.Guard;
                        break;
                    case Enumerators.GameMechanicDescription.Destroy:
                        unit.AddBuff(Enumerators.BuffType.DESTROY);
                        actionEffectType = Enumerators.ActionEffectType.DeathMark;
                        break;
                    case Enumerators.GameMechanicDescription.Reanimate:
                        unit.AddBuff(Enumerators.BuffType.REANIMATE);
                        actionEffectType = Enumerators.ActionEffectType.Reanimate;
                        break;
                    case Enumerators.GameMechanicDescription.Heavy:
                        unit.SetAsHeavyUnit();
                        actionEffectType = Enumerators.ActionEffectType.Heavy;
                        break;
                    case Enumerators.GameMechanicDescription.Feral:
                        unit.SetAsFeralUnit();
                        actionEffectType = Enumerators.ActionEffectType.Feral;
                        break;
                    case Enumerators.GameMechanicDescription.SwingX:
                        unit.AddBuffSwing();
                        actionEffectType = Enumerators.ActionEffectType.Swing;
                        break;
                }

                TargetEffects.Add(new PastActionsPopup.TargetEffectParam()
                {
                    ActionEffectType = actionEffectType,
                    Target = unit,
                });
            }

            if (TargetEffects.Count > 0)
            {
                ActionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
                {
                    ActionType = Enumerators.ActionType.CardAffectingCard,
                    Caller = AbilityUnitOwner,
                    TargetEffects = TargetEffects
                });
            }
        }
    }
}
