using UnityEngine;

namespace CardGame
{
    public enum CardType
    {
        Character,   // Персонажі (Герої гравців)
        Additional,  // Додаткові карти для гравців (закляття, бафи, ефекти)
        Enemy        // Вороги на полі бою
    }

    public enum CardElement
    {
        Arcane,
        Fire,
        Frost,
        Nature
    }

    public enum CardRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(fileName = "NewCardData", menuName = "CardGame/CardData")]
    public class CardData : ScriptableObject
    {
        [Header("Card Identity")]
        public string id;
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public CardType cardType;
        public CardElement element = CardElement.Arcane;
        public CardRarity rarity = CardRarity.Common;

        [Header("Visuals")]
        public Sprite cardSprite;
        public Color frameColor = Color.white;

        [Header("Stats")]
        [Range(0, 10)]
        public int manaCost = 1;
        [Range(0, 50)]
        public int health = 10;
        [Range(0, 50)]
        public int attack = 4;
    }
}
