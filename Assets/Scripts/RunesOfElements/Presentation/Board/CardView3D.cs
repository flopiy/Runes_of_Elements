using UnityEngine;

namespace RunesOfElements.Presentation
{
    public sealed class CardView3D : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Layout")]
        [SerializeField] private float _maxWidth = 2.5f;
        [SerializeField] private float _maxHeight = 3.1f;
        [SerializeField] private float _tiltDegrees = 40f;
        [SerializeField] private float _frameThickness = 0.12f;
        [Header("Animation")]
        [Tooltip("Повна тривалість анімації атаки (замах + випад + повернення).")]
        [SerializeField] private float _attackDuration = 0.75f;
        [Tooltip("Частка відстані до цілі, яку карта пролітає під час випаду.")]
        [SerializeField, Range(0.1f, 0.9f)] private float _attackReach = 0.55f;
        [SerializeField] private float _attackMaxDistance = 4f;
        [SerializeField] private float _attackLift = 0.6f;
        [SerializeField] private float _idleAmplitude = 0.035f;
        [Header("Raise / Selection")]
        [SerializeField] private float _raiseHeight = 0.45f;
        [SerializeField] private float _raiseScale = 1.08f;
        [SerializeField] private float _raiseSpeed = 6f;

        private Transform _pivot, _healthRoot;
        private Renderer _art, _frame, _glow, _healthFill;
        private TextMesh _healthText, _feedback;
        private Camera _camera;
        private Font _font;
        private MaterialPropertyBlock _mpb;
        private Color _frameColor = Color.white;
        private Color _glowColor = Color.clear;
        private float _flash, _scaleTarget, _scale, _bob, _attackTime, _feedbackTime;
        private bool _frozen, _dimmed;
        private float _raise, _raiseTarget;
        private Vector3 _attackOffset, _healthAnchor;
        private float _healthRatio = 1f;
        private int _hp = -1, _maxHp = -1;

        public bool IsShown => _scaleTarget > 0f;
        public Vector3 EffectPosition => _art != null ? _art.transform.position : transform.position;

        public void Build(Material artMat, Material frameMat, Material glowMat)
        {
            _camera = Camera.main;
            _mpb = new MaterialPropertyBlock();
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.localRotation = Quaternion.Euler(_tiltDegrees, 0f, 0f);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            Destroy(frame.GetComponent<Collider>());
            frame.transform.SetParent(_pivot, false);
            _frame = frame.GetComponent<Renderer>();
            _frame.sharedMaterial = frameMat;

            var art = GameObject.CreatePrimitive(PrimitiveType.Quad);
            art.name = "Art";
            art.transform.SetParent(_pivot, false);
            _art = art.GetComponent<Renderer>();
            _art.sharedMaterial = artMat;
            _art.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "Glow";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(transform, false);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            glow.transform.localScale = new Vector3(3.1f, 3.3f, 1f);
            _glow = glow.GetComponent<Renderer>();
            _glow.sharedMaterial = glowMat;
            _glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glow.enabled = false;

            _healthRoot = new GameObject("Health").transform;
            _healthRoot.SetParent(transform, false);
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "HealthBackground";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(_healthRoot, false);
            back.transform.localScale = new Vector3(2.5f, 0.6f, 1f);
            var backRenderer = back.GetComponent<Renderer>();
            backRenderer.sharedMaterial = frameMat;
            _mpb.SetColor(BaseColorId, new Color(0.015f, 0.025f, 0.045f));
            backRenderer.SetPropertyBlock(_mpb);
            _mpb.Clear();

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "HealthFill";
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(_healthRoot, false);
            _healthFill = fill.GetComponent<Renderer>();
            _healthFill.sharedMaterial = frameMat;
            _font = Font.CreateDynamicFontFromOSFont("Arial", 64);
            _healthText = CreateText("HealthValue", _healthRoot, 0.045f);
            _healthText.transform.localPosition = new Vector3(0f, 0.09f, -0.03f);
            _feedback = CreateText("DamageNumber", _healthRoot, 0.065f);
            _feedback.gameObject.SetActive(false);
            _scale = 0f;
            transform.localScale = Vector3.zero;
        }

        private TextMesh CreateText(string name, Transform parent, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMesh>();
            text.font = _font;
            text.fontSize = 64;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        public void SetArt(Sprite sprite, Color frameColor)
        {
            _frameColor = frameColor;
            float w = _maxWidth, h = _maxHeight;
            _art.GetPropertyBlock(_mpb);
            if (sprite != null)
            {
                var tex = sprite.texture;
                var r = sprite.textureRect;
                float aspect = r.width / Mathf.Max(1f, r.height);
                if (aspect > _maxWidth / _maxHeight) h = _maxWidth / aspect; else w = _maxHeight * aspect;
                _mpb.SetTexture(BaseMapId, tex);
                _mpb.SetVector(BaseMapStId, new Vector4(r.width / tex.width, r.height / tex.height, r.x / tex.width, r.y / tex.height));
            }
            else
            {
                _mpb.SetTexture(BaseMapId, Texture2D.whiteTexture);
                _mpb.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
            }
            _art.SetPropertyBlock(_mpb);
            _art.transform.localScale = new Vector3(w, h, 1f);
            _art.transform.localPosition = new Vector3(0f, h * 0.5f + 0.15f, -0.05f);
            _frame.transform.localScale = new Vector3(w + _frameThickness * 2f, h + _frameThickness * 2f, 0.05f);
            _frame.transform.localPosition = new Vector3(0f, h * 0.5f + 0.15f, 0.03f);
            _healthAnchor = _pivot.localRotation * new Vector3(0f, h + 0.6f, -0.1f);
            ApplyColors();
        }

        public void SetHealth(int hp, int maxHp)
        {
            hp = Mathf.Max(0, hp);
            maxHp = Mathf.Max(1, maxHp);
            if (_hp == hp && _maxHp == maxHp) return;
            _hp = hp;
            _maxHp = maxHp;
            _healthText.text = $"HP {hp}/{maxHp}";
            _healthRatio = Mathf.Clamp01((float)hp / maxHp);
            _healthFill.transform.localScale = new Vector3(2.3f * _healthRatio, 0.11f, 1f);
            _healthFill.transform.localPosition = new Vector3(-1.15f * (1f - _healthRatio), -0.2f, -0.02f);
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, Color.Lerp(new Color(1f, 0.16f, 0.1f), new Color(0.15f, 0.95f, 0.4f), _healthRatio));
            _healthFill.SetPropertyBlock(_mpb);
        }

        public void Show() => _scaleTarget = 1f;
        public void Hide() { _scaleTarget = 0f; SetGlow(Color.clear); }
        public void Flash() { _flash = 1f; ApplyColors(); }
        public void SetFrozen(bool frozen) { _frozen = frozen; ApplyColors(); }
        public void SetDimmed(bool dimmed) { _dimmed = dimmed; ApplyColors(); }

        public bool IsRaised => _raiseTarget > 0f;
        public void SetRaised(bool raised) => _raiseTarget = raised ? 1f : 0f;

        public void PlayAttack(Vector3 target)
        {
            _attackTime = _attackDuration;
            var delta = target - EffectPosition;
            float dist = Mathf.Min(delta.magnitude * _attackReach, _attackMaxDistance);
            var worldOffset = delta.sqrMagnitude > 0.0001f ? delta.normalized * dist : Vector3.zero;
            float s = Mathf.Max(0.001f, transform.lossyScale.x);
            _attackOffset = transform.InverseTransformDirection(worldOffset) / s;
        }

        /// <summary>Зміщення карти під час атаки: замах (підйом + відхід) → випад до цілі → повернення.</summary>
        private Vector3 AttackOffset(out float punch)
        {
            punch = 0f;
            if (_attackTime <= 0f || _attackDuration <= 0f) return Vector3.zero;
            float t = 1f - _attackTime / _attackDuration;
            const float windup = 0.3f, strike = 0.5f;
            float lift, reach;
            if (t < windup)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / windup);
                lift = k;
                reach = -0.25f * k;
            }
            else if (t < strike)
            {
                float k = (t - windup) / (strike - windup);
                k = k * k; // прискорення під час удару
                lift = Mathf.Lerp(1f, 0.4f, k);
                reach = Mathf.Lerp(-0.25f, 1f, k);
                punch = k;
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - strike) / (1f - strike));
                lift = Mathf.Lerp(0.4f, 0f, k);
                reach = Mathf.Lerp(1f, 0f, k);
                punch = 1f - k;
            }
            return _attackOffset * reach + Vector3.up * (_attackLift * lift);
        }

        public void PlayFeedback(int value, bool healing)
        {
            if (!healing) Flash();
            _feedbackTime = 0.9f;
            _feedback.text = healing ? $"+{value}" : $"-{value}";
            _feedback.color = healing ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.4f, 0.25f);
            _feedback.gameObject.SetActive(true);
        }

        public void SetGlow(Color color)
        {
            _glowColor = color;
            _glow.enabled = color.a > 0.01f && IsShown;
            if (!_glow.enabled) return;
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, color);
            _glow.SetPropertyBlock(_mpb);
        }

        private void ApplyColors()
        {
            if (_frame == null) return;
            var fc = _frameColor;
            if (_frozen) fc = Color.Lerp(fc, new Color(0.6f, 0.9f, 1f), 0.7f);
            if (_flash > 0f) fc = Color.Lerp(fc, new Color(1f, 0.15f, 0.1f), _flash);
            if (_dimmed) fc *= 0.35f;
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, fc);
            _frame.SetPropertyBlock(_mpb);
            var ac = _frozen ? new Color(0.7f, 0.9f, 1f) : Color.white;
            if (_flash > 0f) ac = Color.Lerp(ac, new Color(1f, 0.35f, 0.3f), _flash);
            if (_dimmed) ac = new Color(0.25f, 0.25f, 0.25f);
            _art.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, ac);
            _art.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            if (_pivot == null) return;
            float dt = Time.deltaTime;
            _scale = Mathf.MoveTowards(_scale, _scaleTarget, dt * 3f);
            _raise = Mathf.MoveTowards(_raise, _raiseTarget, dt * _raiseSpeed);
            float raiseK = Mathf.SmoothStep(0f, 1f, _raise);
            var attack = AttackOffset(out float punch);
            float s = Mathf.SmoothStep(0f, 1f, _scale) * Mathf.Lerp(1f, _raiseScale, Mathf.Max(raiseK, punch * 0.6f));
            transform.localScale = Vector3.one * s;
            _bob += dt * 3f;
            var offset = Vector3.up * (_frozen || _dimmed ? 0f : Mathf.Sin(_bob + transform.GetSiblingIndex()) * _idleAmplitude);
            offset += Vector3.up * (_raiseHeight * raiseK);
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt * 2.5f);
                ApplyColors();
                offset.x += Mathf.Sin(Time.time * 70f) * _flash * 0.16f;
            }
            if (_attackTime > 0f)
            {
                _attackTime = Mathf.Max(0f, _attackTime - dt);
                offset += attack;
            }
            _pivot.localPosition = offset;
            _healthRoot.localPosition = _healthAnchor + offset;
            if (_camera != null) _healthRoot.rotation = _camera.transform.rotation;
            if (_feedbackTime > 0f)
            {
                _feedbackTime = Mathf.Max(0f, _feedbackTime - dt);
                _feedback.transform.localPosition = new Vector3(0f, 0.5f + (0.9f - _feedbackTime) * 0.8f, -0.06f);
                if (_feedbackTime == 0f) _feedback.gameObject.SetActive(false);
            }
            if (_glow != null && _glow.enabled)
            {
                var c = _glowColor;
                c.a *= 0.75f + 0.25f * Mathf.Sin(_bob);
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, c);
                _glow.SetPropertyBlock(_mpb);
            }
        }

        private void OnDestroy()
        {
            if (_font != null) Destroy(_font);
        }
    }
}
