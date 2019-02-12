using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Loom.Client;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Protobuf;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using DebugCheatsConfiguration = Loom.ZombieBattleground.BackendCommunication.DebugCheatsConfiguration;
using InstanceId = Loom.ZombieBattleground.Data.InstanceId;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Loom.ZombieBattleground.Test
{
    public class TestHelper
    {
        /// <summary>
        /// When false, tests are executed as fast as possible.
        /// When true, they are executed slowly to easy debugging.
        /// </summary>
        private const bool DebugTests = false;

        /// <summary>
        /// To be in line with AI Brain, 1.1f was taken as value from AIController.
        /// </summary>
        private const float DefaultThinkTime = DebugTests ? 1.1f : 0f;

        /// <summary>
        /// Delay between main menu transition clicks.
        /// </summary>
        private const float DefaultMainMenuTransitionDelay = DebugTests ? 1f : 0f;

        /// <summary>
        /// Time scale to use during tests.
        /// </summary>
        public const int TestTimeScale = DebugTests ? 1 : 25;

        private static TestHelper _instance;

        public bool Initialized { get; private set; }

        private readonly List<LogMessage> _errorMessages = new List<LogMessage>();

        private Scene _testScene;
        private GameObject _testerGameObject;
        private VirtualInputModule _virtualInputModule;
        private RectTransform _fakeCursorTransform;
        private GameObject _fakeCursorGameObject;

        private Enumerators.AppState _lastCheckedAppState;

        private TestBroker _testBroker;
        private Enumerators.MatchPlayer _player;
        private Enumerators.MatchPlayer _opponent;

        private float _positionalTolerance = 0.1f;

        private IAppStateManager _appStateManager;
        private IGameplayManager _gameplayManager;
        private IUIManager _uiManager;
        private IDataManager _dataManager;
        private IPvPManager _pvpManager;
        private BackendFacade _backendFacade;
        private BackendDataControlMediator _backendDataControlMediator;

        private BattlegroundController _battlegroundController;
        private SkillsController _skillsController;
        private CardsController _cardsController;
        private ActionsQueueController _actionsQueueController;
        private AbilitiesController _abilitiesController;
        private BoardArrowController _boardArrowController;
        private PlayerController _playerController;
        private BoardController _boardController;

        private GameObject _canvas1GameObject, _canvas2GameObject, _canvas3GameObject;

        public IGameplayManager GameplayManager => _gameplayManager;

        public IDataManager DataManager => _dataManager;

        public BattlegroundController BattlegroundController => _battlegroundController;

        public BackendDataControlMediator BackendDataControlMediator => _backendDataControlMediator;

        public AbilitiesController AbilitiesController => _abilitiesController;

        private int pageTransitionWaitTime = 30;

        private string _recordedExpectedValue, _recordedActualValue;

        private float _waitStartTime;
        private float _waitAmount;
        private bool _waitUnscaledTime;

        public BoardCard CurrentSpellCard;

        private readonly Random _random = new Random();

        private List<Loom.ZombieBattleground.Data.Card> _createdArmyCards;

        private List<string> _overlordNames = new List<string>()
        {
            "Brakuus",
            "Razu",
            "Vash'Kala",
            "Kalile",
            "Mhalik",
            "Valash"
        };

        private string _currentElementName = "";
        private MultiplayerDebugClient _opponentDebugClient;
        private OnBehaviourHandler _opponentDebugClientOwner;

        public int SelectedHordeIndex { get; private set; }

        public UserDataModel TestUserDataModel { get; set; }

        public static TestHelper Instance => _instance ?? (_instance = new TestHelper());

        /// <summary>
        /// Initializes a new instance of the <see cref="T:TestHelper"/> class.
        /// </summary>
        private TestHelper()
        {
        }

        /// <summary>
        /// Gets the name of the test.
        /// </summary>
        public string GetTestName()
        {
            return TestContext.CurrentContext.Test.Name;
        }

        public string GetTestUserName()
        {
            return "Test_" + GetTestName() + "_" + Guid.NewGuid();
        }

        public string GetOpponentTestUserName()
        {
            return "Test" + GetTestName() + "_Opponent_" + Guid.NewGuid();
        }

        /// <summary>
        /// SetUp method to be used for most Solo and PvP tests. Logs in and sets up a number of stuff.
        /// </summary>
        public async Task PerTestSetup()
        {
            // HACK: Unity sometimes log an harmless internal assert, but the testing framework trips on it
            // So instead, implement our own log handler that ignores asserts.
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded -= IgnoreAssertsLogMessageReceivedHandler;
            Application.logMessageReceivedThreaded += IgnoreAssertsLogMessageReceivedHandler;

            Time.timeScale = TestTimeScale;

            TestUserDataModel = new UserDataModel(GetTestUserName(), CryptoUtils.GeneratePrivateKey()) {
                IsRegistered = true
            };

            if (!Initialized)
            {
                _testScene = SceneManager.GetActiveScene();
                _testerGameObject = _testScene.GetRootGameObjects()[0];
                Object.DontDestroyOnLoad(_testerGameObject);

                GameClient.Instance.ServicesInitialized += async () =>
                {
                    GameClient.Instance.UpdateServices = false;
                    SetGameplayManagers();
                    await HandleLogin(false);
                    GameClient.Instance.UpdateServices = true;
                };

                await SceneManager.LoadSceneAsync("APP_INIT", LoadSceneMode.Single);

                await AddVirtualInputModule();

                await SetCanvases();

                #region Login

                await new WaitUntil(() => CheckCurrentAppState(Enumerators.AppState.MAIN_MENU));

                if (IsTestFailed)
                    return;

                #endregion

                Initialized = true;

                await LetsThink();
            }
            else if (_appStateManager != null)
            {
                while (_appStateManager.AppState != Enumerators.AppState.MAIN_MENU)
                {
                    await GoOnePageHigher();
                }

                await HandleLogin();
            }
        }

        public async Task PerTestTearDown()
        {
            if (TestContext.CurrentContext.Test.Name == "TestN_Cleanup")
            {
                await TearDown_Cleanup();
            }
            else
            {
                await TearDown();
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// TearDown method to be used to clear up everything after either a successful or unsuccessful test.
        /// </summary>
        /// <remarks>Generally is used only for the last test in the group.</remarks>
        public async Task TearDown_Cleanup()
        {
            Initialized = false;

            if (_opponentDebugClient != null)
            {
                await TaskAsIEnumerator(_opponentDebugClient.Reset());
            }

            if (_opponentDebugClientOwner != null)
            {
                UnityEngine.Object.Destroy(_opponentDebugClientOwner.gameObject);
            }

            Scene dontDestroyOnLoadScene = _testerGameObject.scene;

            _testScene = SceneManager.CreateScene("testScene");

            await new WaitForUpdate();

            SceneManager.MoveGameObjectToScene(_testerGameObject, _testScene);
            Scene currentScene = SceneManager.GetActiveScene();

            await new WaitForUpdate();

            foreach (GameObject rootGameObject in currentScene.GetRootGameObjects())
            {
                GameObject.Destroy(rootGameObject);
            }

            await new WaitForUpdate();

            foreach (GameObject rootGameObject in dontDestroyOnLoadScene.GetRootGameObjects())
            {
                GameObject.Destroy(rootGameObject);
            }

            await LetsThink();

            SceneManager.SetActiveScene(_testScene);

            await new WaitForUpdate();

            await SceneManager.UnloadSceneAsync(currentScene);

            Application.logMessageReceivedThreaded -= IgnoreAssertsLogMessageReceivedHandler;
            Time.timeScale = 1;
        }

        /// <summary>
        /// TearDown method to be used to go back to MainMenuPage, so that other tests can take it from there and go further.
        /// </summary>
        /// <remarks>Generally is used for all tests in the group, except for the last one (where actual cleanup happens).</remarks>
        public async Task TearDown()
        {
            await GoBackToMainScreen();
        }

        /// <summary>
        /// TearDown method to be used to go back to MainMenuPage, so that other tests can take it from there and go further.
        /// </summary>
        /// <remarks>Generally is used for all tests in the group, except for the last one (where actual cleanup happens).</remarks>
        public async Task GoBackToMainScreen()
        {
            while (_lastCheckedAppState != Enumerators.AppState.MAIN_MENU)
            {
                await GoOnePageHigher();

                await LetsThink();
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Goes one page higher in the page hierarchy, towards MainMenuPage.
        /// </summary>
        /// <remarks>Generally we need a number of these to actually get to the MainMenuPage.</remarks>
        public async Task GoOnePageHigher()
        {
            await new WaitUntil(() =>
            {
                if (_canvas1GameObject != null && _canvas1GameObject.transform.childCount >= 2)
                {
                    return true;
                }

                return false;
            });

            Enumerators.AppState appState = _appStateManager.AppState;

            await AssertCurrentPageName(appState, isGoingBack: true);

            await LetsThink();

            switch (appState)
            {
                case Enumerators.AppState.GAMEPLAY:
                    if (GameObject.Find("Button_Back") != null)
                    {
                        await ClickGenericButton("Button_Back", isGoingBack: true);

                        await LetsThink();

                        await RespondToYesNoOverlay(true, isGoingBack: true);

                        await AssertCurrentPageName(Enumerators.AppState.MAIN_MENU, isGoingBack: true);

                        await LetsThink();
                    }
                    else if (GameObject.Find("Button_Settings") != null)
                    {
                        await ClickGenericButton("Button_Settings", isGoingBack: true);

                        await LetsThink();

                        await ClickGenericButton("Button_QuitToMainMenu", isGoingBack: true);

                        await LetsThink();

                        await RespondToYesNoOverlay(true, isGoingBack: true);

                        await AssertCurrentPageName(Enumerators.AppState.MAIN_MENU, isGoingBack: true);

                        await LetsThink();
                    }

                    break;
                case Enumerators.AppState.HordeSelection:
                    await ClickGenericButton("Button_Back", isGoingBack: true);

                    await AssertCurrentPageName(Enumerators.AppState.PlaySelection, isGoingBack: true);

                    break;
                case Enumerators.AppState.DECK_EDITING:
                    await ClickGenericButton("Button_Back", isGoingBack: true);

                    await LetsThink();

                    await RespondToYesNoOverlay(false, isGoingBack: true);

                    await AssertCurrentPageName(Enumerators.AppState.HordeSelection, isGoingBack: true);

                    break;
                case Enumerators.AppState.PlaySelection:
                    await ClickGenericButton("Button_Back", isGoingBack: true);

                    await AssertCurrentPageName(Enumerators.AppState.MAIN_MENU, isGoingBack: true);

                    break;
                case Enumerators.AppState.MAIN_MENU:

                    return;
                case Enumerators.AppState.APP_INIT:
                    return;
                default:
                    throw new ArgumentException("Unhandled page: " + appState);
            }

            await new WaitForUpdate();
        }

        private void SetGameplayManagers()
        {
            _testBroker = new TestBroker();
            _player = Enumerators.MatchPlayer.CurrentPlayer;
            _opponent = Enumerators.MatchPlayer.OpponentPlayer;

            _appStateManager = GameClient.Get<IAppStateManager>();
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _uiManager = GameClient.Get<IUIManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _pvpManager = GameClient.Get<IPvPManager>();
            _backendFacade = GameClient.Get<BackendFacade>();
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();

            _battlegroundController = _gameplayManager.GetController<BattlegroundController>();
            _skillsController = _gameplayManager.GetController<SkillsController>();
            _cardsController = _gameplayManager.GetController<CardsController>();
            _actionsQueueController = _gameplayManager.GetController<ActionsQueueController>();
            _abilitiesController = _gameplayManager.GetController<AbilitiesController>();
            _boardArrowController = _gameplayManager.GetController<BoardArrowController>();
            _playerController = _gameplayManager.GetController<PlayerController>();
            _boardController = _gameplayManager.GetController<BoardController>();
        }

        private async Task SetCanvases()
        {
            _canvas1GameObject = null;

            await new WaitUntil(() => GameObject.Find("Canvas1") != null);

            _canvas1GameObject = GameObject.Find("Canvas1");
            _canvas2GameObject = GameObject.Find("Canvas2");
            _canvas3GameObject = GameObject.Find("Canvas3");

            await new WaitForUpdate();
        }

        public void TestEndHandler()
        {
            //FIXME
        }

        public bool IsTestFailed { get; private set; }

        /// <summary>
        /// Asserts if we were sent to tutorial. This is used to get out of tutorial, so that test can go on with its purpose.
        /// </summary>
        public async Task AssertIfWentDirectlyToTutorial(Func<Task> callback1, Func<Task> callback2 = null)
        {
            if (IsTestFailed)
            {
                return;
            }

            await CombinedCheck(
                (() => CheckCurrentAppState(Enumerators.AppState.GAMEPLAY), callback1),
                (() => CheckCurrentAppState(Enumerators.AppState.PlaySelection), callback1)
            );
        }

        // @todo: Get this to working using an artificial timeout
        /// <summary>
        /// Asserts if PvP match is started or matchmaking has failed.
        /// </summary>
        /// <remarks>This currently doesn't work, as timeouts have been removed.</remarks>
        public async Task AssertPvPStartedOrMatchmakingFailed(Func<Task> callback1, Func<Task> callback2)
        {
            if (IsTestFailed)
            {
                return;
            }

            WaitStart(60);

            await CombinedCheck(
                (() => CheckCurrentAppState(Enumerators.AppState.GAMEPLAY), callback1),
                (WaitTimeIsUp, callback1)
            );

            await new WaitForUpdate();
        }

        public async Task AssertMulliganPopupCameUp(Func<Task> callback1, Func<Task> callback2)
        {
            if (IsTestFailed)
                return;

            if (Constants.MulliganEnabled || GameClient.Get<IMatchManager>().MatchType != Enumerators.MatchType.PVP)
            {
                WaitStart(5);

                await CombinedCheck(
                    (CheckIfMulliganPopupCameUp, callback1),
                    (WaitTimeIsUp, callback2)
                );
            }
        }

        private async Task CombinedCheck(params (Func<bool> check, Func<Task> action)[] checks)
        {
            bool outcomeDecided = false;
            while (!outcomeDecided)
            {
                if (IsTestFailed)
                    break;

                foreach ((Func<bool> check, Func<Task> action) tuple in checks)
                {
                    if (tuple.check())
                    {
                        outcomeDecided = true;
                        if (tuple.action != null)
                        {
                            await tuple.action();
                        }
                        break;
                    }
                }

                await new WaitForUpdate();
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Checks if login box appeared.
        /// </summary>
        /// <returns><c>true</c>, if login box appeared was checked, <c>false</c> otherwise.</returns>
        private bool CheckIfLoginBoxAppeared()
        {
            if (_canvas2GameObject != null && _canvas2GameObject.transform.childCount >= 2)
            {
                if (_canvas2GameObject.transform.GetChild(1).name.Split('(')[0] == "LoginPopup")
                    return true;

                return false;
            }

            return false;
        }

        /// <summary>
        /// Checks if login error occured.
        /// </summary>
        /// <returns><c>true</c>, if login error occured, <c>false</c> otherwise.</returns>
        private bool CheckIfLoginErrorOccured()
        {
            GameObject errorTextObject = GameObject.Find("Beta_Group/Text_Error");

            if (errorTextObject != null && errorTextObject.activeInHierarchy)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if matchmaking error occured.
        /// </summary>
        /// <returns><c>true</c>, if matchmaking error (e.g. timeout) occured, <c>false</c> otherwise.</returns>
        private bool CheckIfMatchmakingErrorOccured()
        {
            if (_canvas3GameObject != null && _canvas3GameObject.transform.childCount >= 2)
            {
                if (_canvas3GameObject.transform.GetChild(1).name.Split('(')[0] == "WarningPopup")
                    return true;

                return false;
            }

            return false;
        }

        private bool CheckIfMulliganPopupCameUp()
        {
            if (GameObject.Find("MulliganPopup(Clone)") != null)
                return true;

            return false;
        }

        /// <summary>
        /// In case Terms Popup shows up, the method closes it.
        /// </summary>
        public async Task CloseTermsPopupIfRequired()
        {
            if (GameObject.Find("TermsPopup(Clone)") != null)
            {
                if (GameObject.Find("Toggle")?.GetComponent<Toggle>() != null)
                {
                    GameObject.Find("Toggle").GetComponent<Toggle>().isOn = true;
                    GameObject.Find("Toggle").GetComponent<Toggle>().onValueChanged.Invoke(true);
                }

                await LetsThink();

                await ClickGenericButton("Button_GotIt");
            }
        }

        /// <summary>
        /// Checks if the current app state is as expected.
        /// </summary>
        /// <returns><c>true</c>, if current page name is as expected, <c>false</c> otherwise.</returns>
        private bool CheckCurrentAppState(Enumerators.AppState expectedState)
        {
            return _appStateManager.AppState == expectedState;
        }

        /// <summary>
        /// Checks current app state and confirms that it’s correct with what was expected.
        /// </summary>
        /// <remarks>
        /// In case we decide to use this, we need to use it for every page. Using it for just a single one may not work as expected.
        /// </remarks>
        /// <example>
        /// await AssertCurrentPageName (Enumerators.AppState.MAIN_MENU);
        /// </example>
        /// <param name="expectedAppState">Page name</param>
        public async Task AssertCurrentPageName(Enumerators.AppState expectedAppState, string errorTextName = "", bool isGoingBack = false)
        {
            if (!isGoingBack && IsTestFailed)
                return;

            if (expectedAppState == _lastCheckedAppState)
                return;

            WaitStart(pageTransitionWaitTime, true);
            bool transitionTimeout = false;

            GameObject errorTextObject;
            await new WaitUntil(() =>
            {
                if (WaitTimeIsUp())
                {
                    transitionTimeout = true;

                    return true;
                }

                if (errorTextName.Length >= 1)
                {
                    errorTextObject = GameObject.Find(errorTextName);

                    if (errorTextObject != null && errorTextObject.activeInHierarchy)
                    {
                        Assert.Fail("Wasn't able to login. Try using USE_STAGING_BACKEND");

                        return true;
                    }
                }

                return _appStateManager.AppState == expectedAppState;
            });

            if (transitionTimeout)
            {
                Assert.Fail($"Page transition took too long from {_lastCheckedAppState} to {expectedAppState}");
            }

            Enumerators.AppState actualAppState = _appStateManager.AppState;

            Assert.AreEqual(expectedAppState, actualAppState);
            _lastCheckedAppState = _appStateManager.AppState;

            await new WaitForUpdate();
        }

        /// <summary>
        /// Goes back by one page and clicks on "Play" button.
        /// </summary>
        /// <remarks>
        /// Used when tutorial is shown instead of letting the script to test what it is meant for.
        /// </remarks>
        public async Task GoBackToMainAndPressPlay()
        {
            await GoOnePageHigher();

            await MainMenuTransition("Button_Play");
        }

        /// <summary>
        /// Adds virtual input module to the scene to handle fake mouse movements and clicks.
        /// </summary>
        /// <returns></returns>
        public async Task AddVirtualInputModule()
        {
            GameObject testSetup = GameObject.Instantiate(Resources.Load<GameObject>("Prefabs/TestSetup"));
            Object.DontDestroyOnLoad(testSetup);
            _fakeCursorGameObject = testSetup.transform.Find("Canvas/FakeCursor").gameObject;
            _fakeCursorTransform = _fakeCursorGameObject.GetComponent<RectTransform>();
            Camera uiCamera = testSetup.transform.Find("UI Camera").GetComponent<Camera>();

            UnityEngine.EventSystems.StandaloneInputModule inputModule =
                GameObject.FindObjectOfType<UnityEngine.EventSystems.StandaloneInputModule>();
            _virtualInputModule = inputModule.gameObject.AddComponent<VirtualInputModule>();
            inputModule.enabled = false;
            _virtualInputModule.SetLinks(_fakeCursorTransform, uiCamera);

            await new WaitForUpdate();
        }

        /// <summary>
        /// Moves cursor to the location of the object
        /// </summary>
        /// <param name="objectName">Name of the object in the scene</param>
        /// <param name="duration">Movement duration</param>
        public async Task MoveCursorToObject(string objectName, float duration)
        {
            GameObject targetObject = GameObject.Find(objectName);

            Vector2 from = _fakeCursorTransform.position;
            Vector2 to = targetObject.transform.position;

            Vector2 cursorPosition = from;
            float interpolation = 0f;
            while (Vector2.Distance(cursorPosition, to) >= _positionalTolerance)
            {
                cursorPosition = Vector2.Lerp(from, to, interpolation / duration);
                _fakeCursorTransform.position = cursorPosition;

                interpolation = Mathf.Min(interpolation + Time.time, duration);

                await new WaitForUpdate();
            }
        }

        /// <summary>
        /// Clicks using the virtual mouse cursor.
        /// </summary>
        /// <remarks>
        /// Useful only on UI items.
        /// </remarks>
        public async Task FakeClick()
        {
            _virtualInputModule.Press();

            await new WaitForUpdate();

            _virtualInputModule.Release();

            await new WaitForUpdate();
        }

        /// <summary>
        /// Goes through list of buttons and checks if they are clickable.
        /// </summary>
        /// <param name="buttonNames">Button names.</param>
        public async Task ButtonListClickCheck(string[] buttonNames)
        {
            foreach (string buttonName in buttonNames)
            {
                await ButtonClickCheck(buttonName);

                await LetsThink();

                await new WaitForUpdate();
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Checks if a button is clickable.
        /// </summary>
        /// <param name="buttonName">Button name.</param>
        public async Task ButtonClickCheck(string buttonName)
        {
            GameObject targetGameObject = GameObject.Find(buttonName);

            if (targetGameObject != null)
            {
                if (targetGameObject.GetComponent<ButtonShiftingContent>() != null)
                {
                    ButtonShiftingContent targetButton = targetGameObject.GetComponent<ButtonShiftingContent>();

                    bool buttonClickable = false;

                    Button dummyButton = new GameObject().AddComponent<Button>();

                    dummyButton.onClick = targetButton.onClick;
                    targetButton.onClick = new Button.ButtonClickedEvent();
                    targetButton.onClick.AddListener(() =>
                    {
                        buttonClickable = true;
                    });

                    await new WaitForUpdate();

                    await MoveCursorToObject(buttonName, 1);
                    await FakeClick();

                    await new WaitForUpdate();

                    WaitStart(3);
                    await new WaitUntil(() => buttonClickable || WaitTimeIsUp());

                    if (!buttonClickable)
                    {
                        targetButton.onClick = dummyButton.onClick;
                        dummyButton.onClick = new Button.ButtonClickedEvent();
                        dummyButton.onClick.RemoveAllListeners();

                        Assert.Fail("Button is not clickable: " + buttonName);
                    }
                    else
                    {
                        targetButton.onClick = dummyButton.onClick;
                        dummyButton.onClick = new Button.ButtonClickedEvent();
                        dummyButton.onClick.RemoveAllListeners();

                        Debug.Log("Checked button and it worked fine: " + buttonName);
                    }

                    await new WaitForUpdate();
                }
                else if (targetGameObject.GetComponent<MenuButtonNoGlow>() != null)
                {
                    MenuButtonNoGlow targetButton = targetGameObject.GetComponent<MenuButtonNoGlow>();

                    bool buttonClickable = false;

                    MenuButtonNoGlow dummyButton = new GameObject().AddComponent<MenuButtonNoGlow>();

                    dummyButton.Clicked = targetButton.Clicked;
                    targetButton.Clicked = new UnityEngine.Events.UnityEvent();
                    targetButton.Clicked.AddListener(() =>
                    {
                        buttonClickable = true;
                    });

                    await new WaitForUpdate();

                    await MoveCursorToObject(buttonName, 1);
                    await FakeClick();

                    await new WaitForUpdate();

                    WaitStart(3);
                    await new WaitUntil(() => buttonClickable || WaitTimeIsUp());

                    if (!buttonClickable)
                    {
                        targetButton.Clicked = dummyButton.Clicked;
                        dummyButton.Clicked = new Button.ButtonClickedEvent();
                        dummyButton.Clicked.RemoveAllListeners();

                        Assert.Fail("Button is not clickable: " + buttonName);
                    }
                    else
                    {
                        targetButton.Clicked = dummyButton.Clicked;
                        dummyButton.Clicked = new Button.ButtonClickedEvent();
                        dummyButton.Clicked.RemoveAllListeners();

                        Debug.Log("Checked button and it worked fine: " + buttonName);
                    }

                    await new WaitForUpdate();
                }
                else if (targetGameObject.GetComponent<Button>() != null)
                {
                    Button targetButton = targetGameObject.GetComponent<Button>();

                    bool buttonClickable = false;

                    Button dummyButton = new GameObject().AddComponent<Button>();

                    dummyButton.onClick = targetButton.onClick;
                    targetButton.onClick = new Button.ButtonClickedEvent();
                    targetButton.onClick.AddListener(() =>
                    {
                        buttonClickable = true;
                    });

                    await new WaitForUpdate();

                    await MoveCursorToObject(buttonName, 1);
                    await FakeClick();

                    await new WaitForUpdate();

                    WaitStart(3);
                    await new WaitUntil(() => buttonClickable || WaitTimeIsUp());

                    if (!buttonClickable)
                    {
                        targetButton.onClick = dummyButton.onClick;
                        dummyButton.onClick = new Button.ButtonClickedEvent();
                        dummyButton.onClick.RemoveAllListeners();

                        Assert.Fail("Button is not clickable: " + buttonName);
                    }
                    else
                    {
                        targetButton.onClick = dummyButton.onClick;
                        dummyButton.onClick = new Button.ButtonClickedEvent();
                        dummyButton.onClick.RemoveAllListeners();

                        Debug.Log("Checked button and it worked fine: " + buttonName);
                    }

                    await new WaitForUpdate();
                }
            }
            else
            {
                Assert.Fail("Button wasn't found: " + buttonName);
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Logs in into the game using one of the keys. Picks a correct one depending on whether it is an passive or active tester.
        /// </summary>
        /// <remarks>The login.</remarks>
        public async Task HandleLogin(bool waitForMainMenu = true) {
            if (BackendDataControlMediator.UserDataModel != null &&
                BackendDataControlMediator.UserDataModel == TestUserDataModel)
                return;

            BackendDataControlMediator.UserDataModel = TestUserDataModel;

            await BackendDataControlMediator.LoginAndLoadData();

            WaitStart(10);
            await new WaitUntil(() => waitForMainMenu && CheckCurrentAppState(Enumerators.AppState.MAIN_MENU) || WaitTimeIsUp());

            if (waitForMainMenu && !CheckCurrentAppState(Enumerators.AppState.MAIN_MENU))
            {
                Assert.Fail(
                    $"Login wasn't completed. Please ensure you have logged in previously, and that you're pointing to the Stage or Production server.");
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Takes name of the gameObject that has Button or ButtonShiftingContent component and clicks it.
        /// </summary>
        /// <param name="buttonName">Name of the button to click</param>
        /// <param name="parentGameObject">(Optional) Parent object to look under</param>
        /// <param name="count">(Optional) Number of times to click</param>
        public async Task ClickGenericButton(string buttonName, GameObject parentGameObject = null, int count = 1, bool isGoingBack = false)
        {
            if (!isGoingBack && IsTestFailed)
            {
                return;
            }

            WaitStart(5);
            GameObject menuButtonGameObject;
            bool clickTimeout = false;

            await new WaitUntil(() =>
            {
                if (parentGameObject != null)
                {
                    menuButtonGameObject = parentGameObject.transform.Find(buttonName)?.gameObject;
                }
                else
                {
                    menuButtonGameObject = GameObject.Find(buttonName);
                }

                if (WaitTimeIsUp())
                {
                    clickTimeout = true;

                    return true;
                }
                else if (menuButtonGameObject == null || !menuButtonGameObject.activeInHierarchy)
                {
                    return false;
                }
                else if (menuButtonGameObject.GetComponent<ButtonShiftingContent>() != null)
                {
                    menuButtonGameObject.GetComponent<ButtonShiftingContent>().onClick.Invoke();

                    return true;
                }
                else if (menuButtonGameObject.GetComponent<Button>() != null)
                {
                    menuButtonGameObject.GetComponent<Button>().onClick.Invoke();

                    return true;
                }

                return false;
            });

            if (clickTimeout)
            {
                Assert.Fail($"Couldn't find the button: {buttonName}");
            }

            await LetsThink(0.5f);

            if (count >= 2)
            {
                await ClickGenericButton(buttonName, parentGameObject, count - 1);
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Takes a transition path (list of buttons to click) and goes through them clicking each.
        /// </summary>
        /// <param name="transitionPath">Slash separated list of buttons</param>
        /// <param name="delay">(Optional) Delay between clicks</param>
        public async Task MainMenuTransition(string transitionPath, float delay = DefaultMainMenuTransitionDelay, bool isGoingBack = false)
        {
            foreach (string buttonName in transitionPath.Split('/'))
            {
                await ClickGenericButton(buttonName);

                if (!isGoingBack && IsTestFailed)
                {
                    break;
                }

                if (delay <= 0f)
                {
                    await new WaitForEndOfFrame();
                    await new WaitForEndOfFrame();
                }
                else
                {
                    await new WaitForSeconds(delay);
                }
            }
        }

        /// <summary>
        /// Clicks on the overlay Yes/No button.
        /// </summary>
        /// <param name="isResponseYes">Is the response Yes?</param>
        public async Task RespondToYesNoOverlay(bool isResponseYes, bool isGoingBack = false)
        {
            if (!isGoingBack && IsTestFailed)
            {
                return;
            }

            string buttonName = isResponseYes ? "Button_Yes" : "Button_No";

            ButtonShiftingContent overlayButton = null;
            await new WaitUntil(() =>
            {
                overlayButton = GameObject.Find(buttonName)?.GetComponent<ButtonShiftingContent>();
                return overlayButton != null;
            });

            overlayButton.onClick.Invoke();

            await new WaitForUpdate();
        }

        /// <summary>
        /// Waits until a page unloads.
        /// </summary>
        public async Task WaitUntilPageUnloads()
        {
            await new WaitUntil(() =>
            {
                if (_canvas1GameObject != null && _canvas1GameObject.transform.childCount <= 1)
                {
                    return true;
                }

                return false;
            });
        }

        #region Interactions with PvP module

        /// <summary>
        /// Sets tags to be used by the matchmaking system.
        /// </summary>
        /// <param name="tags">Tags</param>
        public void SetPvPTags(IList<string> tags)
        {
            if (IsTestFailed)
            {
                return;
            }

            if (tags == null || tags.Count <= 0)
            {
                _pvpManager.PvPTags = null;

                return;
            }

            _pvpManager.PvPTags = new List<string>();
            foreach (string tag in tags)
            {
                _pvpManager.PvPTags.Add(tag);
            }
        }

        /// <summary>
        /// Get matchmaking tags.
        /// </summary>
        public List<string> GetPvPTags()
        {
            return _pvpManager.PvPTags;
        }

        public DebugCheatsConfiguration DebugCheats
        {
            get => _pvpManager.DebugCheats;
        }

        #endregion

        #region Adapted from AIController

        public async Task PlayCardFromHandToBoard(WorkingCard card, ItemPosition position, BoardObject entryAbilityTarget = null)
        {
            bool needTargetForAbility = false;
            if (card.LibraryCard.Abilities != null && card.LibraryCard.Abilities.Count > 0)
            {
                needTargetForAbility =
                    card.LibraryCard.Abilities.FindAll(x => x.AbilityTargetTypes.Count > 0).Count > 0;
            }

            switch (card.LibraryCard.CardKind)
            {
                case Enumerators.CardKind.CREATURE when _testBroker.GetBoardCards(_player).Count < _gameplayManager.OpponentPlayer.MaxCardsInPlay:
                    if (_player == Enumerators.MatchPlayer.CurrentPlayer)
                    {
                        BoardCard boardCard = _battlegroundController.PlayerHandCards.FirstOrDefault(x => x.WorkingCard.Equals(card));
                        Assert.NotNull(boardCard, $"Card {card} not found in local player hand");

                        _cardsController.PlayPlayerCard(_testBroker.GetPlayer(_player),
                            boardCard,
                            boardCard.HandBoardCard,
                            playCardOnBoard =>
                            {
                                PlayerMove playerMove = new PlayerMove(Enumerators.PlayerActionType.PlayCardOnBoard, playCardOnBoard);
                                _gameplayManager.PlayerMoves.AddPlayerMove(playerMove);
                            },
                            entryAbilityTarget);

                        await new WaitForUpdate();

                        /*if (target == null && needTargetForAbility)
                        {
                            WaitStart(3);
                            await new WaitUntil(() => _boardArrowController.CurrentBoardArrow != null || WaitTimeIsUp());
                            _boardArrowController.ResetCurrentBoardArrow();

                            await LetsThink();

                            WaitStart(3);
                            await new WaitUntil(() => _abilitiesController.CurrentActiveAbility != null || WaitTimeIsUp());
                            _abilitiesController.CurrentActiveAbility.Ability.SelectedTargetAction();
                            _abilitiesController.CurrentActiveAbility.Ability.DeactivateSelectTarget();

                            await LetsThink();
                        }*/
                    }
                    else
                    {
                        _testBroker.GetPlayer(_player).RemoveCardFromHand(card);
                        _testBroker.GetPlayer(_player).AddCardToBoard(card, position);

                        _cardsController.PlayOpponentCard(_testBroker.GetPlayer(_player), card.InstanceId, entryAbilityTarget, null, PlayCardCompleteHandler);
                    }

                    _cardsController.DrawCardInfo(card);

                    break;
                case Enumerators.CardKind.SPELL:
                    if (entryAbilityTarget != null && needTargetForAbility || !needTargetForAbility)
                    {
                        _testBroker.GetPlayer(_player).RemoveCardFromHand(card);
                        _testBroker.GetPlayer(_player).AddCardToBoard(card, position);

                        if (_player == Enumerators.MatchPlayer.CurrentPlayer)
                        {
                            BoardCard boardCard = _battlegroundController.PlayerHandCards.First(x => x.WorkingCard.Equals(card));

                            _cardsController.PlayPlayerCard(_testBroker.GetPlayer(_player),
                                boardCard,
                                boardCard.HandBoardCard,
                                playCardOnBoard =>
                                {
                                    //todo: handle abilities here

                                    PlayerMove playerMove = new PlayerMove(Enumerators.PlayerActionType.PlayCardOnBoard, playCardOnBoard);
                                    _gameplayManager.PlayerMoves.AddPlayerMove(playerMove);
                                },
                                entryAbilityTarget);
                        }
                        else
                        {
                            _cardsController.PlayOpponentCard(_testBroker.GetPlayer(_player), card.InstanceId, entryAbilityTarget, null, PlayCardCompleteHandler);
                        }

                        _cardsController.DrawCardInfo(card);
                    }

                    break;
            }

            _testBroker.GetPlayer(_player).CurrentGoo -= card.LibraryCard.Cost;

            await new WaitForUpdate();
        }

        private void PlayCardCompleteHandler(WorkingCard card, BoardObject target)
        {
            WorkingCard workingCard = null;

            if (_gameplayManager.OpponentPlayer.CardsOnBoard.Count > 0)
            {
                workingCard = _gameplayManager.OpponentPlayer.CardsOnBoard[_gameplayManager.OpponentPlayer.CardsOnBoard.Count - 1];
            }

            if (workingCard == null || card == null)
                return;

            switch (card.LibraryCard.CardKind)
            {
                case Enumerators.CardKind.CREATURE:
                {
                    BoardUnitView boardUnitViewElement = new BoardUnitView(new BoardUnitModel(), GameObject.Find("OpponentBoard").transform);
                    GameObject boardUnit = boardUnitViewElement.GameObject;
                    boardUnit.tag = SRTags.OpponentOwned;
                    boardUnit.transform.position = Vector3.up * 2f; // Start pos before moving cards to the opponents board
                    boardUnitViewElement.Model.OwnerPlayer = card.Owner;
                    boardUnitViewElement.Model.TutorialObjectId = card.TutorialObjectId;
                    boardUnitViewElement.SetObjectInfo(workingCard);
                    _battlegroundController.OpponentBoardCards.Insert(ItemPosition.End, boardUnitViewElement);
                    _gameplayManager.OpponentPlayer.BoardCards.Insert(ItemPosition.End, boardUnitViewElement);

                    _actionsQueueController.PostGameActionReport(new PastActionsPopup.PastActionParam()
                    {
                        ActionType = Enumerators.ActionType.PlayCardFromHand,
                        Caller = boardUnitViewElement.Model,
                        TargetEffects = new List<PastActionsPopup.TargetEffectParam>()
                    });

                    boardUnitViewElement.PlayArrivalAnimation();

                    _abilitiesController.ResolveAllAbilitiesOnUnit(boardUnitViewElement.Model, false);
                    _boardController.UpdateCurrentBoardOfPlayer(_gameplayManager.CurrentPlayer,
                        () =>
                        {
                            bool createTargetArrow = false;

                            if (card.LibraryCard.Abilities != null && card.LibraryCard.Abilities.Count > 0)
                            {
                                createTargetArrow =
                                    _abilitiesController.IsAbilityCanActivateTargetAtStart(
                                        card.LibraryCard.Abilities[0]);
                            }

                            if (target != null)
                            {
                                Action callback = () =>
                                {
                                    _abilitiesController.CallAbility(card.LibraryCard,
                                        null,
                                        workingCard,
                                        Enumerators.CardKind.CREATURE,
                                        boardUnitViewElement.Model,
                                        null,
                                        false,
                                        null,
                                        null,
                                        target);
                                };

                                _boardArrowController.DoAutoTargetingArrowFromTo<OpponentBoardArrow>(boardUnit.transform,
                                    target,
                                    action: callback);
                            }
                            else
                            {
                                _abilitiesController.CallAbility(card.LibraryCard,
                                    null,
                                    workingCard,
                                    Enumerators.CardKind.CREATURE,
                                    boardUnitViewElement.Model,
                                    null,
                                    false,
                                    null,
                                    null);
                            }
                        });
                    break;
                }
                case Enumerators.CardKind.SPELL:
                {
                    GameObject spellCard = UnityEngine.Object.Instantiate(_cardsController.ItemCardViewPrefab);
                    spellCard.transform.position = GameObject.Find("OpponentSpellsPivot").transform.position;

                    CurrentSpellCard = new SpellBoardCard(spellCard);

                    CurrentSpellCard.Init(workingCard);
                    CurrentSpellCard.SetHighlightingEnabled(false);

                    BoardSpell boardSpell = new BoardSpell(spellCard, workingCard);

                    spellCard.gameObject.SetActive(false);

                    bool createTargetArrow = false;

                    if (card.LibraryCard.Abilities != null && card.LibraryCard.Abilities.Count > 0)
                    {
                        createTargetArrow =
                            _abilitiesController.IsAbilityCanActivateTargetAtStart(card.LibraryCard.Abilities[0]);
                    }

                    if (target != null)
                    {
                        Action callback = () =>
                        {
                            _abilitiesController.CallAbility(card.LibraryCard,
                                null,
                                workingCard,
                                Enumerators.CardKind.SPELL,
                                boardSpell,
                                null,
                                false,
                                null,
                                null,
                                target);
                        };

                        _boardArrowController.DoAutoTargetingArrowFromTo<OpponentBoardArrow>(
                            _gameplayManager.OpponentPlayer.AvatarObject.transform,
                            target,
                            action: callback);
                    }
                    else
                    {
                        _abilitiesController.CallAbility(card.LibraryCard,
                            null,
                            workingCard,
                            Enumerators.CardKind.SPELL,
                            boardSpell,
                            null,
                            false,
                            null,
                            null);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Uses a skill (potentially on specific object).
        /// </summary>
        /// <param name="skill">Skill.</param>
        /// <param name="target">Target.</param>
        public async Task DoBoardSkill(
            BoardSkill skill,
            BoardObject target = null)
        {
            TaskCompletionSource<GameplayQueueAction<object>> taskCompletionSource = new TaskCompletionSource<GameplayQueueAction<object>>();
            skill.StartDoSkill();

            if (target != null)
            {
                Assert.IsNotNull(skill.FightTargetingArrow, "skill.FightTargetingArrow == null, are you sure this skill has an active target?");
                skill.FightTargetingArrow.SetTarget(target);
                await new WaitForSeconds(0.4f); // just so we can see the arrow for a short bit

                switch (target)
                {
                    case Player player:
                        skill.FightTargetingArrow.OnPlayerSelected(player);
                        break;
                    case BoardUnitModel boardUnitModel:
                        skill.FightTargetingArrow.OnCardSelected(_battlegroundController.GetBoardUnitViewByModel(boardUnitModel));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(target), target.GetType(), null);
                }
            }

            GameplayQueueAction<object> gameplayQueueAction = skill.EndDoSkill();
            Action<GameplayQueueAction<object>> onDone = null;
            onDone = gameplayQueueAction2 =>
            {
                taskCompletionSource.SetResult(gameplayQueueAction2);
                gameplayQueueAction.OnActionDoneEvent -= onDone;
            };
            gameplayQueueAction.OnActionDoneEvent += onDone;

            await taskCompletionSource.Task;
        }

        #endregion

        public Player GetCurrentPlayer()
        {
            return _testBroker.GetPlayer(_player);
        }

        public Player GetOpponentPlayer()
        {
            return _testBroker.GetPlayer(_opponent);
        }

        public BoardUnitView GetCardOnBoardByInstanceId(InstanceId instanceId, Enumerators.MatchPlayer player)
        {
            BoardUnitView boardUnitView =
                _testBroker.GetPlayer(player)
                    .BoardCards
                    .FirstOrDefault(card => card.Model.InstanceId == instanceId);

            if (boardUnitView == null)
                throw new Exception($"Card {instanceId} not found on board");

            return boardUnitView;
        }

        /// <summary>
        /// Waits for a specific amount of time.
        /// </summary>
        public async Task LetsThink(float thinkTime = DefaultThinkTime, bool forceRealtime = false)
        {
            if (thinkTime <= 0f)
            {
                await new WaitForEndOfFrame();
                await new WaitForEndOfFrame();
            }
            else
            {
                if (forceRealtime)
                {
                    await new WaitForSecondsRealtime(thinkTime);
                }
                else
                {
                    await new WaitForSeconds(thinkTime);
                }
            }
        }

        /// <summary>
        /// Waits until the player order is decided and records the player overlord’s name in the process (in case we need it for assertion).
        /// </summary>
        public async Task WaitUntilPlayerOrderIsDecided()
        {
            // await new WaitUntil(() => GameObject.Find("PlayerOrderPopup(Clone)") != null);
            await new WaitUntil(() => _gameplayManager.CurrentPlayer != null && _gameplayManager.OpponentPlayer != null);
            await new WaitUntil(() => _gameplayManager.CurrentPlayer.MulliganWasStarted);

            // TODO: there is a race condition when the popup has shown and hidden itself
            // *before* this method is even entered. As a result, test gets stuck, waiting for the popup forever.

            /*await new WaitUntil ( () => GameObject.Find ("PlayerOrderPopup (Clone)") != null);

            RecordActualOverlordName ();

            await new WaitUntil (() => GameObject.Find ("PlayerOrderPopup(Clone)") == null);

            await new WaitForUpdate();
            await new WaitUntil ( () => GameObject.Find ("PlayerOrderPopup (Clone)") == null);
    */
        }

        public BoardSkill GetBoardSkill(Player player, SkillId skillId)
        {
            List<BoardSkill> boardSkills = new List<BoardSkill>();
            if (player == GetCurrentPlayer())
            {
                boardSkills.Add(_skillsController.PlayerPrimarySkill);
                boardSkills.Add(_skillsController.PlayerSecondarySkill);
            }
            else
            {
                boardSkills.Add(_skillsController.OpponentPrimarySkill);
                boardSkills.Add(_skillsController.OpponentSecondarySkill);
            }

            return boardSkills.First(skill => skill.SkillId == skillId);
        }

        /// <summary>
        /// Picks Mulligan Cards.
        /// </summary>
        /// <remarks>todo: Doesn't work, after the latest changes done to the way this is handled.</remarks>
        public async Task DecideWhichCardsToPick()
        {
            if (IsGameEnded())
                return;

            await LetsThink();

            await ClickGenericButton("Button_Keep");
        }

        /// <summary>
        /// Ends the turn for the player.
        /// </summary>
        public async Task EndTurn()
        {
            await HandleConnectivityIssues();
            if (IsGameEnded())
                return;

            _battlegroundController.StopTurn();
            GameObject.Find("_1_btn_endturn").GetComponent<EndTurnButton>().SetEnabled(false);

            await new WaitForUpdate();
        }

        /// <summary>
        /// Waits for player’s first turn, to start off playing. In case it is our turn, it does nothing, if not, it tracks until input is unblocked.
        /// </summary>
        public async Task WaitUntilOurFirstTurn()
        {
            if (IsGameEnded())
                return;

            if (_gameplayManager.CurrentTurnPlayer.InstanceId == _gameplayManager.CurrentPlayer.InstanceId)
            {
                await new WaitForUpdate();
            }
            else
            {
                if (IsGameEnded())
                    return;

                await WaitUntilOurTurnStarts();

                if (IsGameEnded())
                    return;

                await WaitUntilInputIsUnblocked();
            }

            await LetsThink();
        }

        /// <summary>
        /// Waits until player’s turn starts.
        /// </summary>
        public async Task WaitUntilOurTurnStarts()
        {
            await HandleConnectivityIssues();

            await new WaitUntil(() =>
                IsGameEnded() || _uiManager.GetPopup<YourTurnPopup>().Self != null || _uiManager.GetPopup<ConnectionPopup>().Self != null);

            await HandleConnectivityIssues();

            await new WaitUntil(() => IsGameEnded() || _uiManager.GetPopup<YourTurnPopup>().Self == null);

            await HandleConnectivityIssues();

            await new WaitUntil(() => IsGameEnded() || _playerController.IsActive);
        }

        /// <summary>
        /// Waits until player can make a move.
        /// </summary>
        public async Task WaitUntilInputIsUnblocked()
        {
            await HandleConnectivityIssues();

            await new WaitUntil(() => IsGameEnded() || _gameplayManager.IsLocalPlayerTurn());

            await HandleConnectivityIssues();
        }

        // todo: reconsider having this
        /// <summary>
        /// Waits until the card is added to board.
        /// </summary>
        /// <remarks>Was written specifically for tutorials, where some steps require it.</remarks>
        /// <param name="boardName">Board name.</param>
        public async Task WaitUntilCardIsAddedToBoard(string boardName)
        {
            Transform boardTransform = GameObject.Find(boardName).transform;
            int boardChildrenCount = boardTransform.childCount;

            await new WaitUntil(() =>
                boardChildrenCount < boardTransform.childCount && boardChildrenCount < _battlegroundController.OpponentBoardCards.Count);
        }

        /// <summary>
        /// Executes tasks on each turn of a match for the local player.
        /// </summary>
        /// <param name="turnTaskGenerator">I
        /// Enumerator-like generator function that returns the current task to execute.
        /// The method stops if null is returned from the generator.
        /// </param>
        /// <returns></returns>
        public async Task PlayMoves(Func<Func<Task>> turnTaskGenerator)
        {
            await AssertCurrentPageName(Enumerators.AppState.GAMEPLAY);

            //Debug.Log("!a -3");

            await WaitUntilPlayerOrderIsDecided();

            //Debug.Log("!a -2");

            await AssertMulliganPopupCameUp(
                DecideWhichCardsToPick,
                null);

            //Debug.Log("!a -1");

            await WaitUntilOurFirstTurn();

            Func<Task> currentTurnTask;
            while ((currentTurnTask = turnTaskGenerator()) != null)
            {
                await LetsThink();

                //Debug.Log("!a 0");

                await TaskAsIEnumerator(currentTurnTask());

                //Debug.Log("!a 1");

                if (IsGameEnded())
                    break;

                await WaitUntilOurTurnStarts();

                //Debug.Log("!a 2");

                if (IsGameEnded())
                    break;

                await WaitUntilInputIsUnblocked();

                //Debug.Log("!a 3");

                if (IsGameEnded())
                    break;
            }
        }

        /// <summary>
        /// Checks if game has ended.
        /// </summary>
        public bool IsGameEnded()
        {
            if (IsTestFailed)
            {
                return true;
            }
            else if (_gameplayManager == null || _gameplayManager.IsGameEnded)
            {
                return true;
            }
            else if (_gameplayManager.CurrentPlayer == null || _gameplayManager.OpponentPlayer == null)
            {
                return true;
            }

            int playerHP = _gameplayManager.CurrentPlayer.Defense;
            int opponentHP = _gameplayManager.OpponentPlayer.Defense;

            if (playerHP <= 0 || opponentHP <= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task HandleConnectivityIssues()
        {
            if (IsTestFailed)
            {
                return;
            }

            if (_uiManager.GetPopup<ConnectionPopup>().Self != null)
            {
                WaitStart(10);

                await ClickGenericButton("Button_Reconnect");

                await new WaitUntil(() => _uiManager.GetPopup<ConnectionPopup>().Self != null || WaitTimeIsUp());

                if (_uiManager.GetPopup<ConnectionPopup>().Self != null)
                {
                    Assert.Fail("Connectivity issue came up.");
                }
            }

            await new WaitForUpdate();
        }

        #region Horde Creation / Editing

        /// <summary>
        /// Adds a Valash Deck and cards from Life group.
        /// </summary>
        public async Task AddValashHorde()
        {
            if (IsTestFailed)
            {
                return;
            }

            await ClickGenericButton("Image_BaackgroundGeneral");
            await AssertCurrentPageName(Enumerators.AppState.HERO_SELECTION);

            await PickOverlord("Valash", false);
            await PickOverlordAbility(0);

            await ClickGenericButton("Canvas_BackLayer/Button_Continue");
            await AssertCurrentPageName(Enumerators.AppState.DECK_EDITING);

            SetupArmyCards();

            await SetDeckTitle("Valash");

            await AddCardToHorde("Life", "Azuraz", 4);
            await AddCardToHorde("Life", "Bloomer", 4);
            await AddCardToHorde("Life", "Zap", 4);
            await AddCardToHorde("Life", "Amber", 4);
            await AddCardToHorde("Life", "Bark", 4);
            await AddCardToHorde("Life", "Puffer", 2);
            await AddCardToHorde("Life", "Sapper", 2);
            await AddCardToHorde("Life", "Keeper", 2);
            await AddCardToHorde("Life", "Cactuz", 2);
            await AddCardToHorde("Life", "EverlaZting", 2);

            AssertCorrectNumberOfCards();

            await ClickGenericButton("Button_Save");
        }

        /// <summary>
        /// Adss a Kalile Horde and cards from Air group.
        /// </summary>
        public async Task AddKalileHorde()
        {
            if (IsTestFailed)
            {
                return;
            }

            await ClickGenericButton("Image_BaackgroundGeneral");
            await AssertCurrentPageName(Enumerators.AppState.HERO_SELECTION);

            await PickOverlord("Kalile", false);
            await PickOverlordAbility(1);

            await ClickGenericButton("Canvas_BackLayer/Button_Continue");
            await AssertCurrentPageName(Enumerators.AppState.DECK_EDITING);

            SetupArmyCards();

            await SetDeckTitle("Kalile");

            await AddCardToHorde("Air", "Whizpar", 4);
            await AddCardToHorde("Air", "Soothsayer", 4);
            await AddCardToHorde("Air", "FumeZ", 4);
            await AddCardToHorde("Air", "Breezee", 4);
            await AddCardToHorde("Air", "Banshee", 4);
            await AddCardToHorde("Air", "Zhocker", 4);
            await AddCardToHorde("Air", "Bouncer", 2);
            await AddCardToHorde("Air", "Wheezy", 4);

            AssertCorrectNumberOfCards();

            await ClickGenericButton("Button_Save");
        }

        /// <summary>
        /// Adds a Razu Horde deck and cards from Fire group.
        /// </summary>
        public async Task AddRazuHorde()
        {
            if (IsTestFailed)
            {
                return;
            }

            await ClickGenericButton("Image_BaackgroundGeneral");
            await AssertCurrentPageName(Enumerators.AppState.HERO_SELECTION);

            await PickOverlord("Razu", true);
            await PickOverlordAbility(1);

            await ClickGenericButton("Canvas_BackLayer/Button_Continue");
            await AssertCurrentPageName(Enumerators.AppState.DECK_EDITING);

            SetupArmyCards();

            await SetDeckTitle("Razu");

            await AddCardToHorde("Fire", "Pyromaz", 4);
            await AddCardToHorde("Fire", "Quazi", 4);
            await AddCardToHorde("Fire", "Ember", 4);
            await AddCardToHorde("Fire", "Firewall", 4);
            await AddCardToHorde("Fire", "BurZt", 4);
            await AddCardToHorde("Fire", "Firecaller", 4);
            await AddCardToHorde("Fire", "Burrrnn", 2);
            await AddCardToHorde("Fire", "Werezomb", 2);
            await AddCardToHorde("Fire", "Modo", 2);

            AssertCorrectNumberOfCards();

            await ClickGenericButton("Button_Save");
        }

        /// <summary>
        /// Creates list of cards according to the ones available on the page.
        /// </summary>
        public void SetupArmyCards()
        {
            DeckBuilderCard[] deckBuilderCards = GameObject.FindObjectsOfType<DeckBuilderCard>();

            if (deckBuilderCards == null || deckBuilderCards.Length == 0)
                return;

            _createdArmyCards = new List<Loom.ZombieBattleground.Data.Card>();
            foreach (DeckBuilderCard deckBuilderCard in deckBuilderCards)
            {
                _createdArmyCards.Add((Loom.ZombieBattleground.Data.Card) deckBuilderCard.Card);
            }
        }

        /// <summary>
        /// Picks an overlord, by the specified name.
        /// </summary>
        /// <param name="overlordName">Overlord name.</param>
        /// <param name="goRight">If set to <c>true</c> goes right, until finds what you set.</param>
        public async Task PickOverlord(string overlordName, bool goRight = true)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            int selectedIndex = 0;

            while (_overlordNames[selectedIndex] != overlordName)
            {
                if (goRight)
                {
                    await ClickGenericButton("Button_RightArrow");

                    selectedIndex = (selectedIndex + 1) % _overlordNames.Count;
                }
                else
                {
                    await ClickGenericButton("Button_LeftArrow");

                    selectedIndex = (selectedIndex + 6 - 1) % _overlordNames.Count;
                }

                await LetsThink();
                await LetsThink();
            }

            await ClickGenericButton("Button_Continue");

            await new WaitForUpdate();
        }

        /// <summary>
        /// Picks the overlord ability.
        /// </summary>
        /// <param name="index">Index.</param>
        public async Task PickOverlordAbility(int index)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            GameObject abilitiesParent = GameObject.Find("Abilities");

            if (index >= abilitiesParent.transform.childCount)
            {
                Assert.Fail("Index higher than number of abilities");
            }

            if (abilitiesParent.transform.GetChild(index).GetComponent<Button>().IsInteractable())
            {
                abilitiesParent.transform.GetChild(index).GetComponent<Button>().onClick.Invoke();
            }

            await LetsThink();
        }

        /// <summary>
        /// Sets the name/title of the deck.
        /// </summary>
        /// <param name="deckTitle">Deck title.</param>
        public async Task SetDeckTitle(string deckTitle)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            GameObject deckTitleInput = GameObject.Find("DeckTitleInputText");

            if (deckTitleInput == null)
            {
                Assert.Fail("DeckTitleInputText doesn't exist");
            }

            TMP_InputField deckTitleInputField = deckTitleInput.GetComponent<TMP_InputField>();

            if (deckTitleInputField == null)
            {
                Assert.Fail("TextMeshPro InputField doesn't exist");
            }

            deckTitleInputField.text = deckTitle; // for visibility during testing
            deckTitleInputField.onEndEdit.Invoke(deckTitle); // for post deck creation result

            await LetsThink();
        }

        private async Task PickElement(string elementName)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            Transform elementsParent = GameObject.Find("ElementsToggles").transform;

            Toggle elementToggle = elementsParent.Find(elementName)?.GetComponent<Toggle>();

            if (elementToggle != null)
            {
                if (elementName == _currentElementName)
                {
                    if (elementName != "Fire")
                    {
                        await PickElement("Fire");
                    }
                    else
                    {
                        await PickElement("Water");
                    }
                }

                elementToggle.onValueChanged.Invoke(true);

                _currentElementName = elementName;
            }

            await LetsThink();

            SetupArmyCards();

            await LetsThink();
        }

        /// <summary>
        /// Adds a card to the Horde from specified element.
        /// </summary>
        /// <param name="elementName">Element name.</param>
        /// <param name="cardName">Card name.</param>
        /// <param name="count">Count.</param>
        public async Task AddCardToHorde(string elementName, string cardName, int count = 1)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            Loom.ZombieBattleground.Data.Card armyCard = _createdArmyCards.Find(x =>
                x.Name == cardName);

            if (armyCard == null)
            {
                await PickElement(elementName);
            }

            await AddCardToHorde2(cardName, count);
        }

        //The method that actually adds the cards is AddCardToHorde2
        //AddCardToHorde is only used to check if in the current displayed element, there's the card we're looking for
        //If it's not, the correct element is picked so that the card can be found
        //Otherwise it can proceed directly to adding said cards

        private async Task AddCardToHorde2(string cardName, int count = 1)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            int checkedPage;
            bool cardAdded = false;

            for (checkedPage = 0; checkedPage <= 4; checkedPage++)
            {
                Loom.ZombieBattleground.Data.Card armyCard = _createdArmyCards.Find(x =>
                    x.Name == cardName);

                if (armyCard == null)
                {
                    await ClickGenericButton("Army/ArrowRightButton");

                    await LetsThink();

                    SetupArmyCards();

                    await LetsThink();

                    continue;
                }

                Debug.Log("Adding " + cardName + " (" + armyCard.Cost + ") x" + count);
                cardAdded = true;

                for (int counter = 0; counter < count; counter++)
                {
                    _uiManager.GetPage<HordeEditingPage>().AddCardToDeck(null, armyCard);

                    await LetsThink();
                }

                await LetsThink();

                break;
            }

            if (cardAdded == false)
            {
                Assert.Fail($"Card named \"{cardName}\" was not found.");
            }

            await new WaitForUpdate();
        }

        private bool CheckCorrectNumberOfCards(int correctNumber = 30)
        {
            TextMeshProUGUI cardsAmountText = GameObject.Find("CardsAmountText")?.GetComponent<TextMeshProUGUI>();

            return cardsAmountText != null && cardsAmountText.text == "30 / 30";
        }

        private void AssertCorrectNumberOfCards(int correctNumber = 30)
        {
            if (IsTestFailed)
            {
                return;
            }

            if (!CheckCorrectNumberOfCards(correctNumber))
            {
                Assert.Fail($"Exactly {correctNumber} cards need to be added to the deck.");
            }
        }

        /// <summary>
        /// Gets the number of Hordes.
        /// </summary>
        public int GetNumberOfHordes()
        {
            GameObject hordesParent = GameObject.Find("Panel_DecksContainer/Group");

            if (hordesParent != null)
            {
                return hordesParent.transform.childCount;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Selects a Horde by name.
        /// </summary>
        /// <param name="hordeName">Horde name.</param>
        public async Task SelectAHordeByName(
            string hordeName,
            bool failIfNotFound = true,
            string failureMessage = "Couldn't find Horde by that name")
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            GameObject hordesParent = GameObject.Find("Panel_DecksContainer/Group");

            SelectedHordeIndex = -1;
            bool hordeSelected = false;

            for (int i = 0; i < GetNumberOfHordes() - 1; i++)
            {
                Transform selectedHordeTransform = hordesParent.transform.GetChild(i);

                if (selectedHordeTransform?.Find("Panel_Description/Text_Description")?.GetComponent<TextMeshProUGUI>()?.text != null &&
                    selectedHordeTransform?.Find("Panel_Description/Text_Description")?.GetComponent<TextMeshProUGUI>()?.text == hordeName)
                {
                    selectedHordeTransform.Find("Button_Select").GetComponent<Button>().onClick.Invoke();

                    SelectedHordeIndex = i - 1;
                    hordeSelected = true;
                }
            }

            if (!hordeSelected && failIfNotFound)
            {
                Assert.Fail(failureMessage);
            }

            await new WaitForUpdate();
        }

        /// <summary>
        /// Selects a Horde by index.
        /// </summary>
        /// <param name="index">Index.</param>
        public async Task SelectAHordeByIndex(int index)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            if (index + 1 >= GetNumberOfHordes())
            {
                Assert.Fail("Horde removal index is too high");
            }

            GameObject hordesParent = GameObject.Find("Panel_DecksContainer/Group");
            // +1 to account for Item_HordeSelectionNewHordeLeft
            Transform selectedHordeTransform = hordesParent.transform.GetChild(index + 1);
            selectedHordeTransform.Find("Button_Select").GetComponent<Button>().onClick.Invoke();

            await LetsThink();
        }

        /// <summary>
        /// Removes a Horde by index.
        /// </summary>
        /// <param name="index">Index.</param>
        public async Task RemoveAHorde(int index)
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            await SelectAHordeByIndex(index);

            GameObject.Find("Button_Delete").GetComponent<Button>().onClick.Invoke();

            await LetsThink();

            await RespondToYesNoOverlay(true);

            await LetsThink();
            await LetsThink();
        }

        /// <summary>
        /// Removes all Hordes except first.
        /// </summary>
        public async Task RemoveAllHordesExceptDefault()
        {
            await HandleConnectivityIssues();
            if (IsTestFailed)
            {
                return;
            }

            for (int i = GetNumberOfHordes() - 2; i >= 1; i--)
            {
                await RemoveAHorde(1);

                await LetsThink();
            }

            await new WaitForUpdate();
        }

        #endregion

        public void RecordExpectedOverlordName(int index)
        {
            if (IsTestFailed)
            {
                return;
            }

            GameObject hordesParent = GameObject.Find("Panel_DecksContainer/Group");

            if (index >= hordesParent.transform.childCount || index == -1)
            {
                Assert.Fail("Horde index is too high");

                return;
            }

            Transform selectedHordeTransform = hordesParent.transform.GetChild(index);

            RecordAValue(selectedHordeTransform, "Panel_Description/Text_Description", RecordedValue.Expected);
        }

        public void RecordActualOverlordName()
        {
            RecordAValue(null, "Text_PlayerOverlordName", RecordedValue.Actual);
        }

        private void RecordAValue(string value, RecordedValue recordedValue)
        {
            if (value == null || value.Length <= 1)
            {
                value = "";
            }

            switch (recordedValue)
            {
                case RecordedValue.Expected:
                    _recordedExpectedValue = UppercaseFirst(value);

                    break;
                case RecordedValue.Actual:
                    _recordedActualValue = UppercaseFirst(value);

                    break;
            }
        }

        /// <summary>
        /// Records a value (expected or actual) to be used for assertion.
        /// </summary>
        /// <param name="parentTransform">Parent transform.</param>
        /// <param name="objectName">Object name.</param>
        /// <param name="recordedValue">Recorded value.</param>
        public void RecordAValue(Transform parentTransform, string objectName, RecordedValue recordedValue)
        {
            if (parentTransform != null)
            {
                RecordAValue(parentTransform.Find(objectName)?.GetComponent<TextMeshProUGUI>()?.text, recordedValue);
            }
            else
            {
                RecordAValue(GameObject.Find(objectName)?.GetComponent<TextMeshProUGUI>()?.text, recordedValue);
            }
        }

        /// <summary>
        /// Checks and confirms that Overlord name is as expected.
        /// </summary>
        public void AssertOverlordName()
        {
            // FIXME: overlord name is not recorded, see WaitUntilPlayerOrderIsDecided
            return;

            if (string.IsNullOrEmpty(_recordedExpectedValue) || string.IsNullOrEmpty(_recordedActualValue))
            {
                Debug.LogWarning("One of the overlord names was null, so didn't check.");

                return;
            }
            else if (_recordedExpectedValue == "Default")
            {
                _recordedExpectedValue = "Mhalik";
            }

            Debug.LogFormat("{0} vs {1}", _recordedExpectedValue, _recordedActualValue);

            Assert.AreEqual(_recordedExpectedValue, _recordedActualValue);
        }

        private string UppercaseFirst(string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
        }

        public enum RecordedValue
        {
            Expected,
            Actual
        }

        #region PvP gameplay

        /// <summary>
        /// Presses OK or GotIt button if it's on.
        /// </summary>
        /// <remarks>Useful where you expect to get a popup with this button.</remarks>
        public async Task PressOK()
        {
            if (GameObject.Find("Button_OK") != null)
                await ClickGenericButton("Button_OK");
            else
                await ClickGenericButton("Button_GotIt");
        }

        #endregion

        #region PvP Second Client

        /// <summary>
        /// Gets the opponent's simulated game client.
        /// </summary>
        public MultiplayerDebugClient GetOpponentDebugClient()
        {
            Assert.NotNull(_opponentDebugClient);
            Assert.NotNull(_opponentDebugClientOwner);

            return _opponentDebugClient;
        }

        /// <summary>
        /// Instantiates a simulated game client for the opponent, running in parallel to the game.
        /// Connects that client to the backend.
        /// </summary>
        /// <returns></returns>
        public async Task CreateAndConnectOpponentDebugClient()
        {
            if (_opponentDebugClient != null)
            {
                await _opponentDebugClient.Reset();
                _opponentDebugClient = null;
            }

            if (_opponentDebugClientOwner != null)
            {
                Object.Destroy(_opponentDebugClientOwner);
                _opponentDebugClientOwner = null;
            }

            GameObject owner = new GameObject("_OpponentDebugClient");
            owner.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            OnBehaviourHandler onBehaviourHandler = owner.AddComponent<OnBehaviourHandler>();

            MultiplayerDebugClient client = new MultiplayerDebugClient(GetOpponentTestUserName());

            _opponentDebugClient = client;
            _opponentDebugClientOwner = onBehaviourHandler;

            Func<Contract, IContractCallProxy> contractCallProxyFactory =
                contract => new ThreadedContractCallProxyWrapper(new TimeMetricsContractCallProxy(contract, false, false));
            await client.Start(
                contractCallProxyFactory,
                onClientCreatedCallback: chainClient =>
                {
                    chainClient.Configuration.StaticCallTimeout = 10000;
                    chainClient.Configuration.CallTimeout = 10000;
                },
                enabledLogs: false);

            onBehaviourHandler.Updating += async go => await client.Update();
        }

        /// <summary>
        /// Starts matchmaking flow for the simulated game client of the opponent.
        /// </summary>
        /// <returns></returns>
        public async Task MatchmakeOpponentDebugClient(Action<DebugCheatsConfiguration> modifyDebugCheatsAction = null)
        {
            MultiplayerDebugClient client = _opponentDebugClient;
            bool matchConfirmed = false;

            void SecondClientMatchConfirmedHandler(MatchMetadata matchMetadata)
            {
                client.MatchRequestFactory = new MatchRequestFactory(matchMetadata.Id);
                client.PlayerActionFactory = new PlayerActionFactory(client.UserDataModel.UserId);
                matchConfirmed = true;
            }

            client.DebugCheats = new DebugCheatsConfiguration
            {
                Enabled = DebugCheats.Enabled,
                CustomRandomSeed = DebugCheats.CustomRandomSeed,
                ForceFirstTurnUserId = DebugCheats.ForceFirstTurnUserId,
                DisableDeckShuffle = DebugCheats.DisableDeckShuffle,
                IgnoreGooRequirements = DebugCheats.IgnoreGooRequirements
            };

            modifyDebugCheatsAction?.Invoke(client.DebugCheats);

            // TODO: add customization
            client.DeckId = 1;

            client.MatchMakingFlowController.MatchConfirmed += SecondClientMatchConfirmedHandler;
            await client.MatchMakingFlowController.Start(
                client.DeckId,
                client.CustomGameAddress,
                GetPvPTags(),
                client.UseBackendGameLogic,
                client.DebugCheats
            );

            while (!matchConfirmed)
            {
                await new WaitForEndOfFrame();
            }
        }

        /// <summary>
        /// Setups very dumb logic for the simulated opponent that only skips turns.
        /// </summary>
        public void SetupOpponentDebugClientToEndTurns()
        {
            MultiplayerDebugClient client = _opponentDebugClient;

            async Task EndTurnIfCurrentTurn(bool isFirstTurn)
            {
                GetGameStateResponse gameStateResponse =
                    await client.BackendFacade.GetGameState(client.MatchMakingFlowController.MatchMetadata.Id);
                GameState gameState = gameStateResponse.GameState;
                if (gameState.PlayerStates[gameState.CurrentPlayerIndex].Id == client.UserDataModel.UserId)
                {
                    Debug.Log("ending FIRST turn: " + isFirstTurn);
                    await client.BackendFacade.SendPlayerAction(
                        client.MatchRequestFactory.CreateAction(
                            client.PlayerActionFactory.EndTurn()
                        )
                    );
                }
            }

            client.MatchMakingFlowController.MatchConfirmed += async metadata =>
            {
                await EndTurnIfCurrentTurn(true);
            };

            client.BackendFacade.PlayerActionDataReceived += async bytes =>
            {
                PlayerActionEvent playerActionEvent = PlayerActionEvent.Parser.ParseFrom(bytes);
                bool? isLocalPlayer =
                    playerActionEvent.PlayerAction != null ?
                        playerActionEvent.PlayerAction.PlayerId == client.UserDataModel.UserId :
                        (bool?) null;

                if (isLocalPlayer != null)
                {
                    await EndTurnIfCurrentTurn(false);
                }
            };
        }

        #endregion

        public AbilityBoardArrow GetAbilityBoardArrow()
        {
            return GameObject.FindObjectOfType<AbilityBoardArrow>();
        }

        /// <summary>
        /// Starts the waiting process.
        /// </summary>
        /// <remarks>Useful in case you have concern of getting a response for a request. To be coupled with WaitTimeIsUp.</remarks>
        /// <param name="waitAmount">Wait amount.</param>
        private void WaitStart(int waitAmount, bool unscaledTime = false)
        {
            _waitUnscaledTime = unscaledTime;
            _waitStartTime = _waitUnscaledTime ? Time.unscaledTime : Time.time;
            _waitAmount = waitAmount;
        }

        /// <summary>
        /// Checks if waiting amount has been reached
        /// </summary>
        /// <remarks>Useful in case you have concern of getting a response for a request. To be coupled with WaitStart.</remarks>
        /// <returns><c>true</c>, if time is up, <c>false</c> otherwise.</returns>
        private bool WaitTimeIsUp()
        {
            float baseTime = _waitUnscaledTime ? Time.unscaledTime : Time.time;
            return baseTime > _waitStartTime + _waitAmount;
        }

        private void IgnoreAssertsLogMessageReceivedHandler(string condition, string stacktrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    _errorMessages.Add(new LogMessage(condition, stacktrace, type));
                    break;
                case LogType.Assert:
                case LogType.Warning:
                case LogType.Log:
                    break;
            }
        }

        public static IEnumerator TaskAsIEnumerator(Func<Task> taskFunc, int timeout = Timeout.Infinite)
        {
            return TaskAsIEnumerator(taskFunc(), timeout);
        }

        public static IEnumerator TaskAsIEnumerator(Task task, int timeout = Timeout.Infinite)
        {
            Stopwatch timeoutStopwatch = timeout != Timeout.Infinite ? Stopwatch.StartNew() : null;
            while (!task.IsCompleted)
            {
                if (timeoutStopwatch != null && timeoutStopwatch.ElapsedMilliseconds > timeout)
                    throw new TimeoutException($"Test task {task} timed out after {timeout} ms");

                yield return null;
            }

            task.Wait();
        }

        private struct LogMessage
        {
            public string Message { get; }

            public string StackTrace { get; }

            public LogType LogType { get; }

            public LogMessage(string message, string stackTrace, LogType logType)
            {
                Message = message;
                StackTrace = stackTrace;
                LogType = logType;
            }

            public override string ToString()
            {
                return $"[{LogType}] {Message}";
            }
        }
    }
}
