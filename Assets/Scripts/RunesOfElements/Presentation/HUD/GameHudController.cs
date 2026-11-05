using System;
using System.Collections.Generic;
using RunesOfElements.Core;
using RunesOfElements.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RunesOfElements.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameHudController : MonoBehaviour
    {
        [SerializeField] private BoardPresenter _boardPresenter;
        [SerializeField] private GameDatabase _database;

        private UIDocument _doc;
        private IGameSession _session;
        private GameSnapshot _snap;

        private const string HiddenClass = "hud-hidden";

        // UI Elements
        private VisualElement _boundRoot;
        private Label _towerTitle, _floorName, _turnSubtext, _poolTotal;
        private VisualElement _poolGrid;
        private Label _heroName, _heroTurn, _heroPassive, _heroHp, _heroConc;
        private Image _heroPortrait, _heroElement;
        private VisualElement _heroStatuses;
        private VisualElement _teamIcons;
        private Label _teamHp, _teamEffects;
        private VisualElement _blessingCard;
        private Label _blessingKind, _blessingName, _blessingDesc, _blessingUses;
        private Label _handHeader, _message;
        private float _messageUntil;
        private Label _dieD4, _dieD6, _dieD8;
        private Button[] _elSlots = new Button[2];
        private Button[] _formSlots = new Button[2];
        private VisualElement _handContainer, _spellsContainer, _spellsPopup;
        private Label _spellsEmpty;
        private Button _meditateBtn, _passBtn, _cryoBtn, _leaveBtn, _spellsBtn;

        // Overlays
        private VisualElement _decisionOverlay, _flowOverlay, _flowExtraActions;
        private Label _decisionText, _flowTitle, _flowBody;
        private Button _decideAcceptBtn, _decideRerollBtn, _decideMirrorBtn, _flowContinueBtn;

        // Selection & Targeting
        private int _selectedHandIndex = -1;
        private int _selectedElementSlot = -1;
        private int _selectedFormSlot = -1;
        private bool _isTargetingCryo = false;
        private int _setupSeat = -1;
        private int _lastEventSeq = -1;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_database == null) _database = Resources.Load<GameDatabase>("GameDatabase");
        }

        private void OnEnable() => EnsureUiBound();

        /// <summary>
        /// UIDocument може перебудувати дерево після Awake/OnEnable, тому елементи шукаються в актуальному
        /// rootVisualElement і перепідключаються, якщо дерево змінилось.
        /// </summary>
        private bool EnsureUiBound()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null || root.Q("hud-root") == null) return false;
            if (_boundRoot == root && _heroName != null && _heroName.panel != null) return true;
            _boundRoot = root;
            QueryElements(root);
            HookEvents();
            if (_snap != null) RenderAll();
            return true;
        }

        private void QueryElements(VisualElement root)
        {
            _towerTitle = root.Q<Label>("tower-title");
            _floorName = root.Q<Label>("floor-name");
            _turnSubtext = root.Q<Label>("turn-subtext");
            _poolGrid = root.Q<VisualElement>("pool-grid");
            _poolTotal = root.Q<Label>("pool-total");
            _leaveBtn = root.Q<Button>("leave-btn");

            _heroName = root.Q<Label>("hero-name");
            _heroTurn = root.Q<Label>("hero-turn");
            _heroPassive = root.Q<Label>("hero-passive");
            _blessingCard = root.Q<VisualElement>("blessing-card");
            _blessingKind = root.Q<Label>("blessing-kind");
            _blessingName = root.Q<Label>("blessing-name");
            _blessingDesc = root.Q<Label>("blessing-desc");
            _blessingUses = root.Q<Label>("blessing-uses");
            _message = root.Q<Label>("hud-message");
            _heroHp = root.Q<Label>("hero-hp");
            _heroConc = root.Q<Label>("hero-conc");
            _heroPortrait = root.Q<Image>("hero-portrait");
            _heroElement = root.Q<Image>("hero-element");
            _heroStatuses = root.Q<VisualElement>("hero-statuses");
            if (_heroPortrait != null) _heroPortrait.scaleMode = ScaleMode.ScaleToFit;

            _teamIcons = root.Q<VisualElement>("team-icons");
            _teamHp = root.Q<Label>("team-hp");
            _teamEffects = root.Q<Label>("team-effects");

            _handHeader = root.Q<Label>("hand-header");
            _dieD4 = root.Q<Label>("die-d4");
            _dieD6 = root.Q<Label>("die-d6");
            _dieD8 = root.Q<Label>("die-d8");

            _spellsBtn = root.Q<Button>("spells-btn");
            _spellsPopup = root.Q<VisualElement>("spells-popup");
            _spellsEmpty = root.Q<Label>("spells-empty");

            _elSlots[0] = root.Q<Button>("slot-el-0");
            _elSlots[1] = root.Q<Button>("slot-el-1");
            _formSlots[0] = root.Q<Button>("slot-form-0");
            _formSlots[1] = root.Q<Button>("slot-form-1");

            _handContainer = root.Q<VisualElement>("hand-container");
            _spellsContainer = root.Q<VisualElement>("spells-container");

            _meditateBtn = root.Q<Button>("meditate-btn");
            _passBtn = root.Q<Button>("pass-btn");
            _cryoBtn = root.Q<Button>("cryo-btn");

            _decisionOverlay = root.Q<VisualElement>("decision-overlay");
            _decisionText = root.Q<Label>("decision-text");
            _decideAcceptBtn = root.Q<Button>("decide-accept-btn");
            _decideRerollBtn = root.Q<Button>("decide-reroll-btn");
            _decideMirrorBtn = root.Q<Button>("decide-mirror-btn");

            _flowOverlay = root.Q<VisualElement>("flow-overlay");
            _flowTitle = root.Q<Label>("flow-title");
            _flowBody = root.Q<Label>("flow-body");
            _flowExtraActions = root.Q<VisualElement>("flow-extra-actions");
            _flowContinueBtn = root.Q<Button>("flow-continue-btn");
        }

        private void HookEvents()
        {
            _leaveBtn.clicked += OnLeaveClicked;
            _spellsBtn.clicked += () => _spellsPopup.ToggleInClassList(HiddenClass);

            for (int i = 0; i < 2; i++)
            {
                int slotIdx = i;
                _elSlots[i].clicked += () => OnSlotClicked(RuneCategory.Element, slotIdx);
                _formSlots[i].clicked += () => OnSlotClicked(RuneCategory.Form, slotIdx);
            }

            _meditateBtn.clicked += () =>
            {
                if (_snap != null && _session != null && _session.CanControl(_snap.ActiveSeat))
                    _session.Submit(new MeditateCommand(_snap.ActiveSeat));
            };

            _passBtn.clicked += () =>
            {
                if (_snap != null && _session != null && _session.CanControl(_snap.ActiveSeat))
                    _session.Submit(new EndTurnCommand(_snap.ActiveSeat));
            };

            _cryoBtn.clicked += () =>
            {
                _isTargetingCryo = !_isTargetingCryo;
                ClearSpellSelection();
                if (_boardPresenter != null) _boardPresenter.SetTargeting(_isTargetingCryo ? (RuneType?)RuneType.Beam : null);
            };

            _decideAcceptBtn.clicked += () => SubmitDecision(RollChoice.Accept);
            _decideRerollBtn.clicked += () => SubmitDecision(RollChoice.Reroll);
            _decideMirrorBtn.clicked += () => SubmitDecision(RollChoice.Mirror);

            _flowContinueBtn.clicked += OnFlowContinueClicked;
        }

        private void Start()
        {
            if (_session == null && _boardPresenter != null)
            {
                _boardPresenter.MonsterSlotClicked -= OnMonsterSlotClicked;
                _boardPresenter.MonsterSlotClicked += OnMonsterSlotClicked;
            }
        }

        public void Bind(IGameSession session, BoardPresenter presenter)
        {
            if (_boardPresenter != null) _boardPresenter.MonsterSlotClicked -= OnMonsterSlotClicked;
            _boardPresenter = presenter;
            if (_boardPresenter != null) _boardPresenter.MonsterSlotClicked += OnMonsterSlotClicked;

            if (_session != null)
            {
                _session.Updated -= HandleSnapshotUpdated;
                _session.Rejected -= HandleCommandRejected;
            }
            _session = session;
            if (_session != null)
            {
                _session.Updated += HandleSnapshotUpdated;
                _session.Rejected += HandleCommandRejected;
                if (_session.Current != null) HandleSnapshotUpdated(_session.Current);
            }
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.Updated -= HandleSnapshotUpdated;
                _session.Rejected -= HandleCommandRejected;
            }
            if (_boardPresenter != null) _boardPresenter.MonsterSlotClicked -= OnMonsterSlotClicked;
        }

        private void HandleCommandRejected(string reason)
        {
            ShowMessage(reason);
        }

        private void HandleSnapshotUpdated(GameSnapshot s)
        {
            if (_snap != null && _snap.ActiveSeat != s.ActiveSeat && _boardPresenter != null)
                _boardPresenter.SetSelectedTarget(-1);
            _snap = s;
            ClearTargetingStateIfInvalid();
            if (!EnsureUiBound()) return;
            RenderAll();
        }

        private void RenderAll()
        {
            DisplayedMage(); // визначає _setupSeat до малювання «Команди»
            RenderHeader();
            RenderRunePool();
            RenderTeam();
            RenderActiveHero();
            RenderDice();
            RenderOverlays();
        }

        private void Update()
        {
            if (!EnsureUiBound()) return;
            if (_message != null && _messageUntil > 0f && Time.unscaledTime > _messageUntil)
            {
                _message.text = string.Empty;
                _messageUntil = 0f;
            }
            if (_session != null && _snap != null && _snap.Phase == BattlePhase.MagesTurn)
            {
                float t = _session.TurnTimeRemaining;
                if (t >= 0f && t < 3600f && _turnSubtext != null)
                {
                    var m = _snap.ActiveMage;
                    string name = m != null ? m.Name : "";
                    _turnSubtext.text = $"Раунд {_snap.Round} · Хід: {name} ({Mathf.CeilToInt(t)} с)";
                }
            }
        }

        private void RenderHeader()
        {
            _towerTitle.text = $"Поверх {_snap.Floor + 1}/{_snap.FloorCount} · Хвиля {_snap.Wave + 1}/{_snap.WavesInFloor}";
            _floorName.text = string.IsNullOrEmpty(_snap.FloorName) ? "" : _snap.FloorName;
            var act = _snap.ActiveMage;
            string actName = act != null ? act.Name : "";
            _turnSubtext.text = $"Раунд {_snap.Round} · Хід: {actName}";
}

        private void RenderRunePool()
        {
            _poolGrid.Clear();
            var counts = _snap.RunePoolByType;
            foreach (RuneType r in Enum.GetValues(typeof(RuneType)))
            {
                int idx = (int)r;
                int count = counts != null && idx < counts.Length ? counts[idx] : 0;

                var tile = new VisualElement();
                tile.AddToClassList("hud-pool-tile");
                if (count == 0) tile.AddToClassList("hud-pool-tile--empty");
                tile.tooltip= Rules.RuneName(r);

                var icon = new Image { sprite = RuneIcon(r), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("hud-pool-icon");
                tile.Add(icon);

                var lbl = new Label(count.ToString());
                lbl.AddToClassList("hud-pool-count");
                tile.Add(lbl);

                _poolGrid.Add(tile);
            }
            _poolTotal.text = _snap.RunePoolCount.ToString();
        }

        private void RenderTeam()
        {
            _teamIcons.Clear();
            int hp = 0, maxHp = 0;
            var effects = new List<string>();
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                var m = _snap.Mages[i];
                if (m == null || !m.Exists) continue;
                hp += Mathf.Max(0, m.Hp);
                maxHp += m.MaxHp;
                foreach (var st in m.Statuses) effects.Add($"{m.Name}: {st.Label}");

                var member = new VisualElement();
                member.AddToClassList("hud-team-member");
                bool shown = _snap.Phase == BattlePhase.Setup ? i == _setupSeat : i == _snap.ActiveSeat;
                if (shown) member.AddToClassList("hud-team-member--active");
                if (_snap.Phase == BattlePhase.Setup && _session != null && _session.CanControl(i))
                {
                    int seatIdx = i;
                    member.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_snap == null || _snap.Phase != BattlePhase.Setup || _setupSeat == seatIdx) return;
                        _setupSeat = seatIdx;
                        _selectedHandIndex = -1;
                        RenderTeam();
                        RenderActiveHero();
                    });
                }
                if (m.Eliminated) member.AddToClassList("hud-team-member--eliminated");
                member.tooltip = $"{m.Name} ({m.PlayerName}) · Конц. {m.Concentration}/{Rules.MaxConcentration}";

                var frame = new VisualElement();
                frame.AddToClassList("hud-team-icon-frame");
                var icon= new Image { sprite = RuneIcon(m.Element), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("hud-team-icon");
                frame.Add(icon);
                member.Add(frame);

                var hpLbl = new Label(m.Eliminated ? "вибув" : $"♥ {m.Hp}/{m.MaxHp}");
                hpLbl.AddToClassList("hud-team-hp");
                member.Add(hpLbl);

                _teamIcons.Add(member);
            }

            _teamHp.text = $"♥ {hp} / {maxHp}";
            _teamEffects.text = effects.Count > 0 ? $"Ефектів: {effects.Count}" : "Ефектів немає";
            _teamEffects.tooltip = string.Join("\n", effects);
        }

        private void RenderDice()
        {
            int d4 = -1, d6 = -1, d8 = -1;
            for (int i = _snap.Events.Count - 1; i >= 0; i--)
            {
                var ev = _snap.Events[i];
                if (ev.Type != GameEventType.DiceRolled) continue;
                if (ev.Die == DieType.D4 && d4 < 0) d4 = ev.Value;
                else if (ev.Die == DieType.D6 && d6 < 0) d6 = ev.Value;
                else if (ev.Die == DieType.D8 && d8 < 0) d8 = ev.Value;
                if (d4 >= 0 && d6 >= 0 && d8 >= 0) break;
            }
            _dieD4.text = d4 >= 0 ? d4.ToString() : "–";
            _dieD6.text = d6 >= 0 ? d6.ToString() : "–";
            _dieD8.text = d8 >= 0 ? d8.ToString() : "–";
        }

        private void FillSlot(Button slot, RuneType? rune, string emptyText)
        {
            slot.Clear();
            slot.text = string.Empty;
            slot.tooltip = rune.HasValue ? Rules.RuneName(rune.Value) : emptyText;
            if (rune.HasValue)
            {
                var icon = new Image { sprite = RuneIcon(rune.Value), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hud-slot-icon");
                slot.Add(icon);
            }
            else slot.Add(new Label(emptyText) { pickingMode = PickingMode.Ignore });
        }

        private void RenderBlessing(MageSnap m)
        {
            bool has = m != null && !string.IsNullOrEmpty(m.BlessingName);
            _blessingCard.EnableInClassList("hud-blessing-card--empty", !has);
            _blessingCard.EnableInClassList("hud-blessing-card--curse", has && m.BlessingKind == BlessingKind.Curse);
            _blessingCard.EnableInClassList("hud-blessing-card--risk", has && m.BlessingKind == BlessingKind.Risk);
            if (!has)
            {
                _blessingKind.text = "Карта бафу";
                _blessingName.text = "Немає карти";
                _blessingDesc.text = string.Empty;
                _blessingUses.text = string.Empty;
                return;
            }

            switch (m.BlessingKind)
            {
                case BlessingKind.Curse: _blessingKind.text = "Прокляття"; break;
                case BlessingKind.Risk: _blessingKind.text = "Ризик"; break;
                default: _blessingKind.text = "Благословення"; break;
            }
            _blessingName.text = m.BlessingName;
            _blessingDesc.text = m.BlessingText;
            string uses = string.Empty;
            if (m.BlessingUsesPerBattle > 0) uses = $"За битву: {m.BlessingUsesBattle}/{m.BlessingUsesPerBattle}";
            if (m.BlessingUsesPerCampaign > 0) uses += (uses.Length > 0 ? " · " : "") + $"За кампанію: {m.BlessingUsesCampaign}/{m.BlessingUsesPerCampaign}";
            _blessingUses.text = uses;
        }

        private Sprite RuneIcon(RuneType r)
        {
            var d = _database != null ? _database.Rune(r) : null;
            return d != null ? d.Icon : null;
        }

        private void FillRuneCard(Button card, RuneType? rune, string emptyText)
        {
            card.Clear();
            card.text = string.Empty;
            card.EnableInClassList("hud-rune-card--empty", !rune.HasValue);
            if (rune.HasValue)
            {
                var icon = new Image { sprite = RuneIcon(rune.Value), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hud-rune-card-icon");
                card.Add(icon);
                card.Add(new Label(Rules.RuneName(rune.Value)) { pickingMode = PickingMode.Ignore });
            }
            else
            {
                card.Add(new Label(emptyText) { pickingMode = PickingMode.Ignore });
            }
        }

        /// <summary>
        /// Маг, якого показує панель героя. У фазі підготовки активного мага ще немає, тому показуємо
        /// мага, яким керує цей гравець (у hot-seat перемикається кліком по іконці в «Команді»).
        /// </summary>
        private MageSnap DisplayedMage()
        {
            if (_snap == null) return null;
            if (_snap.Phase != BattlePhase.Setup) return _snap.ActiveMage;
            if (IsEditableSetupSeat(_setupSeat)) return _snap.Mages[_setupSeat];
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                if (!IsEditableSetupSeat(i)) continue;
                _setupSeat = i;
                return _snap.Mages[i];
            }
            return null;
        }

        private bool IsEditableSetupSeat(int seat)
        {
            if (seat < 0 || seat >= Rules.MageSeats || _session == null) return false;
            var m = _snap.Mages[seat];
            return m != null && m.Exists && _session.CanControl(seat);
        }

        /// <summary>Чи можна зараз класти руни на картку цього мага.</summary>
        private bool CanEditRunes(MageSnap m)
        {
            if (m == null || _session == null || !_session.CanControl(m.Seat)) return false;
            if (_snap.Phase == BattlePhase.Setup) return true;
            return _snap.Phase == BattlePhase.MagesTurn && _snap.ActiveSeat == m.Seat && _snap.Step == TurnStep.SwapRune;
        }

        private void RenderActiveHero()
        {
            var m = DisplayedMage();
            bool canControl = m != null && _session != null && _session.CanControl(m.Seat);
            bool isMyTurn = canControl && _snap.Phase == BattlePhase.MagesTurn;
            bool isSetup = _snap.Phase == BattlePhase.Setup;
            bool canEditRunes = CanEditRunes(m);

            if (m == null || !m.Exists)
            {
                _heroName.text = "Очікування...";
                _heroTurn.text = "";
                _heroPassive.text = "";
                RenderBlessing(null);
                return;
            }

            var def = _database != null ? _database.Mage(m.MageId) : null;
            _heroPortrait.sprite = def != null ? def.Portrait : null;
            _heroElement.sprite = RuneIcon(m.Element);
            RenderBlessing(m);

            _heroName.text = m.Name;
            _heroTurn.text = isSetup ? (canControl ? "Підготовка: розкладіть руни" : $"Підготовка: {m.PlayerName}")
                : isMyTurn ? "Ваш хід" : $"Хід гравця {m.PlayerName}";
            _heroHp.text = $"{m.Hp}/{m.MaxHp}";
            _heroConc.text = $"{m.Concentration}/{Rules.MaxConcentration}";
            _heroPassive.text = $"{m.PassiveName}: {m.PassiveText}";

            _heroStatuses.Clear();
            if (m.Statuses.Count == 0)
            {
                var none = new Label("Немає");
                none.AddToClassList("hud-caption");
                _heroStatuses.Add(none);
            }
            foreach (var st in m.Statuses)
            {
                var tag = new Label(st.Rounds > 0 ? $"{st.Label} ({st.Rounds})" : st.Label);
                tag.AddToClassList("hud-tag");
                _heroStatuses.Add(tag);
            }

            // Комірки рун
            for (int i = 0; i < 2; i++)
            {
                FillSlot(_elSlots[i], m.Slot(RuneCategory.Element, i), "Стихія");
                FillSlot(_formSlots[i], m.Slot(RuneCategory.Form, i), "Форма");
            }

            // Рука
            _handHeader.text = isSetup
                ? $"Рука мага ({m.Hand.Count}/{m.HandLimit}) · підготовка: руни кладуться без обмежень"
                : $"Рука мага ({m.Hand.Count}/{m.HandLimit}) · заміни: {m.SwapsThisTurn}/{m.SwapLimit}";
            _handContainer.Clear();
            for (int i = 0; i < m.Hand.Count; i++)
            {
                int handIdx = i;
                var r = (RuneType)m.Hand[i];
                var chip = new Button();
                chip.AddToClassList("hud-rune-card");
                FillRuneCard(chip, r, "");
                if (handIdx == _selectedHandIndex) chip.AddToClassList("hud-rune-card--selected");

                chip.clicked += () =>
                {
                    if (!canEditRunes)
                    {
                        if (canControl && isMyTurn && _snap.Step != TurnStep.SwapRune) ShowMessage("Руну можна змінити лише до застосування заклинання.");
                        return;
                    }
                    if (!isSetup && m.SwapsThisTurn >= m.SwapLimit)
                    {
                        ShowMessage(m.SwapLimit == 0 ? "Цей маг не може змінювати руни." : "Цього ходу руну вже змінено (1 раз за хід).");
                        return;
                    }
                    _selectedHandIndex = _selectedHandIndex == handIdx ? -1 : handIdx;
                    if (_selectedHandIndex >= 0)
                        ShowMessage(Rules.CategoryOf(r) == RuneCategory.Element ? "Клікніть комірку «Стихія» на картці" : "Клікніть комірку «Форма» на картці");
                    RenderActiveHero();
                };
                _handContainer.Add(chip);
            }

            // Комбінації заклинань
            _spellsContainer.Clear();
            for (int es = 0; es < 2; es++)
            for (int fs = 0; fs < 2; fs++)
            {
                var el = m.Slot(RuneCategory.Element, es);
                var f = m.Slot(RuneCategory.Form, fs);
                if (!el.HasValue || !f.HasValue) continue;

                int slotEl = es, slotF = fs;
                int cost = m.SpellCosts[es * 2 + fs];
                bool enoughConc = cost >= 0 && cost <= m.Concentration;
                bool isSelected = _selectedElementSlot == es && _selectedFormSlot == fs;

                var btn = new Button();
                btn.text = $"{Rules.RuneName(el.Value)} + {Rules.RuneName(f.Value)} (Ціна: {cost})";
                btn.AddToClassList("roe-btn");
                btn.AddToClassList("hud-spell-btn");
                if (isSelected) btn.AddToClassList("roe-btn--primary");
                if (!enoughConc || !isMyTurn) btn.AddToClassList("roe-btn--disabled");

                btn.clicked += () =>
                {
                    if (!isMyTurn || !enoughConc) return;
                    _spellsPopup.AddToClassList(HiddenClass);
                    int preTarget = _boardPresenter != null ? _boardPresenter.SelectedTarget : -1;
                    if (preTarget >= 0)
                    {
                        ClearSpellSelection();
                        _boardPresenter.SetSelectedTarget(-1);
                        _session.Submit(new CastSpellCommand(m.Seat, slotEl, slotF, preTarget));
                        return;
                    }
                    SelectSpell(slotEl, slotF, f.Value);
                };
                _spellsContainer.Add(btn);
            }
            _spellsEmpty.EnableInClassList(HiddenClass, _spellsContainer.childCount > 0);
            bool spellSelected = _selectedElementSlot >= 0 && _selectedFormSlot >= 0;
            _spellsBtn.text = spellSelected ? "Оберіть ціль…" : "Заклинання";
            _spellsBtn.SetEnabled(isMyTurn && _snap.Step == TurnStep.SwapRune);
            if (!isMyTurn) _spellsPopup.AddToClassList(HiddenClass);

            // Кнопки дій
            _meditateBtn.SetEnabled(isMyTurn && _snap.Step == TurnStep.SwapRune);
            _passBtn.SetEnabled(isMyTurn && _snap.Step == TurnStep.SwapRune);

            // Кріомант
            if (m.HasCryo)
            {
                _cryoBtn.style.display = DisplayStyle.Flex;
                _cryoBtn.SetEnabled(isMyTurn && m.CryoAvailable && _snap.Step == TurnStep.SwapRune);
                _cryoBtn.text = m.CryoAvailable ? "Крижаний полон (1/битву)" : "Крижаний полон (Використано)";
            }
            else _cryoBtn.style.display = DisplayStyle.None;
            }

        private Color RuneColor(RuneType r) => _database != null ? _database.RuneColor(r) : Color.white;

        private void SelectSpell(int es, int fs, RuneType form)
        {
            if (_selectedElementSlot == es && _selectedFormSlot == fs)
            {
                ClearSpellSelection();
                return;
            }
            _selectedElementSlot = es;
            _selectedFormSlot = fs;
            _isTargetingCryo = false;
            if (_boardPresenter != null) _boardPresenter.SetTargeting(form);
            RenderActiveHero();
        }

        private void ClearSpellSelection()
        {
            _selectedElementSlot = -1;
            _selectedFormSlot = -1;
            if (_boardPresenter != null) _boardPresenter.SetTargeting(null);
        }

        private void ClearTargetingStateIfInvalid()
        {
            if (_snap.Phase != BattlePhase.MagesTurn || _snap.Step != TurnStep.SwapRune)
            {
                if (_snap.Phase != BattlePhase.Setup) _selectedHandIndex = -1;
                ClearSpellSelection();
                _isTargetingCryo = false;
                if (_boardPresenter != null) _boardPresenter.SetSelectedTarget(-1);
            }
        }

        private void OnSlotClicked(RuneCategory cat, int slotIdx)
        {
            if (_snap == null || _session == null) return;
            var m = DisplayedMage();
            if (!CanEditRunes(m)) return;
            if (_selectedHandIndex < 0 || _selectedHandIndex >= m.Hand.Count)
            {
                ShowMessage("Спершу оберіть руну в руці мага.");
                return;
            }

            var rune = (RuneType)m.Hand[_selectedHandIndex];
            if (Rules.CategoryOf(rune) != cat)
            {
                HandleCommandRejected(cat == RuneCategory.Element ? "Сюди кладуться лише Стихії!" : "Сюди кладуться лише Форми!");
                return;
            }

            _session.Submit(new SwapRuneCommand(m.Seat, _selectedHandIndex, cat, slotIdx));
            _selectedHandIndex = -1;
        }

        private void OnMonsterSlotClicked(int pos)
        {
            if (_snap == null || _session == null || !_session.CanControl(_snap.ActiveSeat)) return;
            int seat = _snap.ActiveSeat;

            if (_isTargetingCryo)
            {
                _isTargetingCryo = false;
                if (_boardPresenter != null) _boardPresenter.SetTargeting(null);
                _session.Submit(new UseActiveAbilityCommand(seat, "mage_cryo", pos));
                return;
            }

            if (_selectedElementSlot >= 0 && _selectedFormSlot >= 0)
            {
                int es = _selectedElementSlot, fs = _selectedFormSlot;
                ClearSpellSelection();
                if (_boardPresenter != null) _boardPresenter.SetSelectedTarget(-1);
                _session.Submit(new CastSpellCommand(seat, es, fs, pos));
                return;
            }

            // Без обраного заклинання — клік обирає ціль атаки (повторний клік знімає вибір).
            if (_boardPresenter == null || _snap.Phase != BattlePhase.MagesTurn || _snap.Step != TurnStep.SwapRune) return;
            var monster = _snap.MonsterAt(pos);
            if (monster == null || !monster.Exists || monster.Hp <= 0) return;
            bool deselect = _boardPresenter.SelectedTarget == pos;
            _boardPresenter.SetSelectedTarget(deselect ? -1 : pos);
            ShowMessage(deselect ? "Ціль знято" : $"Ціль: {monster.Name}. Оберіть заклинання для атаки.");
            if (!deselect && _spellsPopup != null) _spellsPopup.RemoveFromClassList(HiddenClass);
        }

        private void SubmitDecision(RollChoice choice)
        {
            if (_snap != null && _session != null && _snap.HasPending)
            {
                _session.Submit(new ResolveRollCommand(_snap.PendingSeat, choice));
            }
        }

        private void OnFlowContinueClicked()
        {
            if (_snap == null || _session == null) return;
            if (_snap.Phase == BattlePhase.Setup)
            {
                _session.Submit(new ConfirmSetupCommand());
            }
            else if (_snap.IsBetweenBattles)
            {
                _session.Submit(new ContinueCampaignCommand());
            }
            else if (_snap.IsOver)
            {
                SceneManager.LoadScene(0);
            }
        }

        private void OnLeaveClicked()
        {
            SceneManager.LoadScene(0);
        }

        private void RenderOverlays()
        {
            // Рішення щодо кидка
            if (_snap.HasPending)
            {
                _decisionOverlay.style.display = DisplayStyle.Flex;
                _decisionText.text = $"На кубику шкоди випало: {_snap.PendingRoll} (Стихія: {Rules.RuneName(_snap.PendingElement)}, Форма: {Rules.RuneName(_snap.PendingForm)})";
                _decideRerollBtn.style.display = _snap.PendingCanReroll ? DisplayStyle.Flex : DisplayStyle.None;
                _decideMirrorBtn.style.display = _snap.PendingCanMirror ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                _decisionOverlay.style.display = DisplayStyle.None;
            }

            // Фазові модальні вікна. У підготовці вікно — компактна плашка зверху, що не перекриває
            // руку й комірки картки, щоб гравець міг розкласти стартові руни.
            bool setup = _snap.Phase == BattlePhase.Setup;
            _flowOverlay.EnableInClassList("hud-flow--setup", setup);
            _flowOverlay.pickingMode = setup ? PickingMode.Ignore : PickingMode.Position;
            _flowExtraActions.Clear();
            switch (_snap.Phase)
            {
                case BattlePhase.Setup:
                    _flowOverlay.style.display = DisplayStyle.Flex;
                    _flowTitle.text = "ПІДГОТОВКА ДО БОЮ";
                    _flowBody.text = "Клікніть руну в «Руці мага», потім комірку «Стихія» або «Форма» на картці (праворуч). Коли готові — «До бою!».";
                    _flowContinueBtn.text = "До бою!";
                    _flowContinueBtn.style.display = _session.CanControlFlow ? DisplayStyle.Flex : DisplayStyle.None;
                    break;

                case BattlePhase.WaveWon:
                    _flowOverlay.style.display = DisplayStyle.Flex;
                    _flowTitle.text = $"ХВИЛЮ {_snap.Wave + 1}/{_snap.WavesInFloor} ВІДБИТО!";
                    _flowBody.text = "Живі маги отримують +3 HP перепочинку. Вибулі лишаються поза грою до кінця поверху.";
                    _flowContinueBtn.text = "Наступна хвиля";
                    _flowContinueBtn.style.display = _session.CanControlFlow ? DisplayStyle.Flex : DisplayStyle.None;
                    break;

                case BattlePhase.BattleWon:
                    _flowOverlay.style.display = DisplayStyle.Flex;
                    _flowTitle.text = $"ПОВЕРХ {_snap.Floor + 1} ПРОЙДЕНО!";
                    _flowBody.text = "Живі маги отримують випадкову руну та +3 HP. Вибулі маги відновились (3 HP).";
                    _flowContinueBtn.text = "Піднятися на наступний поверх";
                    _flowContinueBtn.style.display = _session.CanControlFlow ? DisplayStyle.Flex : DisplayStyle.None;
                    RenderBetweenBattlesExtraActions();
                    break;

                case BattlePhase.Defeat:
                    _flowOverlay.style.display = DisplayStyle.Flex;
                    _flowTitle.text = "ПОРАЗКА";
                    _flowBody.text = $"Усі маги полягли. Досягнуто поверх: {_snap.HighestFloor} з {_snap.FloorCount}.";
                    _flowContinueBtn.text = "В головне меню";
                    _flowContinueBtn.style.display = DisplayStyle.Flex;
                    break;

                case BattlePhase.CampaignWon:
                    _flowOverlay.style.display = DisplayStyle.Flex;
                    _flowTitle.text = "БАШТУ ПІДКОРЕНО!";
                    _flowBody.text = "Усі 10 поверхів пройдено! Боса розлому переможено. Перемога в кампанії!";
                    _flowContinueBtn.text = "В головне меню";
                    _flowContinueBtn.style.display = DisplayStyle.Flex;
                    break;

                default:
                    _flowOverlay.style.display = DisplayStyle.None;
                    break;
            }
        }

        private void RenderBetweenBattlesExtraActions()
        {
            // Скидання карти благословення (якщо поверх 3)
            if (_snap.BlessingDiscardOffered)
            {
                for (int i = 0; i < Rules.MageSeats; i++)
                {
                    int seat = i;
                    var m = _snap.Mages[i];
                    if (m != null && m.Exists && !string.IsNullOrEmpty(m.BlessingName) && _session.CanControl(seat))
                    {
                        var btn = new Button();
                        btn.text = $"Скинути карту «{m.BlessingName}» ({m.Name})";
                        btn.AddToClassList("roe-btn");
                        btn.clicked += () => _session.Submit(new DiscardBlessingCommand(seat));
                        _flowExtraActions.Add(btn);
                    }
                }
            }

            // Здача зайвих рун (якщо перевищено ліміт)
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                int seat = i;
                var m = _snap.Mages[i];
                if (m != null && m.Exists && m.Hand.Count > m.HandLimit && _session.CanControl(seat))
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.Add(new Label($"{m.Name}: здайте зайві руни ({m.Hand.Count}/{m.HandLimit}): "));
                    for (int h = 0; h < m.Hand.Count; h++)
                    {
                        int hIdx = h;
                        var r = (RuneType)m.Hand[h];
                        var btn = new Button();
                        btn.text = Rules.RuneName(r);
                        btn.AddToClassList("roe-rune-chip");
                        btn.clicked += () => _session.Submit(new DiscardRuneCommand(seat, hIdx));
                        row.Add(btn);
                    }
                    _flowExtraActions.Add(row);
                }
            }
        }

        private void ShowMessage(string text)
        {
            if (_message == null) return;
            _message.text = text ?? string.Empty;
            _messageUntil = Time.unscaledTime + 4f;
        }
    }
}
