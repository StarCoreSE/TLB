@BlockID "Drill"
@Version 2
@Author b

using rotator as Subpart("rotating_drill")
using piston as Subpart("piston_drill")

var CargoStorage = 1

#--- Animations
func cycle() {
	rotator.rotate([0, 1, 0], 120, 360, Linear)
	piston.delay(0).translate([0, 5, 0], 60, InOutCubic)
	piston.delay(60).translate([0, -5, 0], 60, InOutCubic)
}

func reset() {
	piston.reset()
	api.stoploop("cycle")
}

action block() {
    notworking() {
        reset()
    }
}

action Inventory() {
	Changed(CargoCapacity){
		if (CargoCapacity != CargoStorage) { 
			api.startloop("cycle", 120, 5)
		}
		else
		{
			api.stoploop("cycle")
		}
	}
}