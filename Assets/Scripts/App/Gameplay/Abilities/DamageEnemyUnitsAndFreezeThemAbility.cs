﻿// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/


using LoomNetwork.CZB.Common;
using LoomNetwork.CZB.Data;

namespace LoomNetwork.CZB
{
    public class DamageEnemyUnitsAndFreezeThemAbility : AbilityBase
    {
        public int value;

        public DamageEnemyUnitsAndFreezeThemAbility(Enumerators.CardKind cardKind, AbilityData ability) : base(cardKind, ability)
        {
            value = ability.value;
        }

        public override void Activate()
        {
            base.Activate();

            if (abilityCallType != Enumerators.AbilityCallType.AT_START)
                return;

            Action();
        }

        public override void Update()
        {
            base.Update();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnInputEndEventHandler()
        {
            base.OnInputEndEventHandler();
        }

        protected override void UnitOnAttackEventHandler(object info, int damage)
        {
            base.UnitOnAttackEventHandler(info, damage);
        }

        public override void Action(object info = null)
        {
            base.Action(info);

            var opponent = playerCallerOfAbility.Equals(_gameplayManager.CurrentPlayer) ? _gameplayManager.OpponentPlayer : _gameplayManager.CurrentPlayer;

            foreach (var unit in opponent.BoardCards)
                _battleController.AttackUnitByAbility(GetCaller(), abilityData, unit);

            foreach (var unit in opponent.BoardCards)
                unit.Stun(Enumerators.StunType.FREEZE, value);
        }
    }
}