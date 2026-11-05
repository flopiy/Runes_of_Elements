using System;
using System.Collections.Generic;
using RunesOfElements.Core;
using RunesOfElements.Data;
using RunesOfElements.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace RunesOfElements.Network
{
    [Serializable]
    public struct SeatAssignment
    {
        public ulong ClientId;
        public bool IsAssigned;
    }

    /// <summary>
    /// Сервер-авторитетна мережева сесія: хост виконує RulesEngine і HostDriver,
    /// клієнти надсилають команди через RPC, хост транслює GameSnapshot.
    /// Працює як для хоста, так і для віддалених клієнтів через єдиний IGameSession.
    /// </summary>
    public sealed class NetworkGameSession : NetworkBehaviour, IGameSession
    {
        public static NetworkGameSession Instance { get; private set; }

        private HostDriver _driver;
        private GameSnapshot _currentSnapshot;
        private int _mySeat = -1;

        // Прив'язка місць до ClientId: індекс = номер місця (0..3)
        private readonly ulong[] _seatClientIds = new ulong[Rules.MageSeats];
        private readonly bool[] _seatIsHuman = new bool[Rules.MageSeats];

        public event Action<GameSnapshot> Updated;
        public event Action<string> Rejected;

        public GameSnapshot Current => _currentSnapshot;
        public bool CanControlFlow => IsServer;
        public string ModeLabel => IsServer ? "Онлайн (Хост)" : "Онлайн (Клієнт)";

        public float TurnTimeRemaining
        {
            get
            {
                if (IsServer && _driver != null) return _driver.TurnTimeRemaining;
                return -1f;
            }
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            GameSessionProvider.SetExternal(this);

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            if (GameSessionProvider.External == (IGameSession)this)
            {
                GameSessionProvider.SetExternal(null);
            }
        }

        /// <summary>Викликається хостом перед стартом гри для налаштування місць і створення RulesEngine.</summary>
        public void InitializeHost(GameContent content, GameSetup setup, Dictionary<int, ulong> humanAssignments)
        {
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                if (humanAssignments != null && humanAssignments.TryGetValue(i, out ulong cid))
                {
                    _seatClientIds[i] = cid;
                    _seatIsHuman[i] = true;
                    if (cid == NetworkManager.ServerClientId) _mySeat = i;
                }
                else
                {
                    _seatIsHuman[i] = false;
                }
            }

            _driver = new HostDriver(content, setup, seat => seat >= 0 && seat < Rules.MageSeats && _seatIsHuman[seat]);
            _driver.TurnTimeout = 60f; // Таймаут ходу 60 секунд за GDD §10
            _driver.Published += OnHostSnapshotPublished;
            _driver.Rejected += r => Rejected?.Invoke(r);

            _currentSnapshot = _driver.Snapshot;
            Updated?.Invoke(_currentSnapshot);

            // Сповіщаємо підключених клієнтів про їхні призначені місця
            foreach (var kvp in humanAssignments)
            {
                if (kvp.Value != NetworkManager.ServerClientId)
                {
                    AssignSeatClientRpc(kvp.Value, kvp.Key);
                }
            }
        }

        [Rpc(SendTo.NotServer)]
        private void AssignSeatClientRpc(ulong targetClientId, int seat)
        {
            if (NetworkManager.LocalClientId != targetClientId) return;
            _mySeat = seat;
        }

        private void OnHostSnapshotPublished(GameSnapshot snapshot)
        {
            _currentSnapshot = snapshot;
            Updated?.Invoke(_currentSnapshot);
            // Транслюємо всім клієнтам
            BroadcastSnapshotClientRpc(snapshot.ToJson());
        }

        [Rpc(SendTo.NotServer)]
        private void BroadcastSnapshotClientRpc(string json)
        {
            _currentSnapshot = GameSnapshot.FromJson(json);
            Updated?.Invoke(_currentSnapshot);
        }

        public bool CanControl(int seat)
        {
            if (IsServer)
            {
                return _mySeat == seat;
            }
            return _mySeat == seat;
        }

        public void Submit(IGameCommand cmd)
        {
            var netCmd = CommandCodec.Encode(cmd);
            string json = JsonUtility.ToJson(netCmd);

            if (IsServer)
            {
                ExecuteCommandOnHost(cmd, NetworkManager.ServerClientId);
            }
            else
            {
                SubmitCommandServerRpc(json);
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitCommandServerRpc(string netCommandJson, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            var netCmd = JsonUtility.FromJson<NetCommand>(netCommandJson);
            var cmd = CommandCodec.Decode(netCmd);
            if (cmd == null) return;

            // Перевірка прав: чи має право відправник керувати цим місцем?
            if (netCmd.IsSeatBound)
            {
                if (netCmd.Seat < 0 || netCmd.Seat >= Rules.MageSeats || _seatClientIds[netCmd.Seat] != senderId || !_seatIsHuman[netCmd.Seat])
                {
                    RejectCommandClientRpc(senderId, "Ви не можете керувати цим місцем!");
                    return;
                }
            }

            ExecuteCommandOnHost(cmd, senderId);
        }

        private void ExecuteCommandOnHost(IGameCommand cmd, ulong senderId)
        {
            if (_driver == null) return;
            if (!_driver.Submit(cmd, out string reason))
            {
                RejectCommandClientRpc(senderId, reason);
            }
        }

        [Rpc(SendTo.NotServer)]
        private void RejectCommandClientRpc(ulong targetClientId, string reason)
        {
            if (NetworkManager.LocalClientId != targetClientId) return;
            Rejected?.Invoke(reason);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // Якщо гравець від'єднався, його місце переходить під контроль бота за GDD §11
            for (int i = 0; i < Rules.MageSeats; i++)
            {
                if (_seatClientIds[i] == clientId && _seatIsHuman[i])
                {
                    _seatIsHuman[i] = false;
                    _driver?.Engine?.Log?.Info($"Гравець від'єднався. Місце {i + 1} переходить під контроль бота.");
                    _driver?.Publish();
                }
            }
        }

        public void Tick(float dt)
        {
            if (IsServer && _driver != null)
            {
                _driver.Tick(dt);
            }
        }

        public void Shutdown()
        {
            if (NetworkManager != null && NetworkManager.IsListening)
            {
                NetworkManager.Shutdown();
            }
        }
    }
}
