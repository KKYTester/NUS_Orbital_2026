# Court Smasherz Unity 3D Starter

This folder adds a 3D Unity version of the Court Smasherz prototype.

The current scene is a Wii Tennis-inspired vertical split-screen view:

- Player 1 camera is on the left half.
- Player 2 camera is on the right half.
- Each camera follows from behind its own avatar.
- Phone motion rotates the avatar racquet.
- Characters move automatically toward the ball.

## Build The Scene

1. Open the Unity project: `My project`.
2. Wait for Unity to finish compiling scripts.
3. In the top Unity menu, click:

```text
Court Smasherz > Build 3D Prototype Scene
```

4. Unity will create and save:

```text
Assets/CourtSmasherz/Scenes/CourtSmasherz3D.unity
```

5. Press Play.

## Test Controls

The characters move automatically toward the ball. Phone controls are the main input path.

Keyboard testing is disabled by default. To use keyboard test shots, select `Game Manager` and enable `Enable Keyboard Test Shots` in the inspector.

Player 1:

- `A`: forehand
- `S`: backhand
- `D`: lob
- `F`: smash

Player 2:

- `J`: forehand
- `K`: backhand
- `L`: lob
- `;`: smash

After the match ends, press `R` to restart.

## Phone Motion Controls

1. Start the Node server from the main project folder:

```powershell
& "C:\Users\kumar\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" server/index.js
```

2. In Unity, open or rebuild `CourtSmasherz3D.unity`.
3. Press Play.
4. Unity will show a `Phone room` code near the top of the Game view.
5. On your phone, open:

```text
http://192.168.0.13:3000/controller.html
```

6. Enter the Unity room code.
7. Choose Player 1 or Player 2.
8. Tap `Join room`.
9. Tap `Enable motion sensors`.
10. Tap `Calibrate neutral paddle` after motion values appear.
11. Swing the phone when the ball reaches your paddle.

If browser motion sensors are blocked, use the fallback shot buttons on the phone. They send the same shot events to Unity.

How it works:

- Phone browser sends `shotEvent` to the Node server.
- Unity creates a room through `/unity/create-room`.
- Unity polls `/unity/events?roomCode=...`.
- `PhoneMotionHttpBridge` converts incoming shot events into `CourtSmasherzGameManager.ApplyShot(...)`.

## What Is Included

- 3D pickleball court
- Net and court lines
- Two auto-moving paddles
- 3D pickleball
- Ball movement and side bounces
- Hit timing window
- Forehand, backhand, lob, and smash
- Simplified scoring with win-by-two match point
- HUD score and status text

## Next Steps

1. Replace keyboard test shots with phone WebSocket shot events.
2. Add nicer paddle and character models.
3. Add sound effects and hit particles.
4. Tune ball speed and hit window after playtesting.
5. Add AI opponent mode if you need an extension feature.
