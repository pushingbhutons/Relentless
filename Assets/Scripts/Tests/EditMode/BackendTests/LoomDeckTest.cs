using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Protobuf;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AIDeck = Loom.ZombieBattleground.Data.AIDeck;
using Deck = Loom.ZombieBattleground.Data.Deck;

namespace Loom.ZombieBattleground.Test
{
    [Category("EditQuickSubset")]
    public class LoomDeckTest
    {
        private readonly LoomTestContext LoomTestContext = new LoomTestContext();

        [SetUp]
        public void Init()
        {
            LoomTestContext.TestSetUp();
        }

        [TearDown]
        public void TearDown()
        {
            LoomTestContext.TestTearDown();
        }

        [UnityTest]
        public IEnumerator GetDeck()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_GetDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                ListDecksResponse listDecksResponse = await LoomTestContext.BackendFacade.ListDecks(user);
                Assert.IsNotNull(listDecksResponse);
                Assert.IsNotNull(listDecksResponse.Decks);

                DecksData decksData = listDecksResponse.FromProtobuf();
                Assert.AreEqual(1, decksData.Decks.Count);
                Assert.AreEqual("Default", decksData.Decks[0].Name);
            });
        }

        [UnityTest]
        public IEnumerator GetDeck_Empty_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_GetDeck");
                await LoomTestContext.BackendFacade.SignUp(user);
                Assert.IsNull(await LoomTestContext.BackendFacade.ListDecks(string.Empty));
            });
        }

        [UnityTest]
        public IEnumerator GetDeck_Wrong_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                Assert.IsNull(await LoomTestContext.BackendFacade.ListDecks("GauravIsGreatWorkingInLoom"));
            });
        }

        [UnityTest]
        public IEnumerator GetDeck_User_Have_No_Deck()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_NoDecks");
                await LoomTestContext.BackendFacade.SignUp(user);

                ListDecksResponse listDecksResponse = await LoomTestContext.BackendFacade.ListDecks(user);
                Assert.IsNotNull(listDecksResponse);

                await LoomTestContext.BackendFacade.DeleteDeck(user, 1);

                ListDecksResponse newListDecksResponse = await LoomTestContext.BackendFacade.ListDecks(user);
                Assert.IsNull(newListDecksResponse);
            });
        }

        [UnityTest]
        public IEnumerator AddDeck()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_AddDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                Deck deck = new Deck(0, 0, "Gaurav", null, 0, 0);

                await LoomTestContext.BackendFacade.AddDeck(user, deck);
            });
        }

        [UnityTest]
        public IEnumerator AddDeck_Wrong_User_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_AddDeck_wrong_user");
                Deck deck = new Deck(0, 0, "Gaurav", null, 0, 0);

                await LoomTestContext.AssertThrowsAsync(
                    async () =>
                    {
                        await LoomTestContext.BackendFacade.AddDeck(user, deck);
                    });
            });
        }

        [UnityTest]
        public IEnumerator AddDeck_Wrong_Deck_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_AddDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                List<DeckCardData> cards =
                    new List<DeckCardData>
                    {
                        new DeckCardData(1, 100500)
                    };
                Deck deck = new Deck(0, 0, "Gaurav", cards, 0, 0);

                await LoomTestContext.AssertThrowsAsync(async () =>
                {
                    await LoomTestContext.BackendFacade.AddDeck(user, deck);
                });
            });
        }

        [UnityTest]
        public IEnumerator EditDeck()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_EditDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                Deck deck = new Deck(1, 0, "Default", null, 0, 0);
                await LoomTestContext.BackendFacade.EditDeck(user, deck);
            });
        }

        [UnityTest]
        public IEnumerator EditDeck_Wrong_User_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_EditDeck_wrong_user");
                Deck deck = new Deck(0, 0, "Gaurav", null, 0, 0);

                await LoomTestContext.AssertThrowsAsync(async () =>
                {
                    await LoomTestContext.BackendFacade.EditDeck(user, deck);
                });
            });
        }

        [UnityTest]
        public IEnumerator EditDeck_Wrong_Deck_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_EditDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                Deck deck = new Deck(123, 0, "GauravRandomDeck", null, 0, 0);
                await LoomTestContext.AssertThrowsAsync(async () =>
                {
                    await LoomTestContext.BackendFacade.EditDeck(user, deck);
                });
            });
        }

        [UnityTest]
        public IEnumerator DeleteDeck()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_DeleteDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                await LoomTestContext.BackendFacade.DeleteDeck(user, 1);
            });
        }

        [UnityTest]
        public IEnumerator DeleteDeck_Wrong_User_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_DeleteDeck_wrong_User");
                await LoomTestContext.AssertThrowsAsync(
                    async () =>
                    {
                        await LoomTestContext.BackendFacade.DeleteDeck(user, 0);
                    });
            });
        }

        [UnityTest]
        public IEnumerator DeleteDeck_Wrong_Deck_Request()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_DeleteDeck_wrong_Deck");
                await LoomTestContext.BackendFacade.SignUp(user);

                await LoomTestContext.AssertThrowsAsync(
                    async () =>
                    {
                        await LoomTestContext.BackendFacade.DeleteDeck(user, 123);
                    });
            });
        }

        [UnityTest]
        public IEnumerator GetAiDecks()
        {
            return LoomTestContext.ContractAsyncTest(async () =>
            {
                string user = LoomTestContext.CreateUniqueUserId("LoomTest_GetAiDeck");
                await LoomTestContext.BackendFacade.SignUp(user);

                GetAIDecksResponse getAIDecksResponse = await LoomTestContext.BackendFacade.GetAiDecks();
                Assert.IsNotNull(getAIDecksResponse);
                Assert.IsNotNull(getAIDecksResponse.Decks);

                List<AIDeck> decksData =
                    getAIDecksResponse.Decks
                        .Select(d => d.FromProtobuf())
                        .ToList();
                Assert.GreaterOrEqual(1, decksData.Count);
            });
        }
    }
}
