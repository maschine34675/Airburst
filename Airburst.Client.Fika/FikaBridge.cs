using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Airburst.Networking
{
    public static class FikaBridge
    {
        private static IFikaNetworkManager _manager;
        private static Action<string, float, float, Vector3> _onSolution;
        private static Action _onRaidReset;
        private static Action<string> _logInfo;
        private static Action<string> _logWarning;

        public static bool HasManager => _manager != null;

        public static bool IsServer => FikaBackendUtils.IsServer;

        public static void Subscribe(Action<string, float, float, Vector3> onSolution, Action onRaidReset,
            Action<string> logInfo, Action<string> logWarning)
        {
            _onSolution = onSolution;
            _onRaidReset = onRaidReset;
            _logInfo = logInfo;
            _logWarning = logWarning;
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnManagerCreated);
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(OnManagerDestroyed);
        }
        private static void OnManagerCreated(FikaNetworkManagerCreatedEvent e)
        {
            try
            {
                OnManagerCreatedCore(e);
            }
            catch (Exception ex)
            {
                _manager = null;
                _logWarning?.Invoke($"Airburst could not attach to the Fika network manager; this raid runs local-only. {ex}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnManagerCreatedCore(FikaNetworkManagerCreatedEvent e)
        {
            _onRaidReset?.Invoke();
            _manager = e.Manager;
            if (_manager == null)
            {
                return;
            }
            _manager.RegisterPacket<AirburstSolutionPacket>(OnSolutionPacket);
            _logInfo?.Invoke($"Fika network manager ready ({(IsServer ? "host" : "client")}); airburst solution packet registered.");
        }

        private static void OnManagerDestroyed(FikaNetworkManagerDestroyedEvent e)
        {
            try
            {
                OnManagerDestroyedCore(e);
            }
            catch (Exception ex)
            {
                _manager = null;
                _logWarning?.Invoke($"Airburst: Fika network manager teardown callback failed. {ex}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnManagerDestroyedCore(FikaNetworkManagerDestroyedEvent e)
        {
            if (e.Manager == null || ReferenceEquals(e.Manager, _manager))
            {
                _manager = null;
            }
            _onRaidReset?.Invoke();
        }

        private static void OnSolutionPacket(AirburstSolutionPacket packet)
        {
            _onSolution?.Invoke(packet.ShooterProfileId, packet.BurstDistance, packet.TargetHeight, packet.StartPosition);
        }

        public static void Send(string profileId, float distance, float targetHeight, Vector3 startPosition)
        {
            IFikaNetworkManager manager = _manager;
            if (manager == null)
            {
                return;
            }

            AirburstSolutionPacket packet = new AirburstSolutionPacket
            {
                ShooterProfileId = profileId,
                BurstDistance = distance,
                TargetHeight = targetHeight,
                StartPosition = startPosition,
            };
            manager.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
        }
    }
    public struct AirburstSolutionPacket : INetSerializable
    {
        public string ShooterProfileId;
        public float BurstDistance;
        public float TargetHeight;
        public Vector3 StartPosition;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ShooterProfileId ?? string.Empty);
            writer.Put(BurstDistance);
            writer.Put(TargetHeight);
            writer.Put(StartPosition.x);
            writer.Put(StartPosition.y);
            writer.Put(StartPosition.z);
        }

        public void Deserialize(NetDataReader reader)
        {
            ShooterProfileId = reader.GetString();
            BurstDistance = reader.GetFloat();
            TargetHeight = reader.GetFloat();
            StartPosition = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
    }
}
