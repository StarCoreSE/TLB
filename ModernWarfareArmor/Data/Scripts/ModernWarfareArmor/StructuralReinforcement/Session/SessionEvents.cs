using Digi.Example_NetworkProtobuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace StructuralReinforcement
{
    public partial class Session : MySessionComponentBase
    {
        private void OnEntityCreate(MyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
                grid.AddedToScene += addToStart => _startGrids.Add(grid);
        }
        private void OnGridClose(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            GridComp comp;
            if (GridMap.TryRemove(grid, out comp))
            {
                GridList.Remove(comp);
                comp.Clean();
                _gridCompPool.Push(comp);
            }
        }
        private void PlayerConnected(long id)
        {
            var steamID = MyAPIGateway.Players.TryGetSteamId(id);
            MyLog.Default.WriteLineAndConsole($"SR Server: Sent settings to player " + steamID);
            Networking.SendToPlayer(new PacketSettings(ServerSettings.Instance), steamID);
        }
    }
}
