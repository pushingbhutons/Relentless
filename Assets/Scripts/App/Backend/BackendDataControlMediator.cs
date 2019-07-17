using System;
using System.IO;
using System.Threading.Tasks;
using log4net;
using Loom.Client;
using Newtonsoft.Json;
using UnityEngine;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Helpers;
using Loom.ZombieBattleground.Iap;
using Loom.ZombieBattleground.Protobuf;

namespace Loom.ZombieBattleground.BackendCommunication
{
    public class BackendDataControlMediator : IService
    {
        private static readonly ILog Log = Logging.GetLog(nameof(BackendDataControlMediator));

        private const string UserDataFileName = "UserLoginData.json";

        private IDataManager _dataManager;

        private BackendFacade _backendFacade;
        private AuthApiFacade _authApiFacade;
        private AuthFiatApiFacade _authFiatApiFacade;
        private PlasmachainBackendFacade _plasmaChainBackendFacade;

        protected string UserDataFilePath => Path.Combine(Application.persistentDataPath, UserDataFileName);

        public UserDataModel UserDataModel { get; set; }

        public void Init()
        {
            _dataManager = GameClient.Get<IDataManager>();
            _backendFacade = GameClient.Get<BackendFacade>();
            _authApiFacade = GameClient.Get<AuthApiFacade>();
            _authFiatApiFacade = GameClient.Get<AuthFiatApiFacade>();
            _plasmaChainBackendFacade = GameClient.Get<PlasmachainBackendFacade>();
        }

        public void Update()
        {
        }

        public void Dispose()
        {
        }

        public bool LoadUserDataModel(bool force = false)
        {
            if (UserDataModel != null && !force)
            {
                return true;
            }

            if (!File.Exists(UserDataFilePath))
                return false;

            string modelJson = File.ReadAllText(UserDataFilePath);
            UserDataModel = JsonConvert.DeserializeObject<UserDataModel>(_dataManager.DecryptData(modelJson));
            return true;
        }

        public bool SetUserDataModel(UserDataModel userDataModel)
        {
            if (userDataModel == null)
                throw new ArgumentNullException(nameof(userDataModel));

            string modelJson = JsonConvert.SerializeObject(userDataModel);
            File.WriteAllText(UserDataFilePath, _dataManager.EncryptData(modelJson));
            UserDataModel = userDataModel;
            return true;
        }

        /// <returns>Whether full card sync will be executed</returns>
        public async Task LoginAndLoadData()
        {
            bool gotFullCardSyncEvent = false;
            void OnUserFullCardCollectionSyncEventReceived(BackendFacade.UserFullCardCollectionSyncEventData data)
            {
                gotFullCardSyncEvent = true;
                Log.Debug("Got full card collection sync event");
            }

            LoadUserDataModel();

            Log.Info("User Id: " + UserDataModel.UserId);

            try
            {
                await CreateContract();
                _backendFacade.UserFullCardCollectionSyncEventReceived += OnUserFullCardCollectionSyncEventReceived;
                LoginResponse loginResponse = await _backendFacade.Login(UserDataModel.UserId);

                // Register and login again
                if (loginResponse.UserNotRegistered)
                {
                    Log.Info("User not registered on Gamechain, creating account");
                    await _backendFacade.SignUp(UserDataModel.UserId);
                    loginResponse = await _backendFacade.Login(UserDataModel.UserId);
                }

                if (loginResponse.DataWiped)
                {
                    Log.Warn("NOTE: user data wiped");
                }

                // No need to wait for card collection sync for guests, we know they won't have any
                if (loginResponse.FullCardCollectionSyncExecuted)
                {
                    Log.Debug("Waiting for full card collection sync event...");
                    const float waitForFullCardCollectionSyncEventTimeout = 20;
                    bool timedOut = await InternalTools.WaitWithTimeout(waitForFullCardCollectionSyncEventTimeout, () => gotFullCardSyncEvent);
                    if (timedOut)
                        throw new RpcClientException("Timed out waiting for full card collection sync event", -1, null);
                }
            }
            catch (RpcClientException exception)
            {
                Helpers.ExceptionReporter.SilentReportException(exception);
                Log.Warn("RpcException ==", exception);
                if (UserDataModel.IsValid)
                {
                    GameClient.Get<IAppStateManager>().HandleNetworkExceptionFlow(exception);
                }
            }
            finally
            {
                _backendFacade.UserFullCardCollectionSyncEventReceived -= OnUserFullCardCollectionSyncEventReceived;
            }

            await _dataManager.StartLoadCache();
        }

        public async Task UpdateEndpointsFromZbVersion()
        {
            try
            {
                BackendPurpose defaultBackendPurpose = GameClient.GetDefaultBackendPurpose();
                if (defaultBackendPurpose == BackendPurpose.Production ||
                    GameClient.GetForceUseAuth())
                {
                    ZbVersion zbVersion = await _authApiFacade.GetZbVersionData(defaultBackendPurpose);
                    BackendEndpoint backendEndpoint =
                        _authApiFacade.GetProductionBackendEndpointFromZbVersion(zbVersion, _backendFacade.BackendEndpoint.PlasmachainEndpointsConfiguration);
                    _backendFacade.SetBackendEndpoint(backendEndpoint);
                    _plasmaChainBackendFacade.SetEndpoints(backendEndpoint.PlasmachainEndpointsConfiguration);
                    _authApiFacade.SetEndpoints(backendEndpoint.AuthHost, backendEndpoint.VaultHost);
                    _authFiatApiFacade.SetEndpoints(backendEndpoint.AuthHost);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Failed to update endpoints from zbversion", e);
            }
        }

        private async Task CreateContract()
        {
            DAppChainClientConfiguration clientConfiguration = new DAppChainClientConfiguration
            {
                CallTimeout = Constants.BackendCallTimeout,
                StaticCallTimeout = Constants.BackendCallTimeout
            };
            IDAppChainClientCallExecutor chainClientCallExecutor = new NotifyingDAppChainClientCallExecutor(clientConfiguration);
            await _backendFacade.CreateContract(UserDataModel.PrivateKey, clientConfiguration, chainClientCallExecutor: chainClientCallExecutor);

            // Subscribe to persistent user events right away, since we always listen to them
            await _backendFacade.SubscribeToEvents(UserDataModel.UserId, Array.Empty<string>());
        }
    }
}
