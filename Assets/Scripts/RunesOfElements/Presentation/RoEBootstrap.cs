using RunesOfElements.Core;
using RunesOfElements.Data;
using UnityEngine;

namespace RunesOfElements.Presentation
{
    public sealed class RoEBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabase _database;
        [SerializeField] private BoardPresenter _boardPresenter;
        [SerializeField] private GameHudController _hudController;

        private IGameSession _session;

        public IGameSession Session => _session;

        private void Start()
        {
            if (_database == null)
            {
                _database = Resources.Load<GameDatabase>("GameDatabase");
            }

            // 1. Отримуємо зовнішню сесію (якщо прийшли з мережевого лобі) або створюємо локальну
            if (GameSessionProvider.External != null)
            {
                _session = GameSessionProvider.External;
            }
            else
            {
                var setup = GameLaunchParams.Setup ?? GameLaunchParams.DefaultSolo();
                var content = _database != null ? _database.BuildContent() : DefaultContent.Create();
                bool isHotSeat = GameLaunchParams.Mode == LaunchMode.HotSeat;
                _session = new LocalGameSession(content, setup, isHotSeat);
            }

            // 2. Зв'язуємо презентери
            if (_boardPresenter != null) _boardPresenter.BindSession(_session);
            if (_hudController != null) _hudController.Bind(_session, _boardPresenter);
        }

        private void Update()
        {
            if (_session != null)
            {
                _session.Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_session != null && GameSessionProvider.External == null)
            {
                _session.Shutdown();
            }
        }
    }
}
