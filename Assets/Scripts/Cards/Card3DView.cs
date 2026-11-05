using UnityEngine;
using TMPro;

namespace CardGame
{
    [SelectionBase]
    public class Card3DView : MonoBehaviour
    {
        [Header("Card Data")]
        [SerializeField] private CardData cardData;

        [Header("Visual Components")]
        [SerializeField] private MeshRenderer frontRenderer;
        [SerializeField] private MeshRenderer backRenderer;
        [SerializeField] private MeshRenderer frameRenderer;
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro costText;
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private GameObject highlightGlow;

        [Header("Animation")]
        [SerializeField] private bool enableFloating = true;
        [SerializeField] private float floatSpeed = 2.5f;
        [SerializeField] private float floatAmplitude = 0.08f;
        [SerializeField] private float tiltSpeed = 1.5f;

        private Vector3 m_BaseLocalPosition;
        private Quaternion m_BaseLocalRotation;
        private bool m_IsSelected;
        private bool m_IsHovered;
        private MaterialPropertyBlock m_PropBlock;

        public CardData CardData => cardData;
        public bool IsSelected => m_IsSelected;

        private void Awake()
        {
            m_BaseLocalPosition = transform.localPosition;
            m_BaseLocalRotation = transform.localRotation;
            m_PropBlock = new MaterialPropertyBlock();
        }

        public void SetCardData(CardData data)
        {
            cardData = data;
            UpdateVisuals();
        }

        public void SetBasePosition(Vector3 pos, Quaternion rot)
        {
            m_BaseLocalPosition = pos;
            m_BaseLocalRotation = rot;
            transform.localPosition = pos;
            transform.localRotation = rot;
        }

        public void SetSelected(bool selected)
        {
            m_IsSelected = selected;
            if (highlightGlow != null)
            {
                highlightGlow.SetActive(m_IsSelected || m_IsHovered);
            }
        }

        public void SetHovered(bool hovered)
        {
            m_IsHovered = hovered;
            if (highlightGlow != null)
            {
                highlightGlow.SetActive(m_IsSelected || m_IsHovered);
            }
        }

        public void UpdateVisuals()
        {
            if (cardData == null) return;

            if (frontRenderer != null && cardData.cardSprite != null)
            {
                if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();
                frontRenderer.GetPropertyBlock(m_PropBlock);
                m_PropBlock.SetTexture("_BaseMap", cardData.cardSprite.texture);
                frontRenderer.SetPropertyBlock(m_PropBlock);
            }

            if (nameText != null) nameText.text = cardData.cardName;
            if (costText != null) costText.text = cardData.manaCost.ToString();
            
            if (attackText != null)
            {
                attackText.text = cardData.attack > 0 ? cardData.attack.ToString() : "";
            }

            if (healthText != null)
            {
                healthText.text = cardData.health > 0 ? cardData.health.ToString() : "";
            }

            if (frameRenderer != null)
            {
                Color frameCol = GetElementColor(cardData.element);
                if (m_PropBlock == null) m_PropBlock = new MaterialPropertyBlock();
                frameRenderer.GetPropertyBlock(m_PropBlock);
                m_PropBlock.SetColor("_BaseColor", frameCol);
                m_PropBlock.SetColor("_EmissionColor", frameCol * 1.5f);
                frameRenderer.SetPropertyBlock(m_PropBlock);
            }
        }

        private Color GetElementColor(CardElement element)
        {
            switch (element)
            {
                case CardElement.Fire: return new Color(1f, 0.35f, 0.1f);
                case CardElement.Frost: return new Color(0.2f, 0.6f, 1f);
                case CardElement.Nature: return new Color(0.2f, 0.9f, 0.35f);
                default: return new Color(0.75f, 0.3f, 1f);
            }
        }

        private void Update()
        {
            if (enableFloating)
            {
                float yOffset = Mathf.Sin(Time.time * floatSpeed + transform.GetHashCode() % 10) * floatAmplitude;
                float tiltAngle = Mathf.Sin(Time.time * tiltSpeed + transform.GetHashCode() % 5) * 1.5f;

                Vector3 targetPos = m_BaseLocalPosition + new Vector3(0, yOffset, 0);
                if (m_IsSelected)
                {
                    targetPos += new Vector3(0, 0.25f, -0.15f);
                }
                else if (m_IsHovered)
                {
                    targetPos += new Vector3(0, 0.15f, -0.08f);
                }

                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 8f);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, m_BaseLocalRotation * Quaternion.Euler(tiltAngle, 0, tiltAngle * 0.5f), Time.deltaTime * 6f);
            }
        }

        private void OnMouseEnter()
        {
            SetHovered(true);
        }

        private void OnMouseExit()
        {
            SetHovered(false);
        }

        private void OnMouseDown()
        {
            var boardManager = FindFirstObjectByType<ArenaBoardManager>();
            if (boardManager != null)
            {
                boardManager.SelectCard(this);
            }
        }
    }
}
