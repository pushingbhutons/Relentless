using System.Collections.Generic;
using System.Threading.Tasks;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;

namespace Loom.ZombieBattleground.Test
{
    public interface IPlayerActionTestProxy
    {
        Task EndTurn();
        Task LeaveMatch();
        Task Mulligan(IEnumerable<InstanceId> cards);
        Task CardPlay(InstanceId card, int position);
        Task RankBuff(WorkingCard card, IEnumerable<InstanceId> units);

        Task CardAbilityUsed(
            WorkingCard card,
            Enumerators.AbilityType abilityType,
            Enumerators.CardKind cardKind,
            Enumerators.AffectObjectType affectObjectType,
            IReadOnlyList<ParametrizedAbilityBoardObject> targets = null,
            IEnumerable<InstanceId> cards = null
        );

        Task OverlordSkillUsed(SkillId skillId, Enumerators.AffectObjectType affectObjectType, InstanceId targetInstanceId);
        Task CardAttack(InstanceId attacker, Enumerators.AffectObjectType type, InstanceId target);

        Task<bool> GetIsCurrentTurn();
    }
}
