using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Loom.ZombieBattleground
{
    public class TakeControlEnemyUnitAbility : AbilityBase
    {
        private int Count { get; }

        private List<BoardUnitModel> _movedUnits;

        public TakeControlEnemyUnitAbility(Enumerators.CardKind cardKind, AbilityData ability)
            : base(cardKind, ability)
        {
            Count = ability.Count;
        }

        public override void Activate()
        {
            base.Activate();

            if (AbilityCallType != Enumerators.AbilityCallType.ENTRY)
                return;

            if (AbilityData.AbilitySubTrigger == Enumerators.AbilitySubTrigger.RandomUnit)
            {
                if (PredefinedTargets != null)
                {
                    TakeControlEnemyUnit(PredefinedTargets.Select(x => x.BoardObject as BoardUnitModel).ToList());
                }
                else
                {
                    TakeControlEnemyUnit(GetRandomEnemyUnits(Count));
                }
            }
        }

        protected override void InputEndedHandler()
        {
            base.InputEndedHandler();

            if (IsAbilityResolved)
            {
                InvokeActionTriggered();
            }
        }

        public override void Action(object info = null)
        {
            base.Action(info);

            TakeControlEnemyUnit(new List<BoardUnitModel>() { TargetUnit });
        }

        private void TakeControlEnemyUnit(List<BoardUnitModel> units)
        {
            _movedUnits = new List<BoardUnitModel>();

            foreach (BoardUnitModel unit in units)
            {
                if (PlayerCallerOfAbility.BoardCards.Count >= PlayerCallerOfAbility.MaxCardsInPlay)
                    break;

                _movedUnits.Add(unit);
            }

            InvokeActionTriggered(_movedUnits);
        }

        private void TakeControlEnemyUnitEnded()
        {
            foreach (BoardUnitModel unit in _movedUnits)
            {
                BattlegroundController.TakeControlUnit(PlayerCallerOfAbility, unit);
            }

            if (_movedUnits.Count > 0)
            {
                AbilitiesController.ThrowUseAbilityEvent(MainWorkingCard, _movedUnits.Cast<BoardObject>().ToList(), AbilityData.AbilityType,
                                                         Enumerators.AffectObjectType.Character);
            }
        }

        protected override void VFXAnimationEndedHandler()
        {
            base.VFXAnimationEndedHandler();

            TakeControlEnemyUnitEnded();
        }
    }
}
