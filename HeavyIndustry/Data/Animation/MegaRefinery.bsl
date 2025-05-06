@BlockID "MegaRefinery"
@Version 2
@Author b

using smoke as Emitter("smoke")

var CargoStorage = 1
var particlePlaying = true

#--- Animations
func play() {
	if(particlePlaying == false){
		smoke.playparticle("Damage_HeavyMech_Damaged", 1, 99999)
	}
}

func stop() {
	particlePlaying = false
	smoke.stopparticle()
}

action block() {
	working() {
		play()
	}
    notworking() {
        stop()
    }
}

action Inventory() {
	Changed(CargoCapacity){
		if (CargoCapacity != CargoStorage) { 
			play()
		}
	}
}