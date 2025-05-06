@Author "b"
@BlockID "CargoPad"
@Version 2

using Crate0  as Subpart("cargo_1")
using Crate1  as Subpart("cargo_2")
using Crate2  as Subpart("cargo_3")
using Crate3  as Subpart("cargo_4")
using Crate4  as Subpart("cargo_5")
using Crate5  as Subpart("cargo_6")
using Crate6  as Subpart("cargo_7")
using Crate7  as Subpart("cargo_8")
using Crate8  as Subpart("cargo_9")
using Crate9  as Subpart("cargo_010")
using Crate10 as Subpart("cargo_011")
using Crate11 as Subpart("cargo_012")
using Crate12 as Subpart("cargo_013")
using Crate13 as Subpart("cargo_014")
using Crate14 as Subpart("cargo_015")
using Crate15 as Subpart("cargo_016")
using Crate16 as Subpart("cargo_017")
using Crate17 as Subpart("cargo_018")
using Crate18 as Subpart("cargo_019")
using Crate19 as Subpart("cargo_020")
using Crate20 as Subpart("cargo_021")
using Crate21 as Subpart("cargo_022")
using Crate22 as Subpart("cargo_023")
using Crate23 as Subpart("cargo_024")

var CargoStorage = 1

func CargoCrates() {
	if (CargoStorage >= 0.04)  { Crate0.SetVisible(true) }  else { Crate0.SetVisible(false) }
	if (CargoStorage >= 0.08)  { Crate1.SetVisible(true) }  else { Crate1.SetVisible(false) }
	if (CargoStorage >= 0.12)  { Crate2.SetVisible(true) }  else { Crate2.SetVisible(false) }
	if (CargoStorage >= 0.16)  { Crate3.SetVisible(true) }  else { Crate3.SetVisible(false) }
	if (CargoStorage >= 0.20)  { Crate4.SetVisible(true) }  else { Crate4.SetVisible(false) }
	if (CargoStorage >= 0.24)  { Crate5.SetVisible(true) }  else { Crate5.SetVisible(false) }
	if (CargoStorage >= 0.28)  { Crate6.SetVisible(true) }  else { Crate6.SetVisible(false) }
	if (CargoStorage >= 0.32)  { Crate7.SetVisible(true) }  else { Crate7.SetVisible(false) }
	if (CargoStorage >= 0.36)  { Crate8.SetVisible(true) }  else { Crate8.SetVisible(false) }
	if (CargoStorage >= 0.40)  { Crate9.SetVisible(true) }  else { Crate9.SetVisible(false) }
	if (CargoStorage >= 0.44)  { Crate10.SetVisible(true) } else { Crate10.SetVisible(false) }
	if (CargoStorage >= 0.48)  { Crate11.SetVisible(true) } else { Crate11.SetVisible(false) }
	if (CargoStorage >= 0.52)  { Crate12.SetVisible(true) } else { Crate12.SetVisible(false) }
	if (CargoStorage >= 0.56)  { Crate13.SetVisible(true) } else { Crate13.SetVisible(false) }
	if (CargoStorage >= 0.60)  { Crate14.SetVisible(true) } else { Crate14.SetVisible(false) }
	if (CargoStorage >= 0.64)  { Crate15.SetVisible(true) } else { Crate15.SetVisible(false) }
	if (CargoStorage >= 0.68)  { Crate16.SetVisible(true) } else { Crate16.SetVisible(false) }
	if (CargoStorage >= 0.72)  { Crate17.SetVisible(true) } else { Crate17.SetVisible(false) }
	if (CargoStorage >= 0.76)  { Crate18.SetVisible(true) } else { Crate18.SetVisible(false) }
	if (CargoStorage >= 0.80)  { Crate19.SetVisible(true) } else { Crate19.SetVisible(false) }
	if (CargoStorage >= 0.84)  { Crate20.SetVisible(true) } else { Crate20.SetVisible(false) }
	if (CargoStorage >= 0.88)  { Crate21.SetVisible(true) } else { Crate21.SetVisible(false) }
	if (CargoStorage >= 0.92)  { Crate22.SetVisible(true) } else { Crate22.SetVisible(false) }
	if (CargoStorage >= 0.96)  { Crate23.SetVisible(true) } else { Crate23.SetVisible(false) }
}

action Block() {
	create() {
		CargoCrates()
	}

	built() {
		CargoCrates()
	}
}

action Inventory() {
	Changed(CargoCapacity) {
		if (CargoCapacity != CargoStorage) {
			CargoStorage = CargoCapacity
			CargoCrates()
		}
	}
}