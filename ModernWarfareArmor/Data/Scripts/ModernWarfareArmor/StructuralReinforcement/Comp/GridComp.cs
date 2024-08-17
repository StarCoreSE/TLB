using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
//Take 4
namespace StructuralReinforcement
{
    internal class GridComp
    {
        private Session _session;
        internal MyCubeGrid Grid;
        internal bool firstRun = true;
        internal bool armorDirty = false;
        internal Stopwatch timer = new Stopwatch();

        internal ConcurrentDictionary<IMySlimBlock, MyTuple<int, float>> BoostedArmor = new ConcurrentDictionary<IMySlimBlock, MyTuple<int, float>>();
        internal HashSet<IMySlimBlock> reinfBlocks = new HashSet<IMySlimBlock>();
        internal HashSet<IMySlimBlock> armor1List = new HashSet<IMySlimBlock>();
        internal HashSet<IMySlimBlock> armor1Upgrade = new HashSet<IMySlimBlock>();
        internal HashSet<IMySlimBlock> armor2List = new HashSet<IMySlimBlock>();
        internal HashSet<IMySlimBlock> armor2Upgrade = new HashSet<IMySlimBlock>();
        internal List<IMySlimBlock> tempArmor1 = new List<IMySlimBlock>();
        internal List<IMySlimBlock> neighbors = new List<IMySlimBlock>();
        internal Vector3I buffer = new Vector3I(1, 1, 1);
        internal HashSet<IMySlimBlock> drawReinf = new HashSet<IMySlimBlock>();
        internal ConcurrentDictionary<IMySlimBlock, int> RecentlyAdded = new ConcurrentDictionary<IMySlimBlock, int>();
        internal ConcurrentDictionary<IMySlimBlock, MyTuple<Color, int>> DrawList = new ConcurrentDictionary<IMySlimBlock, MyTuple<Color, int>>();

        internal void Init(MyCubeGrid grid, Session session)
        {
            _session = session;
            Grid = grid;

            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;
            foreach (IMySlimBlock block in grid.GetBlocks())
                BlockAdded(block);
        }

        internal void BlockAdded(IMySlimBlock block)
        {
            bool isReinf = Session.reinfSubtypes.Contains(block.BlockDefinition.Id.SubtypeId);
            bool isArmor = block.FatBlock == null;

            if (!(isReinf || isArmor))
                return;
            armorDirty = true;
            if (isReinf)
                reinfBlocks.Add(block);
            if (!firstRun) RecentlyAdded.TryAdd(block, Session.Tick);
        }

        internal void BlockRemoved(IMySlimBlock block)
        {
            bool isReinf = Session.reinfSubtypes.Contains(block.BlockDefinition.Id.SubtypeId);
            bool isArmor = block.FatBlock == null;

            if (!(isReinf || isArmor))
                return;

            if (isReinf)
            {
                reinfBlocks.Remove(block);
                armorDirty = true;
            }
            else if (BoostedArmor.ContainsKey(block))
            {
                if (BoostedArmor[block].Item1 <= 2)
                    armorDirty = true;
                BoostedArmor.Remove(block);
            }
        }

        internal void RecomputeArmor()
        {
            //timer.Restart();
            armorDirty = false;
            var oldBoosted = BoostedArmor;
            BoostedArmor.Clear();
            armor1List.Clear();
            armor1Upgrade.Clear();
            armor2List.Clear();
            armor2Upgrade.Clear();
            drawReinf.Clear();

            MyAPIGateway.Parallel.ForEach(reinfBlocks, reinf => {
                if (!(reinf.Integrity / reinf.MaxIntegrity >= Session.reinfMinHealth)) return;
                var _tempArmor1 = new List<IMySlimBlock>();
                var _neighbors = new List<IMySlimBlock>();
                reinf.GetNeighbours(_neighbors);
                bool supported = false;
                foreach (var neighbor in _neighbors)
                {
                    bool isArmor = neighbor.FatBlock == null;
                    bool isReinf = !isArmor && Session.reinfSubtypes.Contains(neighbor.BlockDefinition.Id.SubtypeId) && neighbor.Integrity / neighbor.MaxIntegrity >= Session.reinfMinHealth;

                    if (isReinf)
                        supported = true;
                    else if (isArmor)
                        _tempArmor1.Add(neighbor);
                }
                if (supported)
                {
                    lock (drawReinf) drawReinf.Add(reinf);
                    foreach (var armor in _tempArmor1)
                    {
                        if (BoostedArmor.TryAdd(armor, new MyTuple<int, float>(1, armor.BlockGeneralDamageModifier)))
                        {
                            lock (armor1List) armor1List.Add(armor);
                            if (Session.MpServer) armor.BlockGeneralDamageModifier = Session.tier1;                          
                        }
                    }
                }
            });
            //MyAPIGateway.Utilities.ShowMessage("SR", "Ran Reinf " + reinfBlocks.Count + "/" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");            
            MyAPIGateway.Parallel.ForEach(armor1List, armor1 =>
            {
                var _neighbors = new List<IMySlimBlock>();
                armor1.GetNeighbours(_neighbors);

                foreach (var neighbor in _neighbors)
                {
                    if (neighbor.FatBlock == null)
                    {
                        if (BoostedArmor.TryAdd(neighbor, new MyTuple<int, float>(1, neighbor.BlockGeneralDamageModifier)))
                        {
                            if (Session.MpServer) neighbor.BlockGeneralDamageModifier = Session.tier1;
                            lock (armor1Upgrade) armor1Upgrade.Add(neighbor);
                        }
                    }
                }
            });
            //MyAPIGateway.Utilities.ShowMessage("SR", "Ran Armor1 " + armor1List.Count + "/" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");
            armor1List.UnionWith(armor1Upgrade);
            foreach (var armor1up in armor1Upgrade)
            {
                neighbors.Clear();
                armor1up.GetNeighbours(neighbors);

                var oldModifier = BoostedArmor[armor1up].Item2;
                BoostedArmor[armor1up] = new MyTuple<int, float>(1, oldModifier);
                if (Session.MpServer) armor1up.BlockGeneralDamageModifier = Session.tier1;

                if (armor2List.Contains(armor1up))
                    armor2List.Remove(armor1up);

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.FatBlock == null && !armor2List.Contains(neighbor) && !armor1Upgrade.Contains(neighbor))
                    {
                        if (BoostedArmor.TryAdd(neighbor, new MyTuple<int, float>(2, neighbor.BlockGeneralDamageModifier)))
                        {
                            if (Session.MpServer) neighbor.BlockGeneralDamageModifier = Session.tier2;
                            armor2List.Add(neighbor);
                        }
                    }
                }
            }
            //MyAPIGateway.Utilities.ShowMessage("SR", "Ran Armor1Up " + armor1Upgrade.Count + "/" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");        
            MyAPIGateway.Parallel.ForEach(armor2List, armor2 =>
            {
                var _neighbors = new List<IMySlimBlock>();
                armor2.GetNeighbours(_neighbors);
                foreach (var neighbor in _neighbors)
                {
                    if (neighbor.FatBlock == null)
                    {
                        if (BoostedArmor.TryAdd(neighbor, new MyTuple<int, float>(3, neighbor.BlockGeneralDamageModifier)))
                        {
                            if (Session.MpServer) neighbor.BlockGeneralDamageModifier = Session.tier3;
                        }
                        else if (BoostedArmor[neighbor].Item1 == 3)
                            lock (armor2Upgrade) armor2Upgrade.Add(neighbor);
                    }
                }

            });
            //MyAPIGateway.Utilities.ShowMessage("SR", "Ran Armor2 " + armor2List.Count + "/" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");
            MyAPIGateway.Parallel.ForEach(armor2Upgrade, armor2up =>
            {
                var _neighbors = new List<IMySlimBlock>();
                armor2up.GetNeighbours(_neighbors);

                var oldModifier = BoostedArmor[armor2up].Item2;
                BoostedArmor[armor2up] = new MyTuple<int, float>(2, oldModifier);
                armor2up.BlockGeneralDamageModifier = Session.tier2;

                foreach (var neighbor in _neighbors)
                {
                    if (neighbor.FatBlock == null)
                    {
                        if (BoostedArmor.TryAdd(neighbor, new MyTuple<int, float>(3, neighbor.BlockGeneralDamageModifier)))
                            if (Session.MpServer) neighbor.BlockGeneralDamageModifier = Session.tier3;
                    }
                }
            });
            //MyAPIGateway.Utilities.ShowMessage("SR", "Ran Armor2Up " + armor2Upgrade.Count + "/" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");
            foreach (var boosted in oldBoosted)
            {
                if (!BoostedArmor.ContainsKey(boosted.Key))
                    if (Session.MpServer) boosted.Key.BlockGeneralDamageModifier = boosted.Value.Item2; //Restore original value
            }       
            //MyAPIGateway.Utilities.ShowMessage("SR", "Restored " + oldBoosted.Count + ":" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms");
            //timer.Stop();
            //MyAPIGateway.Utilities.ShowMessage("SR", "Updated " + reinfBlocks.Count + ":" + BoostedArmor.Count + " in " + timer.ElapsedMilliseconds + "ms \n ");
            
            firstRun = false;
        }

        internal void Clean()
        {
            Grid.OnBlockAdded -= BlockAdded;
            Grid.OnBlockRemoved -= BlockRemoved;
            Grid = null;
            BoostedArmor.Clear();
            reinfBlocks.Clear();
            armor1List.Clear();
            armor2List.Clear();
            tempArmor1.Clear();
            neighbors.Clear();
            drawReinf.Clear();
            armor1Upgrade.Clear();
            armor2Upgrade.Clear();
        }
    }
}
