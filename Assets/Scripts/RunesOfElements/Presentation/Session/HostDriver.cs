using System;
using RunesOfElements.Core;

namespace RunesOfElements.Presentation
{
    /// <summary>
    /// Авторитетний «стіл»: тримає RulesEngine і RNG, ходить ботами з паузою (щоб гравці бачили дії)
    /// і передає хід боту після таймауту. Використовується і локальною, і мережевою (на хості) сесією.
    /// </summary>
    public sealed class HostDriver
    {
        public RulesEngine Engine { get; }
        public GameSnapshot Snapshot { get; private set; }
        public int Version { get; private set; }

        public float BotActionDelay = 0.75f;
        public float TurnTimeout = 60f;
        /// <summary>Пауза перед автоматичним «Далі», якщо між битвами немає людей, що мають вирішувати.</summary>
        public float AutoContinueDelay = 2.5f;

        public event Action<GameSnapshot> Published;
        public event Action<string> Rejected;

        private readonly Func<int, bool> _isHumanSeat;
        private float _botTimer;
        private float _turnTimer;
        private int _timerSeat = -2;
        private BattlePhase _timerPhase;

        public HostDriver(GameContent content, GameSetup setup, Func<int, bool> isHumanSeat)
        {
            _isHumanSeat = isHumanSeat;
            Engine = new RulesEngine(content, setup, new SeededRandom(setup.Seed));
            Engine.StartCampaign();
            Publish();
        }

        public bool IsHuman(int seat) => seat >= 0 && _isHumanSeat(seat) && Engine.State.Mages[seat] != null;

        public float TurnTimeRemaining
        {
            get
            {
                var s = Engine.State;
                if (s.Phase != BattlePhase.MagesTurn || !IsHuman(s.ActiveSeat)) return -1f;
                return Math.Max(0f, TurnTimeout - _turnTimer);
            }
        }

        public bool Submit(IGameCommand cmd, out string reason)
        {
            var r = Engine.Submit(cmd);
            reason = r.Reason;
            if (!r.Ok)
            {
                Rejected?.Invoke(r.Reason);
                return false;
            }
            _botTimer = 0f;
            Publish();
            return true;
        }

        public void Publish()
        {
            Version++;
            Snapshot = GameSnapshot.From(Engine, Version);
            Published?.Invoke(Snapshot);
        }

        public void Tick(float dt)
        {
            if (Engine.IsOver) return;
            var s = Engine.State;

            // таймер ходу людини
            if (s.ActiveSeat != _timerSeat || s.Phase != _timerPhase)
            {
                _timerSeat = s.ActiveSeat;
                _timerPhase = s.Phase;
                _turnTimer = 0f;
            }
            _turnTimer += dt;
            _botTimer += dt;
            if (_botTimer < BotActionDelay) return;

            IGameCommand cmd = null;
            switch (s.Phase)
            {
                case BattlePhase.Setup:
                    cmd = FirstBotCommand();
                    if (cmd == null && !AnyHuman()) cmd = new ConfirmSetupCommand();
                    break;

                case BattlePhase.WaveWon:
                case BattlePhase.BattleWon:
                    cmd = FirstBotCommand();
                    if (cmd == null && !AnyHuman() && _turnTimer >= AutoContinueDelay) cmd = new ContinueCampaignCommand();
                    break;

                case BattlePhase.MagesTurn:
                    if (s.ActiveSeat < 0) break;
                    if (!IsHuman(s.ActiveSeat))
                        cmd = BotPolicy.NextCommand(Engine, s.ActiveSeat);
                    else if (_turnTimer >= TurnTimeout)
                    {
                        Engine.Log.Info($"Час ходу вичерпано — за {s.ActiveMage.DisplayName} діє бот.");
                        cmd = BotPolicy.NextCommand(Engine, s.ActiveSeat);
                    }
                    break;
            }

            if (cmd == null) return;
            if (!Submit(cmd, out var reason))
            {
                // Бот не має зависати: якщо команда раптом невалідна — медитація / завершення ходу.
                if (s.Phase == BattlePhase.MagesTurn && s.ActiveSeat >= 0)
                    Submit(new EndTurnCommand(s.ActiveSeat), out _);
                UnityEngine.Debug.LogWarning($"[HostDriver] Бот подав невалідну команду: {reason}");
            }
        }

        private IGameCommand FirstBotCommand()
        {
            foreach (var m in Engine.State.AllMages)
            {
                if (IsHuman(m.Seat)) continue;
                var c = BotPolicy.NextCommand(Engine, m.Seat);
                if (c != null) return c;
            }
            return null;
        }

        private bool AnyHuman()
        {
            foreach (var m in Engine.State.AllMages) if (IsHuman(m.Seat)) return true;
            return false;
        }
    }
}
