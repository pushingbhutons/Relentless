﻿using Loom.Client;
using Loom.ZombieBattleground.BackendCommunication;
using Opencoding.CommandHandlerSystem;

namespace Loom.ZombieBattleground
{
    public static class TutorialRewardCommandsHandler
    {
        private static IGameplayManager _gameplayManager;
    
        public static void Initialize()
        {
            CommandHandlers.RegisterCommandHandlers(typeof(TutorialRewardCommandsHandler));
            _gameplayManager = GameClient.Get<IGameplayManager>();
        }
        
        [CommandHandler(Description = "Skips to last tutorial")]
        public static void SkipToLastTutorial()
        {
            if (!GameClient.Get<ITutorialManager>().IsTutorial)
                return;

            if (GameClient.Get<IAppStateManager>().AppState == Common.Enumerators.AppState.GAMEPLAY)
            {
                GameClient.Get<IGameplayManager>().EndGame(Common.Enumerators.EndGameType.WIN);
                GameClient.Get<IMatchManager>().FinishMatch(Common.Enumerators.AppState.MAIN_MENU);
            }
            else
            {
                GameClient.Get<IAppStateManager>().ChangeAppState(Common.Enumerators.AppState.MAIN_MENU);
            }

            GameClient.Get<ITutorialManager>().StopTutorial(true);
            GameClient.Get<IDataManager>().CachedUserLocalData.CurrentTutorialId = GameClient.Get<ITutorialManager>().TutorialsCount-2;
        }
        
        [CommandHandler(Description = "Reduce the current def of the AI overlord to zero")]
        private static void WinBattle()
        {
            _gameplayManager.OpponentPlayer.Defense = 0;
        }
    }
}
