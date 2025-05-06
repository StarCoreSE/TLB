@BlockID "InserterExtractor"
@Version 2
@Author b

using arm as Subpart("arm")

var CargoStorage = 1
var ArmMoving = false

#--- Animations
func cycle() {
	reset()
	ArmMoving = true
	arm.rotate([1, 0, 0], 30, 5, InOutCubic)
	arm.delay(15).rotate([0, 1, 0], -180, 15, InOutCubic)
	arm.delay(30).rotate([1, 0, 0], 30, 10, InOutCubic)
	arm.delay(45).rotate([1, 0, 0], -30, 5, InOutCubic)
	arm.delay(60).rotate([0, 1, 0], -180, 15, InOutCubic)
	arm.delay(75).rotate([1, 0, 0], -30, 10, InOutCubic)
	ArmMoving = false
}

func reset() {
	arm.reset()
	arm.rotate([1, 0, 0], 30, 0, InOutCubic)
	ArmMoving = false
}

action block() {
    notworking() {
        reset()
    }
}

action Inventory() {
	Changed(CargoCapacity){
		if (CargoCapacity != CargoStorage && ArmMoving == false) { 
			cycle()
		}
	}
}