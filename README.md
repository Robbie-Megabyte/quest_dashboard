# G1 Quest Viewer

Operator manual for the Meta Quest Pro mixed-reality interface used with the Unitree G1 robot and the SLAM vehicle.

> [!WARNING]
> This is laboratory research software, not a certified safety system.  
> Always keep the physical emergency-stop controls available, maintain a clear operating area, and be prepared to stop the robot independently of the Quest application.

## Contents

- [Purpose](#purpose)
- [System overview](#system-overview)
- [Safety rules](#safety-rules)
- [Starting the system](#starting-the-system)
- [User interface overview](#user-interface-overview)
- [Edit and Live modes](#edit-and-live-modes)
- [Grid and window management](#grid-and-window-management)
- [Available windows](#available-windows)
- [G1 teleoperation](#g1-teleoperation)
- [Controller pose hold and locomotion](#controller-pose-hold-and-locomotion)
- [Teleoperation safety faults](#teleoperation-safety-faults)
- [Autonomous agent goals](#autonomous-agent-goals)
- [Network configuration](#network-configuration)
- [Installing the Agents configuration](#installing-the-agents-configuration)
- [Installing the APK on another headset](#installing-the-apk-on-another-headset)
- [Ports and data flows](#ports-and-data-flows)
- [Troubleshooting](#troubleshooting)
- [Diagnostic commands](#diagnostic-commands)
- [Known limitations](#known-limitations)
- [Development reference](#development-reference)

## Purpose

G1 Quest Viewer is a mixed-reality operator interface for:

- viewing live Unitree G1 telemetry;
- viewing the live G1 digital twin;
- viewing robot and external camera feeds;
- visualizing saved and live SLAM point clouds;
- entering and leaving G1 arm teleoperation safely;
- freezing the G1 arm pose while switching from hand tracking to controllers;
- driving the G1 base while its arms remain frozen;
- viewing the G1 and SLAM vehicle in a shared map;
- selecting and previewing autonomous goals;
- confirming autonomous movement for the G1 or SLAM vehicle.

The Quest application is a user interface. It does not start the required robot-side services and does not replace the physical safety controls.

## System overview

The complete system has three main parts:

1. **Meta Quest Pro application**
   - Displays the mixed-reality interface.
   - Receives camera, robot-state, telemetry, SLAM, map, and agent data.
   - Sends authenticated teleoperation and locomotion requests.

2. **G1 dashboard and teleoperation backend**
   - Normally available on port `8080`.
   - Supplies telemetry, teleoperation state, safety state, and SLAM data.
   - Sends robot and camera UDP streams to the configured headset IP.
   - Receives signed Quest locomotion and teleoperation-action packets.

3. **Autonomous G1 and vehicle backend**
   - Normally available on port `3003`.
   - Supplies G1 and vehicle poses, maps, scans, point clouds, and planned paths.
   - Accepts preview and goal requests for both agents.

The headset and robot computers must be reachable on the same network.

## Safety rules

Before operating either agent:

- Clear people, chairs, cables, tables, and loose objects from the planned path.
- Keep the physical stop control with a separate safety operator whenever possible.
- Confirm that the displayed pose corresponds to the real robot.
- Do not rely exclusively on the digital twin, point cloud, camera image, or planned path.
- Never send an autonomous goal based on stale data.
- Always request and inspect a path preview before holding `GO`.
- Do not run autonomous navigation and manual locomotion at the same time.
- Stop unnecessary robot-side processes if CPU load introduces control or visualization latency.
- Treat an offline indicator or stale telemetry message as a loss of trustworthy state.

Important behavior:

- `CLEAR` in the Agents window only clears the selected goal and preview. It does **not** stop an agent that is already moving.
- `STOP TELEOP` requests a controlled arm handback. The arms may move while ownership returns to the normal controller.
- Releasing a teleoperation safety fault may move the arms and open the fingers.
- A fault-held pose should only be released after confirming that the arms, hands, held object, table, and surrounding area are clear.
- The Quest application must never be treated as an emergency-stop device.

## Starting the system

### 1. Prepare the area

- Position the G1 in a clear area.
- Confirm that the robot is stable.
- Make sure the physical stop controls work.
- Confirm that the headset and robot are on the same network.

### 2. Start the G1 dashboard/backend

Current laboratory example:

```bash
cd "$HOME/xr_teleoperate_g1demo/dashboard/step4_8/g1_dashboard_step4_8_inspire_hand_twin_v1_2"

export G1_QUEST_TELEMETRY_IP="192.168.0.183"
export G1_QUEST_TELEMETRY_PORT="5055"
export G1_QUEST_TELEMETRY_INTERFACE="enP8p1s0"
export G1_QUEST_TELEMETRY_HZ="30"
export G1_DASHBOARD_WEB_DERIVED_FPS="${G1_DASHBOARD_WEB_DERIVED_FPS:-30}"
export G1_UNITY_TELEVUER_MOTION_ENABLED=1

G1_UNITY_TELEVUER_MOTION_ENABLED=1 \
G1_UNITY_TELEVUER_STABLE_FRAMES=4 \
G1_UNITY_TELEVUER_STALE_TIMEOUT_S=0.50 \
./start_dashboard.sh
```

`G1_QUEST_TELEMETRY_IP` must be the IP address of the headset currently being used.

If a different headset is used, update this address before starting the dashboard.

### 3. Start the autonomous backend

Start the autonomous G1/vehicle dashboard from the appropriate robot-side repository.

Verify that these endpoints are available on port `3003`:

- `/api/agents/status`
- `/api/agents/world`
- `/api/robot/path/preview`
- `/api/robot/goal`
- `/api/car/path/preview`
- `/api/car/goal`

### 4. Open the Quest application

The application starts in **Edit mode**. Arrange the windows or select a preset, then switch to **Live mode** for normal operation.

## User interface overview

The interface is placed on a curved surface around the operator.

The bottom control bar contains:

| Control | Purpose |
|---|---|
| `MENU` | Opens the list of available windows. |
| `PRESETS` | Opens the built-in layout presets. |
| `LIVE` | Leaves Edit mode and locks the layout. |
| `EDIT` | Returns to Edit mode when teleoperation is not active. |
| `HIDE UI` | Hides view-control overlays while in Live mode. |
| `SHOW UI` | Restores the hidden view-control overlays. |

A button label generally describes what pressing it will do. For example, `LIVE` means “enter Live mode.”

The permanent teleoperation control is displayed above the grid in Live mode.

## Edit and Live modes

### Edit mode

Edit mode is used to configure the interface.

In Edit mode:

- the layout grid is visible;
- windows can be opened and closed;
- windows can be moved;
- windows can be resized;
- presets can be applied;
- headers, close buttons, resize handles, and grab handles are visible;
- viewport dragging is reserved for moving windows rather than rotating their contents.

### Live mode

Live mode is used for operation.

In Live mode:

- the grid is hidden;
- window positions are locked;
- edit controls are hidden;
- SLAM, Robot, and Agents view controls become interactive;
- the teleoperation button becomes available;
- `HIDE UI` can hide the pan, rotate, zoom, and reset controls.

Edit mode is blocked while:

- teleoperation is active;
- teleoperation is entering or leaving;
- the arms are pose-held;
- the operator is realigning;
- a teleoperation safety fault is active.

If any of these states begin while Edit mode is active, the app automatically returns to Live mode.

## Grid and window management

The current interface uses a curved:

- `16 × 16` cell grid;
- `90°` horizontal span;
- `70°` vertical span;
- `-5°` center pitch;
- `2 m` radius.

### Moving a window

1. Enter Edit mode.
2. Grab the window header or grab handle.
3. Move the window toward the desired location.
4. Release it.

The window snaps to the nearest valid grid position.

The grid prevents windows from occupying the same cells. If the requested position is invalid, the system attempts to restore the previous placement or find another free placement.

### Resizing a window

1. Enter Edit mode.
2. Grab an edge or corner resize handle.
3. Move the handle.
4. Release it at the required size.

A resize is accepted only if it remains inside the grid and does not overlap another window.

Shared-boundary resize support allows adjacent windows to be resized along their common boundary.

### Closing and reopening a window

- Press the window’s `X` button in Edit mode to close it.
- Press `MENU`.
- Select the window from the list to open it again.

Closing a window removes it from the layout but does not stop its robot-side service.

### Presets

Current built-in presets are:

| Preset | Layout |
|---|---|
| `Default Camera` | Large Camera window with SLAM and Robot windows below it. |
| `Side Camera` | Large Camera window with Robot and SLAM windows on the side. |
| `Agents` | Large Agents planning and visualization window. |

Applying a preset replaces the active managed-window layout.

The internal preset system supports custom preset storage, but the current operator interface does not expose a complete custom-preset creation workflow.

## Available windows

### Camera window

The Camera window displays camera and locally rendered sensor views.

Available views are:

- `RGB`
- `DEPTH`
- `OVERLAY`
- `DISPARITY`
- `POINT CLOUD`
- `LIFECAM`

Select a hotbar item to make that stream the main view.

#### YOLO

The `YOLO` toggle asks the robot-side camera service to enable its shared object-detection pipeline.

YOLO availability depends on the backend being started correctly. Enabling the toggle does not start the detection service by itself.

Camera transport uses:

- UDP `5056` for image and point-cloud data sent to the Quest;
- UDP `5057` for robot-camera subscription control;
- UDP `5058` for external LifeCam subscription control.

### SLAM window

The SLAM window displays:

- the saved 3D PCD map;
- the live 3D lidar cloud;
- the G1 visualization aligned with the map.

The saved map is downloaded from:

```text
http://192.168.0.116:8080/api/slam/map
```

The live cloud is polled from:

```text
http://192.168.0.116:8080/api/slam/cloud
```

Controls:

| Control | Action |
|---|---|
| `ROTATE` | Dragging rotates the view. |
| `PAN` | Dragging moves the view target. |
| `+` | Zooms in. |
| `-` | Zooms out. |
| Double-click | Resets the view. |

The button displays the active drag mode. Press it to switch between `PAN` and `ROTATE`.

### Robot window

The Robot window displays the live G1 mesh.

Joint telemetry is received over UDP port `5055`. The window renders the robot separately with balanced lighting, so the model remains readable from multiple directions.

Controls:

| Control | Action |
|---|---|
| `ROTATE` | Dragging rotates around the robot. |
| `PAN` | Dragging moves the view target. |
| `+` | Zooms in. |
| `-` | Zooms out. |
| Reset control | Restores the default camera position. |

The Robot and SLAM viewport interactions are available in Live mode. In Edit mode, dragging is used to move the windows.

### G1 Telemetry window

The telemetry window obtains data from:

```text
http://192.168.0.116:8080/api/unity/telemetry
```

It contains cards for:

- `CONNECTION`
- `MOTION`
- `ARMS`
- `MOTORS`
- `HANDS & TRACKING`
- `COMPUTER`

The status strip distinguishes between:

- robot online;
- computer online but robot telemetry offline;
- dashboard telemetry offline;
- tracking hold;
- safety fault.

Telemetry is intended for operator awareness. It should not be used as the only safety indication.

### Agents window

The Agents window presents the G1 and SLAM vehicle in a shared map coordinate system.

It can display:

- the live G1 pose;
- the live SLAM vehicle pose;
- the real G1 mesh;
- the scanned SLAM vehicle mesh;
- online/offline nameplates;
- a static 3D point cloud;
- the vehicle’s 2D map;
- the vehicle’s live laser scan;
- G1 and vehicle planner paths;
- selected goal position and heading.

#### Agent colors

| Element | Color |
|---|---|
| G1 nameplate, selection, and route | Bright green |
| Vehicle nameplate, selection, scan, and route | Orange |
| Vehicle 2D map | Red |
| Static 3D PCD | Height-based multicolor scheme |
| Offline status dot | Red |
| Online status dot | Green |

#### Map controls

The top-left controls are:

- `PCD ON` / `PCD OFF`
- `2D MAP ON` / `2D MAP OFF`

These toggle the static 3D point cloud and the vehicle 2D map independently.

#### View controls

The top-right controls are:

- `PAN` / `ROTATE`
- `+`
- `-`
- `RESET`

View manipulation is temporarily disabled while `SET GOAL` is active, because pointer dragging is then reserved for choosing a position and heading.

#### Agent controls

The bottom controls are:

| Control | Purpose |
|---|---|
| `G1` | Selects the G1 as the target agent. |
| `VEHICLE` | Selects the SLAM vehicle. |
| `SET GOAL` | Starts goal-position and heading selection. |
| `PREVIEW` | Requests a route without starting movement. |
| `HOLD GO` | Confirms a valid preview after a one-second hold. |
| `GO LOCKED` | Indicates that motion confirmation is disabled by configuration. |
| `CLEAR` | Clears the selected goal and preview. It does not stop an agent. |

The G1 route is green. The vehicle route is orange.

The Quest receives vehicle SLAM and autonomous-agent data through the HTTP backend on port `3003`. There is no separate Quest UDP port dedicated to SLAM vehicle data.

## G1 teleoperation

Teleoperation is available only in Live mode.

### Entry requirements

The app will enable teleoperation entry only when:

- dashboard telemetry is fresh;
- the robot/controller connection is online;
- the controller reports a locomotion-ready state;
- the Quest action-request channel is available;
- LowState is valid;
- XR tracking is acceptable;
- no safety fault is active;
- the movement controls are neutral;
- the deadman control is released;
- readiness remains stable for the required interval.

If entry is unavailable, the top button displays `TELEOP LOCKED` and an XR status. The locked button can still be pressed to show the exact reason.

### Entering teleoperation

1. Enter Live mode.
2. Make sure both sticks are centered.
3. Release the locomotion deadman.
4. Wait for the top button to show `ENTER TELEOP`.
5. Press `ENTER TELEOP`.
6. Read the confirmation panel.
7. Hold `HOLD ENTER` for one second.
8. Keep your hands aligned with the robot during the three-second countdown.
9. Wait until the top button changes to `STOP TELEOP`.

Do not move abruptly during the alignment countdown.

### Leaving teleoperation

Press `STOP TELEOP` once.

This sends a controlled `HAND_BACK_ARMS` request. The frozen target remains held while arm ownership fades back to the regular controller.

The arms may move during handback. Confirm that they have sufficient clearance before pressing `STOP TELEOP`.

## Controller pose hold and locomotion

The application supports switching from hand tracking to the Quest Touch Pro controllers without leaving teleoperation.

This is intended for workflows such as:

1. Use hand teleoperation to pick up or position an object.
2. Hold a stable robot pose.
3. Pick up both Quest controllers.
4. Let the app freeze the robot’s arm and finger targets.
5. Drive the robot base using the controllers while the upper-body target remains held.
6. Place the controllers back on the table.
7. Let the headset return to hand tracking.
8. Realign your hands with the held robot pose.
9. Resume normal hand teleoperation.

### Pose-held state

When controller input becomes active during teleoperation, the app requests pose hold.

The top button shows:

```text
STOP TELEOP
POSE HELD
```

During pose hold:

- arm targets remain frozen;
- finger targets remain held;
- ordinary arm tracking does not resume;
- base locomotion can be armed after controller tracking and neutral-input checks pass.

### Controller locomotion

Current control mapping:

- left stick forward/back: forward and backward motion;
- left stick left/right: lateral motion;
- right stick left/right: yaw rotation;
- left grip: locomotion deadman.

The robot should move only while the locomotion gate is open and the deadman is held.

Release the deadman before changing input modality or setting down the controllers.

### Resuming hand teleoperation

When the controllers are set down and hand tracking returns:

- base movement is blocked;
- the state changes to hand realignment;
- the arms remain at the held target;
- the operator must align their hands with the held robot pose;
- teleoperation resumes only after the tracking checks pass.

Do not force a large correction. Move into alignment gradually.

## Teleoperation safety faults

A safety fault changes the teleoperation interface to:

```text
SAFETY FAULT · ARMS HELD
```

The fault may be caused by conditions such as:

- invalid or stale robot state;
- sustained unexpected lower-body motion during XR control;
- tracking failure;
- a controller safety gate;
- excessive disagreement between expected and measured state.

The exact reason is displayed in the panel and telemetry window.

### Fault behavior

During the fault:

- arms remain held at the frozen target;
- fingers remain held;
- automatic arm handback is not performed;
- Edit mode remains blocked;
- normal teleoperation cannot resume immediately.

This prevents the robot from automatically moving its arms toward a default pose near a table, object, or person.

### Keeping the pose held

Press `KEEP HELD` to close the panel without releasing arm ownership.

Use this option if it is not yet safe for the arms or fingers to move.

### Releasing the held pose

Only release the fault when:

- the robot has stopped;
- the stop gate reports ready;
- the surrounding area is clear;
- the arms have a safe handback path;
- opening the fingers is safe;
- dropping a held object is safe.

Then:

1. Open the fault panel.
2. Confirm that the release control is enabled.
3. Hold `HOLD RELEASE` for one second.
4. Observe the robot throughout the handback.

The panel explicitly warns:

```text
RELEASE MAY MOVE ARMS · FINGERS WILL OPEN
```

A listener restart should not be the normal fault-recovery procedure. Preserve the held pose until the operator decides that controlled release is safe.

## Autonomous agent goals

Use the following sequence for either G1 or the vehicle.

### 1. Select the agent

Press:

- `G1`, or
- `VEHICLE`.

The selected button uses the agent’s nameplate color.

### 2. Select a goal

1. Press `SET GOAL`.
2. Point at the desired floor position.
3. Press and drag toward the desired final heading.
4. Release to confirm the pose.

The first point determines position. The drag direction determines yaw.

### 3. Request a preview

Press `PREVIEW`.

A preview does not command movement.

For G1, the Quest calls:

```text
POST /api/robot/path/preview
```

For the vehicle, it calls:

```text
POST /api/car/path/preview
```

The planned path is rendered in the corresponding agent color.

### 4. Inspect the route

Before confirming:

- make sure the selected agent is correct;
- check the start and goal positions;
- inspect the complete route;
- verify that the real environment is clear;
- make sure the pose and map are current;
- verify that no other controller is commanding the same agent.

### 5. Confirm movement

Hold `HOLD GO` for one second.

For G1, the preview identifier is sent to:

```text
POST /api/robot/goal
```

For the vehicle, the goal is sent to:

```text
POST /api/car/goal
```

The G1 backend controls the actual navigation speed. The speed is not selected from the Quest interface.

### Preview invalidation

A preview may become invalid if:

- it expires;
- the agent goes offline;
- the selected goal changes;
- the selected agent changes;
- the robot moves too far from the preview start;
- a new preview replaces the old one;
- the backend map or planner state changes.

If confirmation fails, clear the goal and request a new preview.

### Clearing a goal

`CLEAR` removes:

- the goal marker;
- the selected heading;
- the local preview state;
- the displayed planner path.

`CLEAR` does not cancel navigation that has already started.

Use the appropriate robot-side stop mechanism to stop an agent in motion.

## Network configuration

Current default addresses compiled into the scene are:

| Service | Address |
|---|---|
| G1 dashboard telemetry and SLAM | `http://192.168.0.116:8080` |
| Agents and autonomous navigation | `http://192.168.0.116:3003` |
| Quest locomotion/action destination | `192.168.0.116:5059` |
| Expected robot UDP sender | `192.168.0.116` |

The Agents base URL can be overridden without rebuilding the APK by installing `g1_agents_connection.json`.

Other addresses are currently scene or script configuration and may require a new build if the robot address changes.

## Installing the Agents configuration

The application reads:

```text
g1_agents_connection.json
```

from `Application.persistentDataPath`.

For the current Android package, the accessible location is:

```text
/sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/g1_agents_connection.json
```

Example contents:

```json
{
  "base_url": "http://192.168.0.116:3003",
  "token": "REPLACE_WITH_DASHBOARD_TOKEN"
}
```

Do not commit a real dashboard token to Git.

Example installation:

```bash
PACKAGE="com.UnityTechnologies.com.unity.template.urpblank"
ROBOT="unitree@192.168.0.116"

TOKEN="$(
    ssh "$ROBOT" \
    'head -n 1 "$HOME/Robbie_Megabyte/dashboard_robo_car_nav2/.dashboard_token"'
)"

CONFIG="/tmp/g1_agents_connection.json"

umask 077
printf \
'{"base_url":"http://192.168.0.116:3003","token":"%s"}\n' \
"$TOKEN" >"$CONFIG"

adb shell am force-stop "$PACKAGE"

adb shell mkdir -p \
"/sdcard/Android/data/$PACKAGE/files"

adb push \
"$CONFIG" \
"/sdcard/Android/data/$PACKAGE/files/g1_agents_connection.json"

adb shell ls -l \
"/sdcard/Android/data/$PACKAGE/files/g1_agents_connection.json"
```

The production APK is not debuggable, so `adb shell run-as` is not expected to work.

## Installing the APK on another headset

1. Connect the second headset through ADB.
2. Install the APK:

```bash
adb install -r /path/to/G1_Quest_Viewer.apk
```

3. Open and close the application once so Android creates its application data directory.
4. Install `g1_agents_connection.json`.
5. Determine the second headset’s network IP.
6. Change `G1_QUEST_TELEMETRY_IP` to the second headset’s IP.
7. Restart the robot dashboard.
8. Open the application and verify telemetry, robot visualization, camera streams, and Agents connectivity.

The HTTP endpoints can normally be read by more than one headset, but the current dashboard launch configuration sends unicast UDP streams to one configured Quest IP. Changing headsets therefore normally requires restarting the dashboard with the other headset’s IP.

## Ports and data flows

| Port | Direction relative to Quest | Purpose |
|---|---|---|
| TCP `8080` | Quest → robot/dashboard | Telemetry and SLAM HTTP endpoints |
| TCP `3003` | Quest → autonomous backend | Agent status, maps, scans, previews, and goals |
| UDP `5055` | Robot → Quest | G1 joint/full-body visualization telemetry |
| UDP `5056` | Robot → Quest | Camera images and camera point-cloud payloads |
| UDP `5057` | Quest → robot | Robot-camera subscription control |
| UDP `5058` | Quest → robot | External LifeCam subscription control |
| UDP `5059` | Quest → robot | Signed locomotion packets and teleoperation actions |

The SLAM vehicle does not have a separate Quest receive port. Its pose, 2D map, laser scan, point cloud, and planner data are obtained from the HTTP service on port `3003`.

## Troubleshooting

### Dashboard telemetry offline

Check:

```bash
curl -fsS \
"http://192.168.0.116:8080/api/unity/telemetry" |
python3 -m json.tool
```

If this fails:

- confirm that the G1 dashboard is running;
- confirm that port `8080` belongs to the correct dashboard;
- check the network route between the Quest and robot;
- inspect dashboard logs.

### Agents show offline

Check:

```bash
ROOT="$HOME/Robbie_Megabyte/dashboard_robo_car_nav2"
TOKEN="$(head -n 1 "$ROOT/.dashboard_token")"

curl -fsS \
-H "X-G1-Token: $TOKEN" \
"http://127.0.0.1:3003/api/agents/status" |
python3 -m json.tool
```

Also verify:

- the correct configuration file is installed on the Quest;
- the token is current;
- port `3003` is served by the correct backend;
- localization or odometry is publishing fresh data;
- the G1 or vehicle stack is actually running.

A successful HTTP connection does not mean an individual agent is online. Each agent needs fresh pose data.

### HTTP 404 from the Quest

A different team or application may be serving the same port.

Check the current API:

```bash
curl -fsS \
-H "X-G1-Token: $TOKEN" \
"http://127.0.0.1:3003/openapi.json" |
python3 -m json.tool
```

Confirm that the expected routes exist.

### Preview reports an unexpected response

Check the raw endpoint response directly.

For G1:

```bash
curl -sS \
-X POST \
-H "Content-Type: application/json" \
-H "X-G1-Token: $TOKEN" \
--data '{"x":1.0,"y":1.0,"yaw_deg":0.0,"speed":0.22}' \
"http://127.0.0.1:3003/api/robot/path/preview" |
python3 -m json.tool
```

A successful G1 preview should contain:

- `success: true`;
- `preview_id` or `request_id`;
- `route.points` with at least two valid points.

### HOLD GO cannot be used

Possible causes:

- no goal has been selected;
- the selected agent is offline;
- the preview failed;
- the vehicle path revision has not updated;
- the preview expired;
- the command-confirmation safety option is disabled;
- another request is still in progress.

Request a new preview after correcting the problem.

### No map or point cloud

For SLAM on port `8080`, verify:

```bash
curl -I \
"http://192.168.0.116:8080/api/slam/map"
```

For Agents world data on port `3003`, inspect:

```bash
curl -fsS \
-H "X-G1-Token: $TOKEN" \
"http://127.0.0.1:3003/api/agents/world" |
python3 -m json.tool
```

Check for nonzero:

- point-cloud point count;
- vehicle map point count;
- scan point count;
- map and scan revisions.

### Controllers or buttons do not work

- Confirm that the Quest application has focus.
- Wake both controllers.
- Confirm that controller tracking works in the Quest system interface.
- Restart the application if input modality did not recover.
- Confirm that the OpenXR build includes the Meta Quest Touch Pro Controller profile.
- Remember that window-content dragging is disabled in Edit mode.
- Remember that window editing is disabled during teleoperation and safety transitions.

### Teleoperation entry remains locked

Press the locked teleoperation button and read the displayed reason.

Typical causes include:

- stale telemetry;
- controller process not running;
- action request channel disabled;
- LowState invalid;
- XR tracking invalid;
- safety fault active;
- stick input not centered;
- deadman still held;
- controller not in the required state.

### Arms remain held after a fault

This is intentional.

The application no longer performs an automatic handback after a safety fault. Press `KEEP HELD` until the area is safe. Then satisfy the release conditions and hold `HOLD RELEASE`.

### Robot or command ghost has visible latency

Check robot-side CPU load before changing network or smoothing settings.

Common causes include:

- multiple dashboards running simultaneously;
- RViz or other visualization processes;
- autonomous navigation and mapping load;
- camera encoding or YOLO inference;
- other teams running heavy processes on the G1 computer.

Stop unnecessary processes and test again.

## Diagnostic commands

### Quest process

```bash
PACKAGE="com.UnityTechnologies.com.unity.template.urpblank"
adb shell pidof "$PACKAGE"
```

### Relevant Quest logs

```bash
adb logcat -d -v time |
grep -F \
-e '[G1 Agents Goal]' \
-e '[G1 Agents Planner]' \
-e '[G1 Agents Live]' \
-e '[G1 Agents World]' \
-e '[G1 Dashboard Telemetry]' \
-e '[G1 Locomotion TX]' |
tail -250
```

### Check robot ports

Run on the robot:

```bash
ss -ltnup |
grep -E ':(8080|3003|5055|5056|5057|5058|5059)\b' ||
true
```

### Check Agents route availability

```bash
curl -fsS \
-H "X-G1-Token: $TOKEN" \
"http://127.0.0.1:3003/openapi.json" |
python3 -c '
import json, sys
document = json.load(sys.stdin)
for path in sorted(document.get("paths", {})):
    if any(word in path for word in ("agent", "robot", "car")):
        print(path)
'
```

## Known limitations

- The application assumes the current laboratory network addressing unless a configurable override is provided.
- Only the Agents HTTP base URL and token have a runtime configuration file.
- The dashboard normally streams UDP data to one configured headset IP.
- The application does not start or stop the robot-side dashboard, controller, SLAM, localization, camera, or navigation processes.
- `CLEAR` does not stop autonomous movement.
- The Quest application currently has no general autonomous emergency-stop button.
- Custom preset storage exists internally, but complete custom-preset editing is not exposed to the operator.
- UDP camera or telemetry packets may be lost under heavy CPU or network load.
- Displayed data can remain visually plausible briefly after a source becomes stale; always check the status indicators.
- The G1 navigation speed is controlled by the backend rather than by the Quest UI.
- The application package identifier is still the Unity template identifier.

## Development reference

Current project settings:

| Item | Value |
|---|---|
| Unity version | `6000.5.9f1` |
| Platform | Android |
| XR runtime | OpenXR / Meta Quest |
| Package ID | `com.UnityTechnologies.com.unity.template.urpblank` |
| Bundle version | `0.1.0` |
| Main scene | `Assets/Scenes/SampleScene.unity` |

Important scripts include:

- `G1QuestTeleopModeCoordinator.cs`
- `G1QuestTeleopUiController.cs`
- `G1QuestLocomotionSender.cs`
- `G1DashboardTelemetryClient.cs`
- `G1CameraUdpReceiver.cs`
- `G1CameraWindowController.cs`
- `G1RobotWindowView.cs`
- `G1SlamWindowView.cs`
- `G1TelemetryWindowView.cs`
- `G1AgentsLivePoseClient.cs`
- `G1AgentsWorldClient.cs`
- `G1AgentsGoalPicker.cs`
- `G1AgentsPlannerController.cs`
- `HudModeManager.cs`
- `HudGridManager.cs`
- `HudPresetManager.cs`

When changing safety behavior, test in this order:

1. local Unity validation;
2. Quest build with no robot motion;
3. robot powered but arms clear;
4. pose-hold entry and cancellation;
5. controlled handback;
6. controller locomotion with the robot elevated or in a clear area;
7. low-speed floor testing with a separate stop operator;
8. autonomous preview without confirmation;
9. short autonomous movement in a clear area.
