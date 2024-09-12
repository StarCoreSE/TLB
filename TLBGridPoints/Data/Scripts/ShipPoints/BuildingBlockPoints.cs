using System.Text;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using TLB.ShareTrack.API;
using VRageMath;
using VRageRender;

namespace TLB.ShareTrack
{
    internal class BuildingBlockPoints
    {
        internal string LastHeldSubtype;
        private HudAPIv2.HUDMessage _pointsMessage;

        public BuildingBlockPoints()
        {
            MasterSession.I.HudRegistered += () =>
            {
                _pointsMessage = new HudAPIv2.HUDMessage(scale: 1f, font: "BI_SEOutlined", Message: new StringBuilder(""),
                    origin: new Vector2D(0, 0.2), blend: MyBillboard.BlendTypeEnum.PostPP);
            };
        }

        private int _ticks;
        public void Update()
        {
            // Disable the tooltip by ensuring it is not visible
            if (_pointsMessage != null)
            {
                _pointsMessage.Visible = false;
            }

            // Existing logic, but now it won't display anything
            if (_ticks++ % 10 != 0)
                return;

            if (LastHeldSubtype != MyHud.BlockInfo?.DefinitionId.SubtypeName)
            {
                LastHeldSubtype = MyHud.BlockInfo?.DefinitionId.SubtypeName;
                UpdateHud(MyHud.BlockInfo);
            }
        }

        private void UpdateHud(MyHudBlockInfo blockInfo)
        {
            if (_pointsMessage == null)
                return;

            // This code is still executed, but the message is never made visible
            double blockPoints;
            if (blockInfo == null || !AllGridsList.PointValues.TryGetValue(blockInfo.DefinitionId.SubtypeName, out blockPoints))
            {
                _pointsMessage.Visible = false;
                return;
            }

            string blockDisplayName = blockInfo.BlockName;

            float thisClimbingCostMult = 0;
            AllGridsList.ClimbingCostRename(ref blockDisplayName, ref thisClimbingCostMult);  // Use double instead of float.

            _pointsMessage.Message.Clear();
            _pointsMessage.Message.Append($"{blockDisplayName}:\n{blockPoints}bp");
            if (thisClimbingCostMult != 0)
                _pointsMessage.Message.Append($" +{(blockPoints * thisClimbingCostMult)}bp/b");

            // Even though this is here, the tooltip will not be visible
            //_pointsMessage.Visible = false; // Ensures the HUD is never displayed
        }
    }
}
