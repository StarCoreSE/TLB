using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.ModAPI;
using StructuralReinforcement;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.Example_NetworkProtobuf
{
    public class Networking
    {
        public readonly ushort ChannelId;
        private List<IMyPlayer> tempPlayers = new List<IMyPlayer>();
        public Networking(ushort channelId)
        {
            ChannelId = channelId;
        }
        public void Register()
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(ChannelId, ReceivedPacket);
        }
        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(ChannelId, ReceivedPacket);
            tempPlayers.Clear();
        }

        private void ReceivedPacket(byte[] rawData) // executed when a packet is received on this machine
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);

                HandlePacket(packet, rawData);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");
                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author]", 10000, MyFontEnum.Red);
            }
        }

        private void HandlePacket(PacketBase packet, byte[] rawData = null)
        {
            packet.Received();
        }

        public void SendToPlayer(PacketSettings packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, bytes, steamId);
        }
    }




    // tag numbers in ProtoInclude collide with numbers from ProtoMember in the same class, therefore they must be unique.
    [ProtoInclude(1000, typeof(PacketSettings))]

    [ProtoContract]
    public abstract class PacketBase
    {
        // this field's value will be sent if it's not the default value.
        // to define a default value you must use the [DefaultValue(...)] attribute.
        [ProtoMember(1)]
        public ulong SenderId;

        public PacketBase()
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
        }

        /// <summary>
        /// Called when this packet is received on this machine.
        /// </summary>
        /// <returns>Return true if you want the packet to be sent to other clients (only works server side)</returns>
        public abstract void Received();
    }
    [ProtoContract]
    public partial class PacketSettings : PacketBase
    {
        // tag numbers in this class won't collide with tag numbers from the base class
        [ProtoMember(2)]
        public ServerSettings sSettings;

        public PacketSettings() { } // Empty constructor required for deserialization

        public PacketSettings(ServerSettings ssettings)
        {
            sSettings = ssettings;
        }

        public override void Received()
        {
            if (!MyAPIGateway.Utilities.IsDedicated) //client crap
            {
                MyLog.Default.WriteLineAndConsole($"Structural Reinf: Received packet");
                try
                {
                    Session.reinfSubtypes.Clear();
                    foreach (var block in sSettings.reinfSubtypeIDs)
                    {
                        Session.reinfSubtypes.Add(MyStringHash.GetOrCompute(block));
                        MyLog.Default.WriteLineAndConsole($"Structural Reinf: Added '{block}' as reinf block");
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"Structural Reinf: Failed to process packet" + e);
                }
            }
            else//Client requested settings
            {
                MyLog.Default.WriteLineAndConsole($"Structural Reinf: Client requested settings");
            }
        }
    }
}