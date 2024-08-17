using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;

namespace StructuralReinforcement
{
    [ProtoContract]
    public class ServerSettings
    {
        public static ServerSettings Instance;
        public static readonly ServerSettings Default = new ServerSettings()
        {
            reinfSubtypeIDs = new List<string>() {
            "ArmorCenter",
            "ArmorCorner",
            "ArmorInvCorner",
            "ArmorSide",
            "SmallArmorCenter",
            "SmallArmorCorner",
            "SmallArmorInvCorner",
            "SmallArmorSide",

            "Armor1x1Slope_Large",
            "Armor1x2Slope_Large",
            "Armor1x3Slope_Large",
            "Armor1x4Slope_Large",
            "Armor1x5Slope_Large",
            "Armor1x6Slope_Large",

            "Armor1x1Slope_Small",
            "Armor1x2Slope_Small",
            "Armor1x3Slope_Small",
            "Armor1x4Slope_Small",
            "Armor1x5Slope_Small",
            "Armor1x6Slope_Small",

            "Armor1x2InvCorner_Large",
            "Armor1x3InvCorner_Large",
            "Armor1x4InvCorner_Large",
            "Armor1x5InvCorner_Large",
            "Armor1x6InvCorner_Large",

            "Armor1x2InvCorner_Small",
            "Armor1x3InvCorner_Small",
            "Armor1x4InvCorner_Small",
            "Armor1x5InvCorner_Small",
            "Armor1x6InvCorner_Small",
            }
        };

        [ProtoMember(1)]
        public List<string> reinfSubtypeIDs { get; set; }
    }
    public partial class Session
    {

        private void InitServerConfig()
        {
            ServerSettings s = ServerSettings.Default;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(settingsServerFile, typeof(ServerSettings)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(settingsServerFile, typeof(ServerSettings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    s = MyAPIGateway.Utilities.SerializeFromXML<ServerSettings>(text);
                    reinfSubtypes.Clear();
                    foreach (var block in s.reinfSubtypeIDs)
                    {
                        reinfSubtypes.Add(MyStringHash.GetOrCompute(block));
                        MyLog.Default.WriteLineAndConsole($"Structural Reinf: Added '{block}' as reinforcement type");
                    }
                    ServerSettings.Instance = s;
                }
                else
                {
                    s = ServerSettings.Default;
                    SaveServer(s);
                    MyLog.Default.WriteLineAndConsole($"Structural Reinf: Error with server config formatting, using defaults");
                }
            }
            catch
            {
                ServerSettings.Instance = ServerSettings.Default;
                s = ServerSettings.Default;
                SaveServer(s);
            }
        }
        public static void SaveServer(ServerSettings settings)
        {
            try
            {
                TextWriter writer;
                writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(settingsServerFile, typeof(ServerSettings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
                ServerSettings.Instance = settings;
                reinfSubtypes.Clear();
                foreach (var block in settings.reinfSubtypeIDs)
                {
                    reinfSubtypes.Add(MyStringHash.GetOrCompute(block));
                    MyLog.Default.WriteLineAndConsole($"Structural Reinf: Added '{block}' as reinforcement type");
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"Structural Reinf: Error writing server config, using defaults");
            }
        }
    }
}

