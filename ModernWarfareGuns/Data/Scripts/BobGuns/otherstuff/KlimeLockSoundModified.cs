using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;


namespace klime.LockSound
{
    [ProtoContract]
    public class TargetChange
    {
        [ProtoMember(11)]
        public TargetStatus targetStatus;

        public TargetChange()
        {

        }

        public TargetChange(TargetStatus targetStatus)
        {
            this.targetStatus = targetStatus;
        }
    }


    public enum TargetStatus
    {
        Lock,
        Unlock
    }

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LockSound : MySessionComponentBase
    {
        //retardation
        int clientLocks = 0;

        //Sound Config

        // lmao, needed a separate mod that didnt exist
        string lockSoundID = "RWR-TrackingAndTargeting";
        string unlockSoundID = "RWR-SpecialContact";


        //Core
        MyEntity3DSoundEmitter emitter;
        MySoundPair lockPair;
        MySoundPair unlockPair;

        //Network
        ushort netId = 31829;
        int timer = 0;
        
        //Take the previous list of players, compare to the new list, send events accordingly
        //Server
        List<IMyPlayer> allPlayers = new List<IMyPlayer>();
        List<MyCubeGrid> allLocks = new List<MyCubeGrid>();
        Dictionary<IMyCharacter, IMyPlayer> characterLookup = new Dictionary<IMyCharacter, IMyPlayer>();

        //LockCheck
        List<ulong> previousLockedPlayers = new List<ulong>();
        List<ulong> currentLockedPlayers = new List<ulong>();

        //ByteArrays
        TargetChange newLockChange;
        TargetChange newUnlockChange;
        byte[] newLockArray;
        byte[] newUnlockArray;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(netId, NetworkHandler);
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                if (timer % 60 == 0)
                {
                    allPlayers.Clear();
                    allLocks.Clear();
                    characterLookup.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(allPlayers);

                    foreach (var p in allPlayers)
                    {
                        if (p.Character != null)
                        {
                            var playerComp = p.Character.Components.Get<MyTargetLockingComponent>();
                            if (playerComp != null && playerComp.Target != null)
                            {
                                allLocks.Add(playerComp.Target);
                            }

                            characterLookup.Add(p.Character, p);
                        }
                    }

                    ResolveTargets();
                }
            }
            else if (clientLocks > 0)
                MyAPIGateway.Utilities.ShowNotification("<<<LOCKED>>>", 16, "Red");

            //if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.T))
            //{
            //    PlayLockSound();
            //}

            //if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.U))
            //{
            //    PlayUnlockSound();
            //}
            
            timer += 1;
        }

        private void ResolveTargets()
        {
            currentLockedPlayers.Clear();

            foreach (var target in allLocks)
            {
                if (target.MainCockpit != null)
                {
                    var cockpit = target.MainCockpit as IMyCockpit;
                    if (cockpit.Pilot != null && characterLookup.ContainsKey(cockpit.Pilot))
                    {
                        var player = characterLookup[cockpit.Pilot];
                        if (player != null && !currentLockedPlayers.Contains(player.SteamUserId))
                        {
                            currentLockedPlayers.Add(player.SteamUserId);
                        }
                    }
                }
            }

            if (newLockChange == null || newUnlockChange == null)
            {
                newLockChange = new TargetChange(TargetStatus.Lock);
                newLockArray = MyAPIGateway.Utilities.SerializeToBinary(newLockChange);

                newUnlockChange = new TargetChange(TargetStatus.Unlock);
                newUnlockArray = MyAPIGateway.Utilities.SerializeToBinary(newUnlockChange);
            }

            foreach (var currentId in currentLockedPlayers)
            {
                if (!previousLockedPlayers.Contains(currentId))
                {
                    SendNewLock(currentId);
                }
            }

            foreach (var previousId in previousLockedPlayers)
            {
                if (!currentLockedPlayers.Contains(previousId))
                {
                    SendNewUnlock(previousId);
                }
            }

            previousLockedPlayers.Clear();
            previousLockedPlayers = new List<ulong>(currentLockedPlayers);
        }

        private void SendNewLock(ulong id)
        {
            MyAPIGateway.Multiplayer.SendMessageTo(netId, newLockArray, id);
        }

        private void SendNewUnlock(ulong id)
        {
            MyAPIGateway.Multiplayer.SendMessageTo(netId, newUnlockArray, id);
        }

        private void NetworkHandler(ushort arg1, byte[] arg2, ulong arg3, bool arg4)
        {
            TargetChange incomingTargetChange = MyAPIGateway.Utilities.SerializeFromBinary<TargetChange>(arg2);

            if (incomingTargetChange != null)
            {
                if (incomingTargetChange.targetStatus == TargetStatus.Lock)
                {
                    PlayLockSound();
                    clientLocks++;
                }

                if (incomingTargetChange.targetStatus == TargetStatus.Unlock)
                {
                    PlayUnlockSound();
                    clientLocks--;
                }
            }
        }

        private void PlayLockSound()
        {
            if (MyAPIGateway.Session.Player?.Character == null)
            {
                return;
            }

            var charac = MyAPIGateway.Session.Player?.Character;

            if (lockPair == null)
            {
                lockPair = new MySoundPair(lockSoundID);
            }

            if (emitter == null)
            {
                emitter = new MyEntity3DSoundEmitter((MyEntity)charac);
                emitter.CanPlayLoopSounds = true;
            }

            if (emitter != null && lockPair != null)
            {
                if (emitter.IsPlaying)
                {
                    emitter.StopSound(false);
                }

                emitter.Entity = (MyEntity)charac;
                emitter.SetPosition(MyAPIGateway.Session.Camera.WorldMatrix.Translation);
                emitter.PlaySound(lockPair, false, false, true, true);
            }
        }

        private void PlayUnlockSound()
        {
            if (MyAPIGateway.Session.Player?.Character == null)
            {
                return;
            }

            var charac = MyAPIGateway.Session.Player?.Character;

            if (unlockPair == null)
            {
                unlockPair = new MySoundPair(unlockSoundID);
            }

            if (emitter == null)
            {
                emitter = new MyEntity3DSoundEmitter((MyEntity)charac);
                emitter.CanPlayLoopSounds = true;
            }

            if (emitter != null && unlockPair != null)
            {
                if (emitter.IsPlaying)
                {
                    emitter.StopSound(false);
                }

                emitter.Entity = (MyEntity)charac;
                emitter.SetPosition(MyAPIGateway.Session.Camera.WorldMatrix.Translation);
                emitter.PlaySound(unlockPair, false, false, true, true);
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(netId, NetworkHandler);
        }
    }
}