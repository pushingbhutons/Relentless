using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Loom.ZombieBattleground
{
    public class FillBoardByUnitsAbility : AbilityBase
    {
        private int Cost { get; }

        public FillBoardByUnitsAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
            Cost = ability.Cost;
        }

        public override void Activate()
        {
            base.Activate();

            InvokeUseAbilityEvent();
        }

        protected override void UnitDiedHandler()
        {
            base.UnitDiedHandler();

            if (AbilityTrigger != Enumerators.AbilityTrigger.DEATH)
                return;

            foreach(Enumerators.Target target in AbilityData.Targets)
            {
                switch(target)
                {
                    case Enumerators.Target.OPPONENT:
                        FillBoard(GetOpponentOverlord());
                        break;
                    case Enumerators.Target.PLAYER:
                        FillBoard(PlayerCallerOfAbility);
                        break;
                }
            }

            if (AbilityUnitOwner.BuffsOnUnit.Contains(Enumerators.BuffType.REANIMATE))
                Activate();
        }

        private void FillBoard(Player targetPlayer)
        {
            if (!HasEmptySpaceOnBoard(targetPlayer, out int maxUnits))
                return;

            if(AbilityUnitOwner.HasActiveMechanic(Enumerators.GameMechanicDescription.Reanimate))
            {
                if(!AbilityUnitOwner.IsAlive())
                {
                    maxUnits--;
                }
            }

            List<Card> cards = DataManager.CachedCardsLibraryData.Cards.FindAll(card => card.Cost == Cost && card.Kind == Enumerators.CardKind.CREATURE);

            cards = GetRandomElements(cards, maxUnits);

            List<PastActionsPopup.TargetEffectParam> TargetEffects = new List<PastActionsPopup.TargetEffectParam>();

            BoardUnitModel boardUnit;
            for (int i = 0; i < cards.Count; i++)
            {
                if (targetPlayer.PlayerCardsController.CardsOnBoard.Count >= targetPlayer.MaxCardsInPlay)
                    break;

                boardUnit = targetPlayer.PlayerCardsController.SpawnUnitOnBoard(cards[i].Name, ItemPosition.End, IsPVPAbility).Model;

                TargetEffects.Add(new PastActionsPopup.TargetEffectParam()
                {
                    ActionEffectType = Enumerators.ActionEffectType.None,
                    Target = boardUnit
                });
            }

            if (TargetEffects.Count > 0)
            {
                ActionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
                {
                    ActionType = Enumerators.ActionType.CardAffectingMultipleCards,
                    Caller = GetCaller(),
                    TargetEffects = TargetEffects
                });
            }
        }
    }
}
