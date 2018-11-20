using System;
using System.Collections.Generic;
using DG.Tweening;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class OverlordSelectionPage : IUIElement
    {
        private const float ScrollAnimationDuration = 0.5f;

        private const int LoopStartFakeHeroCount = 1;

        private const int LoopEndFakeHeroCount = 2;

        private IUIManager _uiManager;

        private ILoadObjectsManager _loadObjectsManager;

        private IDataManager _dataManager;

        private ISoundManager _soundManager;

        private IAppStateManager _appStateManager;

        private GameObject _selfPage;

        private Button _backButton, _continueButton, _leftArrowButton, _rightArrowButton;

        private OverlordObject _currentOverlordObject;

        private List<OverlordObject> _overlordObjects;

        private List<OverlordObject> _loopFakeOverlordObjects;

        private HorizontalLayoutGroup _overlordsContainer;

        private GameObject _rightContentObject;

        private int _selectedHeroIndex;

        private Sequence _heroSelectScrollSequence;

        public void Init()
        {
            _uiManager = GameClient.Get<IUIManager>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _appStateManager = GameClient.Get<IAppStateManager>();
        }

        public void Update()
        {
        }

        public void Show()
        {
            _selfPage = Object.Instantiate(
                _loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Pages/OverlordSelectionPage"),
                _uiManager.Canvas.transform, false);

            _backButton = _selfPage.transform.Find("Button_Back").GetComponent<Button>();
            _continueButton = _selfPage.transform.Find("Image_BottomMask/Button_Continue").GetComponent<Button>();
            _leftArrowButton = _selfPage.transform.Find("Button_LeftArrow").GetComponent<Button>();
            _rightArrowButton = _selfPage.transform.Find("Button_RightArrow").GetComponent<Button>();

            _backButton.onClick.AddListener(BackButtonOnClickHandler);
            _continueButton.onClick.AddListener(ContinueButtonOnClickHandler);
            _leftArrowButton.onClick.AddListener(LeftArrowButtonOnClickHandler);
            _rightArrowButton.onClick.AddListener(RightArrowButtonOnClickHandler);

            _overlordsContainer = _selfPage.transform.Find("Panel_OverlordContent/Group")
                .GetComponent<HorizontalLayoutGroup>();
            _rightContentObject = _selfPage.transform.Find("Panel_OverlordContent/Panel_OverlordInfo").gameObject;

            FillOverlordObjects();
            SetSelectedHeroIndexAndUpdateScrollPosition(0, false, force: true);
        }

        public void Hide()
        {
            if (_selfPage == null)
                return;

            _selfPage.SetActive(false);
            Object.Destroy(_selfPage);
            _selfPage = null;
        }

        public void Dispose()
        {
            _heroSelectScrollSequence?.Kill();
        }

        private bool SetSelectedHeroIndexAndUpdateScrollPosition(
            int heroIndex, bool animateTransition, bool selectOverlordObject = true, bool force = false, int direction = 1)
        {
            if (!force && heroIndex == _selectedHeroIndex)
            {
                return false;
            }
            _selectedHeroIndex = heroIndex;

            RectTransform overlordContainerRectTransform = _overlordsContainer.GetComponent<RectTransform>();
            _heroSelectScrollSequence?.Kill();
            if (animateTransition)
            {
                _heroSelectScrollSequence = DOTween.Sequence();
                _heroSelectScrollSequence.Append(
                        DOTween.To(
                            () => overlordContainerRectTransform.anchoredPosition,
                            v => overlordContainerRectTransform.anchoredPosition = v,
                            CalculateOverlordContainerShiftForHeroIndex(_selectedHeroIndex),
                            ScrollAnimationDuration))
                    .AppendCallback(() => _heroSelectScrollSequence = null);
            }
            else
            {
                overlordContainerRectTransform.anchoredPosition =
                    CalculateOverlordContainerShiftForHeroIndex(_selectedHeroIndex);
            }

            if (selectOverlordObject)
            {
                _overlordObjects[_selectedHeroIndex].Select(animateTransition, direction);
            }

            return true;
        }

        private void FillOverlordObjects()
        {
            ResetOverlordObjects();

            _overlordObjects = new List<OverlordObject>();
            _loopFakeOverlordObjects = new List<OverlordObject>();

            for (int i = LoopStartFakeHeroCount - 1; i >= 0; i--)
            {
                int index = _dataManager.CachedHeroesData.Heroes.Count - i - 1;
                Hero hero = _dataManager.CachedHeroesData.Heroes[index];
                OverlordObject current = new OverlordObject(_overlordsContainer.GetComponent<RectTransform>(),
                    _rightContentObject, hero);
                current.SelfObject.name += $" #{index}";
                _loopFakeOverlordObjects.Add(current);
            }

            for (int i = 0; i < _dataManager.CachedHeroesData.Heroes.Count; i++)
            {
                Hero hero = _dataManager.CachedHeroesData.Heroes[i];
                OverlordObject current = new OverlordObject(_overlordsContainer.GetComponent<RectTransform>(),
                    _rightContentObject, hero);
                current.SelfObject.name += $" #{i}";
                current.OverlordObjectSelectedEvent += OverlordObjectSelectedEventHandler;
                _overlordObjects.Add(current);
            }

            for (int i = 0; i < LoopEndFakeHeroCount; i++)
            {
                Hero hero = _dataManager.CachedHeroesData.Heroes[i];
                OverlordObject current = new OverlordObject(_overlordsContainer.GetComponent<RectTransform>(),
                    _rightContentObject, hero);
                current.SelfObject.name += $" #{i}";
                _loopFakeOverlordObjects.Add(current);
            }

            _overlordObjects[0].Select(false);
        }

        private void OverlordObjectSelectedEventHandler(OverlordObject overlordObject)
        {
            foreach (OverlordObject item in _overlordObjects)
            {
                if (!item.Equals(overlordObject))
                {
                    item.Deselect();
                }
                else
                {
                    item.Select();
                }
            }

            _currentOverlordObject = overlordObject;
        }

        private void SwitchOverlordObject(int direction)
        {
            int newIndex = _selectedHeroIndex;
            newIndex += direction;

            if (newIndex < 0)
            {
                SetSelectedHeroIndexAndUpdateScrollPosition(_overlordObjects.Count, false, false, false, direction);
                SetSelectedHeroIndexAndUpdateScrollPosition(_overlordObjects.Count - 1, true, true, false, direction);
                _loopFakeOverlordObjects[LoopStartFakeHeroCount].Deselect(true, true);
            }
            else if (newIndex >= _overlordObjects.Count)
            {
                SetSelectedHeroIndexAndUpdateScrollPosition(-1, false, false, false, direction);
                SetSelectedHeroIndexAndUpdateScrollPosition(0, true, true, false, direction);
                _loopFakeOverlordObjects[LoopStartFakeHeroCount - 1].Deselect(true, true);
            }
            else
            {
                SetSelectedHeroIndexAndUpdateScrollPosition(newIndex, true, true, false, direction);
            }
        }

        private Vector2 CalculateOverlordContainerShiftForHeroIndex(int heroIndex)
        {
            return Vector2.left * (heroIndex + LoopStartFakeHeroCount) * _overlordsContainer.spacing;
        }

        private void ResetOverlordObjects()
        {
            if (_overlordObjects != null)
            {
                foreach (OverlordObject item in _overlordObjects)
                {
                    item.Dispose();
                }

                _overlordObjects.Clear();
                _overlordObjects = null;

                foreach (OverlordObject item in _loopFakeOverlordObjects)
                {
                    item.Dispose();
                }

                _loopFakeOverlordObjects.Clear();
                _loopFakeOverlordObjects = null;
            }
        }

        public class OverlordObject
        {
            private readonly ILoadObjectsManager _loadObjectsManager;

            private readonly Image _highlightImage;

            private readonly Material _glowMeshMaterial;
           
            private readonly Material _glowParticleShine;

            private readonly Material _glowParticleDots;

            private readonly Image _overlordPicture;

            private readonly Image _overlordPictureGray;

            private readonly Image _elementIcon;

            private readonly Sprite _overlordPictureSprite;

            private readonly Sprite _elementIconSprite;

            private readonly TextMeshProUGUI _overlordNameText;

            private readonly TextMeshProUGUI _overlordDescriptionText;

            private readonly TextMeshProUGUI _overlordShortDescription;

            private Sequence _stateChangeSequence;

            public OverlordObject(Transform parent, GameObject informationPanelObject, Hero hero)
            {
                SelfHero = hero;

                _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();

                SelfObject =
                    Object.Instantiate(
                        _loadObjectsManager.GetObjectByPath<GameObject>(
                            "Prefabs/UI/Elements/Item_OverlordSelectionOverlord"), parent, false);
                SelfObject.gameObject.name = hero.FullName;

                _highlightImage = SelfObject.transform.Find("Image_Highlight").gameObject.GetComponent<Image>();
                Transform glowContainer = SelfObject.transform.Find("Glow");
                Transform glowObject = Object.Instantiate(
                        _loadObjectsManager.GetObjectByPath<GameObject>(
                            "Prefabs/VFX/UI/Overlord/ZB_ANM_OverLordSelector_" + SelfHero.HeroElement), glowContainer, false).transform;

                _glowMeshMaterial = glowObject.Find("VFX_All/MeshGlow").GetComponent<MeshRenderer>().material;
                _glowParticleShine = glowObject.Find("VFX_All/glowingEffect/energy").GetComponent<ParticleSystemRenderer>().material;
                _glowParticleDots = glowObject.Find("VFX_All/glowingEffect/Dots").GetComponent<ParticleSystemRenderer>().material;

                _overlordPicture = SelfObject.transform.Find("Image_OverlordPicture").GetComponent<Image>();
                _overlordPictureGray = SelfObject.transform.Find("Image_OverlordPictureGray").GetComponent<Image>();
                _elementIcon = informationPanelObject.transform.Find("Panel_Type/Image_ElementType")
                    .GetComponent<Image>();

                _overlordNameText = informationPanelObject.transform.Find("Text_OverlordName")
                    .GetComponent<TextMeshProUGUI>();
                _overlordDescriptionText = informationPanelObject.transform.Find("Text_LongDescription")
                    .GetComponent<TextMeshProUGUI>();
                _overlordShortDescription = informationPanelObject.transform.Find("Text_ShortDescription")
                    .GetComponent<TextMeshProUGUI>();

                _overlordPictureSprite =
                    _loadObjectsManager.GetObjectByPath<Sprite>("Images/UI/ChooseOverlord/portrait_" +
                        SelfHero.HeroElement.ToString().ToLowerInvariant() + "_hero");
                _overlordPictureGray.sprite = _overlordPicture.sprite = _overlordPictureSprite;
                _elementIconSprite =
                    _loadObjectsManager.GetObjectByPath<Sprite>("Images/UI/ElementIcons/Icon_element_" +
                        SelfHero.HeroElement.ToString().ToLowerInvariant());

                _overlordPictureGray.sprite = _overlordPicture.sprite = _overlordPictureSprite;

                _glowMeshMaterial.color.SetAlpha(0);
                _glowParticleShine.SetFloat("_Alpha", 0);
                _glowParticleDots.SetFloat("_Alpha", 0);
                //_glow.SetActive(false);

                Deselect(false, true);
            }

            public event Action<OverlordObject> OverlordObjectSelectedEvent;

            public GameObject SelfObject { get; }

            public Hero SelfHero { get; }

            public bool IsSelected { get; private set; }

            public void Dispose()
            {
                Object.Destroy(SelfObject);
            }

            public void Select(bool animateTransition = true, int direction = 1)
            {
                if (IsSelected)
                    return;

                IsSelected = true;

                SelfObject.gameObject.name = SelfHero.Name + " (Selected)";

                _overlordNameText.text = SelfHero.Name;
                _overlordDescriptionText.text = SelfHero.LongDescription;
                _overlordShortDescription.text = SelfHero.ShortDescription;

                _elementIcon.sprite = _elementIconSprite;

                SetUIActiveState(true, animateTransition, false, direction);

                OverlordObjectSelectedEvent?.Invoke(this);
            }

            public void Deselect(bool animateTransition = true, bool force = false)
            {
                if (!IsSelected && !force)
                    return;

                IsSelected = false;

                SelfObject.gameObject.name = SelfHero.Name;
                SetUIActiveState(false, animateTransition, force);
            }

            private void SetUIActiveState(bool active, bool animateTransition, bool forceResetAlpha, int direction = 1)
            {
                float duration = animateTransition ? ScrollAnimationDuration : 0f;
                float targetAlpha = active ? 1f : 0f;

                _stateChangeSequence?.Kill();
                _stateChangeSequence = DOTween.Sequence();

                Action<Image, bool> applyAnimation = (image, invert) =>
                {
                    image.gameObject.SetActive(true);
                    if (forceResetAlpha)
                    {
                        image.color = image.color.SetAlpha(invert ? targetAlpha : 1f - targetAlpha);
                    }
                    Color color = image.color.SetAlpha(invert ? 1f - targetAlpha : targetAlpha);

                    float delay = (direction < 0 && active) ? 0.3f : 0;
                    float durationGlow = duration / 3f;

                    _stateChangeSequence.Insert(delay, _glowMeshMaterial.DOFade(color.a, durationGlow));
                    _stateChangeSequence.Insert(delay, _glowParticleShine.DOFloat(color.a, "_Alpha", durationGlow));
                    _stateChangeSequence.Insert(delay, _glowParticleDots.DOFloat(color.a, "_Alpha", durationGlow));
                    _stateChangeSequence.Insert(0f,
                            image.DOColor(color, duration)
                            .OnComplete(() => {
                                image.gameObject.SetActive(invert ? !active : active);
                            }));

                };

                

                applyAnimation(_overlordPicture, false);
                applyAnimation(_overlordPictureGray, true);
                applyAnimation(_highlightImage, false);
            }
        }

        #region Buttons Handlers

        private void BackButtonOnClickHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            _appStateManager.ChangeAppState(Enumerators.AppState.HordeSelection);
        }

        private void ContinueButtonOnClickHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);

            _uiManager.GetPopup<OverlordAbilitySelectionPopup>().PopupHiding += AbilityPopupClosedEvent;
            _uiManager.DrawPopup<OverlordAbilitySelectionPopup>(_currentOverlordObject.SelfHero);
        }

        private void AbilityPopupClosedEvent()
        {
            _uiManager.GetPopup<OverlordAbilitySelectionPopup>().PopupHiding -= AbilityPopupClosedEvent;

            _uiManager.GetPage<HordeEditingPage>().CurrentHeroId = _currentOverlordObject.SelfHero.HeroId;

            _appStateManager.ChangeAppState(Enumerators.AppState.DECK_EDITING);
        }

        private void LeftArrowButtonOnClickHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            SwitchOverlordObject(-1);
        }

        private void RightArrowButtonOnClickHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            SwitchOverlordObject(1);
        }

        #endregion

    }
}
