using Digi.Example_NetworkProtobuf;
using Sandbox.Game.Entities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace StructuralReinforcement
{
    public partial class Session : MySessionComponentBase
    {
        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);
        private readonly ConcurrentCachingList<MyCubeGrid> _startGrids = new ConcurrentCachingList<MyCubeGrid>();
        internal readonly List<GridComp> GridList = new List<GridComp>();
        internal readonly ConcurrentDictionary<IMyCubeGrid, GridComp> GridMap = new ConcurrentDictionary<IMyCubeGrid, GridComp>();
        internal readonly ConcurrentDictionary<GridComp, int> scheduledRecalc = new ConcurrentDictionary<GridComp, int>();
        internal GridComp clientGrid = null;
        internal bool firstPress = false;
        internal static float tier1 = 0.5f;
        internal static float tier2 = 0.65f;
        internal static float tier3 = 0.85f;
        internal static float reinfMinHealth = 0.5f;
        internal static string settingsFile = "StructuralReinforcementSettings.cfg";
        internal static string settingsServerFile = "SRServerSettings.cfg";
        internal bool updateKeybind = false;
        internal MyStringId particle = MyStringId.GetOrCompute("Square"); //Square  GizmoDrawLine  particle_laser
        public static Networking Networking = new Networking(0356);


        internal static readonly Dictionary<MyStringHash, float> non1Defs = new Dictionary<MyStringHash, float>();

        internal static readonly List<MyStringHash> reinfSubtypes = new List<MyStringHash>();

            /*
        {
            MyStringHash.GetOrCompute("ArmorCenter"),
            MyStringHash.GetOrCompute("ArmorCorner"),
            MyStringHash.GetOrCompute("ArmorInvCorner"),
            MyStringHash.GetOrCompute("ArmorSide"),
            MyStringHash.GetOrCompute("SmallArmorCenter"),
            MyStringHash.GetOrCompute("SmallArmorCorner"),
            MyStringHash.GetOrCompute("SmallArmorInvCorner"),
            MyStringHash.GetOrCompute("SmallArmorSide")
        };
            */

        public Session()
        {
            
        }
        private void Clean()
        {
            _gridCompPool.Clear();
            _startGrids.ClearImmediate();
            GridList.Clear();
            GridMap.Clear();
            scheduledRecalc.Clear();
            clientGrid = null;
            reinfSubtypes.Clear();
            non1Defs.Clear();
        }

    }
}
