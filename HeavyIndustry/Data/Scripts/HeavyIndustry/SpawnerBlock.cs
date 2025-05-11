using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using Sandbox.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using System;

namespace bla
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Collector), false, "")]

    public class SpawnerBlock : MyGameLogicComponent
    {
        // CONFIGURABLE --------------------------------------------------
        int spawnAmount = 1;
        int spawnDistanceSquared = 400;
        int spawnDelayTicks = 1;

        // the float value is the relative weight ... more weight is more chance to be picked
        Dictionary<string, float> spawns = new Dictionary<string, float>()
        {
            { "MyObjectBuilder_Component/MechanicalParts", 0.1f },
            { "MyObjectBuilder_Component/StructuralParts", 0.1f },
        };

        // CONFIGURABLE --------------------------------------------------

        // Do not touch below

        IMyCubeBlock block;
        List<IMyPlayer> playerList = new List<IMyPlayer>();

        int tick = 0;


                                                                            
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyCubeBlock)Entity;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            // server only
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            // time delay
            if (tick < spawnDelayTicks)
            {
                tick++;
                //MyAPIGateway.Utilities.ShowNotification("NOT TIME YET", 1600);
                return;
            }

            playerList.Clear();

            MyAPIGateway.Players.GetPlayers(playerList);

            bool inRange = false;

            foreach(IMyPlayer player in playerList)
            {
                if (player == null || player.Character == null || player.Character.IsDead || player.Character.IsBot)
                    continue;

                if(Vector3D.DistanceSquared(player.Character.WorldMatrix.Translation, block.WorldMatrix.Translation) < spawnDistanceSquared)
                {
                    inRange = true;
                    break;
                }
            }

            if (inRange)
            {
                tick = 0;
                SpawnItem();
            }
            else
            {
                //MyAPIGateway.Utilities.ShowNotification("NOT IN RANGE", 2000);
            }
        }

        public static string PickRandom(Dictionary<string, float> weights)
        {
            float totalWeight = 0f;
            foreach (var weight in weights.Values)
                totalWeight += weight;

            float randomValue = (float)(new Random().NextDouble() * totalWeight);
            float cumulative = 0f;

            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (randomValue < cumulative)
                    return kvp.Key;
            }

            return null;
        }

        public void SpawnItem()
        {

            var item = PickRandom(spawns);
            var itemId = MyDefinitionId.Parse(item);

            if (itemId == null)
            {
                //MyAPIGateway.Utilities.ShowNotification("NO ITEM", 2000);
                MyLog.Default.WriteLineAndConsole($"ERROR: Item Id {itemId.ToString()} didnt work.");
                return;
            }
            MyVisualScriptLogicProvider.SpawnItem(itemId, block.WorldMatrix.Translation, "", spawnAmount);
        }
    }
}
