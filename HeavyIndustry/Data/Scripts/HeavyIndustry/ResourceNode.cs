using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;

namespace ResourceNodes
{

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class ResourceNode : MySessionComponentBase
    {

        public List<string> MiningBlacklist = new List<string>();
        public Dictionary<string, HashSet<long>> Miners = new Dictionary<string, HashSet<long>>();
        public Dictionary<long, Vector3D> Locations = new Dictionary<long, Vector3D>();

        public static ResourceNode Instance { get; private set; }

        public override void BeforeStart()
        {
            base.BeforeStart();
            Instance = this;
        }
    }
}
