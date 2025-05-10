@BlockID "ConveyorSplitter1W"
@Version 2
@Author b

using s1 as Subpart("splitterspinner")
using s2 as Subpart("splitterspinner.001")

#--- Animations
func cycle() {	
	s1.rotate([1, 0, 0], 1, 1, InOutCubic)
	s2.rotate([1, 0, 0], -1, 1, InOutCubic)
}

func reset() {
	s1.reset()
	s2.reset()
}

action block() {
	create() {
		api.startloop("cycle", 1, 99999999)
	}
}