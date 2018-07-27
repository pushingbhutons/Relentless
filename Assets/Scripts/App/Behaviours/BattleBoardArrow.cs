// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using System.Collections.Generic;

namespace LoomNetwork.CZB
{
    public class BattleBoardArrow : BoardArrow
    {
        public List<object> ignoreBoardObjectsList;
        public List<BoardUnit> BoardCards;
        public BoardUnit owner;

        public bool ignoreHeavy = false;

        private void Awake()
        {
            Init();
        }

        public void End(BoardUnit creature)
        {
            if (!startedDrag)
            {
                return;
            }

            startedDrag = false;

            creature.DoCombat(selectedCard != null ? (object)selectedCard : (object)selectedPlayer);
            Destroy(gameObject);
        }

        public override void OnCardSelected(BoardUnit unit)
        {
            if (_gameplayManager.IsTutorial && (_gameplayManager.TutorialStep == 19 || _gameplayManager.TutorialStep == 27))
                return;

            if (ignoreBoardObjectsList != null && ignoreBoardObjectsList.Contains(unit))
                return;

            if (targetsType.Contains(Common.Enumerators.SkillTargetType.ALL_CARDS) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.PLAYER_CARD) && unit.transform.CompareTag("PlayerOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.OPPONENT_CARD) && unit.transform.CompareTag("OpponentOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.OPPONENT) && unit.transform.CompareTag("OpponentOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.PLAYER) && unit.transform.CompareTag("PlayerOwned")))
            {
                var opponentHasProvoke = OpponentBoardContainsProvokingCreatures();
                if (!opponentHasProvoke || (opponentHasProvoke && unit.IsHeavyUnit()) || ignoreHeavy)
                {
                    if(selectedCard != null)
                        selectedCard.SetSelectedUnit(false);

                    selectedCard = unit;
                    if (selectedPlayer != null)
                        selectedPlayer.SetGlowStatus(false);
                    selectedPlayer = null;
                    selectedCard.SetSelectedUnit(true);
                    CreateTarget(unit.transform.position);
                }
            }
        }

        public override void OnCardUnselected(BoardUnit creature)
        {
            if (selectedCard == creature)
            {
                selectedCard.SetSelectedUnit(false);
                //  _targetObjectsGroup.SetActive(false);
                selectedCard = null;
            }
        }

        public override void OnPlayerSelected(Player player)
        {
            if (_gameplayManager.IsTutorial && (_gameplayManager.TutorialStep != 19 &&
                                                _gameplayManager.TutorialStep != 28 &&
                                                _gameplayManager.TutorialStep != 31))
                return;

            if (ignoreBoardObjectsList != null && ignoreBoardObjectsList.Contains(player))
                return;

            if (owner != null && !owner.hasFeral && owner.HasBuffRush)
                return;

            if (targetsType.Contains(Common.Enumerators.SkillTargetType.ALL_CARDS) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.PLAYER_CARD) && player.AvatarObject.CompareTag("PlayerOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.OPPONENT_CARD) && player.AvatarObject.CompareTag("OpponentOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.OPPONENT) && player.AvatarObject.CompareTag("OpponentOwned")) ||
                (targetsType.Contains(Common.Enumerators.SkillTargetType.PLAYER) && player.AvatarObject.CompareTag("PlayerOwned")))
            {
                var opponentHasProvoke = OpponentBoardContainsProvokingCreatures();
                if (!opponentHasProvoke || ignoreHeavy) 
                {
                    selectedPlayer = player;

                    selectedPlayer.SetGlowStatus(true);
                    if (selectedCard != null)
                        selectedCard.SetSelectedUnit(false);
                    selectedCard = null;
                    CreateTarget(player.AvatarObject.transform.position);
                }
            }
        }

        public override void OnPlayerUnselected(Player player)
        {
            if (selectedPlayer == player)
            {
                if (selectedCard != null)
                    selectedCard.SetSelectedUnit(false);
                selectedCard = null;

                selectedPlayer.SetGlowStatus(false);
                //_targetObjectsGroup.SetActive(false);
                selectedPlayer = null;
            }
        }

        protected bool OpponentBoardContainsProvokingCreatures()
        {
            var provokeCards = BoardCards.FindAll(x => x.IsHeavyUnit());
            return provokeCards.Count > 0;
        }
    }
}