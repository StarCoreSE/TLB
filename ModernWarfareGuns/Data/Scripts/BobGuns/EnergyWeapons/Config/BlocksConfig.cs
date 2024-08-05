using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using ProtoBuf;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using MWI2_CustomEnergyWeapons;

namespace MWI2_CustomEnergyWeapons.Config{
	
	public static class BlocksConfig{
		
		public static WeaponConfig GetWeaponConfig(string subtypeName){

            ////////////////////////////////////////////////////////
            //////////////// DO NOT EDIT ABOVE HERE ////////////////
            ////////////////////////////////////////////////////////

            ////////////// --- Copy From Here --- //////////////////

            //Add Your Block SubtypeId below



            #region PulseBattery


            if (subtypeName == "Laser" || subtypeName == "Laser_Large")
            {

                //Do Not Edit This Line
                var Settings = new WeaponConfig();

                //Weapon Subtype
                Settings.WeaponSubtypeId = "LaserWep"; //Change this to the same weapon subtype as used above in the script setup
                Settings.UseScriptedFire = true; //Change this to false if you only want to use the ammo generating feature of the weapon.
                                                 //Incomplete Feature: Settings.RegisterDamageHandlerForTracers = false; //Set to true if you plan to use non-beam vanilla tracers with this weapon.

                //Regenerative Ammo Settings
                Settings.UseRegenerativeAmmo = false; //If set to 'true', then this weapon will consume grid energy and generate ammo automatically.
                Settings.AmmoMagazineSubtypeId = "LaserMag"; //The AmmoMagazine SubtypeId this weapon uses.
                Settings.AmmoRegenerationMaxPowerDraw = 10; //Maximum amount of power the weapon should draw to generate ammo.
                Settings.AmmoRegenerationMedPowerDraw = 10; //If Maximum amount of power draw is unavailable, then this amount is drawn instead.
                Settings.AmmoRegenerationTime = 10; //Time until ammo is generated (at rate of 1MW per second).
                Settings.AmmoAmountToAdd = 1; //Number of ammo magazines added when a charge is complete.
                Settings.MaxAmmoInInventory = 1000; //If ammo in weapon meets or exceeds this number, ammo regeneration will stop.
                Settings.AmmoRegenerationFreeForNPC = false; //If true and the block is owned by a valid NPC identity, the weapon will not draw energy to generate ammo, but will still create the ammo as if charging at AmmoRegenerationMaxPowerDraw rate.
                Settings.MustBeWorkingAtMaxDraw = false; //If true, weapon must be on, undamaged, and charging at max. If any of the consitions are not met, charge is reset to 0 and ammo is removed.

                //Pre-Fire Settings
                Settings.UsePreFireDelay = true; //If true, the weapon will have a delayed fire when the weapon is shot.
                Settings.PreHitTimerLimit = 10; //How long (in game ticks) the pre-fire phase should last
                Settings.PreFireSoundId = "thunkk"; //Here you can specify a sound that will play during the pre-fire
                Settings.PreFireEmissiveCharge = true; //If true, the Firing Emissive will fade from EmissiveFiringOffColor to EmissiveFiringColor before the weapon fires

                //Damage / Hit Timer Settings
                Settings.TickTimerLimit = 250; //Total Time (in game ticks) the beam is active
                Settings.DamageTimerLimit = 10; //Damage is applied at this game tick interval.

                //Distance Settings
                Settings.WeaponDistance = 1000; //Beam Distance
                Settings.SafeRange = 100; //If Beam Hits Own Grid, If Distance From Barrel to Hit is less than this value, it will be ignored.

                //Emissives - Off/Disabled/Damaged
                Settings.EmissiveInactiveName = "Emissive3"; //Emissive Material Name
                Settings.EmissiveInactiveAmount = 0.1f; //Emissive Amount - can be between 0-1 (non integers should be formatted with 'f' at the end. eg: 0.5f)
                Settings.EmissiveInactiveColor = new Color(25, 25, 0, 10); //RGBA value of Emissive

                //Emissives - Idle
                Settings.EmissiveIdleName = "Emissive3"; //Emissive Material Name
                Settings.EmissiveIdleAmount = 0.1f; //Emissive Amount - can be between 0-1 (non integers should be formatted with 'f' at the end. eg: 0.5f)
                Settings.EmissiveIdleColor = new Color(10, 0, 0, 255); //RGBA value of Emissive

                //Emissives - Charging
                Settings.EmissiveChargingName = "Emissive2"; //Emissive Material Name
                Settings.EmissiveChargingAmount = 1; //Emissive Amount - can be between 0-1 (non integers should be formatted with 'f' at the end. eg: 0.5f)
                Settings.EmissiveChargingColor = new Color(150, 75, 0, 255); //RGBA value of Emissive

                //Emissives - Firing
                Settings.EmissiveFiringName = "Emissive3"; //Emissive Material Name
                Settings.EmissiveFiringAmount = 2; //Emissive Amount - can be between 0-1 (non integers should be formatted with 'f' at the end. eg: 0.5f)
                Settings.EmissiveFiringColor = new Color(255, 0, 0, 255); //RGBA value of Emissive when Firing
                Settings.EmissiveFiringOffColor = new Color(10, 0, 0, 255); //RGBA value of Emissive when Not Firing

                //Multibeam Settings
                Settings.BarrelSubpartOffsets.Add(new Vector3D(0, 0, -1.0)); //Copy This Line and Provide the XYZ offset of any additional barrels that will fire beams on your weapons. Default offset of 0,0,0 can be changed if needed.
                //Settings.BarrelSubpartOffsets.Add(new Vector3D(2.75, 0, -6.0));
                //Settings.BarrelSubpartOffsets.Add(new Vector3D(-2.75, 0, -6.0));

                //Upgrade Valid Names
                /*
				Please note that your upgrade definitions attached to your upgrade blocks should only
				ever use <ModifierType>Additive</ModifierType>
				
				Upgrades do not set a new level for the modifier its affecting, but increases or decreases by the value you've provided.
				*/
                Settings.AllowUpgrades = false; //If true, this block will be able to accept upgrade modules.
                Settings.UpgradeDamageName = "ChangeToValidUpgradeName"; //The upgrade name for Damage and Explosion Damage. Increase/decrease by a percentage (eg: 25% would be 0.25 or -0.25)
                Settings.UpgradePowerName = "ChangeToValidUpgradeName"; //The upgrade name for Power Draw (assuming ammo regeneration is enabled). Increase/decrease by a percentage (eg: 25% would be 0.25 or -0.25)
                Settings.UpgradePowerStoreName = "ChangeToValidUpgradeName"; //The upgrade name for the Charged Power trigger that generates a round of ammo. Increase/decrease by a regular number (eg: 50, 100, -25, etc)
                Settings.UpgradeRangeName = "ChangeToValidUpgradeName"; //The upgrade name for Weapon Range. Increase/decrease by a percentage (eg: 25% would be 0.25 or -0.25)
                Settings.UpgradeTeslaEffectName = "ChangeToValidUpgradeName"; //The upgrade name for the Tesla Effect. Increase/decrease by a regular number (eg: 1, 2, -1, etc)
                Settings.UpgradeJumpEffectName = "ChangeToValidUpgradeName"; //The upgrade name for the Jump Drive Inhibitor Effect. Increase/decrease by a floating point number (eg: 0.1, 0.2, -0.1). Amount reduced is in MW.
                Settings.UpgradeHackEffectName = "ChangeToValidUpgradeName"; //The upgrade name for the Hacking Effect. Increase/decrease by a regular number (eg: 1, 2, -1, etc)
                Settings.UpgradeShieldEffectName = "ChangeToValidUpgradeName"; //The upgrade name for the Shield Buster Effect. Set to 1 to Enable on Attached Weapon.

                //Settings.UpgradeTractorEffectName = "ChangeToValidUpgradeName"; This isn't a thing yet ;)

                //Base Damage
                Settings.UseBaseDamage = true; //Specifies if beam should deal regular damage.
                Settings.BaseDamageAmount = 5500; //Damage amount per step (steps defined by DamageTimerLimit setting above)
                Settings.UsePenetrativeDamage = false; //If true, the beam will damage multiple blocks within a grid per step.
                Settings.PenetrativeDistance = 20; //Distance the penetrative damage can reach if enabled.
                Settings.RelaxedMissileIntercept = false; //If true, shots fired at lasers that come close to hitting will still register as a hit.

                //Explosive Damage
                Settings.UseExplosionDamage = false; //If true, beam will create an explosion each step
                Settings.ExplosionDamage = 0; //Explosion damage
                Settings.ExplosionRadius = 5.0f; //Explosion radius from where beam hits
                Settings.ExplosionForwardOffset = 0; //Forward OFfset From Hit Position

                //Voxel Damage
                Settings.UseVoxelDamage = false; //If true, the beam will cut out voxels at hit position each step.
                Settings.VoxelDamageRadius = 3; //Radius of voxels that are removed at beam hit position.

                //Voxel Paint
                Settings.UseVoxelPaint = false; //If true, the beam will paint voxels at hit position each step.
                Settings.VoxelPaintMaterial = "Ice_01"; //Material affected voxels will be replaced with.
                Settings.VoxelPaintRadius = 8; //Radius of voxels that are painted at beam hit position.

                //Voxel Add - Feature Not Complete
                Settings.UseVoxelAdd = false; //If true, the beam will add voxels at hit position each step.
                Settings.VoxelAddMaterial = "Ice_01"; //Material added voxels will use.
                Settings.VoxelAddRadius = 3; //Radius of voxels that are added at beam hit position.

                //Tesla Damage
                Settings.UseTeslaEffect = false; //If true, a beam hit on a grid will shut off a selection of random blocks.
                Settings.TeslaMaxBlocksAffected = 1; //maximum blocks affected by tesla effect

                //Jump Drive Damage
                Settings.UseJumpDriveInhibitor = false; //If true, a beam hit on a grid will drain stored energy on Jump Drives
                Settings.AmountToReduceDrives = 0.3f; //Amount of energy to reduce from Jump Drives (in MW).
                Settings.SplitAcrossEachDrive = true; //If true, the amount to reduce is evenly split across all jump drives on the grid, otherwise the amount is reduced per drive.

                //Shield Damage
                Settings.UseShieldBuster = false; //if true, a beam hit on a grid will apply damage (as percentage of Shield Total Capacity) to any grid shields.
                Settings.ShieldDamagePercentage = 25; //Percentage of Shield Total Capacity that is damaged.

                //Hacking Damage
                Settings.UseHackingDamage = false; //if true, a beam hit on a grid will cause a random selection of blocks to be converted to the beam owners
                Settings.HackingMinBlocksAffected = 1; //minimum blocks affected by hacking effect
                Settings.HackingMaxBlocksAffected = 2; //maximum blocks affected by hacking effect

                //Painter Damage
                Settings.UsePainterDamage = false; //If true, a beam hit on a grid will recolor the block it makes contact with using the color of the laser block.
                Settings.RandomPaintColor = false; //If true, and if UsePainterDamage is true, a random color will be used on target blocks instead.

                //Physics Push
                Settings.UsePhysicsPush = false; // If true, a hit grid will get pushed or pull away from your weapon position/direction.
                Settings.PhysicsPushForce = 30000; //Force that each weapon hit will push/pull target by. Higher than 0 is push, lower than 0 is pull
                Settings.ApplyToCenterOfMass = false; //If true, the push/pull force will be applied to the grid center of mass, which eliminates most rotation as a result of the physics event.
                Settings.ReverseForceWithinDistance = -1; //If the distance to the hit target is within this many meters, then the value of PhysicsPushForce is reversed.

                //Speed Reduction
                Settings.UseSpeedReduction = false; //If true, hit grids will have their velocity reduced each hit.
                Settings.SpeedReductionForce = 2000000; //Force that each weapon hit will reduce speed by
                Settings.MinimumTargetSpeed = 15; //Target Speed will not be reduced below this value

                //Clang Cannon
                Settings.UseClangCannonEffect = false; //If true, hit blocks will dismount from their parent grid.

                //DefenseShieldMod Options
                Settings.BypassBubble = false; //If true, the beam will ignore the physical bubble of the Defense Shield mod (shield damage modifier may still apply).

                //Sound Settings
                Settings.FiringSoundId = "buzzz"; //You can specify an AudioDefinition subtype ID that will play when the weapon is fired.

                //Beam Effect
                Settings.UseRegularBeam = true; //if true, a straight laser beam will be drawn from the weapon barrel, 
                Settings.UseBeamFlicker = true; //If true, the beam will not use BeamRadius, but rather random values between BeamMinimumRadius and BeamMaximumRadius
                Settings.BeamRadius = 0.1f; //The beam radius if UseBeamFlicker is false
                Settings.BeamMinimumRadius = 0.1f; //Minimum Random Beam Radius if UseBeamFlicker is true
                Settings.BeamMaximumRadius = 0.15f; //Maximum Random Beam Radius if UseBeamFlicker is true
                Settings.BeamColors.Add(Color.White);  //The color of the bolt. Copy this line to use other colors in the bolt.
                Settings.BeamColors.Add(Color.Red);
                Settings.FadeThroughColors = true; //If true, beam color will not be randomized. The color will fade from one color to the next in the list you provide (requires at least 2 colors to use)

                //Tesla Effect
                Settings.UseTeslaBeam = false; //If true, an electric bolt effect will be fired from the barrels of the weapon.
                Settings.UseTeslaBeamFlicker = false; //If true, the beam will not use TeslaBeamRadius, but rather random values between TeslaBeamMinimumRadius and TeslaBeamMaximumRadius
                Settings.TeslaBeamMaxLateral = 3; //The max lateral distance of the bolt effect
                Settings.TeslaBeamMinStep = 3; //Minimum distance of bolt arc forward
                Settings.TeslaBeamMaxStep = 7; //Maximum distance of bolt arc forward
                Settings.TeslaBeamRadius = 0.3f; //Radius of bolt beam if UseTeslaBeamFlicker is false
                Settings.TeslaBeamMinimumRadius = 0.3f; //Minimum Random Beam Radius if UseTeslaBeamFlicker is true
                Settings.TeslaBeamMaximumRadius = 0.6f; //Maximum Random Beam Radius if UseTeslaBeamFlicker is true
                Settings.TeslaBeamColors.Add(Color.White); //The color of the bolt. Copy this line to use other colors in the bolt.

                //Particle Barrel Settings
                Settings.UseBarrelParticles = false; //If true, a particle is created at the barrel position when fired.
                Settings.BarrelParticleName = "Warp"; //ID of the barrel particle ID
                Settings.BarrelParticleScale = 1f; //Size multiplier of the barrel particle
                Settings.BarrelParticleColor = new Vector4(1, 1, 1, 1); //Color of the barrel particle.
                Settings.LoopBarrelAfterTicks = 20; //After this many ticks, the barrel particle animation resets

                //Particle Hit Settings
                Settings.UseHitParticles = true; //if true, a particle will be created when the beam hits a target.
                Settings.UseParticleAfterRayCount = 5; //Particle is created after this many raycasts - this helps reduce particle spam and increases performance.
                Settings.ParticleName = "BlockDestroyedExplosion_Tiny"; //SubtypeId of the particle you want to display
                Settings.ParticleColor = new Vector4(1, 1, 1, 1); //RBGA to change the particle color. Range from 0-1 (if using a floating point value, add f as suffix - eg: 0.5f). Use 0,0,0,0 for default
                Settings.ParticleScale = 1.0f; //Size multiplier of particles created.
                Settings.UseHitParticleMaxDuration = false; //If true, particle will only play up until time specified below.
                Settings.HitParticleMaxDuration = 0.15f; //Time until particle stops playing (in seconds // 1 is 1 second)

                //Do Not Edit This Line
                return Settings;

            }

            #endregion
           
            /////////////// --- Copy To Here And Paste Below This Line --- ///////////////////

            ////////////////////////////////////////////////////////
            //////////////// DO NOT EDIT BELOW HERE ////////////////
            ////////////////////////////////////////////////////////

            return new WeaponConfig();
			
		}
	}
}