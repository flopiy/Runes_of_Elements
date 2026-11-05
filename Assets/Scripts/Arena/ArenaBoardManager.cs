using System;
using System.Collections.Generic;
using UnityEngine;
using CardGame;

namespace CardGame
{
    public class ArenaBoardManager : MonoBehaviour
    {
        public static ArenaBoardManager Instance { get; private set; }

        [Header("Databases & Prefabs")]
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private GameObject cardPrefab;

        [Header("Board Slots (4 Lanes / Columns)")]
        [SerializeField] private List<CardSlot> playerHeroSlots = new List<CardSlot>();
        [SerializeField] private List<CardSlot> playerLaneSlots = new List<CardSlot>();
        [SerializeField] private List<CardSlot> opponentLaneSlots = new List<CardSlot>();
        [SerializeField] private List<CardSlot> opponentHeroSlots = new List<CardSlot>();

        [Header("Center Nexus")]
        [SerializeField] private Transform nexusCrystal;
        [SerializeField] private Light nexusLight;
        [SerializeField] private float crystalRotationSpeed = 30f;
        [SerializeField] private float crystalBobSpeed = 2f;

        [Header("4 Player Stations (Multiplayer up to 4 players)")]
        [SerializeField] private List<PlayerCardStation> playerStations = new List<PlayerCardStation>();

        [Header("Audio / SFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip playSound;

        // Runtime State
        private Card3DView m_SelectedCard;
        private int m_CurrentTurn = 1;
        private int m_ActivePlayerIndex = 0; // 0, 1, 2, 3 for 4 players

        public event Action<Card3DView> OnCardSelected;
        public event Action<Card3DView, CardSlot> OnCardPlayed;
        public event Action<int> OnTurnChanged;

        public Card3DView SelectedCard => m_SelectedCard;
        public int CurrentTurn => m_CurrentTurn;
        public int ActivePlayerIndex => m_ActivePlayerIndex;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            InitializeBoard();
        }

        private void Update()
        {
            AnimateNexusCrystal();
        }

        private void AnimateNexusCrystal()
        {
            if (nexusCrystal != null)
            {
                nexusCrystal.Rotate(Vector3.up, crystalRotationSpeed * Time.deltaTime, Space.World);
                nexusCrystal.Rotate(Vector3.right, crystalRotationSpeed * 0.4f * Time.deltaTime, Space.Self);
                float yOffset = Mathf.Sin(Time.time * crystalBobSpeed) * 0.1f;
                nexusCrystal.localPosition = new Vector3(0, 1.8f + yOffset, 0);
            }

            if (nexusLight != null)
            {
                nexusLight.intensity = 2.5f + Mathf.Sin(Time.time * 3f) * 0.5f;
            }
        }

        public void InitializeBoard()
        {
            if (cardDatabase == null) return;

            // 1. Spawn 4 Main Heroes for player row (lying flat on table)
            var charCards = cardDatabase.CharacterCards;
            for (int i = 0; i < playerHeroSlots.Count && i < charCards.Count; i++)
            {
                if (playerHeroSlots[i] != null && !playerHeroSlots[i].IsOccupied)
                {
                    var cardObj = InstantiateCard(charCards[i]);
                    playerHeroSlots[i].PlaceCard(cardObj);
                    cardObj.SetBasePosition(new Vector3(0, 0.05f, 0), Quaternion.Euler(90f, 0, 0));
                }
            }

            // 2. Spawn 4 Front Enemies (opponentLaneSlots, lying flat on table)
            var enemyCards = cardDatabase.EnemyCards;
            for (int i = 0; i < opponentLaneSlots.Count && i < enemyCards.Count; i++)
            {
                if (opponentLaneSlots[i] != null && !opponentLaneSlots[i].IsOccupied)
                {
                    var enemyObj = InstantiateCard(enemyCards[i]);
                    opponentLaneSlots[i].PlaceCard(enemyObj);
                    enemyObj.SetBasePosition(new Vector3(0, 0.05f, 0), Quaternion.Euler(90f, 180f, 0));
                }
            }

            // 3. Spawn 4 Back Enemies (opponentHeroSlots, lying flat on table)
            for (int i = 0; i < opponentHeroSlots.Count && (i + 4) < enemyCards.Count; i++)
            {
                if (opponentHeroSlots[i] != null && !opponentHeroSlots[i].IsOccupied)
                {
                    var enemyObj = InstantiateCard(enemyCards[i + 4]);
                    opponentHeroSlots[i].PlaceCard(enemyObj);
                    enemyObj.SetBasePosition(new Vector3(0, 0.05f, 0), Quaternion.Euler(90f, 180f, 0));
                }
            }

            // 4. Initialize player stations (4 players)
            for (int p = 0; p < playerStations.Count; p++)
            {
                if (playerStations[p] != null)
                {
                    playerStations[p].InitializeStation(p, cardDatabase);
                }
            }
        }

        public Card3DView InstantiateCard(CardData data)
        {
            if (cardPrefab == null) return null;
            var obj = Instantiate(cardPrefab);
            var view = obj.GetComponent<Card3DView>();
            if (view != null)
            {
                view.SetCardData(data);
            }
            return view;
        }

        public void SelectCard(Card3DView card)
        {
            if (m_SelectedCard != null)
            {
                m_SelectedCard.SetSelected(false);
            }

            m_SelectedCard = card;

            if (m_SelectedCard != null)
            {
                m_SelectedCard.SetSelected(true);
                PlaySfx(selectSound);
            }

            OnCardSelected?.Invoke(m_SelectedCard);
        }

        public void OnSlotClicked(CardSlot slot)
        {
            if (m_SelectedCard != null && slot != null && !slot.IsOccupied)
            {
                PlayCardToSlot(m_SelectedCard, slot);
            }
        }

        public void PlayCardToSlot(Card3DView card, CardSlot slot)
        {
            if (card == null || slot == null) return;

            slot.PlaceCard(card);
            card.SetSelected(false);
            m_SelectedCard = null;

            PlaySfx(playSound);
            OnCardPlayed?.Invoke(card, slot);
        }

        public void NextTurn()
        {
            int playerCount = playerStations.Count > 0 ? playerStations.Count : 4;
            m_ActivePlayerIndex = (m_ActivePlayerIndex + 1) % playerCount;
            if (m_ActivePlayerIndex == 0)
            {
                m_CurrentTurn++;
            }

            OnTurnChanged?.Invoke(m_CurrentTurn);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
