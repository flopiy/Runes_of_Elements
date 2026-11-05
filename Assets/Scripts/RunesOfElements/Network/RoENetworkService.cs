using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace RunesOfElements.Network
{
    /// <summary>
    /// Сервіс керування підключенням: автентифікація в Unity Services,
    /// створення сесії Relay або локальний запуск Netcode при відсутності інтернету.
    /// </summary>
    public static class RoENetworkService
    {
        private static bool _isInitialized;
        private static ISession _currentSession;

        public static ISession CurrentSession => _currentSession;
        public static string CurrentRoomCode => _currentSession != null ? _currentSession.Code : "LOCAL";

        public static async Task<bool> EnsureInitializedAsync()
        {
            if (_isInitialized) return true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoENetworkService] Unity Services не підключено: {ex.Message}. Використовується локальний транспорт.");
                return false;
            }
        }

        public static async Task<string> HostSessionAsync(int maxPlayers = 4)
        {
            bool ugsOk = await EnsureInitializedAsync();
            if (ugsOk && MultiplayerService.Instance != null)
            {
                try
                {
                    var options = new SessionOptions
                    {
                        MaxPlayers = maxPlayers,
                        Name = "RunesOfElements_" + UnityEngine.Random.Range(1000, 9999)
                    }.WithRelayNetwork();

                    _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                    return _currentSession.Code;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RoENetworkService] Не вдалося створити Relay сесію ({ex.Message}), перемикання на локальний хост.");
                }
            }

            // Fallback: запуск NetworkManager безпосередньо як хост (127.0.0.1)
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsListening)
            {
                var utp = nm.GetComponent<UnityTransport>();
                if (utp != null) utp.SetConnectionData("127.0.0.1", 7777);
                nm.StartHost();
            }
            return "LOCAL";
        }

        public static async Task<bool> JoinSessionAsync(string roomCode)
        {
            bool ugsOk = await EnsureInitializedAsync();
            if (ugsOk && MultiplayerService.Instance != null && !string.Equals(roomCode, "LOCAL", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(roomCode);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RoENetworkService] Помилка входу за кодом {roomCode}: {ex.Message}");
                    return false;
                }
            }

            // Fallback: приєднання до локального хоста
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsListening)
            {
                var utp = nm.GetComponent<UnityTransport>();
                if (utp != null) utp.SetConnectionData("127.0.0.1", 7777);
                return nm.StartClient();
            }
            return false;
        }

        public static async Task LeaveSessionAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.LeaveAsync();
                }
                catch { }
                _currentSession = null;
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                nm.Shutdown();
            }
        }
    }
}
