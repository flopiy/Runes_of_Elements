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
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameDatabase _database;
        [SerializeField] private string _battleScene = "Assets/Arena/Dungeon/RunesOfElements_Dungeon.unity";
        private UIDocument _document;
        private VisualElement _root, _title, _lobby, _rules;
        private GameContent _content;
        private GameSetup _setup;
        private LaunchMode _mode;
        private int _bossIndex = -1;
        private readonly List<MonsterDef> _bosses = new List<MonsterDef>();
        private readonly Dictionary<Button, Action> _handlers = new Dictionary<Button, Action>();
        private UnityEngine.InputSystem.InputAction _backAction;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _content = _database != null ? _database.BuildContent() : DefaultContent.Create();
            foreach (var monster in _content.Monsters) if (monster.Level == MonsterLevel.Boss) _bosses.Add(monster);
            _setup = GameSetup.Default();
            for (int i = 0; i < Rules.MageSeats; i++) _setup.Seats[i].MageId = _content.Mages[i % _content.Mages.Count].Id;
            _backAction = new UnityEngine.InputSystem.InputAction("MenuBack", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/escape");
            _backAction.performed += OnBack;
        }
        private void OnEnable() { _backAction.Enable(); EnsureBound(); }
        private void OnDisable() { _backAction.Disable(); Unbind(); }
        private void OnDestroy() { _backAction.performed -= OnBack; _backAction.Dispose(); }
        private void Update() => EnsureBound();
        private void OnBack(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (_rules != null && !_rules.ClassListContains("menu-hidden")) CloseRules();
            else BackToTitle();
        }
        private void Unbind()
        {
            foreach (var pair in _handlers) pair.Key.clicked -= pair.Value;
            _handlers.Clear(); _root = null;
        }
        private void Hook(string name, Action handler)
        {
            var button = _root.Q<Button>(name);
            if (button == null) throw new InvalidOperationException("Menu button missing: " + name);
            _handlers.Add(button, handler); button.clicked += handler;
        }
        private void EnsureBound()
        {
            var root = _document.rootVisualElement;
            if (root == null || root.Q("menu-root") == null || (_root == root && _title != null && _title.panel != null)) return;
            Unbind(); _root = root;
            _title = root.Q("title-view"); _lobby = root.Q("lobby-view"); _rules = root.Q("rules-modal");
            Hook("btn-solo", OpenSolo); Hook("btn-hotseat", OpenHotSeat);
            Hook("btn-rules", OpenRules); Hook("btn-rules-close", CloseRules);
            Hook("btn-quit", Application.Quit); Hook("btn-lobby-back", BackToTitle); Hook("btn-lobby-start", StartExpedition);
            Hook("boss-select-btn", CycleBoss); Hook("overheat-toggle-btn", ToggleOverheat);
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                int seat = i; Hook("seat-" + i + "-mage", () => CycleMage(seat));
                if (i > 0) Hook("seat-" + i + "-toggle", () => ToggleSeat(seat));
            }
            root.Q<Button>("btn-create-online").SetEnabled(false);
            root.Q<Button>("btn-join-online").SetEnabled(false);
            root.Q("join-code-input").SetEnabled(false);
            var holder = root.Q("rules-content");
            if (holder != null && holder.Q<ScrollView>() == null)
            {
                var text = holder.Q<Label>();
                if (text != null)
                {
                    text.RemoveFromHierarchy();
                    var scroll = new ScrollView(); scroll.AddToClassList("menu-rules-scroll");
                    holder.Add(scroll); scroll.Add(text);
                }
            }
            RefreshSetup();
        }
        public void OpenSolo() => OpenSetup(LaunchMode.Solo);
        public void OpenHotSeat() => OpenSetup(LaunchMode.HotSeat);
        private void OpenSetup(LaunchMode mode)
        {
            if (_mode != mode)
                for (int i = 0; i < Rules.MageSeats; i++) _setup.Seats[i].IsBot = i >= (mode == LaunchMode.HotSeat ? 2 : 1);
            _mode = mode;
            _title.AddToClassList("menu-hidden"); _lobby.RemoveFromClassList("menu-hidden"); RefreshSetup();
        }
        public void BackToTitle() { _lobby.AddToClassList("menu-hidden"); _rules.AddToClassList("menu-hidden"); _title.RemoveFromClassList("menu-hidden"); }
        public void OpenRules() => _rules.RemoveFromClassList("menu-hidden");
        public void CloseRules() => _rules.AddToClassList("menu-hidden");
        public void CycleMage(int seat)
        {
            int index = _content.Mages.FindIndex(m => m.Id == _setup.Seats[seat].MageId);
            string next = _content.Mages[(index + 1) % _content.Mages.Count].Id;
            for (int i = 0; i < Rules.MageSeats; i++) if (i != seat && _setup.Seats[i].MageId == next) _setup.Seats[i].MageId = _setup.Seats[seat].MageId;
            _setup.Seats[seat].MageId = next; RefreshSetup();
        }
        public void ToggleSeat(int seat)
        {
            if (_mode != LaunchMode.HotSeat || seat == 0) return;
            _setup.Seats[seat].IsBot = !_setup.Seats[seat].IsBot; RefreshSetup();
        }
        public void CycleBoss() { _bossIndex++; if (_bossIndex >= _bosses.Count) _bossIndex = -1; _setup.BossId = _bossIndex < 0 ? null : _bosses[_bossIndex].Id; RefreshSetup(); }
        public void ToggleOverheat() { _setup.ElementalOverheat = !_setup.ElementalOverheat; RefreshSetup(); }
        private void RefreshSetup()
        {
            if (_root == null) return;
            _root.Q<Label>("room-code-label").text = _mode == LaunchMode.HotSeat ? "Локальна гра • передавайте хід наступному гравцю" : "Сольна гра • Ви та три боти";
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                var seat = _setup.Seats[i];
                seat.PlayerName = seat.IsBot ? "Бот " + (i + 1) : (_mode == LaunchMode.Solo ? "Ви" : "Гравець " + (i + 1));
                _root.Q<Label>("seat-" + i + "-name").text = seat.PlayerName;
                _root.Q<Button>("seat-" + i + "-mage").text = _content.Mage(seat.MageId).Name;
                if (i > 0) { var b = _root.Q<Button>("seat-" + i + "-toggle"); b.text = seat.IsBot ? "Бот" : "Людина"; b.SetEnabled(_mode == LaunchMode.HotSeat); }
            }
            _root.Q<Button>("boss-select-btn").text = "Бос: " + (_bossIndex < 0 ? "Випадковий" : _bosses[_bossIndex].Name);
            _root.Q<Button>("overheat-toggle-btn").text = "Перегрів: " + (_setup.ElementalOverheat ? "Увімкнено" : "Вимкнено");
        }
        public void StartExpedition()
        {
            GameLaunchParams.Mode = _mode;
            GameLaunchParams.Setup = JsonUtility.FromJson<GameSetup>(JsonUtility.ToJson(_setup));
            GameLaunchParams.Setup.Seed = Environment.TickCount;
            GameSessionProvider.SetExternal(null);
            SceneManager.LoadScene(_battleScene);
        }
    }
}
