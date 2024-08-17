using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Input;

namespace StructuralReinforcement
{
    [ProtoContract]
    public class Settings
    {
        public static Settings Instance;
        public static readonly Settings Default = new Settings()
        {
            enableXray = true,
            xrayKey = MyKeys.LeftControl,
        };

        [ProtoMember(1)]
        public bool enableXray { get; set; }

        [ProtoMember(2)]
        public MyKeys xrayKey { get; set; }

    }
    public partial class Session
    {

        private void InitConfig()
        {
            Settings s = Settings.Default;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(settingsFile, typeof(Settings)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(settingsFile, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Save(s);
                }
                else
                {
                    s = Settings.Default;
                    Save(s);
                }
            }
            catch
            {
                Settings.Instance = Settings.Default;
                s = Settings.Default;
                Save(s);
                MyAPIGateway.Utilities.ShowMessage("SR", "Error with config file, overwriting with default.");
            }
        }
        public static void Save(Settings settings)
        {
            try
            {
                TextWriter writer;
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(settingsFile, typeof(Settings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
                Settings.Instance = settings;
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("SR", "Error with cfg file");
            }
        }
    }
}

