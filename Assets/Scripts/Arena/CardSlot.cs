using UnityEngine;
using CardGame;

namespace CardGame
{
    public enum SlotType
    {
        PlayerHero,      // Герої гравця
        PlayerLane,      // Поле бою гравця
        OpponentLane,    // Поле бою суперника
        OpponentHero,    // Бос / Герої суперника
        PlayerHand       // Рука гравця
    }

    public class CardSlot : MonoBehaviour
    {
        [Header("Slot Configuration")]
        [SerializeField] private SlotType slotType;
        [SerializeField] private int laneIndex; // 0, 1, 2, 3
        [SerializeField] private MeshRenderer slotGlowRenderer;
        [SerializeField] private Color normalColor = new Color(0.5f, 0.2f, 0.9f, 0.4f);
        [SerializeField] private Color highlightColor = new Color(0.9f, 0.4f, 1f, 0.9f);

        private Card3DView m_CurrentCard;
        private MaterialPropertyBlock m_PropBlock;

        public SlotType SlotType => slotType;
        public int LaneIndex => laneIndex;
        public Card3DView CurrentCard => m_CurrentCard;
        public bool IsOccupied => m_CurrentCard != null;

        private void Awake()
        {
            m_PropBlock = new MaterialPropertyBlock();
            SetHighlight(false);
        }

        public void PlaceCard(Card3DView card)
        {
            m_CurrentCard = card;
            if (card != null)
            {
                card.transform.SetParent(transform, true);
                card.SetBasePosition(new Vector3(0, 0.15f, 0), Quaternion.identity);
            }
        }

        public void ClearCard()
        {
            m_CurrentCard = null;
        }

        public void SetHighlight(bool highlighted)
        {
            if (slotGlowRenderer != null)
            {
                if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();
                slotGlowRenderer.GetPropertyBlock(m_PropBlock);
                Color c = highlighted ? highlightColor : normalColor;
                m_PropBlock.SetColor("_BaseColor", c);
                m_PropBlock.SetColor("_EmissionColor", c * 2f);
                slotGlowRenderer.SetPropertyBlock(m_PropBlock);
            }
        }

        private void OnMouseDown()
        {
            var board = FindFirstObjectByType<ArenaBoardManager>();
            if (board != null)
            {
                board.OnSlotClicked(this);
            }
        }
    }
}
