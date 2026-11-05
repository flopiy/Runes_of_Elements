using System.Collections.Generic;
using UnityEngine;
using TMPro;
using CardGame;

namespace CardGame
{
    public class PlayerCardStation : MonoBehaviour
    {
        [Header("Player Identity")]
        [SerializeField] private int playerIndex; // 0, 1, 2
        [SerializeField] private string playerName = "Гравець 1";
        [SerializeField] private Color playerColor = new Color(0.2f, 0.5f, 1f);

        [Header("Transforms & Slots")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private CardSlot heroCardSlot;
        [SerializeField] private List<CardSlot> handSlots = new List<CardSlot>();
        [SerializeField] private TextMeshPro labelText;
        [SerializeField] private MeshRenderer pedestalRenderer;

        [Header("Player Stats")]
        [SerializeField] private int maxHealth = 30;
        [SerializeField] private int currentHealth = 30;
        [SerializeField] private int maxMana = 10;
        [SerializeField] private int currentMana = 3;

        private Card3DView m_HeroCard;
        private List<Card3DView> m_HandCards = new List<Card3DView>();

        public int PlayerIndex => playerIndex;
        public string PlayerName => playerName;
        public Color PlayerColor => playerColor;
        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public int MaxMana => maxMana;
        public int CurrentMana => currentMana;
        public IReadOnlyList<Card3DView> HandCards => m_HandCards;

        public void InitializeStation(int index, CardDatabase db)
        {
            playerIndex = index;
            string[] names = new string[] { 
                "Гравець 1 (Маг Води)", 
                "Гравець 2 (Піромант)", 
                "Гравець 3 (Друїд Землі)", 
                "Гравець 4 (Жриця Пустоти)" 
            };
            Color[] colors = new Color[] {
                new Color(0.2f, 0.55f, 1f),
                new Color(1f, 0.25f, 0.25f),
                new Color(0.25f, 0.9f, 0.4f),
                new Color(0.75f, 0.35f, 1f)
            };

            if (index >= 0 && index < names.Length)
            {
                playerName = names[index];
                playerColor = colors[index];
            }

            if (labelText != null)
            {
                labelText.text = playerName;
                labelText.color = playerColor;
            }

            if (pedestalRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                pedestalRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", playerColor * 0.8f);
                block.SetColor("_EmissionColor", playerColor * 0.5f);
                pedestalRenderer.SetPropertyBlock(block);
            }

            var board = ArenaBoardManager.Instance != null ? ArenaBoardManager.Instance : FindFirstObjectByType<ArenaBoardManager>();
            if (board == null) return;

            // Assign hero card if slot available (lying flat on table)
            if (heroCardSlot != null && !heroCardSlot.IsOccupied && db != null)
            {
                var heroes = db.CharacterCards;
                if (heroes.Count > index)
                {
                    var card = board.InstantiateCard(heroes[index]);
                    heroCardSlot.PlaceCard(card);
                    card.SetBasePosition(new Vector3(0, 0.05f, 0), Quaternion.Euler(90f, 0, 0));
                    m_HeroCard = card;
                }
            }

            // Assign initial hand: exactly 1 additional card per player lying flat
            if (db != null && handSlots.Count > 0)
            {
                var additionalCard = db.GetRandomAdditional();
                if (additionalCard != null && handSlots[0] != null && !handSlots[0].IsOccupied)
                {
                    var card = board.InstantiateCard(additionalCard);
                    handSlots[0].PlaceCard(card);
                    card.SetBasePosition(new Vector3(0, 0.05f, 0), Quaternion.Euler(90f, 0, 0));
                    m_HandCards.Add(card);
                }
            }
        }

        public void SpendMana(int amount)
        {
            currentMana = Mathf.Max(0, currentMana - amount);
        }

        public void TakeDamage(int damage)
        {
            currentHealth = Mathf.Max(0, currentHealth - damage);
        }
    }
}
