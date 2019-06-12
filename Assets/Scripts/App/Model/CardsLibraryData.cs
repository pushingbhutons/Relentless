using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Loom.ZombieBattleground.Common;
using Newtonsoft.Json;

namespace Loom.ZombieBattleground.Data
{
    public class CardsLibraryData
    {
        public List<Faction> Factions { get; private set; }

        public IList<Card> Cards { get; private set; }

        public int CardsInActiveFactionsCount { get; private set; }

        public CardsLibraryData(IList<Card> cards)
        {
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
            InitData();
        }

        public Card GetCardByName(string name)
        {
            Card card = Cards.FirstOrDefault(x =>
                x.CardKey.Variant == Enumerators.CardVariant.Standard &&
                String.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase)
            );

            if (card == null)
                throw new KeyNotFoundException();

            return card;
        }

        public Card GetCardByCardKey(CardKey cardKey, bool allowFallbackToNormalEdition = false)
        {
            (bool found, Card card) = TryGetCardFromCardKey(cardKey, allowFallbackToNormalEdition);
            if (!found)
                throw new KeyNotFoundException();

            return card;
        }

        public (bool found, Card card) TryGetCardFromCardKey(CardKey cardKey, bool allowFallbackToNormalEdition = false)
        {
            Card card = Cards.FirstOrDefault(x => x.CardKey == cardKey);
            if (card != null)
                return (true, card);

            if (allowFallbackToNormalEdition)
            {
                card = Cards.FirstOrDefault(x => x.CardKey.MouldId == cardKey.MouldId);
                if (card != null)
                    return (true, card);
            }

            return (false, null);
        }

        private void InitData()
        {
            Cards = Cards.OrderBy(card => card.Cost).ToList();
            Factions =
                Cards
                    .GroupBy(card => card.Faction)
                    .Select(group => new Faction(group.Key, group.ToList()))
                    .OrderBy(set => set.Name)
                    .ToList();

            foreach (Faction set in Factions)
            {
                foreach (Card card in set.Cards)
                {
                    CardsInActiveFactionsCount++;
                }
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            InitData();
        }
    }

    public class Faction
    {
        public Enumerators.Faction Name { get; }

        public List<Card> Cards { get; }

        public Faction(Enumerators.Faction name, List<Card> cards)
        {
            Name = name;
            Cards = cards;
        }

        public override string ToString()
        {
            return $"({nameof(Name)}: {Name}, {Cards.Count} cards)";
        }
    }

    public class CardList
    {
        [JsonProperty("cards")]
        public List<Card> Cards;
    }
}
