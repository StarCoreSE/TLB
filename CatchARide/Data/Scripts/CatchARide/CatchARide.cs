using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;


namespace Klime.CatchARide
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class CatchARide : MySessionComponentBase
    {
        Type reuse_type = typeof(MyObjectBuilder_EntityBase);
        string panel_save_subtype = "save_panel_catcharide";
        string panel_claim_subtype = "claim_panel_catcharide";
        string panel_info_subtype = "info_panel_catcharide";

        string grid_name_to_save = "[save]";

        double search_radius = 25;

        long cost_to_claim = 5000;
        Dictionary<int, string> codes = new Dictionary<int, string>
        {
            [0] = "Unspecified Error\nPlease try again",
            [1] = "Error:\nNo Faction",
            [2] = "Error:\nNo grid tagged with [save]",
            [3] = "Success!\nGrid Saved",
            [4] = "Error:\nNot enough SC",
            [5] = "Error:\nNo grid to claim",
            [6] = "Success!\nGrid Claimed",
            [7] = "",
            [8] = "",
            [9] = "",
            [10] = ""
        };



        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName += Pressed;
            }
        }

        private void Pressed(string name, int button, long playerId, long blockId)
        {
            IMyButtonPanel panel = MyAPIGateway.Entities.GetEntityById(blockId) as IMyButtonPanel;
            IMyPlayer player = GetPlayer(playerId);

            if (panel != null && player != null && panel.CubeGrid.Physics != null)
            {
                if (panel.BlockDefinition.SubtypeName == panel_save_subtype)
                {
                    int result = TrySave(panel, player);

                    var ig_panel = panel as SpaceEngineers.Game.ModAPI.Ingame.IMyButtonPanel;
                    var provider = ig_panel as Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
                    if (provider != null)
                    {
                        var surface = provider.GetSurface(0);

                        if (surface != null)
                        {
                            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                            surface.FontSize = 4.0f;
                            surface.WriteText(codes[result], false);
                        }
                    }
                }

                if (panel.BlockDefinition.SubtypeName == panel_claim_subtype)
                {
                    int result = TryClaim(panel, player);

                    var ig_panel = panel as SpaceEngineers.Game.ModAPI.Ingame.IMyButtonPanel;
                    var provider = ig_panel as Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
                    if (provider != null)
                    {
                        var surface = provider.GetSurface(0);

                        if (surface != null)
                        {
                            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                            surface.FontSize = 3.0f;
                            surface.WriteText(codes[result], false);
                        }
                    }
                }

                //if (panel.BlockDefinition.SubtypeName == panel_info_subtype)
                //{
                //    string info = TryInfo(panel, player);

                //    var ig_panel = panel as SpaceEngineers.Game.ModAPI.Ingame.IMyButtonPanel;
                //    var provider = ig_panel as Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
                //    if (provider != null)
                //    {
                //        var surface = provider.GetSurface(0);

                //        if (surface != null)
                //        {
                //            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                //            surface.FontSize = 3.0f;
                //            surface.WriteText(info, false);
                //        }
                //    }
                //}
            }
        }

        //private string TryInfo(IMyButtonPanel panel, IMyPlayer player)
        //{
            
        //}

        private IMyBatteryBlock GetBattery(IMyButtonPanel panel)
        {
            IMyBatteryBlock return_battery = null;

            List<IMySlimBlock> all_blocks = new List<IMySlimBlock>();
            panel.CubeGrid.GetBlocks(all_blocks);
            foreach (var block in all_blocks)
            {
                if (block.FatBlock != null)
                {
                    return_battery = block.FatBlock as IMyBatteryBlock;
                    if (return_battery != null)
                    {
                        break;
                    }
                }
            }

            return return_battery;
        }

        private int TrySave(IMyButtonPanel panel, IMyPlayer player)
        {
            int result = 0;

            try
            {
                string poi_name = panel.CubeGrid.CustomName;
                string faction_tag = MyVisualScriptLogicProvider.GetPlayersFactionTag(player.IdentityId);

                if (!string.IsNullOrWhiteSpace(poi_name) && !string.IsNullOrWhiteSpace(faction_tag))
                {
                    BoundingSphereD search_sphere = new BoundingSphereD(panel.WorldMatrix.Translation, search_radius);
                    List<MyEntity> nearby_ents = new List<MyEntity>();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref search_sphere, nearby_ents);

                    bool got_grid = false;

                    foreach (var ent in nearby_ents)
                    {
                        IMyCubeGrid grid = ent as IMyCubeGrid;
                        if (grid != null && grid.Physics != null && grid.EntityId != panel.CubeGrid.EntityId && !grid.IsStatic)
                        {
                            if (grid.CustomName.StartsWith(grid_name_to_save))
                            {
                                List<long> faction_members = MyVisualScriptLogicProvider.GetFactionMembers(faction_tag);
                                if (grid.BigOwners.Intersect(faction_members).Any())
                                {
                                    IMyBatteryBlock custom_battery = GetBattery(panel);

                                    if (custom_battery != null)
                                    {
                                        ClearCockpits(grid);
                                        result = DoSave(custom_battery, grid, poi_name, faction_tag);
                                        grid.CustomName = grid.CustomName.Substring(6);

                                        got_grid = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!got_grid)
                    {
                        result = 2;
                    }
                }
                else
                {
                    result = 1;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("KLIME CATCHARIDE: " + e);
            }


            return result;
        }

        private int DoSave(IMyBatteryBlock custom_battery, IMyCubeGrid grid, string poi_name, string faction_tag)
        {
            int result = 0;

            try
            {
                string save_file_name = poi_name + faction_tag;

                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(save_file_name, reuse_type))
                {
                    MyAPIGateway.Utilities.DeleteFileInWorldStorage(save_file_name, reuse_type);
                    List<string> cleanup_list = new List<string>();
                    cleanup_list = custom_battery.CustomData.Split('\n').ToList();

                    List<string> reform_list = new List<string>();

                    if (cleanup_list != null && cleanup_list.Count > 0)
                    {
                        foreach (var entry in cleanup_list)
                        {
                            if (!entry.StartsWith(save_file_name))
                            {
                                reform_list.Add(entry);
                            }
                        }

                        custom_battery.CustomData = "";

                        foreach (var entry in reform_list)
                        {
                            custom_battery.CustomData += entry;
                        }
                    }
                }

                List<IMyCubeGrid> all_grids = new List<IMyCubeGrid>();
                all_grids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Logical);

                List<MyObjectBuilder_EntityBase> all_ob = new List<MyObjectBuilder_EntityBase>();

                foreach (var subgrid in all_grids)
                {
                    all_ob.Add(subgrid.GetObjectBuilder());
                }

                var writer = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage(save_file_name, reuse_type);
                if (writer != null)
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToBinary<List<MyObjectBuilder_EntityBase>>(all_ob));
                    writer.Close();
                }

                custom_battery.CustomData += save_file_name + ";" + grid.EntityId + "\n";
                result = 3;
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("KLIME CATCHARIDE: " + e);
            }
            

            return result;
        }

        private int TryClaim(IMyButtonPanel panel, IMyPlayer player)
        {
            int result = 0;

            try
            {
                string poi_name = panel.CubeGrid.CustomName;
                string faction_tag = MyVisualScriptLogicProvider.GetPlayersFactionTag(player.IdentityId);

                if (!string.IsNullOrWhiteSpace(poi_name) && !string.IsNullOrWhiteSpace(faction_tag))
                {
                    string save_file_name = poi_name + faction_tag;
                    if (MyAPIGateway.Utilities.FileExistsInWorldStorage(save_file_name,reuse_type))
                    {
                        long current_balance = 0;
                        player.TryGetBalanceInfo(out current_balance);

                        if (current_balance >= cost_to_claim)
                        {
                            IMyBatteryBlock custom_battery = GetBattery(panel);
                            bool success = DoClaim(custom_battery, player,save_file_name);
                            if (success)
                            {
                                result = 6;
                                player.RequestChangeBalance(-1 * cost_to_claim);
                            }
                        }
                        else
                        {
                            result = 4;
                        }
                    }
                    else
                    {
                        result = 5;
                    }
                }
                else
                {
                    result = 1;
                }

            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("KLIME CATCHARIDE: " + e);
            }

            return result;
        }

        private bool DoClaim(IMyBatteryBlock custom_battery, IMyPlayer player, string save_file_name)
        {
            bool outcome = false;

            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(save_file_name, reuse_type))
                {
                    List<string> cleanup_list = new List<string>();
                    cleanup_list = custom_battery.CustomData.Split('\n').ToList();

                    foreach (var entry in cleanup_list)
                    {
                        List<string> id_pair = new List<string>();
                        id_pair = entry.Split(';').ToList();
                        if (id_pair != null && id_pair.Count == 2)
                        {
                            string test_save_name = id_pair[0];

                            long test_entity_id = 0;

                            if (long.TryParse(id_pair[1], out test_entity_id))
                            {
                                if (test_save_name == save_file_name)
                                {
                                    IMyCubeGrid del_grid = MyAPIGateway.Entities.GetEntityById(test_entity_id) as IMyCubeGrid;

                                    if (del_grid != null && !del_grid.MarkedForClose && del_grid.Physics != null)
                                    {
                                        List<IMyCubeGrid> con_grids = MyAPIGateway.GridGroups.GetGroup(del_grid, GridLinkTypeEnum.Logical);

                                        foreach (var subgrid in con_grids)
                                        {
                                            if (subgrid.Physics != null && !subgrid.MarkedForClose)
                                            {
                                                ClearCockpits(subgrid);
                                                subgrid.Close();
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    var reader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage(save_file_name, reuse_type);
                    if (reader != null)
                    {
                        List<MyObjectBuilder_EntityBase> all_ob = new List<MyObjectBuilder_EntityBase>();
                        all_ob = MyAPIGateway.Utilities.SerializeFromBinary<List<MyObjectBuilder_EntityBase>>(reader.ReadBytes((int)reader.BaseStream.Length));
                        if (all_ob != null)
                        {
                            MyAPIGateway.Entities.RemapObjectBuilderCollection(all_ob);
                            IMyCubeGrid spawning_grid = null;
                            foreach (var ob in all_ob)
                            {
                                IMyCubeGrid test_grid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(ob) as IMyCubeGrid;
                                List<IMySlimBlock> all_blocks = new List<IMySlimBlock>();
                                test_grid.GetBlocks(all_blocks);

                                foreach (var block in all_blocks)
                                {
                                    if (block.FatBlock != null && block.FatBlock.HasInventory)
                                    {
                                        IMyInventory temp_inv = block.FatBlock.GetInventory();
                                        if (temp_inv != null)
                                        {
                                            temp_inv.Clear(true);
                                        }
                                    }
                                }
                                if (test_grid != null && test_grid.CustomName.StartsWith(grid_name_to_save))
                                {
                                    spawning_grid = test_grid;
                                }
                            }

                            if (spawning_grid != null)
                            {
                                outcome = true;
                                spawning_grid.CustomName = spawning_grid.CustomName.Substring(6);

                                List<string> update_cleanup_list = new List<string>();
                                update_cleanup_list = custom_battery.CustomData.Split('\n').ToList();

                                List<string> reform_list = new List<string>();

                                if (update_cleanup_list != null && update_cleanup_list.Count > 0)
                                {
                                    foreach (var entry in update_cleanup_list)
                                    {
                                        if (!entry.StartsWith(save_file_name))
                                        {
                                            reform_list.Add(entry);
                                        }
                                        else
                                        {
                                            reform_list.Add(save_file_name + ";" + spawning_grid.EntityId);
                                        }
                                    }

                                    custom_battery.CustomData = "";

                                    foreach (var entry in reform_list)
                                    {
                                        custom_battery.CustomData += entry + "\n";
                                    }
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("KLIME CATCHARIDE: " + e);
            }
            

            return outcome;
        }

        private void ClearCockpits(IMyCubeGrid subgrid)
        {
            List<IMySlimBlock> all_blocks = new List<IMySlimBlock>();

            subgrid.GetBlocks(all_blocks);

            foreach (var block in all_blocks)
            {
                if (block.FatBlock != null)
                {
                    IMyCockpit cockpit = block.FatBlock as IMyCockpit;

                    if (cockpit != null && cockpit.Pilot != null)
                    {
                        cockpit.RemovePilot();
                    }
                }
            }
        }

        private IMyPlayer GetPlayer(long playerId)
        {
            IMyPlayer return_player = null;

            List<IMyPlayer> all_players = new List<IMyPlayer>();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);

            foreach (var p in all_players)
            {
                if (p.IdentityId == playerId)
                {
                    return_player = p;
                    break;
                }
            }

            return return_player;
        }

        public override void UpdateAfterSimulation()
        {
            
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName -= Pressed;
            }
        }
    }
}