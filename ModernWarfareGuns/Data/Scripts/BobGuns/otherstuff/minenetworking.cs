using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.Utils;
using static ScriptedMissiles.ScriptedMissileSession;
using VRageMath;

namespace ScriptedMissiles
{
    public class HeartNetwork
    {
        public static HeartNetwork I;
        private ushort NetworkId;

        public void LoadData(ushort networkId)
        {
            I = this;
            NetworkId = networkId;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NetworkId, ReceivedPacket);
        }

        public void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NetworkId, ReceivedPacket);
            I = null;
        }

        private void ReceivedPacket(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(serialized);
                packet.Received(senderSteamId);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"ScriptedMissiles: Error in ReceivedPacket: {ex}");
            }
        }

        public void SendToEveryone(PacketBase packet)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            var serialized = MyAPIGateway.Utilities.SerializeToBinary(packet);
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var player in players)
            {
                if (player.SteamUserId != MyAPIGateway.Multiplayer.ServerId)
                    MyAPIGateway.Multiplayer.SendMessageTo(NetworkId, serialized, player.SteamUserId);
            }
        }

        public void SendToServer(PacketBase packet)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                return;

            var serialized = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(NetworkId, serialized);
        }
    }

    [ProtoContract]
    [ProtoInclude(100, typeof(MineSyncPacket))]
    [ProtoInclude(101, typeof(ExplosionRequestPacket))]
    public abstract class PacketBase
    {
        public abstract void Received(ulong SenderSteamId);
    }

    [ProtoContract]
    internal class MineSyncPacket : PacketBase
    {
        [ProtoMember(1)] public long EntityId;
        [ProtoMember(2)] public Vector3D Position;
        [ProtoMember(3)] public Quaternion Orientation;
        //[ProtoMember(4)] public float Damage;
        //[ProtoMember(5)] public float ExplosionRadius;
        //[ProtoMember(6)] public float DetectionRadius;
        [ProtoMember(7)] public bool IsRemoved;

        public MineSyncPacket() { }

        public MineSyncPacket(Mine mine, bool isRemoved = false)
        {
            EntityId = mine.EntityId;
            Position = mine.WorldMatrix.Translation;
            Orientation = Quaternion.CreateFromRotationMatrix(mine.WorldMatrix);
            //Damage = mine.damage;
            //ExplosionRadius = mine.explosion_radius;
            //DetectionRadius = mine.detection_radius;
            IsRemoved = isRemoved;
        }

        public override void Received(ulong SenderSteamId)
        {
            if (MyAPIGateway.Session.IsServer)
                return;

            if (IsRemoved)
            {
                ScriptedMissileSession.instance.RemoveClientMine(EntityId);
            }
            else
            {
                MatrixD worldMatrix = MatrixD.CreateFromQuaternion(Orientation);
                worldMatrix.Translation = Position;
                ScriptedMissileSession.instance.AddOrUpdateClientMine(EntityId, worldMatrix);
            }
        }
    }

    [ProtoContract]
    internal class ExplosionRequestPacket : PacketBase
    {
        [ProtoMember(1)] public long MineEntityId;

        public ExplosionRequestPacket() { }

        public ExplosionRequestPacket(long mineEntityId)
        {
            MineEntityId = mineEntityId;
        }

        public override void Received(ulong SenderSteamId)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Mine mine = ScriptedMissileSession.instance.mines.FirstOrDefault(m => m.EntityId == MineEntityId) as Mine;
            if (mine != null)
            {
                mine.Explode();
            }
        }
    }



}
