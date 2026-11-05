using System;
using System.Collections.Generic;
using RunesOfElements.Core;
using RunesOfElements.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RunesOfElements.Presentation
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [Header("Databases & Materials")]
        [SerializeField] private GameDatabase _database;
        [SerializeField] private Material _cardArtMaterial;
        [SerializeField] private Material _cardFrameMaterial;
        [SerializeField] private Material _cardGlowMaterial;

        [Header("Targeting Glow Colors")]
        [SerializeField] private Color _hoverColor = new Color(1f, 0.9f, 0.2f, 0.85f);
        [SerializeField] private Color _secondaryTargetColor = new Color(1f, 0.5f, 0.1f, 0.7f);
        [SerializeField] private Color _validTargetColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        [SerializeField] private Color _selectedTargetColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        [SerializeField] private Color _activeMageColor = new Color(1f, 0.85f, 0.35f, 0.6f);

        private int _selectedTargetPos = -1;

        /// <summary>Обрана гравцем ціль атаки (позиція монстра) або -1.</summary>
        public int SelectedTarget => _selectedTargetPos;

        public void SetSelectedTarget(int pos)
        {
            _selectedTargetPos = pos;
            RefreshGlows();
        }

        private readonly Transform[] _monsterSlotTransforms = new Transform[Rules.BoardPositions];
        private readonly Transform[] _mageSlotTransforms = new Transform[Rules.MageSeats];
        private readonly CardView3D[] _monsterCards = new CardView3D[Rules.BoardPositions];
        private readonly CardView3D[] _mageCards = new CardView3D[Rules.MageSeats];

        private IGameSession _session;
        private GameSnapshot _lastSnapshot;
        private RuneType? _targetingForm;
        private int _hoveredMonsterPos = -1;
        private Camera _mainCamera;
        private readonly Queue<EventSnap> _animationEvents = new Queue<EventSnap>();
        private int _lastEventSeq = -1;
        private float _nextAnimationTime;
        [SerializeField] private float _eventSpacing = 0.35f;

        [Header("Spell VFX (маги не рухаються — грають ефект)")]
        [Tooltip("Ефект на цілі для заклинань Вогню.")]
        [SerializeField] private GameObject _fireSpellVfx;
        [Tooltip("Ефект на цілі для заклинань Води.")]
        [SerializeField] private GameObject _waterSpellVfx;
        [Tooltip("Ефект на цілі для заклинань Льоду.")]
        [SerializeField] private GameObject _iceSpellVfx;
        [Tooltip("Ефект на цілі для заклинань Блискавки.")]
        [SerializeField] private GameObject _lightningSpellVfx;
        [Tooltip("Ефект на цілі, якщо для стихії не задано окремого.")]
        [SerializeField] private GameObject _defaultSpellVfx;
        [Tooltip("Ефект на картці мага в момент касту (необов'язково).")]
        [SerializeField] private GameObject _castVfx;
        [Tooltip("Скільки секунд ефект випромінює частинки (ефекти пакета зациклені).")]
        [SerializeField] private float _vfxEmitDuration = 1.2f;
        [Tooltip("Скільки секунд дати частинкам догоріти після зупинки.")]
        [SerializeField] private float _vfxFadeDuration = 1f;
        [SerializeField] private float _vfxScale = 1f;
        [SerializeField] private Vector3 _vfxOffset = new Vector3(0f, 0.3f, 0f);

        public event Action<int> MonsterSlotClicked;
        public event Action<int> MageSlotClicked;

        private void Awake()
        {
            _mainCamera = Camera.main;
            EnsureMaterials();
            FindSlots();
            CreateCardViews();
        }

        private void EnsureMaterials()
        {
            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
            if (_cardArtMaterial == null) _cardArtMaterial = new Material(unlitShader) { name = "RoE_CardArt_Runtime" };
            if (_cardFrameMaterial == null) _cardFrameMaterial = new Material(unlitShader) { name = "RoE_CardFrame_Runtime" };
            if (_cardGlowMaterial == null)
            {
                _cardGlowMaterial = new Material(unlitShader) { name = "RoE_CardGlow_Runtime" };
                _cardGlowMaterial.SetFloat("_Surface", 1); // Transparent
                _cardGlowMaterial.SetFloat("_Blend", 0);
                _cardGlowMaterial.renderQueue = 3000;
            }
        }

        private void FindSlots()
        {
            var battleSlots = GameObject.Find("BattleSlots");
            var root = battleSlots != null ? battleSlots.transform : transform;

            for (int i = 0; i < Rules.BoardPositions; i++)
            {
                string name = $"MonsterSlot_{i + 1:00}";
                var t = root.Find(name);
                if (t == null)
                {
                    var go = GameObject.Find(name);
                    if (go != null) t = go.transform;
                }
                _monsterSlotTransforms[i] = t;
            }

            for (int i = 0; i < Rules.MageSeats; i++)
            {
                string name = $"MageSlot_{i + 1:00}";
                var t = root.Find(name);
                if (t == null)
                {
                    var go = GameObject.Find(name);
                    if (go != null) t = go.transform;
                }
                _mageSlotTransforms[i] = t;
            }
        }

        private void CreateCardViews()
        {
            for (int i = 0; i < Rules.BoardPositions; i++)
            {
                var parent = _monsterSlotTransforms[i] != null ? _monsterSlotTransforms[i] : transform;
                var go = new GameObject($"Card_Monster_{i + 1:00}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var view = go.AddComponent<CardView3D>();
                view.Build(_cardArtMaterial, _cardFrameMaterial, _cardGlowMaterial);
                _monsterCards[i] = view;
            }

            for (int i = 0; i < Rules.MageSeats; i++)
            {
                var parent = _mageSlotTransforms[i] != null ? _mageSlotTransforms[i] : transform;
                var go = new GameObject($"Card_Mage_{i + 1:00}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var view = go.AddComponent<CardView3D>();
                view.Build(_cardArtMaterial, _cardFrameMaterial, _cardGlowMaterial);
                _mageCards[i] = view;
            }
        }

        public void BindSession(IGameSession session)
        {
            if (_session != null) _session.Updated -= HandleSnapshotUpdated;
            _session = session;
            if (_session != null)
            {
                _session.Updated += HandleSnapshotUpdated;
                if (_session.Current != null) HandleSnapshotUpdated(_session.Current);
            }
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Updated -= HandleSnapshotUpdated;
        }

        public void SetTargeting(RuneType? form)
        {
            _targetingForm = form;
            RefreshGlows();
        }

        private void HandleSnapshotUpdated(GameSnapshot s)
        {
            bool initial = _lastSnapshot == null || s.BattleIndex != _lastSnapshot.BattleIndex;
            if (initial) _animationEvents.Clear();
            foreach (var ev in s.Events)
            {
                bool mageAbilityOnMonster = ev.Type == GameEventType.BlessingTriggered &&
                    ev.SourceValid && ev.SourceIsMage && ev.TargetValid && !ev.TargetIsMage;
                if (!initial && ev.Seq > _lastEventSeq &&
                    (ev.Type == GameEventType.SpellCast || ev.Type == GameEventType.MonsterAttack ||
                     ev.Type == GameEventType.DamageDealt || ev.Type == GameEventType.Healed ||
                     ev.Type == GameEventType.MonsterDied || ev.Type == GameEventType.MageDied ||
                     mageAbilityOnMonster))
                    _animationEvents.Enqueue(ev);
                _lastEventSeq = Mathf.Max(_lastEventSeq, ev.Seq);
            }
            _lastSnapshot = s;
            UpdateUnits(s);
            RefreshGlows();
        }

        private CardView3D EventCard(bool valid, bool mage, int index)
        {
            if (!valid || index < 0) return null;
            var cards = mage ? _mageCards : _monsterCards;
            return index < cards.Length ? cards[index] : null;
        }

        private void PlayNextEvent()
        {
            if (_animationEvents.Count == 0 || Time.time < _nextAnimationTime) return;
            var ev = _animationEvents.Dequeue();
            var source = EventCard(ev.SourceValid, ev.SourceIsMage, ev.SourceIndex);
            var target = EventCard(ev.TargetValid, ev.TargetIsMage, ev.TargetIndex);
            if ((ev.Type == GameEventType.SpellCast || ev.Type == GameEventType.BlessingTriggered) && ev.SourceIsMage)
            {
                if (source != null) SpawnVfx(_castVfx, source.EffectPosition);
                if (target != null) SpawnVfx(SpellVfxFor(ev.Element), target.EffectPosition);
            }
            else if (ev.Type == GameEventType.SpellCast || ev.Type == GameEventType.MonsterAttack)
            {
                if (source != null && target != null) source.PlayAttack(target.EffectPosition);
            }
            else if (ev.Type == GameEventType.DamageDealt || ev.Type == GameEventType.Healed)
            {
                if (target != null) target.PlayFeedback(ev.Value, ev.Type == GameEventType.Healed);
            }
            else if (target != null)
            {
                target.SetHealth(0, 1);
                if (ev.Type == GameEventType.MonsterDied) target.Hide();
                else target.SetDimmed(true);
            }
            _nextAnimationTime = Time.time + _eventSpacing;
        }

        private GameObject SpellVfxFor(RuneType element)
        {
            GameObject prefab = null;
            switch (element)
            {
                case RuneType.Fire: prefab = _fireSpellVfx; break;
                case RuneType.Water: prefab = _waterSpellVfx; break;
                case RuneType.Ice: prefab = _iceSpellVfx; break;
                case RuneType.Lightning: prefab = _lightningSpellVfx; break;
            }
            return prefab != null ? prefab : _defaultSpellVfx;
        }

        private void SpawnVfx(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            var instance = Instantiate(prefab, position + _vfxOffset, prefab.transform.rotation);
            if (!Mathf.Approximately(_vfxScale, 1f))
            {
                instance.transform.localScale = prefab.transform.localScale * _vfxScale;
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            StartCoroutine(StopAndDestroyVfx(instance));
        }

        private System.Collections.IEnumerator StopAndDestroyVfx(GameObject instance)
        {
            yield return new WaitForSeconds(_vfxEmitDuration);
            if (instance == null) yield break;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(_vfxFadeDuration);
            if (instance != null) Destroy(instance);
        }

        private void UpdateUnits(GameSnapshot s)
        {
            // Монстри
            for (int i = 0; i < Rules.BoardPositions; i++)
            {
                var view = _monsterCards[i];
                var m = s.MonsterAt(i);
                if (m == null || !m.Exists || m.Hp <= 0)
                {
                    if (_animationEvents.Count == 0 && Time.time >= _nextAnimationTime && view.IsShown) view.Hide();
                    continue;
                }

                var def = _database != null ? _database.Monster(m.MonsterId) : null;
                var sprite = def != null ? def.Portrait : null;
                Color frameCol = LevelColor(m.Level);
                view.SetArt(sprite, frameCol);
                view.SetHealth(m.Hp, m.MaxHp);
                bool frozen = m.Statuses.Exists(st => st.Type == StatusType.Frozen);
                view.SetFrozen(frozen);
                view.Show();
            }

            // Маги
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                var view = _mageCards[i];
                var m = s.Mages[i];
                if (m == null || !m.Exists)
                {
                    if (view.IsShown) view.Hide();
                    continue;
                }

                var def = _database != null ? _database.Mage(m.MageId) : null;
                var sprite = def != null ? def.Portrait : null;
                Color frameCol = def != null ? def.Color : Color.white;
                view.SetArt(sprite, frameCol);
                view.SetHealth(m.Hp, m.MaxHp);
                view.SetDimmed(m.Eliminated);
                view.Show();
            }
        }

        private static Color LevelColor(MonsterLevel l)
        {
            switch (l)
            {
                case MonsterLevel.Level1: return new Color(0.7f, 0.7f, 0.7f);
                case MonsterLevel.Level2: return new Color(0.9f, 0.6f, 0.2f);
                case MonsterLevel.Level3: return new Color(0.85f, 0.25f, 0.9f);
                case MonsterLevel.Boss: return new Color(1f, 0.15f, 0.15f);
                default: return Color.white;
            }
        }

        private void Update()
        {
            bool hadEvents = _animationEvents.Count > 0;
            PlayNextEvent();
            if (hadEvents && _animationEvents.Count == 0 && _lastSnapshot != null)
                UpdateUnits(_lastSnapshot);
            if (_animationEvents.Count == 0 && _nextAnimationTime > 0f && Time.time >= _nextAnimationTime)
            {
                _nextAnimationTime = 0f;
                if (_lastSnapshot != null) UpdateUnits(_lastSnapshot);
            }
            HandleRaycast();
        }

        private void HandleRaycast()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            int newHover = -1;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // Перевіряємо клік / наведення на монстра
                for (int i = 0; i < Rules.BoardPositions; i++)
                {
                    var slotT = _monsterSlotTransforms[i];
                    if (slotT != null && (hit.transform == slotT || hit.transform.IsChildOf(slotT)))
                    {
                        newHover = i;
                        break;
                    }
                }

                // Перевіряємо клік на мага
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    for (int i = 0; i < Rules.MageSeats; i++)
                    {
                        var slotT = _mageSlotTransforms[i];
                        if (slotT != null && (hit.transform == slotT || hit.transform.IsChildOf(slotT)))
                        {
                            MageSlotClicked?.Invoke(i);
                            return;
                        }
                    }

                    if (newHover >= 0)
                    {
                        MonsterSlotClicked?.Invoke(newHover);
                        return;
                    }
                }
            }

            if (newHover != _hoveredMonsterPos)
            {
                _hoveredMonsterPos = newHover;
                RefreshGlows();
            }
        }

        private void RefreshGlows()
        {
            if (_lastSnapshot == null) return;

            // Скидаємо ціль, якщо монстра вже немає.
            if (_selectedTargetPos >= 0)
            {
                var sel = _lastSnapshot.MonsterAt(_selectedTargetPos);
                if (sel == null || !sel.Exists || sel.Hp <= 0) _selectedTargetPos = -1;
            }

            // Атакуюча карта (активний маг) піднята.
            bool magesTurn = _lastSnapshot.Phase == BattlePhase.MagesTurn;
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                var card = _mageCards[i];
                if (card == null) continue;
                var m = _lastSnapshot.Mages[i];
                bool active = magesTurn && i == _lastSnapshot.ActiveSeat && m != null && m.Exists && !m.Eliminated;
                card.SetRaised(active);
                card.SetGlow(active ? _activeMageColor : Color.clear);
            }

            // Пріоритет підсвітки: ціль під курсором (у прицілюванні) або обрана ціль.
            int focus = _hoveredMonsterPos >= 0 && _lastSnapshot.MonsterAt(_hoveredMonsterPos) != null
                ? _hoveredMonsterPos : _selectedTargetPos;

            for (int i = 0; i < Rules.BoardPositions; i++)
            {
                var card = _monsterCards[i];
                if (card == null) continue;
                if (!card.IsShown) { card.SetRaised(false); continue; }

                bool selected = i == _selectedTargetPos;
                card.SetRaised(selected || (i == _hoveredMonsterPos && magesTurn));

                if (!_targetingForm.HasValue)
                {
                    if (selected) card.SetGlow(_selectedTargetColor);
                    else card.SetGlow(i == _hoveredMonsterPos ? _hoverColor : Color.clear);
                    continue;
                }

                // У режимі прицілювання
                var form = _targetingForm.Value;
                if (focus >= 0)
                {
                    var targets = _lastSnapshot.PreviewTargets(form, focus);
                    if (i == focus) card.SetGlow(selected ? _selectedTargetColor : _hoverColor);
                    else if (targets.Contains(i)) card.SetGlow(_secondaryTargetColor);
                    else card.SetGlow(_validTargetColor);
                }
                else
                {
                    card.SetGlow(_validTargetColor);
                }
            }
        }
    }
}
