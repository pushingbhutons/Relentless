// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using LoomNetwork.CZB.Common;
using LoomNetwork.CZB.Data;
using System.Collections.Generic;

namespace LoomNetwork.CZB
{
    public class BattleController : IController
    {
        private IGameplayManager _gameplayManager;
        private ITutorialManager _tutorialManager;

        private ActionsQueueController _actionsQueueController;
        private AbilitiesController _abilitiesController;

        private Dictionary<Enumerators.SetType, Enumerators.SetType> _strongerElemental,
                                                                     _weakerElemental;

        public void Dispose()
        {
        }

        public void Init()
        {
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _tutorialManager = GameClient.Get<ITutorialManager>();

            _actionsQueueController = _gameplayManager.GetController<ActionsQueueController>();
            _abilitiesController = _gameplayManager.GetController<AbilitiesController>();

            FillStrongersAndWeakers();
        }

        public void Update()
        {
        }


        private void FillStrongersAndWeakers()
        {
            _strongerElemental = new Dictionary<Enumerators.SetType, Enumerators.SetType>()
            {
                { Enumerators.SetType.FIRE, Enumerators.SetType.TOXIC },
                { Enumerators.SetType.TOXIC, Enumerators.SetType.LIFE },
                { Enumerators.SetType.LIFE, Enumerators.SetType.EARTH },
                { Enumerators.SetType.EARTH, Enumerators.SetType.AIR },
                { Enumerators.SetType.AIR, Enumerators.SetType.WATER },
                { Enumerators.SetType.WATER, Enumerators.SetType.FIRE },
            };

            _weakerElemental = new Dictionary<Enumerators.SetType, Enumerators.SetType>()
            {
                { Enumerators.SetType.FIRE, Enumerators.SetType.WATER },
                { Enumerators.SetType.TOXIC, Enumerators.SetType.FIRE },
                { Enumerators.SetType.LIFE, Enumerators.SetType.TOXIC },
                { Enumerators.SetType.EARTH, Enumerators.SetType.LIFE },
                { Enumerators.SetType.AIR, Enumerators.SetType.EARTH },
                { Enumerators.SetType.WATER, Enumerators.SetType.AIR },
            };
        }

        private int GetStrongersAndWeakersModifier(Enumerators.SetType attackerElement, Enumerators.SetType defenderElement)
        {
            int modifier = 0;

            if (_strongerElemental.ContainsKey(attackerElement) && _strongerElemental[attackerElement].Equals(defenderElement))
                modifier++;
            else if(_weakerElemental.ContainsKey(attackerElement) && _weakerElemental[attackerElement].Equals(defenderElement))
                modifier--;

            return modifier;
        }

        public void AttackPlayerByCreature(BoardUnit attackingUnit, Player attackedPlayer)
        {
            if (attackingUnit != null && attackedPlayer != null)
            {
                attackedPlayer.HP -= attackingUnit.CurrentDamage;
            }

            _tutorialManager.ReportAction(Enumerators.TutorialReportAction.ATTACK_CARD_HERO);

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_PLAYER_BY_CREATURE,
            new object[]
            {
                attackingUnit,
                attackedPlayer
            }));
        }

        public void AttackCreatureByCreature(BoardUnit attackingUnit, BoardUnit attackedUnit)
        {
            int damageAttacked = 0;
            int damageAttacking = 0;

            if (attackingUnit != null && attackedUnit != null)
            {
                int additionalDamageAttacker = _abilitiesController.GetStatModificatorByAbility(attackingUnit, attackedUnit);
                int additionalDamageAttacked = _abilitiesController.GetStatModificatorByAbility(attackedUnit, attackingUnit);

                additionalDamageAttacker += GetStrongersAndWeakersModifier(attackingUnit.Card.libraryCard.cardSetType, attackedUnit.Card.libraryCard.cardSetType);
                additionalDamageAttacked += GetStrongersAndWeakersModifier(attackedUnit.Card.libraryCard.cardSetType, attackingUnit.Card.libraryCard.cardSetType);

                damageAttacking = attackingUnit.CurrentDamage + additionalDamageAttacker;

                if (attackedUnit.HasBuffShield)
                {
                    damageAttacking = 0;
                    attackedUnit.UseShieldFromBuff();
                }

                attackedUnit.CurrentHP -= damageAttacking;

                if (damageAttacking > 0)
                    attackedUnit.ThrowEventGotDamage(attackingUnit);

                damageAttacked = attackedUnit.CurrentDamage + additionalDamageAttacked;

                if (attackingUnit.HasBuffShield)
                {
                    damageAttacked = 0;
                    attackingUnit.UseShieldFromBuff();
                }

                attackingUnit.CurrentHP -= damageAttacked;
            }

            _tutorialManager.ReportAction(Enumerators.TutorialReportAction.ATTACK_CARD_CARD);

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_CREATURE_BY_CREATURE,
            new object[]
            {
                attackingUnit,
                damageAttacking,
                attackedUnit,
                damageAttacked
            }));
        }

        public void AttackCreatureBySkill(Player attackingPlayer, HeroSkill skill, BoardUnit attackedUnit, int modifier)
        {
            int damage = (skill.value + modifier);

            if (attackedUnit != null)
            {
                if (attackedUnit.HasBuffShield)
                {
                    damage = 0;
                    attackedUnit.UseShieldFromBuff();
                }

                attackedUnit.CurrentHP -= damage;

                if (damage > 0)
                    attackedUnit.ThrowEventGotDamage(attackingPlayer);
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_CREATURE_BY_SKILL,
            new object[]
            {
                attackingPlayer,
                skill,
                damage,
                attackedUnit
            }));
        }

        public void AttackPlayerBySkill(Player attackingPlayer, HeroSkill skill, Player attackedPlayer)
        {
            if (attackedPlayer != null)
            {
                attackedPlayer.HP -= skill.value;
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_PLAYER_BY_SKILL,
            new object[]
            {
                attackingPlayer,
                skill,
                attackedPlayer
            }));
        }

        public void HealPlayerBySkill(Player healingPlayer, HeroSkill skill, Player healedPlayer)
        {
            if (healingPlayer != null)
            {
                //if(healingPlayer.SelfHero.heroElement == Enumerators.SetType.EARTH)
                healedPlayer.HP += skill.value;
                if (healingPlayer.HP > Constants.DEFAULT_PLAYER_HP)
                    healingPlayer.HP = Constants.DEFAULT_PLAYER_HP;
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.HEAL_PLAYER_BY_SKILL,
            new object[]
            {
                healedPlayer,
                skill,
                healedPlayer
            }));
        }

        public void HealCreatureBySkill(Player healingPlayer, HeroSkill skill, BoardUnit healedCreature)
        {
            if (healedCreature != null)
            {
                healedCreature.CurrentHP += skill.value;
                if (healedCreature.CurrentHP > healedCreature.Card.initialHealth)
                    healedCreature.CurrentHP = healedCreature.Card.initialHealth;
            }
            

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.HEAL_CREATURE_BY_SKILL,
            new object[]
            {
                healingPlayer,
                skill,
                healedCreature
            }));
        }
    
        public void AttackCreatureByAbility(object attacker, AbilityData ability, BoardUnit attackedUnit)
        {
            int damage = ability.value;

            if (attackedUnit != null)
            {
                if (attackedUnit.HasBuffShield)
                {
                    damage = 0;
                    attackedUnit.UseShieldFromBuff();
                }

                attackedUnit.CurrentHP -= damage;

                if (damage > 0)
                    attackedUnit.ThrowEventGotDamage(attacker);
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_CREATURE_BY_ABILITY,
            new object[]
            {
                attacker,
                ability,
                damage,
                attackedUnit,
            }));
        }

        public void AttackPlayerByAbility(object attacker, AbilityData ability, Player attackedPlayer)
        {
            if (attackedPlayer != null)
            {
                attackedPlayer.HP -= ability.value;
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.ATTACK_PLAYER_BY_ABILITY,
            new object[]
            {
                attacker,
                ability,
                ability.value,
                attackedPlayer
            }));
        }

        public void HealPlayerByAbility(object healler, AbilityData ability, Player healedPlayer)
        {
            if (healedPlayer != null)
            {
                healedPlayer.HP += ability.value;
                if (healedPlayer.HP > Constants.DEFAULT_PLAYER_HP)
                    healedPlayer.HP = Constants.DEFAULT_PLAYER_HP;
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.HEAL_PLAYER_BY_ABILITY,
            new object[]
            {
                healler,
                ability,
                ability.value,
                healedPlayer
            }));
        }

        public void HealCreatureByAbility(object healler, AbilityData ability, BoardUnit healedCreature)
        {
            if (healedCreature != null)
            {
                healedCreature.CurrentHP += ability.value;
                if (healedCreature.CurrentHP > healedCreature.Card.initialHealth)
                    healedCreature.CurrentHP = healedCreature.Card.initialHealth;
            }

            _actionsQueueController.PostGameActionReport(_actionsQueueController.FormatGameActionReport(Enumerators.ActionType.HEAL_CREATURE_BY_ABILITY,
            new object[]
            {
                healler,
                ability,
                ability.value,
                healedCreature
            }));
        }
    }
}