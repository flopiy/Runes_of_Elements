using System;
using RunesOfElements.Core;

namespace RunesOfElements.Presentation
{
    /// <summary>Джерело стану гри для UI: локальний рушій або мережевий хост.</summary>
    public interface IGameSession
    {
        GameSnapshot Current { get; }
        event Action<GameSnapshot> Updated;
        event Action<string> Rejected;
        /// <summary>Чи може цей клієнт керувати місцем (магом) seat.</summary>
        bool CanControl(int seat);
        /// <summary>Чи може цей клієнт натискати «До бою» / «Далі» між битвами.</summary>
        bool CanControlFlow { get; }
        void Submit(IGameCommand cmd);
        /// <summary>Скільки секунд лишилось до передачі ходу боту (або -1).</summary>
        float TurnTimeRemaining { get; }
        string ModeLabel { get; }
        void Tick(float deltaTime);
        void Shutdown();
    }

    public enum LaunchMode { Solo, HotSeat, Host, Client }

    /// <summary>Параметри, що передаються з меню в ігрову сцену.</summary>
    public static class GameLaunchParams
    {
        public static LaunchMode Mode = LaunchMode.Solo;
        public static GameSetup Setup;
        public static string RoomCode;

        public static GameSetup DefaultSolo()
        {
            var s = GameSetup.Default(1);
            s.Seats[0].PlayerName = "Ви";
            return s;
        }
    }

    /// <summary>Мережевий шар реєструє тут свою сесію; ігрова сцена її підхоплює.</summary>
    public static class GameSessionProvider
    {
        public static IGameSession External;
        public static event Action<IGameSession> ExternalChanged;

        public static void SetExternal(IGameSession session)
        {
            External = session;
            ExternalChanged?.Invoke(session);
        }
    }
}
