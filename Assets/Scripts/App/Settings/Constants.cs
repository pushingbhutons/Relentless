// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using System.Collections.Generic;

namespace LoomNetwork.CZB.Common
{
    public class Constants
    {
	    internal const string CURRENT_VERSION_BASE = "0.1.2";
	    internal const string CURRENT_VERSION_DEVELOPMENT_STAGE = "Alpha";

        internal const string VERSION_FILE_RESOLUTION = ".ver";

        internal const string LOCAL_USER_DATA_FILE_PATH = "0412DD0.dat";
		internal const string LOCAL_CARDS_LIBRARY_DATA_FILE_PATH = "card_library_data.json";
		internal const string LOCAL_COLLECTION_DATA_FILE_PATH = "collection_data.json";
		internal const string LOCAL_HEROES_DATA_FILE_PATH = "heroes_data.json";
		internal const string LOCAL_DECKS_DATA_FILE_PATH = "decks_data.json";
        internal const string LOCAL_OPPONENT_DECKS_DATA_FILE_PATH = "opponent_decks_data.json";
        internal const string LOCAL_OPPONENT_ACTIONS_LIBRARY_DATA_FILE_PATH = "action_library_data.json";
        internal const string LOCAL_CREDITS_DATA_FILE_PATH = "credits_data.json";
        internal const string LOCAL_BUFFS_TOOLTIP_DATA_FILE_PATH = "buffs_tooltip_data.json";
        

        internal const string PRIVATE_ENCRYPTION_KEY_FOR_APP = "sagatdsgsg7687sdg587a8gs89";

        internal static bool DATA_ENCRYPTION_ENABLED =
#if !UNITY_EDITOR
            true;
#else
            false;
#endif

		internal static uint HEROES_AMOUNT = 9;
		internal static uint DECK_MAX_SIZE = 30;
        internal static uint CARD_ITEM_MAX_COPIES = 2;
        internal static uint CARD_MINION_MAX_COPIES = 4;
		internal static uint CARD_OFFICER_MAX_COPIES = 2;
        internal static uint CARD_COMMANDER_MAX_COPIES = 2;
        internal static uint CARD_GENERAL_MAX_COPIES = 1;
		internal static uint CARDS_IN_PACK = 5;

        internal static uint MAX_BOARD_UNITS = 6;

        internal static int DEFAULT_TURN_DURATION = 60;
        internal static int MAX_DECK_SIZE = 30;
        internal static int MIN_DECK_SIZE = 30;
        internal static int MAX_DECKS_AT_ALL = 6;
        internal static int MAX_CARDS_IN_HAND = 10;

        internal static int DEFAULT_PLAYER_HP = 20;
        internal static int DEFAULT_PLAYER_GOO = 0;
        internal static int MAXIMUM_PLAYER_GOO = 10;

        internal static int FIRST_GAME_TURN_INDEX = 1;

        internal static int DEFAULT_CARDS_IN_HAND_AT_START_GAME = 3;

        internal static float DELAY_BETWEEN_GAMEPLAY_ACTIONS = 0.01f;

        internal static int DELAY_BETWEEN_AI_ACTIONS = 1100;

        internal const string TAG_PLAYER_OWNED = "PlayerOwned";
        internal const string TAG_OPPONENT_OWNED = "OpponentOwned";

        internal const string PLAYER_BOARD = "PlayerBoard";
        internal const string OPPONENT_BOARD = "OpponentBoard";


        internal const string LAYER_HAND_CARDS = "HandCards";
        internal const string LAYER_BOARD_CARDS = "BoardCards";
        internal const string LAYER_DEFAULT = "Default";
        internal const string LAYER_FOREGROUND = "Foreground";
		internal const string LAYER_GAME_UI1 = "GameUI1";
		internal const string LAYER_GAME_UI2 = "GameUI2";
		internal const string LAYER_GAME_UI3 = "GameUI3";

        internal const string ZONE_HAND = "Hand";
        internal const string ZONE_BOARD = "Board";
        internal const string ZONE_DECK = "Deck";
        internal const string ZONE_GRAVEYARD = "Graveyard";

        internal const string TAG_LIFE = "Life";
        internal const string TAG_DAMAGE = "Damage";
        internal const string TAG_HP = "HP";
        internal const string TAG_MANA = "Goo";

        internal const string STAT_DAMAGE = "DMG";
        internal const string STAT_HP = "HP";

        internal const string CONTENT_FOLDER_NAME = "SpreadsheetsData/";
        internal const string SPREADSHEET_FILE_FORMAT = ".csv";

        internal const string CARD_SOUND_PLAY = "P";
        internal const string CARD_SOUND_ATTACK = "A";
        internal const string CARD_SOUND_DEATH = "D";


        internal static UnityEngine.Vector3 VFX_OFFSET = UnityEngine.Vector3.zero;

        // SOUNDS VOLUME'S
        internal static float ZOMBIES_SOUND_VOLUME = 0.05f;
        internal static float TUTORIAL_SOUND_VOLUME = 1f;
        internal static float ARRIVAL_SOUND_VOLUME = 0.1f;
        internal static float CREATURE_ATTACK_SOUND_VOLUME = 0.05f;
        internal static float END_TURN_CLICK_SOUND_VOLUME = 0.1f;
        internal static float HERO_DEATH_SOUND_VOLUME = 0.1f;
        internal static float CARDS_MOVE_SOUND_VOLUME = 0.025f;
        internal static float BACKGROUND_SOUND_VOLUME = 0.05f;
        internal static float SFX_SOUND_VOLUME = 0.15f;
        internal static float OVERLORD_ABILITY_SOUND_VOLUME = 0.1f;
        internal static float SPELL_ABILITY_SOUND_VOLUME = 0.1f;
        internal static float BATTLEGROUND_EFFECTS_SOUND_VOLUME = 0.25f;
		internal static float ZOMBIE_DEATH_VO_DELAY_BEFORE_FADEOUT = 3f;

        internal static float DELAY_TO_PLAY_DEATH_SOUND_OF_CREATURE = 2f;
        internal static float CARD_DISTRIBUTION_TIME = 5f;

        internal const float POINTER_ON_CLICK_DELAY = 1.5f;
        internal const float POINTER_MIN_DRAG_DELTA = 3f;
        internal const float POINTER_MIN_DRAG_DELTA_MOBILE = 35f;

        internal const float LOADING_TIME_BETWEEN_GAMEPLAY_AND_APP_INIT = 2f;

        internal const int TUTORIAL_PLAYER_HERO_ID = 4;


        internal const bool DEV_MODE =  false;  
    }
}
