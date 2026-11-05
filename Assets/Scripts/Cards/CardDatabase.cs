using System.Collections.Generic;
using UnityEngine;

namespace CardGame
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "CardGame/CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        [Header("Card Collections")]
        [SerializeField] private List<CardData> characterCards = new List<CardData>();
        [SerializeField] private List<CardData> additionalCards = new List<CardData>();
        [SerializeField] private List<CardData> enemyCards = new List<CardData>();

        public IReadOnlyList<CardData> CharacterCards => characterCards;
        public IReadOnlyList<CardData> AdditionalCards => additionalCards;
        public IReadOnlyList<CardData> EnemyCards => enemyCards;

        public void SetCards(List<CardData> characters, List<CardData> additionals, List<CardData> enemies = null)
        {
            characterCards = new List<CardData>(characters);
            additionalCards = new List<CardData>(additionals);
            if (enemies != null)
            {
                enemyCards = new List<CardData>(enemies);
            }
        }

        public CardData GetCardById(string id)
        {
            foreach (var card in characterCards)
            {
                if (card != null && card.id == id) return card;
            }
            foreach (var card in additionalCards)
            {
                if (card != null && card.id == id) return card;
            }
            return null;
        }

        public CardData GetRandomCharacter()
        {
            if (characterCards.Count == 0) return null;
            return characterCards[Random.Range(0, characterCards.Count)];
        }

        public CardData GetRandomAdditional()
        {
            if (additionalCards.Count == 0) return null;
            return additionalCards[Random.Range(0, additionalCards.Count)];
        }

        public List<CardData> GetRandomAdditionalHand(int count)
        {
            var result = new List<CardData>();
            if (additionalCards.Count == 0) return result;
            var pool = new List<CardData>(additionalCards);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }
    }
}
