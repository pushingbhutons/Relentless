// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using LoomNetwork.CZB.Common;
using LoomNetwork.CZB.Data;

namespace LoomNetwork.CZB
{
    public class WorkingCard
    {
        public int cardId;
        public Card libraryCard;

        public Player owner;

        public int instanceId;


        public int initialHealth,
                   initialDamage,
                   health,
                   damage;

        public int initialCost;

        public Enumerators.CardType type;

        public bool IsPlayable { get; set; }


        public WorkingCard(Card card, Player player)
        {
            libraryCard = card.Clone();
            cardId = libraryCard.id;
            owner = player;

            initialHealth = libraryCard.health;
            initialDamage = libraryCard.damage;
            health = initialHealth;
            damage = initialDamage;
            initialCost = libraryCard.cost;

            type = libraryCard.cardType;

            instanceId = GameClient.Get<IGameplayManager>().GetController<CardsController>().GetNewCardInstanceId();
        }
    }
}
