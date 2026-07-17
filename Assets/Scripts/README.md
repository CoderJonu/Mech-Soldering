# VR PCB solder-point capture

The scene automatically uses the existing camera as an XR player rig. Move physically with the headset, use the **left thumbstick** to walk, and use the **right thumbstick** to snap-turn. Visible blue (left) and orange (right) controller models follow the real controllers.

Press the right controller trigger while pointing at the virtual green PCB to add a solder point. The point is converted from the Unity board surface to millimetres with the lower-left PCB corner as `(X0, Y0)`.

For an editor demo, left-click a point on the PCB. Press `Backspace`/`Delete` to remove the last point, or press `E` to export.

The generated file is written to Unity's `Application.persistentDataPath` as `vr_pcb_solder_points.gcode.txt`; Unity prints the exact path in its Console. The G-code moves to each selected point, lowers to the configured solder height, dwells, and raises again. Configure height, feed-rate, dwell and tool-on/off commands on **VR PCB Soldering Manager** before using it with a real soldering machine.

This exporter does not know your machine's limits, coordinate calibration, solder-dispenser/heater commands, or emergency-stop protocol. Validate it in a simulator or above an unpowered test board before commanding hardware.
