using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DG.Tweening;
using KellermanSoftware.CompareNetObjects;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Gameplay;
using Loom.ZombieBattleground.Helpers;
using Loom.ZombieBattleground.Protobuf;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Card = Loom.ZombieBattleground.Data.Card;
using InstanceId = Loom.ZombieBattleground.Data.InstanceId;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class BattlegroundController : IController
    {
        public bool IsPreviewActive;

        public bool CardsZoomed = false;

        public Coroutine CreatePreviewCoroutine;

        public GameObject CurrentBoardCard;

        public InstanceId CurrentPreviewedCardId;

        public int CurrentTurn;

        public UniquePositionedList<BoardUnitView> OpponentBoardCards { get; } = new UniquePositionedList<BoardUnitView>(new PositionedList<BoardUnitView>());

        public UniquePositionedList<BoardUnitView> OpponentGraveyardCards { get; } =  new UniquePositionedList<BoardUnitView>(new PositionedList<BoardUnitView>());

        public UniquePositionedList<OpponentHandCard> OpponentHandCards { get; } =  new UniquePositionedList<OpponentHandCard>(new PositionedList<OpponentHandCard>());

        public UniquePositionedList<BoardUnitView> PlayerBoardCards { get; } =  new UniquePositionedList<BoardUnitView>(new PositionedList<BoardUnitView>());

        public UniquePositionedList<BoardUnitView> PlayerGraveyardCards { get; } = new UniquePositionedList<BoardUnitView>(new PositionedList<BoardUnitView>());

        public UniquePositionedList<BoardCard> PlayerHandCards { get; } = new UniquePositionedList<BoardCard>(new PositionedList<BoardCard>());

        public GameObject PlayerBoardObject, OpponentBoardObject, PlayerGraveyardObject, OpponentGraveyardObject;

        private AIController _aiController;

        private bool _battleDynamic;

        private CardsController _cardsController;

        private SkillsController _skillsController;

        private IDataManager _dataManager;

        private IGameplayManager _gameplayManager;

        private BoardUnitView _lastBoardUntilOnPreview;

        private PlayerController _playerController;

        private VfxController _vfxController;

        private AbilitiesController _abilitiesController;

        private ActionsQueueController _actionsQueueController;

        private BoardController _boardController;

        private IPlayerManager _playerManager;

        private ISoundManager _soundManager;

        private ITimerManager _timerManager;

        private ITutorialManager _tutorialManager;

        private IUIManager _uiManager;

        private IPvPManager _pvpManager;

        private IMatchManager _matchManager;

        private Sequence _rearrangingTopRealTimeSequence, _rearrangingBottomRealTimeSequence;

        private bool _turnTimerCounting = false;

        private GameObject _endTurnRingsAnimationGameObject;

        private Animator _endTurnButtonAnimationAnimator;

        private ParticleSystem[] _endTurnRingsAnimationParticleSystems;

        public event Action<int> PlayerGraveyardUpdated;

        public event Action<int> OpponentGraveyardUpdated;

        public event Action TurnStarted;

        public event Action TurnEnded;

        public float TurnTimer { get; private set; }

        public void Init()
        {
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _timerManager = GameClient.Get<ITimerManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _tutorialManager = GameClient.Get<ITutorialManager>();
            _uiManager = GameClient.Get<IUIManager>();
            _playerManager = GameClient.Get<IPlayerManager>();
            _pvpManager = GameClient.Get<IPvPManager>();
            _matchManager = GameClient.Get<IMatchManager>();

            _playerController = _gameplayManager.GetController<PlayerController>();
            _vfxController = _gameplayManager.GetController<VfxController>();
            _cardsController = _gameplayManager.GetController<CardsController>();
            _aiController = _gameplayManager.GetController<AIController>();
            _skillsController = _gameplayManager.GetController<SkillsController>();
            _abilitiesController = _gameplayManager.GetController<AbilitiesController>();
            _actionsQueueController = _gameplayManager.GetController<ActionsQueueController>();
            _boardController = _gameplayManager.GetController<BoardController>();

            _gameplayManager.GameEnded += GameEndedHandler;

            _gameplayManager.GameInitialized += OnGameInitializedHandler;

            _pvpManager.EndTurnActionReceived += OnGetEndTurnHandler;
        }

        private void OnGetEndTurnHandler(GameState controlGameState)
        {
            StopTurn(controlGameState);
        }

        public void Dispose()
        {
        }

        public void Update()
        {
            if (_gameplayManager.IsGameStarted && !_gameplayManager.IsGameEnded)
            {
                CheckGameDynamic();

                foreach (BoardUnitView item in PlayerBoardCards)
                {
                    item.Update();
                }

                foreach (BoardUnitView item in OpponentBoardCards)
                {
                    item.Update();
                }

                if (_matchManager.MatchType == Enumerators.MatchType.PVP)
                {
                    if (!_tutorialManager.IsTutorial && _turnTimerCounting)
                    {
                        TurnTimer -= Time.unscaledDeltaTime;

                        if (TurnTimer <= 0)
                        {
                            StopTurn();
                        }
                        else if (TurnTimer <= Constants.TimeForStartEndTurnAnimation && !_endTurnRingsAnimationGameObject.activeInHierarchy)
                        {
                            _endTurnRingsAnimationGameObject.SetActive(true);
                            _endTurnButtonAnimationAnimator.enabled = true;
                            _endTurnButtonAnimationAnimator.Play("TurnTimer");

                            float speed = Constants.TimeForStartEndTurnAnimation / TurnTimer;
                            foreach (ParticleSystem particleSystem in _endTurnRingsAnimationParticleSystems)
                            {
                                ParticleSystem.MainModule mainModule = particleSystem.main;
                                mainModule.simulationSpeed = speed;
                            }
                        }
                    }
                }
            }
        }

        public void ResetAll()
        {
            if (CreatePreviewCoroutine != null)
            {
                MainApp.Instance.StopCoroutine(CreatePreviewCoroutine);
            }

            CreatePreviewCoroutine = null;

            if (CurrentBoardCard != null && CurrentBoardCard)
            {
                Object.Destroy(CurrentBoardCard);
            }

            CurrentBoardCard = null;

            ClearBattleground();
        }

        public void KillBoardCard(BoardUnitModel boardUnitModel, bool withDeathEffect = true)
        {
            BoardUnitView boardUnitView = GetBoardUnitViewByModel(boardUnitModel);

            if (boardUnitView == null)
                return;

            if (_lastBoardUntilOnPreview != null && boardUnitView == _lastBoardUntilOnPreview)
            {
                DestroyCardPreview();
            }

            Action completeCallback = () => { };

            boardUnitView.Transform.position = new Vector3(boardUnitView.Transform.position.x,
                boardUnitView.Transform.position.y, boardUnitView.Transform.position.z + 0.2f);

            InternalTools.DoActionDelayed(() =>
            {
                Action endOfDestroyAnimationCallback = () =>
                {
                    boardUnitView.Transform.DOKill();
                    Object.Destroy(boardUnitView.GameObject);

                    boardUnitView.WasDestroyed = true;
                };

                Action endOfAnimationCallback = () =>
                {
                    boardUnitView.Model.InvokeUnitDied();

                    boardUnitModel.OwnerPlayer.BoardCards.Remove(boardUnitView);
                    boardUnitModel.OwnerPlayer.RemoveCardFromBoard(boardUnitModel.Card);
                    boardUnitModel.OwnerPlayer.AddCardToGraveyard(boardUnitModel.Card);

                    if(_tutorialManager.IsTutorial)
                    {
                        if (boardUnitModel.OwnerPlayer.IsLocalPlayer)
                        {
                            _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.PlayerBattleframeDied);
                        }
                        else
                        {
                            _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.EnemyBattleframeDied);
                        }
                    }
                };

                if (withDeathEffect)
                {
                    _vfxController.CreateDeathZombieAnimation(boardUnitView, endOfDestroyAnimationCallback, endOfAnimationCallback, completeCallback);
                }
                else
                {
                    endOfAnimationCallback();
                    endOfDestroyAnimationCallback();

                    _boardController.UpdateWholeBoard(null);

                    completeCallback?.Invoke();
                }

            }, Time.deltaTime * 60f / 2f);

            _actionsQueueController.ForceContinueAction(boardUnitView.Model.ActionForDying);
        }
    
        public void CheckGameDynamic()
        {
            if (!_battleDynamic)
            {
                _soundManager.CrossfaidSound(Enumerators.SoundType.BATTLEGROUND, null, true);
            }

            _battleDynamic = true;
        }

        public void UpdateGraveyard(int index, Player player)
        {
            if (player.IsLocalPlayer)
            {
                PlayerGraveyardUpdated?.Invoke(index);
            }
            else
            {
                OpponentGraveyardUpdated?.Invoke(index);
            }
        }

        public void ClearBattleground()
        {
            PlayerHandCards.Clear();
            OpponentHandCards.Clear();

            PlayerBoardCards.Clear();
            OpponentBoardCards.Clear();

            PlayerGraveyardCards.Clear();
            OpponentGraveyardCards.Clear();
        }

        public void InitializeBattleground()
        {
            CurrentTurn = 0;

            if (Constants.DevModeEnabled)
            {
                _gameplayManager.OpponentPlayer.Defense = 99;
                _gameplayManager.CurrentPlayer.Defense = 99;
            }

            PlayerBoardObject = GameObject.Find("PlayerBoard");
            OpponentBoardObject = GameObject.Find("OpponentBoard");
            PlayerGraveyardObject = GameObject.Find("GraveyardPlayer");
            OpponentGraveyardObject = GameObject.Find("GraveyardOpponent");

            _endTurnButtonAnimationAnimator = GameObject.Find("EndTurnButton/_1_btn_endturn").GetComponent<Animator>();
            _endTurnRingsAnimationGameObject = GameObject.Find("EndTurnButton").transform.Find("ZB_ANM_TurnTimerEffect").gameObject;
            _endTurnRingsAnimationParticleSystems = _endTurnRingsAnimationGameObject.gameObject.GetComponentsInChildren<ParticleSystem>();
            _endTurnRingsAnimationGameObject.SetActive(false);
            _endTurnButtonAnimationAnimator.enabled = false;
        }

        public void StartGameplayTurns()
        {
            if (_gameplayManager.IsTutorial && 
               _tutorialManager.CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.GameplayBeginManually &&
               !_tutorialManager.CurrentTutorialStep.ToGameplayStep().LaunchGameplayManually)
                return;

            StartTurn();
        }

        public void GameEndedHandler(Enumerators.EndGameType endGameType)
        {
            CurrentTurn = 0;

            ClearBattleground();
        }

        public void StartTurn()
        {
            if (_gameplayManager.IsGameEnded)
                return;

            if (!_tutorialManager.IsTutorial &&
                _matchManager.MatchType == Enumerators.MatchType.PVP &&
                !_turnTimerCounting &&
                _gameplayManager.CurrentTurnPlayer.IsLocalPlayer)
            {
                TurnTimer = _gameplayManager.CurrentTurnPlayer.TurnTime;
                _turnTimerCounting = true;
            }

            CurrentTurn++;

            _gameplayManager.CurrentTurnPlayer.Turn++;

            _uiManager.GetPage<GameplayPage>().SetEndTurnButtonStatus(_gameplayManager.IsLocalPlayerTurn());

            UpdatePositionOfCardsInOpponentHand();
            _playerController.IsActive = _gameplayManager.IsLocalPlayerTurn();

            if (_gameplayManager.IsLocalPlayerTurn())
            {
                List<BoardUnitView> creatures = new List<BoardUnitView>();

                foreach (BoardUnitView card in PlayerBoardCards)
                {
                    if (_playerController == null || !card.GameObject)
                    {
                        creatures.Add(card);
                        continue;
                    }

                    card.Model.OnStartTurn();
                }

                foreach (BoardUnitView item in creatures)
                {
                    PlayerBoardCards.Remove(item);
                }

                creatures.Clear();

                foreach (BoardUnitView card in PlayerBoardCards)
                {
                    card.SetHighlightingEnabled(true);
                }
            }
            else
            {
                foreach (BoardUnitView card in OpponentBoardCards)
                {
                    card.Model.OnStartTurn();
                }

                foreach (BoardCard card in PlayerHandCards)
                {
                    card.SetHighlightingEnabled(false);
                }

                foreach (BoardUnitView card in PlayerBoardCards)
                {
                    card.SetHighlightingEnabled(false);
                }
            }

            _gameplayManager.CurrentPlayer.InvokeTurnStarted();
            _gameplayManager.OpponentPlayer.InvokeTurnStarted();

            _playerController.UpdateHandCardsHighlight();

            _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.StartTurn);

            TurnStarted?.Invoke();
        }

        public void EndTurn()
        {
            if (_gameplayManager.IsGameEnded)
                return;

            if (!_tutorialManager.IsTutorial &&
                _matchManager.MatchType == Enumerators.MatchType.PVP && _turnTimerCounting)
            {
                _turnTimerCounting = false;

                if (_endTurnRingsAnimationGameObject.activeInHierarchy)
                {
                    _endTurnRingsAnimationGameObject.SetActive(false);
                }

                _endTurnButtonAnimationAnimator.enabled = false;
            }

            if (_gameplayManager.IsLocalPlayerTurn())
            {
                _uiManager.GetPage<GameplayPage>().SetEndTurnButtonStatus(false);

                foreach (BoardUnitView card in PlayerBoardCards)
                {
                    card.Model.OnEndTurn();
                }
            }
            else
            {
                foreach (BoardUnitView card in OpponentBoardCards)
                {
                    card.Model.OnEndTurn();
                }
            }

            _gameplayManager.CurrentPlayer.InvokeTurnEnded();
            _gameplayManager.OpponentPlayer.InvokeTurnEnded();


            if (_gameplayManager.IsLocalPlayerTurn())
            {
                TurnEnded?.Invoke();
                _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.EndTurn);
            }

            _gameplayManager.CurrentTurnPlayer = _gameplayManager.IsLocalPlayerTurn() ?
                _gameplayManager.OpponentPlayer :
                _gameplayManager.CurrentPlayer;
        }

        public void StopTurn(GameState pvpControlGameState = null)
        {
            _gameplayManager.GetController<ActionsQueueController>().AddNewActionInToQueue(
                 (parameter, completeCallback) =>
                 {
                     // Validate game state
                     if (Constants.GameStateValidationEnabled && pvpControlGameState != null)
                     {
                         GameState currentGameState = GameStateConstructor.Create().CreateCurrentGameStateFromOnlineGame(true);
                         CompareLogic compareLogic = new CompareLogic();
                         compareLogic.Config.ShowBreadcrumb = true;
                         compareLogic.Config.TreatStringEmptyAndNullTheSame = true;
                         compareLogic.Config.MaxDifferences = 25;
                         compareLogic.Config.MembersToIgnore.Add("CardsInGraveyard");
                         compareLogic.Config.MembersToIgnore.Add("CardsInHand");
                         compareLogic.Config.MembersToIgnore.Add("CardsInDeck");
                         compareLogic.Config.MembersToIgnore.Add("CurrentGoo");
                         compareLogic.Config.ActualName = "OpponentState";
                         compareLogic.Config.ExpectedName = "LocalState";
                         ComparisonResult comparisonResult = compareLogic.Compare(currentGameState, pvpControlGameState);
                         if (!comparisonResult.AreEqual)
                             throw new Exception("Game state de-sync:\n" + comparisonResult.DifferencesString);
                     }

                     EndTurn();

                     if (_gameplayManager.IsLocalPlayerTurn())
                     {
                         _uiManager.DrawPopup<YourTurnPopup>();

                         _timerManager.AddTimer((x) =>
                         {
                             StartTurn();
                             completeCallback?.Invoke();
                         }, null, Constants.DelayBetweenYourTurnPopup);
                     }
                     else
                     {
                         StartTurn();
                         completeCallback?.Invoke();
                     }
                 },  Enumerators.QueueActionType.StopTurn);
        }

        public void RemovePlayerCardFromBoardToGraveyard(WorkingCard card)
        {
            BoardUnitView boardCard = PlayerBoardCards.FirstOrDefault(x => x.Model.Card == card);
            if (boardCard == null)
                return;

            if (!boardCard.WasDestroyed)
            {
                boardCard.Transform.localPosition = new Vector3(boardCard.Transform.localPosition.x,
                boardCard.Transform.localPosition.y, -0.2f);
            }

            PlayerBoardCards.Remove(boardCard);
            PlayerGraveyardCards.Insert(ItemPosition.End, boardCard);

            boardCard.SetHighlightingEnabled(false);
            boardCard.StopSleepingParticles();

            if (!boardCard.WasDestroyed)
            {
                boardCard.GameObject.GetComponent<SortingGroup>().sortingLayerID = SRSortingLayers.BoardCards;
                Object.Destroy(boardCard.GameObject.GetComponent<BoxCollider2D>());
            }
        }

        public void RemoveOpponentCardFromBoardToGraveyard(WorkingCard card)
        {
            Vector3 graveyardPos = OpponentGraveyardObject.transform.position + new Vector3(0.0f, -0.2f, 0.0f);
            BoardUnitView boardCard = OpponentBoardCards.FirstOrDefault(x => x.Model.Card == card);
            if (boardCard != null)
            {
                if (!boardCard.WasDestroyed)
                {
                    boardCard.Transform.localPosition = new Vector3(boardCard.Transform.localPosition.x,
                        boardCard.Transform.localPosition.y, -0.2f);
                }

                OpponentBoardCards.Remove(boardCard);

                boardCard.SetHighlightingEnabled(false);
                boardCard.StopSleepingParticles();

                if (!boardCard.WasDestroyed)
                {
                    boardCard.GameObject.GetComponent<SortingGroup>().sortingLayerID = SRSortingLayers.BoardCards;
                    Object.Destroy(boardCard.GameObject.GetComponent<BoxCollider2D>());
                }
            }
            else if (_aiController.CurrentSpellCard != null && card == _aiController.CurrentSpellCard.WorkingCard)
            {
                _aiController.CurrentSpellCard.SetHighlightingEnabled(false);
                _aiController.CurrentSpellCard.GameObject.GetComponent<SortingGroup>().sortingLayerID = SRSortingLayers.BoardCards;
                Object.Destroy(_aiController.CurrentSpellCard.GameObject.GetComponent<BoxCollider2D>());
                Sequence sequence = DOTween.Sequence();
                sequence.PrependInterval(2.0f);
                sequence.Append(_aiController.CurrentSpellCard.Transform.DOMove(graveyardPos, 0.5f));
                sequence.Append(_aiController.CurrentSpellCard.Transform.DOScale(new Vector2(0.6f, 0.6f), 0.5f));
                sequence.OnComplete(
                    () =>
                    {
                        _aiController.CurrentSpellCard = null;
                    });
            }
        }

        // rewrite
        public void CreateCardPreview(object target, Vector3 pos, bool highlight = true)
        {
            IsPreviewActive = true;

            switch (target)
            {
                case BoardCard card:
                    CurrentPreviewedCardId = card.WorkingCard.InstanceId;
                    break;
                case BoardUnitView unit:
                    _lastBoardUntilOnPreview = unit;
                    CurrentPreviewedCardId = unit.Model.Card.InstanceId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            CreatePreviewCoroutine = MainApp.Instance.StartCoroutine(CreateCardPreviewAsync(target, pos, highlight));
        }

        // rewrite
        public IEnumerator CreateCardPreviewAsync(object target, Vector3 pos, bool highlight)
        {
            yield return new WaitForSeconds(0.3f);

            WorkingCard card = null;

            switch (target)
            {
                case BoardCard card1:
                    card = card1.WorkingCard;
                    break;
                case BoardUnitView unit:
                    card = unit.Model.Card;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            BoardCard boardCard;
            switch (card.LibraryCard.CardKind)
            {
                case Enumerators.CardKind.CREATURE:
                    CurrentBoardCard = Object.Instantiate(_cardsController.CreatureCardViewPrefab);
                    boardCard = new UnitBoardCard(CurrentBoardCard);
                    break;
                case Enumerators.CardKind.SPELL:
                    CurrentBoardCard = Object.Instantiate(_cardsController.ItemCardViewPrefab);
                    boardCard = new SpellBoardCard(CurrentBoardCard);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            boardCard.Init(card);
            if (highlight)
            {
                highlight = boardCard.CanBePlayed(card.Owner) && boardCard.CanBeBuyed(card.Owner);
            }

            boardCard.SetHighlightingEnabled(highlight);
            boardCard.IsPreview = true;

            InternalTools.SetLayerRecursively(boardCard.GameObject, 0);

            switch (target)
            {
                case BoardUnitView boardUnit:
                    boardCard.DrawTooltipInfoOfUnit(boardUnit);
                    UnitBoardCard boardCardUnit = boardCard as UnitBoardCard;
                    boardCardUnit.Damage = boardUnit.Model.MaxCurrentDamage;
                    boardCardUnit.Health = boardUnit.Model.MaxCurrentHp;
                    break;
                case BoardCard tooltipCard:
                    boardCard.DrawTooltipInfoOfCard(tooltipCard);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }

            Vector3 newPos = pos;
            newPos.y += 2.0f;
            CurrentBoardCard.transform.position = newPos;
            CurrentBoardCard.transform.localRotation = Quaternion.Euler(Vector3.zero);

            Vector3 sizeOfCard = Vector3.one;

            sizeOfCard = !InternalTools.IsTabletScreen() ? new Vector3(.8f, .8f, .8f) : new Vector3(.4f, .4f, .4f);

            CurrentBoardCard.transform.localScale = sizeOfCard;

            CurrentBoardCard.GetComponent<SortingGroup>().sortingLayerID = SRSortingLayers.GameUI3;
            CurrentBoardCard.layer = LayerMask.NameToLayer("Default");
            CurrentBoardCard.transform.DOMoveY(newPos.y + 1.0f, 0.1f);
        }

        // rewrite
        public void DestroyCardPreview()
        {
            if (!IsPreviewActive)
                return;

            GameClient.Get<ICameraManager>().FadeOut(null, 1, true);

            MainApp.Instance.StartCoroutine(DestroyCardPreviewAsync());
            if (CreatePreviewCoroutine != null)
            {
                MainApp.Instance.StopCoroutine(CreatePreviewCoroutine);
            }

            IsPreviewActive = false;
        }

        // rewrite
        public IEnumerator DestroyCardPreviewAsync()
        {
            if (CurrentBoardCard != null)
            {
                _lastBoardUntilOnPreview = null;
                GameObject oldCardPreview = CurrentBoardCard;
                foreach (SpriteRenderer renderer in oldCardPreview.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderer.DOFade(0.0f, 0.2f);
                }

                foreach (TextMeshPro text in oldCardPreview.GetComponentsInChildren<TextMeshPro>())
                {
                    text.DOFade(0.0f, 0.2f);
                }

                yield return new WaitForSeconds(0.5f);
                Object.Destroy(oldCardPreview.gameObject);
            }
        }

        public void UpdatePositionOfCardsInPlayerHand(bool isMove = false)
        {
            float handWidth = 0.0f;
            float spacing = -1.5f;
            float scaling = 0.25f;
            Vector3 pivot = new Vector3(6f, -7.5f, 0f);
            int twistPerCard = -5;

            if (CardsZoomed)
            {
                spacing = -2.6f;
                scaling = 0.31f;
                pivot = new Vector3(-1.3f, -6.5f, 0f);
                twistPerCard = -3;
            }

            for (int i = 0; i < PlayerHandCards.Count; i++)
            {
                handWidth += spacing;
            }

            handWidth -= spacing;

            if (PlayerHandCards.Count == 1)
            {
                twistPerCard = 0;
            }

            int totalTwist = twistPerCard * PlayerHandCards.Count;
            float startTwist = (totalTwist - twistPerCard) / 2f;
            float scalingFactor = 0.04f;
            Vector3 moveToPosition = Vector3.zero;

            for (int i = 0; i < PlayerHandCards.Count; i++)
            {
                BoardCard card = PlayerHandCards[i];
                float twist = startTwist - i * twistPerCard;
                float nudge = Mathf.Abs(twist);

                nudge *= scalingFactor;
                moveToPosition = new Vector3(pivot.x - handWidth / 2, pivot.y - nudge,
                    (PlayerHandCards.Count - i) * 0.1f);

                if (isMove)
                {
                    card.IsNewCard = false;
                }

                card.UpdateCardPositionInHand(moveToPosition, Vector3.forward * twist, Vector3.one * scaling);

                pivot.x += handWidth / PlayerHandCards.Count;

                card.GameObject.GetComponent<SortingGroup>().sortingLayerID = SRSortingLayers.HandCards;
                card.GameObject.GetComponent<SortingGroup>().sortingOrder = i;
            }
        }

        public void UpdatePositionOfCardsInOpponentHand(bool isMove = false, bool isNewCard = false)
        {
            float handWidth = 0.0f;
            float spacing = -1.0f;
            float zPositionKoef = -0.1f;

            for (int i = 0; i < OpponentHandCards.Count; i++)
            {
                handWidth += spacing;
            }

            handWidth -= spacing;

            Vector3 pivot = new Vector3(-3.2f, 8.5f, 0f);
            int twistPerCard = 5;

            if (OpponentHandCards.Count == 1)
            {
                twistPerCard = 0;
            }

            int totalTwist = twistPerCard * OpponentHandCards.Count;
            float startTwist = (totalTwist - twistPerCard) / 2f;

            for (int i = 0; i < OpponentHandCards.Count; i++)
            {
                OpponentHandCard card = OpponentHandCards[i];
                float twist = startTwist - i * twistPerCard;

                Vector3 movePosition = new Vector3(pivot.x - handWidth / 2, pivot.y, i * zPositionKoef);
                Vector3 rotatePosition = new Vector3(0, 0, twist);

                if (isMove)
                {
                    if (i == OpponentHandCards.Count - 1 && isNewCard)
                    {
                        card.Transform.position = new Vector3(-8.2f, 5.7f, 0);
                        card.Transform.eulerAngles = Vector3.forward * 90f;
                    }

                    card.Transform.DOMove(movePosition, 0.5f).OnComplete(() => UpdateOpponentHandCardLayer(card.GameObject));
                    card.Transform.DORotate(rotatePosition, 0.5f);
                }
                else
                {
                    card.Transform.position = movePosition;
                    card.Transform.rotation = Quaternion.Euler(rotatePosition);
                    UpdateOpponentHandCardLayer(card.GameObject);
                }

                pivot.x += handWidth / OpponentHandCards.Count;
            }
        }

        private void UpdateOpponentHandCardLayer(GameObject card)
        {
            if (card.layer == 0)
            {
                SortingGroup group = card.GetComponent<SortingGroup>();
                group.sortingLayerID = SRSortingLayers.Default;
                group.sortingOrder = -1;
                List<GameObject> allUnitObj = card.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                foreach (GameObject child in allUnitObj)
                {
                    child.layer = LayerMask.NameToLayer("Battleground");
                }
            }
        }

        /// <summary>
        /// Gets an active <see cref="BoardUnitView"/> that has <paramref name="boardUnitModel"/> attached to it.
        /// </summary>
        /// <param name="boardUnitModel"></param>
        /// <returns></returns>
        public BoardUnitView GetBoardUnitViewByModel(BoardUnitModel boardUnitModel)
        {
            if (boardUnitModel == null)
            {
                Helpers.ExceptionReporter.LogException("Trying to get BoardUnitView from 'null' BoardUnitModel");
                return null;
            }

            BoardUnitView unitView =
                   _gameplayManager.CurrentPlayer.BoardCards
                      .Concat(_gameplayManager.CurrentPlayer.BoardCards)
                      .Concat(_gameplayManager.OpponentPlayer.BoardCards)
                      .FirstOrDefault(x => x != null && x.Model == boardUnitModel);

            if (unitView is default(BoardUnitView))
            {
                Helpers.ExceptionReporter.LogException("BoardUnitView couldnt found for BoardUnitModel");
                return null;
            }

            return unitView;
        }

        public BoardUnitView GetBoardUnitFromHisObject(GameObject unitObject)
        {
            BoardUnitView unit =
                _gameplayManager.CurrentPlayer.BoardCards.FirstOrDefault(x => x.GameObject == unitObject) ??
                _gameplayManager.OpponentPlayer.BoardCards.First(x => x.GameObject == unitObject);

            return unit;
        }

        public BoardCard GetBoardCardFromHisObject(GameObject cardObject)
        {
            BoardCard card = PlayerHandCards.FirstOrDefault(x => x.GameObject.Equals(cardObject));

            return card;
        }

        public void DestroyBoardUnit(BoardUnitModel unit, bool withDeathEffect = true, bool isForceDestroy = false)
        {
            if (!isForceDestroy && unit.HasBuffShield)
            {
                unit.UseShieldFromBuff();
            }
            else
            {
                _gameplayManager.GetController<BattleController>().CheckOnKillEnemyZombie(unit);

                unit?.Die(withDeathEffect: withDeathEffect);
            }
        }

        public void TakeControlUnit(Player newPlayerOwner, BoardUnitModel unit)
        {
            BoardUnitView view = GetBoardUnitViewByModel(unit);

            if (unit.OwnerPlayer.IsLocalPlayer)
            {
                PlayerBoardCards.Remove(view);

                OpponentBoardCards.Insert(ItemPosition.End, view);
            }
            else
            {
                OpponentBoardCards.Remove(view);
                PlayerBoardCards.Insert(ItemPosition.End, view);
            }

            unit.OwnerPlayer.BoardCards.Remove(view);
            unit.OwnerPlayer.CardsOnBoard.Remove(unit.Card);

            unit.OwnerPlayer = newPlayerOwner;
            unit.Card.Owner = newPlayerOwner;

            newPlayerOwner.CardsOnBoard.Insert(ItemPosition.End, unit.Card);
            newPlayerOwner.BoardCards.Insert(ItemPosition.End, view);

            view.Transform.tag = newPlayerOwner.IsLocalPlayer ? SRTags.PlayerOwned : SRTags.OpponentOwned;

            _boardController.UpdateWholeBoard(null);

            foreach (AbilityBase ability in _abilitiesController.GetAbilitiesConnectedToUnit(unit))
            {
                ability.PlayerCallerOfAbility = newPlayerOwner;
            }
        }

        public void DistractUnit(BoardUnitView boardUnit)
        {
            boardUnit.Model.BuffedDamage = 0;
            boardUnit.Model.BuffedHp = 0;
            boardUnit.Model.HasSwing = false;
            boardUnit.Model.TakeFreezeToAttacked = false;
            boardUnit.Model.HasBuffRush = false;
            boardUnit.Model.HasBuffHeavy = false;
            boardUnit.Model.SetAsWalkerUnit();
            boardUnit.Model.UseShieldFromBuff();
            boardUnit.Model.AttackRestriction = Enumerators.AttackRestriction.ANY;
            boardUnit.Model.AttackTargetsAvailability = new List<Enumerators.SkillTargetType>()
            {
                Enumerators.SkillTargetType.OPPONENT,
                Enumerators.SkillTargetType.OPPONENT_CARD
            };

            DeactivateAllAbilitiesOnUnit(boardUnit);

            boardUnit.Model.Distract();
        }

        public void DeactivateAllAbilitiesOnUnit(BoardUnitView boardUnit)
        {
            boardUnit.Model.BuffsOnUnit.Clear();

            boardUnit.Model.ClearEffectsOnUnit();

            List<AbilityBase> abilities = _abilitiesController.GetAbilitiesConnectedToUnit(boardUnit.Model);

            foreach (AbilityBase ability in abilities)
            {
                ability.Deactivate();
                ability.Dispose();
            }
        }

        public BoardUnitView CreateBoardUnit(Player owner, WorkingCard card)
        {
            GameObject playerBoard = owner.IsLocalPlayer ? PlayerBoardObject : OpponentBoardObject;

            BoardUnitView boardUnitView = new BoardUnitView(new BoardUnitModel(), playerBoard.transform);
            boardUnitView.Transform.tag = owner.IsLocalPlayer ? SRTags.PlayerOwned : SRTags.OpponentOwned;
            boardUnitView.Transform.SetParent(playerBoard.transform);
            boardUnitView.Transform.position = new Vector2(1.9f * owner.BoardCards.Count, 0);
            boardUnitView.Model.OwnerPlayer = owner;
            boardUnitView.SetObjectInfo(card);
            boardUnitView.Model.TutorialObjectId = card.TutorialObjectId;

            boardUnitView.PlayArrivalAnimation();

            return boardUnitView;
        }


        public BoardObject GetTargetById(InstanceId id, Enumerators.AffectObjectType affectObjectType)
        {
            switch(affectObjectType)
            {
                case Enumerators.AffectObjectType.Player:
                    return _gameplayManager.OpponentPlayer.InstanceId == id ? _gameplayManager.OpponentPlayer : _gameplayManager.CurrentPlayer;
                case Enumerators.AffectObjectType.Character:
                    {
                        List<BoardUnitView> units = new List<BoardUnitView>();
                        units.AddRange(_gameplayManager.OpponentPlayer.BoardCards);
                        units.AddRange(_gameplayManager.CurrentPlayer.BoardCards);

                        BoardUnitView unit = units.Find(u => u.Model.Card.InstanceId == id);

                        units.Clear();

                        if (unit != null)
                            return unit.Model;
                    }
                    break;
                case Enumerators.AffectObjectType.Card:
                    List<WorkingCard> cards = new List<WorkingCard>();
                    cards.AddRange(_gameplayManager.OpponentPlayer.CardsInDeck);
                    cards.AddRange(_gameplayManager.CurrentPlayer.CardsInDeck);

                    WorkingCard card = cards.Find(u => u.InstanceId == id);

                    cards.Clear();
                    if (card != null)
                    {
                        return CreateCustomHandBoardCard(card).HandBoardCard;
                    }
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(affectObjectType), affectObjectType, null);
            }

            return null;
        }

        public List<BoardObject> GetTargetsById(IList<Unit> targetUnits)
        {
            List<BoardObject> boardObjects = new List<BoardObject>();

            if (targetUnits != null)
            {
                foreach (Unit targetUnit in targetUnits)
                {
                    boardObjects.Add(GetTargetById(targetUnit.InstanceId, targetUnit.AffectObjectType));
                }
            }

            return boardObjects;
        }

        public BoardSkill GetSkillById(Player owner, SkillId skillId)
        {
            if (!owner.IsLocalPlayer)
            {
                if (_skillsController.OpponentPrimarySkill.SkillId == skillId)
                    return _skillsController.OpponentPrimarySkill;
                else if (_skillsController.OpponentSecondarySkill.SkillId == skillId)
                    return _skillsController.OpponentSecondarySkill;
            }
            else
            {
                if (_skillsController.PlayerPrimarySkill.SkillId == skillId)
                    return _skillsController.PlayerPrimarySkill;
                else if (_skillsController.PlayerSecondarySkill.SkillId == skillId)
                    return _skillsController.PlayerSecondarySkill;
            }

            return null;
        }

        public BoardUnitModel GetBoardUnitById(InstanceId id)
        {
            BoardUnitView view = 
                _gameplayManager.OpponentPlayer.BoardCards
                    .Concat(_gameplayManager.CurrentPlayer.BoardCards)
                    .FirstOrDefault(u => u != null && u.Model.Card.InstanceId == id);

            return view?.Model;
        }

        public WorkingCard GetWorkingCardById(InstanceId id)
        {
            BoardUnitModel boardUnitModel = GetBoardUnitById(id);
            if (boardUnitModel != null)
                return boardUnitModel.Card;

            WorkingCard workingCard =
                _gameplayManager.OpponentPlayer.CardsOnBoard
                    .Concat(_gameplayManager.CurrentPlayer.CardsOnBoard)
                    .Concat(_gameplayManager.CurrentPlayer.CardsInHand)
                    .Concat(_gameplayManager.OpponentPlayer.CardsInHand)
                    .FirstOrDefault(u => u != null && u.InstanceId == id);

            return workingCard;
        }

        public BoardObject GetBoardObjectById(InstanceId id)
        {
            BoardUnitModel boardUnitModel = GetBoardUnitById(id);
            if(boardUnitModel != null)
                return boardUnitModel;

            List<BoardObject> boardObjects = new List<BoardObject>
            {
                _gameplayManager.CurrentPlayer,
                _gameplayManager.OpponentPlayer,
            };
            boardObjects.AddRange(_gameplayManager.CurrentPlayer.BoardSpellsInUse);
            boardObjects.AddRange(_gameplayManager.OpponentPlayer.BoardSpellsInUse);

            BoardObject foundObject = boardObjects.Find(boardObject =>
            {
                switch (boardObject)
                {
                    case BoardSpell boardSpell:
                        return boardSpell.Card.InstanceId == id;
                    case IInstanceIdOwner instanceIdOwner:
                        return instanceIdOwner.InstanceId == id;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(boardObject), boardObject, null);
                }
            });

            return foundObject;
        }

        public List<BoardUnitView> GetAdjacentUnitsToUnit(BoardUnitModel targetUnit)
        {
            UniquePositionedList<BoardUnitView> boardCards = targetUnit.OwnerPlayer.BoardCards;

            int targetView = boardCards.IndexOf(GetBoardUnitViewByModel(targetUnit));

            return boardCards.Where(unit => unit.Model != targetUnit && 
            ((boardCards.IndexOf(unit) == Mathf.Clamp(targetView - 1, 0, boardCards.Count - 1)) ||
            (boardCards.IndexOf(unit) == Mathf.Clamp(targetView + 1, 0, boardCards.Count - 1)) &&
            boardCards.IndexOf(unit) != targetView)
            ).ToList();
        }

        public BoardCard CreateCustomHandBoardCard(WorkingCard card)
        {
            BoardCard boardCard = new UnitBoardCard(Object.Instantiate(_cardsController.CreatureCardViewPrefab));
            boardCard.Init(card);
            boardCard.GameObject.transform.position = card.Owner.IsLocalPlayer ? Constants.DefaultPositionOfPlayerBoardCard :
                                                                                 Constants.DefaultPositionOfOpponentBoardCard;
            boardCard.GameObject.transform.localScale = Vector3.one * .3f;
            boardCard.SetHighlightingEnabled(false);

            boardCard.HandBoardCard = new HandBoardCard(boardCard.GameObject, boardCard);
            boardCard.HandBoardCard.OwnerPlayer = card.Owner;

            return boardCard;
        }

        #region specific setup of battleground

        public void SetupBattlegroundAsSpecific(SpecificBattlegroundInfo specificBattlegroundInfo)
        {
            SetupOverlordsAsSpecific(specificBattlegroundInfo);
            SetupOverlordsHandsAsSpecific(specificBattlegroundInfo.PlayerInfo.CardsInHand, specificBattlegroundInfo.OpponentInfo.CardsInHand);
            SetupOverlordsDecksAsSpecific(specificBattlegroundInfo.PlayerInfo.CardsInDeck, specificBattlegroundInfo.OpponentInfo.CardsInDeck);
            SetupOverlordsBoardUnitsAsSpecific(specificBattlegroundInfo.PlayerInfo.CardsOnBoard, specificBattlegroundInfo.OpponentInfo.CardsOnBoard);
            SetupGeneralUIAsSpecific(specificBattlegroundInfo);
        }

        private void SetupOverlordsAsSpecific(SpecificBattlegroundInfo specificBattlegroundInfo)
        {
            _gameplayManager.OpponentPlayer.Defense = specificBattlegroundInfo.OpponentInfo.Defense;
            _gameplayManager.OpponentPlayer.GooVials = specificBattlegroundInfo.OpponentInfo.MaximumGoo;
            _gameplayManager.OpponentPlayer.CurrentGoo = specificBattlegroundInfo.OpponentInfo.CurrentGoo;

            _gameplayManager.CurrentPlayer.Defense = specificBattlegroundInfo.PlayerInfo.Defense;
            _gameplayManager.CurrentPlayer.GooVials = specificBattlegroundInfo.PlayerInfo.MaximumGoo;
            _gameplayManager.CurrentPlayer.CurrentGoo = specificBattlegroundInfo.PlayerInfo.CurrentGoo;
        }

        private void SetupOverlordsHandsAsSpecific(List<SpecificBattlegroundInfo.OverlordCardInfo> playerCards,
                                                    List<SpecificBattlegroundInfo.OverlordCardInfo> opponentCards)
        {
            WorkingCard card;
            foreach (SpecificBattlegroundInfo.OverlordCardInfo cardInfo in playerCards)
            {
                card = _cardsController.GetWorkingCardFromCardName(cardInfo.Name, _gameplayManager.CurrentPlayer);
                card.TutorialObjectId = cardInfo.TutorialObjectId;
                _gameplayManager.CurrentPlayer.AddCardToHand(card, true);
            }

            foreach (SpecificBattlegroundInfo.OverlordCardInfo cardInfo in opponentCards)
            {
                card = _cardsController.GetWorkingCardFromCardName(cardInfo.Name, _gameplayManager.OpponentPlayer);
                card.TutorialObjectId = cardInfo.TutorialObjectId;
                _gameplayManager.OpponentPlayer.AddCardToHand(card, true);
            }
        }

        private void SetupOverlordsDecksAsSpecific(List<SpecificBattlegroundInfo.OverlordCardInfo> playerCards,
                                                   List<SpecificBattlegroundInfo.OverlordCardInfo> opponentCards)
        {
            List<WorkingCard> workingPlayerCards =
                playerCards
                    .Select(cardInfo =>
                    {
                        Card card = _dataManager.CachedCardsLibraryData.GetCardFromName(cardInfo.Name);
                        WorkingCard workingCard = new WorkingCard(card, card, _gameplayManager.CurrentPlayer);
                        workingCard.TutorialObjectId = cardInfo.TutorialObjectId;
                        return workingCard;
                    })
                    .ToList();

            List<WorkingCard> workingOpponentCards =
                opponentCards
                    .Select(cardInfo =>
                    {
                        Card card = _dataManager.CachedCardsLibraryData.GetCardFromName(cardInfo.Name);
                        WorkingCard workingCard = new WorkingCard(card, card, _gameplayManager.OpponentPlayer);
                        workingCard.TutorialObjectId = cardInfo.TutorialObjectId;
                        return workingCard;
                    })
                    .ToList();

            _gameplayManager.CurrentPlayer.SetDeck(workingPlayerCards, false);
            _gameplayManager.OpponentPlayer.SetDeck(workingOpponentCards, true);
        }

        private void SetupOverlordsGraveyardsAsSpecific(List<string> playerCards, List<string> opponentCards)
        {
            // todo implement logic
        }

        private void SetupOverlordsBoardUnitsAsSpecific(List<SpecificBattlegroundInfo.UnitOnBoardInfo> playerCards,
                                                        List<SpecificBattlegroundInfo.UnitOnBoardInfo> opponentCards)
        {
            BoardUnitView workingUnitView = null;

            foreach (SpecificBattlegroundInfo.UnitOnBoardInfo cardInfo in playerCards)
            {
                workingUnitView = _cardsController.SpawnUnitOnBoard(_gameplayManager.CurrentPlayer, cardInfo.Name, 0);
                workingUnitView.Model.TutorialObjectId = cardInfo.TutorialObjectId;
                workingUnitView.Model.CantAttackInThisTurnBlocker = !cardInfo.IsManuallyPlayable;
                workingUnitView.Model.CurrentHp += cardInfo.BuffedHealth;
                workingUnitView.Model.BuffedHp += cardInfo.BuffedHealth;
                workingUnitView.Model.CurrentDamage += cardInfo.BuffedDamage;
                workingUnitView.Model.BuffedDamage += cardInfo.BuffedDamage;
                PlayerBoardCards.Insert(ItemPosition.End, workingUnitView);
            }

            foreach (SpecificBattlegroundInfo.UnitOnBoardInfo cardInfo in opponentCards)
            {
                workingUnitView = _cardsController.SpawnUnitOnBoard(_gameplayManager.OpponentPlayer, cardInfo.Name, 0);
                workingUnitView.Model.TutorialObjectId = cardInfo.TutorialObjectId;
                workingUnitView.Model.CantAttackInThisTurnBlocker = !cardInfo.IsManuallyPlayable;
                OpponentBoardCards.Insert(ItemPosition.End, workingUnitView);
            }
        }

        private void SetupGeneralUIAsSpecific(SpecificBattlegroundInfo specificBattlegroundInfo)
        {
            // todo implement logic
        }

        private void OnGameInitializedHandler()
        {
        }
    }

    #endregion
}
