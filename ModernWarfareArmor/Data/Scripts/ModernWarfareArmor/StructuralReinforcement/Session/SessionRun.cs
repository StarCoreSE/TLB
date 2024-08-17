using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;

namespace StructuralReinforcement
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;
        internal bool IsClient;
        internal static bool MpServer;

        public override void LoadData()
        {
            var MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            MpServer = (MpActive && MyAPIGateway.Multiplayer.IsServer) || !MpActive;
            IsClient = (MpActive && !MyAPIGateway.Utilities.IsDedicated) || !MpActive;
            MyEntities.OnEntityCreate += OnEntityCreate;
            /*
            foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var cubeDef = def as MyCubeBlockDefinition;
                if (cubeDef == null || cubeDef.PCU > 1 || cubeDef.ContainsComputer() == true || cubeDef.GeneralDamageMultiplier == 1) continue;//Adjust PCU to avoid filtering modded armor?
                
                var subtype = MyStringHash.GetOrCompute(cubeDef.Id.SubtypeName);
                if (!non1Defs.ContainsKey(subtype))
                   non1Defs.Add(subtype, cubeDef.GeneralDamageMultiplier);
            }
            */
        }

        public override void BeforeStart()
        {
            if (IsClient)
            {
                InitConfig();
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
            }
            if (MpServer)
            {
                InitServerConfig();
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            }
            Networking.Register();
        }


        private void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            messageText.ToLower();
            var s = Settings.Instance;

            switch(messageText)
            {
                case "/sr help":
                    MyAPIGateway.Utilities.ShowMessage("Structural Reinforcement", "Command options include '/sr xray' and '/sr keybind'");
                    sendToOthers = false;
                    break;

                case "/sr xray":
                    s.enableXray = !s.enableXray;
                    MyAPIGateway.Utilities.ShowNotification("Structural Reinforcement Xray mode " + (s.enableXray == true ? "on" : "off"));
                    Save(Settings.Instance);
                    sendToOthers = false;
                    break;

                case "/sr keybind":
                    MyAPIGateway.Utilities.ShowNotification("Press a key to set as the bind for xray view");
                    updateKeybind = true;
                    sendToOthers = false;
                    break;

                default:
                    break;
                    
            }
            return;
        }

        public override void UpdateBeforeSimulation()
        {
            Tick++;
            if (Tick % 89 == 0)
            {
                if (!_startGrids.IsEmpty)
                    StartComps();
                /*
                if (FirstRun)
                {
                    if(non1Defs.Count > 0)
                    {
                        MyLog.Default.WriteLineAndConsole($"StructReinf: {non1Defs.Count} Blocks with GeneralDamageModifer != 1 detected");
                        foreach (var def in non1Defs)
                        {
                            MyLog.Default.WriteLineAndConsole($"StructReinf: Non 1 def - {def.Key} - {def.Value}");
                        }
                        MyAPIGateway.Utilities.ShowMessage("StructReinf",$"{non1Defs.Count} Blocks with GeneralDamageModifer != 1 detected");
                    }
                    FirstRun = false;
                }
                */
                int scheduleTime = 0;
                foreach (GridComp grid in GridList)
                {
                    if (grid.armorDirty)
                    {
                        if(grid.BoostedArmor.Count > 500 && scheduledRecalc.TryAdd(grid, Tick + scheduleTime))
                        {
                            scheduleTime ++;
                        }
                        else
                            grid.RecomputeArmor();
                    }
                }
            }

            if(scheduledRecalc.Count > 0)
            {
                foreach (var grid in scheduledRecalc)
                {
                    if (Tick >= grid.Value)
                    {
                        int temp;
                        scheduledRecalc.TryRemove(grid.Key, out temp);
                        MyAPIGateway.Parallel.StartBackground(grid.Key.RecomputeArmor);
                    }
                }
            }

            if(IsClient && updateKeybind)
            {
                var _pressedKeys = new List<MyKeys>();
                MyAPIGateway.Input.GetListOfPressedKeys(_pressedKeys);
                if(_pressedKeys.Count > 0 && _pressedKeys[0] != MyKeys.Enter)
                {
                    MyAPIGateway.Utilities.ShowNotification("Xray keybind set to: " + _pressedKeys[0].ToString());
                    Settings.Instance.xrayKey = _pressedKeys[0];
                    Save(Settings.Instance);
                    updateKeybind = false;
                }
            }

            if (IsClient && Session?.Player?.Character != null && !MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Input.IsAnyMousePressed())
            {
                bool currentPress = Settings.Instance.enableXray && MyAPIGateway.Input.IsKeyPress(Settings.Instance.xrayKey);
                bool list = false;
                bool draw = false;
                bool undo = false;
                if (currentPress && !firstPress)
                {
                    list = true;
                    firstPress = true;
                }
                else if (currentPress && firstPress)
                    draw = true;
                else if (!currentPress && firstPress)
                {
                    undo = true;
                    firstPress = false;
                }

                if (list || draw || undo)
                {
                    if(list)
                    {
                        Matrix mat = Session.Player.Character.GetHeadMatrix(true);//thanks Math!!
                        IHitInfo hit;
                        MyAPIGateway.Physics.CastRay(mat.Translation, mat.Translation + mat.Forward * 200, out hit);
                        var hitGridChk = hit?.HitEntity as MyCubeGrid;
                        GridComp comp;
                        if (hitGridChk != null && GridMap.TryGetValue(hitGridChk, out comp))
                        {
                            var hitGrid = comp.Grid;
                            IMyFaction faction;
                            bool noOwnerOrFaction = false;
                            bool enemyGrid = false;
                            if (hitGrid.BigOwners != null && hitGrid.BigOwners.Count > 0)
                            {
                                faction = Session.Factions.TryGetPlayerFaction(hitGrid.BigOwners[0]);
                                if (faction != null)
                                {
                                var currPlayerFaction = Session.Factions.TryGetPlayerFaction(Session.Player.IdentityId);
                                    if (currPlayerFaction == faction || Session.Factions.GetReputationBetweenPlayerAndFaction(Session.Player.IdentityId, faction.FactionId) > 500)
                                        clientGrid = comp;
                                    else
                                    {
                                        MyAPIGateway.Utilities.ShowNotification("No peeking inside enemy or neutral grids", 1600);
                                        clientGrid = null;
                                        enemyGrid = true;
                                    }
                                }
                                else if (hitGrid.BigOwners[0] == Session.Player.IdentityId)
                                    clientGrid = comp;
                                else
                                    noOwnerOrFaction = true;
                            }
                            else
                                noOwnerOrFaction = true;

                            if (noOwnerOrFaction)
                            {
                                MyAPIGateway.Utilities.ShowNotification("No faction or owner found for grid under crosshair", 1600);
                                clientGrid = null;
                            }
                            else if (!enemyGrid)
                                clientGrid = comp;
                        }
                        //else
                            //MyAPIGateway.Utilities.ShowNotification("No grid found within 200m", 1600);
                    }
                    if(clientGrid != null) 
                        XRayDraw(clientGrid.Grid, list, draw, undo);
                    if (draw && clientGrid != null)
                    {
                        var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                        foreach (var entry in clientGrid.BoostedArmor)
                        {
                            var block = entry.Key;

                            var color = Color.Orange;
                            if (entry.Value.Item1 == 2)
                                color = Color.Yellow;
                            else if (entry.Value.Item1 == 1)
                                color = Color.DarkGreen;
                            DrawBlock(entry.Key, color, viewProjectionMat);
                        }
                        //TODO- show reinforcement in blue?
                    }
                }
            }
        }
        internal void XRayDraw(IMyCubeGrid grid, bool list, bool draw, bool undo)
        {
            if(list || undo)
            {
                var tempList = new List<IMySlimBlock>();
                if (grid == null || grid.MarkedForClose || grid.Closed)
                {
                    clientGrid = null;
                    return;
                }
                grid.GetBlocks(tempList);
                foreach (var block in tempList)
                    if (block.FatBlock == null)
                        block.Dithering = list ? 0.5f : 0;
                if (undo)
                    clientGrid = null;
            }
        }
        internal void DrawBlock(IMySlimBlock block, Color color, MatrixD viewProjectionMat)
        {
            BoundingBoxD bounds = new BoundingBoxD();
            Vector3 halfExtents;
            var wm = block.CubeGrid.PositionComp.WorldMatrixRef;
            block.ComputeScaledHalfExtents(out halfExtents);
            var center = block.Position * block.CubeGrid.GridSize;
            var offset = new Vector3D(0.01, 0.01, 0.01);
            bounds.Min = center - halfExtents - offset;
            bounds.Max = center + halfExtents + offset;
            var screenCoords = Vector3D.Transform(Vector3D.Transform(bounds.Center, wm), viewProjectionMat);
            var offscreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;
            if (!offscreen)
                MySimpleObjectDraw.DrawTransparentBox(ref wm, ref bounds, ref color, MySimpleObjectRasterizer.SolidAndWireframe, 1, 0.01f, particle, particle, true, -1, VRageRender.MyBillboard.BlendTypeEnum.Standard, 0.01f);
        }


        protected override void UnloadData()
        {
            if(IsClient)
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
            if (MpServer)
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyEntities.OnEntityCreate -= OnEntityCreate;
            Clean();
            Networking.Unregister();
        }
    }
}
