// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using LoomNetwork.CZB.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using LoomNetwork.CZB.Data;


namespace LoomNetwork.CZB
{
    public class CardInfoPopup : IUIPopup
    {
		public GameObject Self
        {
            get { return _selfPage; }
        }

        private ILoadObjectsManager _loadObjectsManager;
        private IUIManager _uiManager;
        private GameObject _selfPage;

        private TextMeshProUGUI _description,
                                _amountAward;
        private MenuButtonNoGlow _backButton,
                                _desintegrateButton;
		private TextMeshProUGUI _buttonText;

        private Card _card;
        public Transform cardTransform;
        public CollectionCardData _cardData;

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();

            _selfPage = MonoBehaviour.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/CardInfoPopup"));
            _selfPage.transform.SetParent(_uiManager.Canvas2.transform, false);

			_desintegrateButton = _selfPage.transform.Find("DesintegrateArea/DesintegrateButton").GetComponent<MenuButtonNoGlow>();
			_backButton = _selfPage.transform.Find("BackButton").GetComponent<MenuButtonNoGlow>();


			_desintegrateButton.onClickEvent.AddListener(DesintegrateButtonHandler);
			_backButton.onClickEvent.AddListener(Hide);
			_selfPage.GetComponent<Button>().onClick.AddListener(ClosePopup);


			_description = _selfPage.transform.Find("DesintegrateArea/Description").GetComponent<TextMeshProUGUI>();
			_amountAward = _selfPage.transform.Find("DesintegrateArea/GooAward/Value").GetComponent<TextMeshProUGUI>();

            Hide();
        }


		public void Dispose()
		{
		}

		public void Hide()
		{
			_selfPage.SetActive(false);
		}

        public void SetMainPriority()
        {
        }

        public void Show()
        {
            GameClient.Get<ISoundManager>().PlaySound(Common.Enumerators.SoundType.CHANGE_SCREEN, Constants.SFX_SOUND_VOLUME, false, false, true);
            _selfPage.SetActive(true);
        }

        public void Show(object data)
        {
            _card = data as Card;
            _description.text = _card.flavorText;

            _amountAward.text = (5 * ((int)_card.cardRank + 1)).ToString();

            _cardData = GameClient.Get<IDataManager>().CachedCollectionData.GetCardData(_card.name);
            UpdateCardAmount();
            Show();
        }

        public void Update()
        {

        }

        private void ClosePopup()
        {
            Hide();
            GameClient.Get<ISoundManager>().PlaySound(Common.Enumerators.SoundType.DECKEDITING_REMOVE_CARD, Constants.SFX_SOUND_VOLUME, false, false, true);
        }

		public void UpdateCardAmount()
		{
			if (_cardData.amount == 0)
				_desintegrateButton.GetComponent<MenuButtonNoGlow>().interactable = false;
			else
				_desintegrateButton.GetComponent<MenuButtonNoGlow>().interactable = true;
		}

        private void DesintegrateButtonHandler()
        {
            GameClient.Get<ISoundManager>().PlaySound(Common.Enumerators.SoundType.CLICK, Constants.SFX_SOUND_VOLUME, false, false, true);
            int amount = _cardData.amount;
            if (amount == 0)
                _desintegrateButton.GetComponent<MenuButtonNoGlow>().interactable = false;
            //_uiManager.DrawPopup<WarningPopup>("Sorry you don't have cards to desintegrate");
            else
            {
                /*cardTransform.DOKill();
                cardTransform.DOScale(new Vector3(.3f, .3f, .3f), 0.2f);*/
                Hide();
                _uiManager.DrawPopup<DesintigrateCardPopup>(_cardData);
                (_uiManager.GetPopup<DesintigrateCardPopup>() as DesintigrateCardPopup).cardTransform = cardTransform;
            }   
		}
    }
}