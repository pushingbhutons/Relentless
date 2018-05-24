﻿// Copyright (C) 2016-2017 David Pol. All rights reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement,
// a copy of which is available at http://unity3d.com/company/legal/as_terms.

using System;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

using DG.Tweening;
using TMPro;

using CCGKit;

using GrandDevs.CZB.Common;
using GrandDevs.CZB;
using System.Collections.Generic;
using GrandDevs.CZB.Helpers;

public class BoardCreature : MonoBehaviour
{
    public RuntimeCard card { get; private set; }

    [HideInInspector]
    public GameObject fightTargetingArrowPrefab;

    [SerializeField]
    protected SpriteRenderer pictureSprite;

    [SerializeField]
    protected SpriteRenderer frozenSprite;

    [SerializeField]
    protected TextMeshPro attackText;

    [SerializeField]
    protected TextMeshPro healthText;

    [SerializeField]
    protected ParticleSystem sleepingParticles;

    [HideInInspector]
    public Player ownerPlayer;
    [HideInInspector]
    public TargetingArrow abilitiesTargetingArrow;
    [HideInInspector]
    public FightTargetingArrow fightTargetingArrow;

    public Stat attackStat { get; protected set; }
    public Stat healthStat { get; protected set; }

    [HideInInspector]
    public bool hasImpetus;
    [HideInInspector]
    public bool hasProvoke;

    [HideInInspector]
    public int numTurnsOnBoard;

    protected Action<int, int> onAttackStatChangedDelegate;
    protected Action<int, int> onHealthStatChangedDelegate;
	public event Action CreatureOnDieEvent;
	public event Action<object> CreatureOnAttackEvent;

    private int _stunTurns = 0;

    private Server _server;


    public AnimationEventTriggering arrivalAnimationEventHandler;

    public GameObject creatureContentObject;

    public Animator creatureAnimator;

    public List<CreatureAnimatorInfo> animatorControllers;

    public bool IsPlayable
    {
        set{
            card.isPlayable = value;

            var netCard = GameClient.Get<IPlayerManager>().playerInfo.namedZones[Constants.ZONE_BOARD].cards.Find(x => x.instanceId == card.instanceId);
            if(netCard == null)
                netCard = GameClient.Get<IPlayerManager>().opponentInfo.namedZones[Constants.ZONE_BOARD].cards.Find(x => x.instanceId == card.instanceId);
            if(netCard != null)
                netCard.isPlayable = value;
        }
        get{
            return card.isPlayable;
        }
    }

    public bool IsStun
    {
        get{ return (_stunTurns > 0 ? true : false); }
    }

    protected virtual void Awake()
    {
      //Assert.IsNotNull(glowSprite);
     //   Assert.IsNotNull(pictureSprite);
        Assert.IsNotNull(attackText);
        Assert.IsNotNull(healthText);
        Assert.IsNotNull(sleepingParticles);
        Assert.IsNotNull(arrivalAnimationEventHandler);
        Assert.IsNotNull(creatureContentObject);
        Assert.IsNotNull(creatureAnimator);

        arrivalAnimationEventHandler.OnAnimationEvent += ArrivalAnimationEventHandler;
    }

    protected virtual void OnDestroy()
    {
        healthStat.onValueChanged -= onHealthStatChangedDelegate;
        attackStat.onValueChanged -= onAttackStatChangedDelegate;

        if (ownerPlayer != null)
            CreatureOnDieEvent?.Invoke();
    }

    public virtual void ArrivalAnimationEventHandler(string param)
    {
        if (param.Equals("ArrivalAnimationDone"))
        {
            creatureContentObject.SetActive(true);
			if (hasImpetus)
			{
				//  frameSprite.sprite = frameSprites[1];
				StopSleepingParticles();
				//if (ownerPlayer != null)
				//    SetHighlightingEnabled(true);
				IsPlayable = true;
			}
        }
    }

    public virtual void PopulateWithInfo(RuntimeCard card, string setName = "")
    {
        this.card = card;
         
        var libraryCard = GameClient.Get<IDataManager>().CachedCardsLibraryData.GetCard(card.cardId);

        var rarity = Enum.GetName(typeof(Enumerators.CardRarity), libraryCard.cardRarity);

        pictureSprite.sprite = Resources.Load<Sprite>(string.Format("Images/Cards/Illustrations/{0}_{1}_{2}", setName.ToLower(), rarity.ToLower(), libraryCard.picture.ToLower()));

        pictureSprite.transform.localPosition = MathLib.FloatVector3ToVector3(libraryCard.cardViewInfo.position);
        pictureSprite.transform.localScale = MathLib.FloatVector3ToVector3(libraryCard.cardViewInfo.scale);

        creatureAnimator.runtimeAnimatorController = animatorControllers.Find(x => x.cardType == libraryCard.cardType).animator;
        if(libraryCard.cardType == Enumerators.CardType.WALKER)
        {
            sleepingParticles.transform.position += Vector3.up * 0.7f;
        }

        attackStat = card.namedStats["DMG"];
		healthStat = card.namedStats["HP"];

        attackText.text = attackStat.effectiveValue.ToString();
        healthText.text = healthStat.effectiveValue.ToString();


        onAttackStatChangedDelegate = (oldValue, newValue) =>
        {
            UpdateStatText(attackText, attackStat);
        };
        attackStat.onValueChanged += onAttackStatChangedDelegate;

        onHealthStatChangedDelegate = (oldValue, newValue) =>
        {
			UpdateStatText(healthText, healthStat);
        };
        healthStat.onValueChanged += onHealthStatChangedDelegate;

        if(libraryCard.cardType == Enumerators.CardType.FERAL)
            hasImpetus = true;
        else if (libraryCard.cardType == Enumerators.CardType.HEAVY)
			hasProvoke = true;
          
        if (hasProvoke)
        {
         //   glowSprite.gameObject.SetActive(false);
          //  pictureMaskTransform.localScale = new Vector3(50, 55, 1);
           // frameSprite.sprite = frameSprites[2];
        }
        SetHighlightingEnabled(false);

        creatureAnimator.StopPlayback();
        creatureAnimator.Play(0);
    }

    public void OnStartTurn()
    {
        numTurnsOnBoard += 1;
        StopSleepingParticles();

        if (ownerPlayer != null && IsPlayable)
            SetHighlightingEnabled(true);
    }

    public void OnEndTurn()
    {
        if(_stunTurns > 0)
            _stunTurns--;
        if (_stunTurns == 0)
        {
            IsPlayable = true;
            frozenSprite.DOFade(0, 1);
        }

        CancelTargetingArrows();
    }

	public void Stun(int turns)
	{
        Debug.Log("WAS STUNED");
        if(turns > _stunTurns)
            _stunTurns = turns;
        IsPlayable = false;

        frozenSprite.DOFade(1, 1);
        //sleepingParticles.Play();
    }

    public void CancelTargetingArrows()
    {
        if (abilitiesTargetingArrow != null)
        {
            Destroy(abilitiesTargetingArrow.gameObject);
        }
        if (fightTargetingArrow != null)
        {
            Destroy(fightTargetingArrow.gameObject);
        }
    }

    private void UpdateStatText(TextMeshPro text, Stat stat)
    {
        text.text = stat.effectiveValue.ToString();
        if (stat.effectiveValue > stat.originalValue)
        {
            text.color = Color.green;
        }
        else if (stat.effectiveValue < stat.originalValue)
        {
            text.color = Color.red;
        }
        else
        {
            if (stat.statId == 1)
                text.color = Color.white;
            else
                text.color = Color.black;
        }
        var sequence = DOTween.Sequence();
        sequence.Append(text.transform.DOScale(new Vector3(1.4f, 1.4f, 1.0f), 0.4f));
        sequence.Append(text.transform.DOScale(new Vector3(1.0f, 1.0f, 1.0f), 0.2f));
        sequence.Play();
    }

    public void SetHighlightingEnabled(bool enabled)
    {
		//glowSprite.enabled = enabled;
	}

    public void StopSleepingParticles()
    {
        if(sleepingParticles != null)
        sleepingParticles.Stop();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.transform.parent != null)
        {
            var targetingArrow = collider.transform.parent.parent.GetComponent<TargetingArrow>();
            if (targetingArrow != null)
            {
                targetingArrow.OnCardSelected(this);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.transform.parent != null)
        {
            var targetingArrow = collider.transform.parent.parent.GetComponent<TargetingArrow>();
            if (targetingArrow != null)
            {
                targetingArrow.OnCardUnselected(this);
            }
        }
    }

    private void OnMouseDown()
    {
        if (fightTargetingArrowPrefab == null)
            return;

        if (ownerPlayer != null && ownerPlayer.isActivePlayer && IsPlayable)
        {
            fightTargetingArrow = Instantiate(fightTargetingArrowPrefab).GetComponent<FightTargetingArrow>();
            fightTargetingArrow.targetType = EffectTarget.OpponentOrOpponentCreature;
            fightTargetingArrow.opponentBoardZone = ownerPlayer.opponentBoardZone;
            fightTargetingArrow.Begin(transform.position);

            // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ONLY FOR PLAYER!!!!! IMPROVE IT
            if (ownerPlayer is DemoHumanPlayer)
            {
                (ownerPlayer as DemoHumanPlayer).DestroyCardPreview();
                (ownerPlayer as DemoHumanPlayer).isCardSelected = true;

                if (GameClient.Get<ITutorialManager>().IsTutorial)
                {
                    GameClient.Get<ITutorialManager>().DeactivateSelectTarget();
                }
            }
        }
    }

    private void OnMouseUp()
    {
        if (fightTargetingArrow != null)
        {
            fightTargetingArrow.End(this);

            // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ONLY FOR PLAYER!!!!! IMPROVE IT
            if (ownerPlayer is DemoHumanPlayer)
            {
                (ownerPlayer as DemoHumanPlayer).isCardSelected = false;


            }
        }
    }

    public void ResolveCombat()
    {
        var sortingGroup = GetComponent<SortingGroup>();
        if (fightTargetingArrow != null)
        {	
            if (fightTargetingArrow.selectedPlayer != null)
            {
                var targetPlayer = fightTargetingArrow.selectedPlayer;
                SetHighlightingEnabled(false);
                IsPlayable = false;

                var libraryCard = GameClient.Get<IDataManager>().CachedCardsLibraryData.GetCard(card.cardId);
                GameClient.Get<ISoundManager>().PlaySound(Enumerators.SoundType.CARDS, libraryCard.name.ToLower() + "_" + Constants.CARD_SOUND_ATTACK, 0.3f);

                //sortingGroup.sortingOrder = 100;
                CombatAnimation.PlayFightAnimation(gameObject, targetPlayer.gameObject, 0.1f, () =>
                {
                    Debug.Log("CreatureOnAttackEvent?.Invoke(targetPlayer)");
					(ownerPlayer as DemoHumanPlayer).PlayAttackVFX(card.type, targetPlayer.transform.position, attackStat.effectiveValue);

					ownerPlayer.FightPlayer(card);
                    CreatureOnAttackEvent?.Invoke(targetPlayer);
                },
                () =>
                {
                    //sortingGroup.sortingOrder = 0;
                    fightTargetingArrow = null;
                });
            }
            if (fightTargetingArrow.selectedCard != null)                                                                                 
            {
                var targetCard = fightTargetingArrow.selectedCard;
                SetHighlightingEnabled(false);
                IsPlayable = false;

				//sortingGroup.sortingOrder = 100;
                if (targetCard != GetComponent<BoardCreature>() &&
                    targetCard.GetComponent<HandCard>() == null)
                {
                    CombatAnimation.PlayFightAnimation(gameObject, targetCard.gameObject, 0.5f, () =>
                    {
                        Debug.Log("CreatureOnAttackEvent?.Invoke(targetCard)");
                        (ownerPlayer as DemoHumanPlayer).PlayAttackVFX(card.type, targetCard.transform.position, attackStat.effectiveValue);

						ownerPlayer.FightCreature(card, targetCard.card);
                        CreatureOnAttackEvent?.Invoke(targetCard);
                    },
                    () =>
                    {
                        //sortingGroup.sortingOrder = 0;
                        fightTargetingArrow = null;
                    });
                }
            }
            if(fightTargetingArrow.selectedCard == null && fightTargetingArrow.selectedPlayer == null)
            {
                if (GameClient.Get<ITutorialManager>().IsTutorial)
                {
                    GameClient.Get<ITutorialManager>().ActivateSelectTarget();
                }
            }
        }
    }

    public void CreatureOnAttack(object target)
    {
        CreatureOnAttackEvent?.Invoke(target);
    }

}

[Serializable]
public class CreatureAnimatorInfo
{
    public Enumerators.CardType cardType;
    public RuntimeAnimatorController animator;
}