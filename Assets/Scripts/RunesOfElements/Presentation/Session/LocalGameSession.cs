using System;
using RunesOfElements.Core;

namespace RunesOfElements.Presentation
{
    /// <summary>Соло (люди + боти) і hot-seat: усі людські місця керуються з цього комп'ютера.</summary>
    public sealed class LocalGameSession : IGameSession
    {
        private readonly HostDriver _driver;
        private readonly GameSetup _setup;
        private readonly bool _hotSeat;

        public event Action<GameSnapshot> Updated;
        public event Action<string> Rejected;

        public HostDriver Driver => _driver;
        public GameSnapshot Current => _driver.Snapshot;
        public bool CanControlFlow => true;
        public float TurnTimeRemaining => _hotSeat ? -1f : _driver.TurnTimeRemaining;
        public string ModeLabel => _hotSeat ? "Гаряче місце" : "Сольна гра";

        public LocalGameSession(GameContent content, GameSetup setup, bool hotSeat)
        {
            _setup = setup;
            _hotSeat = hotSeat;
            _driver = new HostDriver(content, setup, seat => seat >= 0 && seat < _setup.Seats.Count && !_setup.Seats[seat].IsBot);
            // Офлайн таймаут не потрібен для одного гравця — даємо подумати.
            _driver.TurnTimeout = float.MaxValue;
            _driver.Published += s => Updated?.Invoke(s);
            _driver.Rejected += r => Rejected?.Invoke(r);
        }

        public bool CanControl(int seat) => _driver.IsHuman(seat);

        public void Submit(IGameCommand cmd) => _driver.Submit(cmd, out _);

        public void Tick(float dt) => _driver.Tick(dt);

        public void Shutdown() { }
    }
}
