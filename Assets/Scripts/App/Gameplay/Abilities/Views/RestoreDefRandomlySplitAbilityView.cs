using DG.Tweening;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Helpers;
using System.Collections.Generic;
using UnityEngine;

namespace Loom.ZombieBattleground
{
    public class RestoreDefRandomlySplitAbilityView : AbilityViewBase<RestoreDefRandomlySplitAbility>
    {
        private BattlegroundController _battlegroundController;

        private string _cardName;

        private List<object> _targets;

        public RestoreDefRandomlySplitAbilityView(RestoreDefRandomlySplitAbility ability) : base(ability)
        {
            _battlegroundController = GameClient.Get<IGameplayManager>().GetController<BattlegroundController>();
        }

        protected override void OnAbilityAction(object info = null)
        {
            _targets = info as List<object>;

            if (Ability.AbilityData.HasVisualEffectType(Enumerators.VisualEffectType.Moving))
            {
                Vector3 targetPosition = Vector3.zero;

                VfxObject = LoadObjectsManager.GetObjectByPath<GameObject>(Ability.AbilityData.GetVisualEffectByType(Enumerators.VisualEffectType.Moving).Path);

                foreach (BoardObject boardObject in _targets)
                {
                    switch (boardObject)
                    {
                        case BoardUnitModel unit:
                            targetPosition = _battlegroundController.GetBoardUnitViewByModel(unit).Transform.position;
                            break;
                        case Player player:
                            targetPosition = Ability.TargetPlayer.AvatarObject.transform.position;
                            break;
                    }

                    VfxObject = Object.Instantiate(VfxObject);
                    VfxObject.transform.position = Utilites.CastVfxPosition(_battlegroundController.GetBoardUnitViewByModel(Ability.AbilityUnitOwner).Transform.position);
                    targetPosition = Utilites.CastVfxPosition(targetPosition);
                    VfxObject.transform.DOMove(targetPosition, 0.5f).OnComplete(ActionCompleted);
                    ParticleIds.Add(ParticlesController.RegisterParticleSystem(VfxObject));
                }
            }
            else
            {
                ActionCompleted();
            }
        }

        private void ActionCompleted()
        {
            ClearParticles();

            _cardName = "";
            float delayAfter = 0;
            float delayBeforeDestroy = 5f;
            Vector3 offset = Vector3.zero;
            string soundName = string.Empty;

            if (Ability.AbilityData.HasVisualEffectType(Enumerators.VisualEffectType.Impact))
            {
                Vector3 targetPosition = Vector3.zero;

                VfxObject = LoadObjectsManager.GetObjectByPath<GameObject>(Ability.AbilityData.GetVisualEffectByType(Enumerators.VisualEffectType.Impact).Path);

                AbilityEffectInfoView effectInfo = VfxObject.GetComponent<AbilityEffectInfoView>();

                if (effectInfo != null)
                {
                    _cardName = effectInfo.cardName;
                    delayAfter = effectInfo.delayAfterEffect;
                    delayBeforeDestroy = effectInfo.delayBeforeEffect;
                    offset = effectInfo.offset;
                    soundName = effectInfo.soundName;
                }

                bool isUnit = false;
                BoardUnitView unitModel = null;
                foreach (object boardObject in _targets)
                {
                    switch (boardObject)
                    {
                        case BoardUnitView unit:
                            targetPosition = unit.Transform.position;
                            isUnit = true;
                            break;
                        case Player player:
                            targetPosition = player.AvatarObject.transform.position;
                            isUnit = false;
                            break;
                    }

                    if (isUnit)
                    {
                        CreateVfx(targetPosition + offset, true, delayBeforeDestroy);

                        unitModel = boardObject as BoardUnitView;
                        GameObject frameMaskObject = null;
                        switch (unitModel.Model.InitialUnitType)
                        {
                            case Enumerators.CardType.WALKER:
                                frameMaskObject = VfxObject.transform.Find("WalkerMask").gameObject;
                                break;
                            case Enumerators.CardType.FERAL:
                                frameMaskObject = VfxObject.transform.Find("FeralMask").gameObject;
                                break;
                            case Enumerators.CardType.HEAVY:
                                frameMaskObject = VfxObject.transform.Find("HeavyMask").gameObject;
                                break;
                            default:
                                break;
                        }

                        if (frameMaskObject != null)
                        {
                            frameMaskObject.SetActive(true);
                        }
                    }
                }
            }
            InternalTools.DoActionDelayed(Ability.InvokeVFXAnimationEnded, delayAfter);
        }


        protected override void CreateVfx(Vector3 pos, bool autoDestroy = false, float duration = 3, bool justPosition = false)
        {
            base.CreateVfx(pos, autoDestroy, duration, justPosition);
        }
    }
}
