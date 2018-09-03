using System;
using LoomNetwork.CZB.Common;
using LoomNetwork.CZB.Data;

namespace LoomNetwork.CZB
{
    public static class SetTypeUtility
    {
        public static CardSet GetCardSet(IDataManager dataManager, Enumerators.SetType setType)
        {
            int setIndex = dataManager.CachedCardsLibraryData.Sets.FindIndex(s =>
                s.Name.Equals(setType.ToString(), StringComparison.InvariantCultureIgnoreCase));
            return dataManager.CachedCardsLibraryData.Sets[setIndex];
        }

        public static Enumerators.SetType GetCardSetType(IDataManager dataManager, int setIndex)
        {
            string setName = dataManager.CachedCardsLibraryData.Sets[setIndex].Name;
            return (Enumerators.SetType) Enum.Parse(typeof(Enumerators.SetType), setName, true);
        }
    }
}
