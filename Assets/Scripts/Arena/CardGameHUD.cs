using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGame;

namespace CardGame
{
    public class CardGameHUD : MonoBehaviour
    {
        [Header("Canvas / Root")]
        [SerializeField] private Canvas canvas;

        [Header("Top Center Banner")]
        [SerializeField] private TextMeshProUGUI turnTitleText;
        [SerializeField] private TextMeshProUGUI turnSubtext;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button menuButton;

        [Header("Left Stats Panel")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI concentrationText;
        [SerializeField] private TextMeshProUGUI teamHealthText;
        [SerializeField] private TextMeshProUGUI fireRuneCountText;
        [SerializeField] private TextMeshProUGUI waterRuneCountText;
        [SerializeField] private TextMeshProUGUI earthRuneCountText;
        [SerializeField] private TextMeshProUGUI voidRuneCountText;

        [Header("Player Avatars Column (4 Players)")]
        [SerializeField] private List<Image> playerAvatarHighlights = new List<Image>();

        [Header("Right Side - Hero Details Panel")]
        [SerializeField] private GameObject heroPanel;
        [SerializeField] private Image heroPortrait;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private TextMeshProUGUI heroElementText;
        [SerializeField] private TextMeshProUGUI heroHealthText;
        [SerializeField] private TextMeshProUGUI heroConcentrationText;
        [SerializeField] private TextMeshProUGUI passiveTitleText;
        [SerializeField] private TextMeshProUGUI passiveDescText;

        [Header("Right Side - Spell Selection Panel")]
        [SerializeField] private GameObject spellPanel;
        [SerializeField] private TextMeshProUGUI spellCostText;
        [SerializeField] private Button applySpellButton;
        [SerializeField] private Button endTurnButton;

        private ArenaBoardManager m_BoardManager;

        private void Start()
        {
            m_BoardManager = ArenaBoardManager.Instance != null ? ArenaBoardManager.Instance : FindFirstObjectByType<ArenaBoardManager>();
            if (m_BoardManager != null)
            {
                m_BoardManager.OnCardSelected += HandleCardSelected;
                m_BoardManager.OnTurnChanged += HandleTurnChanged;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnClickEndTurn);
            }

            if (applySpellButton != null)
            {
                applySpellButton.onClick.AddListener(OnClickApplySpell);
            }

            UpdateTurnDisplay();
        }

        private void OnDestroy()
        {
            if (m_BoardManager != null)
            {
                m_BoardManager.OnCardSelected -= HandleCardSelected;
                m_BoardManager.OnTurnChanged -= HandleTurnChanged;
            }
        }

        private void HandleCardSelected(Card3DView card)
        {
            if (card == null || card.CardData == null)
            {
                return;
            }

            var data = card.CardData;
            if (heroNameText != null) heroNameText.text = data.cardName;
            if (heroElementText != null) heroElementText.text = data.element.ToString();
            if (heroHealthText != null) heroHealthText.text = $"{data.health}/{data.health}";
            if (heroConcentrationText != null) heroConcentrationText.text = $"{data.manaCost}/5";
            if (heroPortrait != null && data.cardSprite != null) heroPortrait.sprite = data.cardSprite;

            if (passiveDescText != null)
            {
                passiveDescText.text = !string.IsNullOrEmpty(data.description) ? data.description : "Заклинання завдають додаткову магічну шкоду.";
            }
        }

        private void HandleTurnChanged(int turn)
        {
            UpdateTurnDisplay();
        }

        public void UpdateTurnDisplay()
        {
            if (m_BoardManager == null) m_BoardManager = ArenaBoardManager.Instance;
            if (m_BoardManager == null) return;

            int activeIdx = m_BoardManager.ActivePlayerIndex;
            string[] playerHeroNames = new string[] { "Маг Води", "Піромант", "Друїд Землі", "Жриця Пустоти" };
            string activeName = activeIdx < playerHeroNames.Length ? playerHeroNames[activeIdx] : $"Гравець {activeIdx + 1}";

            if (turnTitleText != null)
            {
                turnTitleText.text = "Ваш хід";
            }

            if (turnSubtext != null)
            {
                turnSubtext.text = $"Хід героя: {activeName}";
            }

            // Update avatar highlight indicator
            for (int i = 0; i < playerAvatarHighlights.Count; i++)
            {
                if (playerAvatarHighlights[i] != null)
                {
                    playerAvatarHighlights[i].enabled = (i == activeIdx);
                }
            }
        }

        public void OnClickEndTurn()
        {
            if (m_BoardManager != null)
            {
                m_BoardManager.NextTurn();
                UpdateTurnDisplay();
            }
        }

        public void OnClickApplySpell()
        {
            Debug.Log("[CardGameHUD] Spell Applied!");
            if (m_BoardManager != null)
            {
                // Trigger feedback
            }
        }
    }
}

