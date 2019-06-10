using System;
using System.Collections.Generic;
using Loom.ZombieBattleground.Common;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class LocalizedPopup : LocalizableUIBase, IUIPopup
    {
        public event Action PopupHiding;

        private ILoadObjectsManager _loadObjectsManager;

        private IUIManager _uiManager;

        private TextMeshProUGUI _text;

        public event Action ConfirmationReceived;

        private ButtonShiftingContent _gotItButton;

        public GameObject Self { get; private set; }

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();
            InitializeLocalization();
        }        

        public void Dispose()
        {
        }

        public void Hide()
        {
            PopupHiding?.Invoke();

            UnRegisterLocalizedTextList();            

            if (Self == null)
                return;

            
            Self.SetActive(false);
            Object.Destroy(Self);
            Self = null;
        }

        public void SetMainPriority()
        {
        }

        public void Show()
        {
            if (Self != null)
                return;

            Self = Object.Instantiate(
                _loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/LocalizedPopup"));
            Self.transform.SetParent(_uiManager.Canvas3.transform, false);

            _gotItButton = Self.transform.Find("Button_GotIt").GetComponent<ButtonShiftingContent>();
            _gotItButton.onClick.AddListener(CloseButtonHandler);
            SetCloseButtonVisible(true);

            LocalizedTextList.Add
            (
                _gotItButton.transform.Find("Text").GetComponent<TextMeshProUGUI>()
            );

            _text = Self.transform.Find("Text_Message").GetComponent<TextMeshProUGUI>();

            RegisterLocalizedTextList();
        }

        public void Show(object data)
        {
            Show();

            _text.text = (string) data;
        }

        public void Update()
        {
        }

        public void SetCloseButtonVisible(bool visible)
        {
            _gotItButton.gameObject.SetActive(visible);
        }

        public void CloseButtonHandler()
        {
            GameClient.Get<ISoundManager>()
                .PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            Hide();
            ConfirmationReceived?.Invoke();
        }
    }
}
