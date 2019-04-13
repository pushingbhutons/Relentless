using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Protobuf;
using NUnit.Framework;
using UnityEngine;
using InstanceId = Loom.ZombieBattleground.Data.InstanceId;
using NotImplementedException = System.NotImplementedException;

namespace Loom.ZombieBattleground.Test
{
    /// <summary>
    /// Initiates gameplay actions on the local client.
    /// </summary>
    public class LocalClientPlayerActionTestProxy : IPlayerActionTestProxy
    {
        private static readonly ILog Log = Logging.GetLog(nameof(LocalClientPlayerActionTestProxy));

        private readonly TestHelper _testHelper;
        private readonly IPvPManager _pvpManager;
        private readonly INetworkMessageSendManager _networkMessageSendManager;
        private readonly BackendDataControlMediator _backendDataControlMediator;

        private readonly Queue<CardAbilityRequest> _cardAbilityRequestsQueue = new Queue<CardAbilityRequest>();

        public LocalClientPlayerActionTestProxy(TestHelper testHelper)
        {
            _testHelper = testHelper;

            _pvpManager = GameClient.Get<IPvPManager>();
            _networkMessageSendManager = GameClient.Get<INetworkMessageSendManager>();
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();
        }

        public async Task EndTurn()
        {
            await Task.Delay(3000);
            await _testHelper.EndTurn();
            await Task.Delay(1000);
        }

        public Task LeaveMatch()
        {
            throw new NotImplementedException();
        }

        public Task Mulligan(IEnumerable<InstanceId> cards)
        {
            throw new NotImplementedException();
        }

        public async Task CardPlay(InstanceId card, ItemPosition position, InstanceId? entryAbilityTarget = null, bool skipEntryAbilities = false, bool forceSkipForPlayerToo = false)
        {
            IBoardObject entryAbilityTargetBoardObject = null;
            if (entryAbilityTarget != null)
            {
                entryAbilityTargetBoardObject = _testHelper.BattlegroundController.GetBoardObjectByInstanceId(entryAbilityTarget.Value);
                if (entryAbilityTargetBoardObject == null)
                    throw new Exception($"'Entry ability target with instance ID {entryAbilityTarget.Value}' not found on board");
            }
            CardModel cardModel = _testHelper.BattlegroundController.GetCardModelByInstanceId(card);

            if (!forceSkipForPlayerToo)
            {
                skipEntryAbilities = false;
            }

            await _testHelper.PlayCardFromHandToBoard(cardModel, position, entryAbilityTargetBoardObject, skipEntryAbilities);
        }

        public Task RankBuff(InstanceId card, IEnumerable<InstanceId> units)
        {
            return Task.CompletedTask;
        }

        public Task CardAbilityUsed(
            InstanceId card,
            Enumerators.AbilityType abilityType,
            IReadOnlyList<ParametrizedAbilityInstanceId> targets = null)
        {
            _cardAbilityRequestsQueue.Enqueue(new CardAbilityRequest(card, abilityType, targets));
            HandleNextCardAbility();
            return Task.CompletedTask;
        }

        public async Task OverlordSkillUsed(SkillId skillId, IReadOnlyList<ParametrizedAbilityInstanceId> targets = null)
        {
            List<ParametrizedAbilityBoardObject> targetBoardObjects = targets != null ? targets.Select(target =>
            {
                return new ParametrizedAbilityBoardObject(_testHelper.BattlegroundController.GetBoardObjectByInstanceId(target.Id), target.Parameters);
            }).ToList() : null;
            BoardSkill boardSkill = _testHelper.GetBoardSkill(_testHelper.GetCurrentPlayer(), skillId);
            await _testHelper.DoBoardSkill(boardSkill, targetBoardObjects);
        }

        public async Task CardAttack(InstanceId attacker, InstanceId target)
        {
            CardModel cardModel = _testHelper.GetCardModelByInstanceId(attacker, Enumerators.MatchPlayer.CurrentPlayer);
            BoardUnitView boardUnitView = _testHelper.BattlegroundController.GetCardViewByModel<BoardUnitView>(cardModel);

            void CheckAttacker()
            {
                Assert.NotNull(cardModel.OwnerPlayer, "boardUnitView.Model.OwnerPlayer != null");
                Assert.True(cardModel.OwnerPlayer.IsLocalPlayer, "boardUnitView.Model.OwnerPlayer != null");
                Assert.True(_testHelper.GameplayManager.GetController<PlayerController>().IsActive, "PlayerController.IsActive");
                Assert.True(cardModel.UnitCanBeUsable(), "boardUnitView.Model.UnitCanBeUsable()");
            }

            CheckAttacker();

            await new WaitUntil(() =>
            {
                AsyncTestRunner.Instance.ThrowIfCancellationRequested();
                return boardUnitView.ArrivalDone;
            });

            boardUnitView.StartAttackTargeting();
            Assert.NotNull(boardUnitView.FightTargetingArrow, "boardUnitView.FightTargetingArrow != null");
            await _testHelper.SelectTargetOnFightTargetArrow(boardUnitView.FightTargetingArrow, _testHelper.BattlegroundController.GetTargetByInstanceId(target));
            CheckAttacker();
            boardUnitView.FinishAttackTargeting();

            await Task.Delay(1000);
        }

        public Task CheatDestroyCardsOnBoard(IEnumerable<InstanceId> targets)
        {
            MatchRequestFactory matchRequestFactory = new MatchRequestFactory(_pvpManager.MatchMetadata.Id);
            PlayerActionFactory playerActionFactory = new PlayerActionFactory(_backendDataControlMediator.UserDataModel.UserId);
            PlayerAction action = playerActionFactory.CheatDestroyCardsOnBoard(targets);
            _networkMessageSendManager.EnqueueMessage(matchRequestFactory.CreateAction(action));

            return Task.CompletedTask;
        }

        public Task<bool> GetIsCurrentTurn()
        {
            throw new NotSupportedException();
        }

        public async Task LetsThink(float thinkTime, bool forceRealtime)
        {
            await _testHelper.LetsThink(thinkTime, forceRealtime);
        }

        public Task AssertInQueue(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        private void HandleNextCardAbility()
        {
            AbilityBoardArrow abilityBoardArrow = GameObject.FindObjectOfType<AbilityBoardArrow>();

            // TODO: Handle non-entry targetable abilities (do they even exist)?
            if (abilityBoardArrow != null)
            {
                Log.Info("! oh wow, abilityBoardArrow " + abilityBoardArrow);
            }
            if (abilityBoardArrow && _cardAbilityRequestsQueue.Count == 0)
            {
                //throw new Exception($"Unhandled card ability - targeting arrow exists, but no CardAbilityUsed call was queued");
            }
        }

        private class CardAbilityRequest
        {
            public readonly InstanceId Card;
            public readonly Enumerators.AbilityType AbilityType;
            public readonly IReadOnlyList<ParametrizedAbilityInstanceId> Targets;

            public CardAbilityRequest(InstanceId card, Enumerators.AbilityType abilityType, IReadOnlyList<ParametrizedAbilityInstanceId> targets)
            {
                Card = card;
                AbilityType = abilityType;
                Targets = targets;
            }

            public override string ToString()
            {
                return
                    $"({nameof(Card)}: {Card}, " +
                    $"{nameof(AbilityType)}: {AbilityType}, " +
                    $"{nameof(Targets)}: {Targets})";
            }
        }
    }
}
