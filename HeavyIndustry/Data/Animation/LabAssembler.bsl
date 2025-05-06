@BlockID "LabAssembler"
@Version 2
@Author b

using lab as subpart("lab")

#--- Animations
func play() {
	lab.rotate([0, 1, 0], 30, 60, Linear)
}

action block() {
	working() {
		api.startloop("play", 60, 99999)
	}
	notworking() { api.stoploop("play") }
}