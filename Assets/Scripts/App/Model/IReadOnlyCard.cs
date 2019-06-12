using System.Collections.Generic;
using Loom.ZombieBattleground.Common;

namespace Loom.ZombieBattleground.Data
{
    public interface IReadOnlyCard
    {
        CardKey CardKey { get; }

        string Name { get; }

        int Cost { get; }

        string Description { get; }

        string FlavorText { get; }

        string Picture { get; }

        int Damage { get; }

        int Defense { get; }

        Enumerators.Faction Faction { get; }

        string Frame { get; }

        Enumerators.CardKind Kind { get; }

        Enumerators.CardRank Rank { get; }

        Enumerators.CardType Type { get; }

        IReadOnlyList<AbilityData> Abilities { get; }

        PictureTransform PictureTransform { get; }

        Enumerators.UniqueAnimation UniqueAnimation { get; }

        bool Hidden { get; }
    }
}
