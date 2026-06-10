import { classifyShot, createCalibration, smoothSample } from "../../shared/motion.js";
import { createRealtimeClient } from "./socket.js";

const client = createRealtimeClient();
const roomInput = document.querySelector("#roomInput");
const p1Button = document.querySelector("#p1Button");
const p2Button = document.querySelector("#p2Button");
const joinButton = document.querySelector("#joinButton");
const permissionButton = document.querySelector("#permissionButton");
const calibrateButton = document.querySelector("#calibrateButton");
const screenBackButton = document.querySelector("#screenBackButton");
const screenFrontButton = document.querySelector("#screenFrontButton");
const connectionStatus = document.querySelector("#connectionStatus");
const motionStatus = document.querySelector("#motionStatus");
const sensorDebug = document.querySelector("#sensorDebug");
const shotStatus = document.querySelector("#shotStatus");
const manualShotButtons = document.querySelectorAll("[data-shot]");

let playerId = "p1";
let roomCode = "";
let latestSample = null;
let smoothedSample = null;
let calibration = null;
let lastShotAt = 0;
let lastSentAt = 0;
let motionListenersAttached = false;
let hasValidMotionSample = false;
let motionEventCount = 0;
let orientationEventCount = 0;
let noMotionTimer = null;
let calibrationScreenSide = "back";

p1Button.addEventListener("click", () => selectPlayer("p1"));
p2Button.addEventListener("click", () => selectPlayer("p2"));
joinButton.addEventListener("click", joinRoom);
permissionButton.addEventListener("click", enableMotion);
calibrateButton.addEventListener("click", calibrate);
screenBackButton.addEventListener("click", () => selectCalibrationScreenSide("back"));
screenFrontButton.addEventListener("click", () => selectCalibrationScreenSide("front"));
manualShotButtons.forEach((button) => {
  button.addEventListener("click", () => sendManualShot(button.dataset.shot));
});
permissionButton.disabled = false;
calibrateButton.disabled = true;
applyInitialQueryParams();

client.on("joinedRoom", (payload) => {
  connectionStatus.textContent = `Connected as ${payload.role.toUpperCase()} in ${payload.roomCode}`;
});

client.on("joinRejected", (payload) => {
  connectionStatus.textContent = payload.reason;
});

function selectPlayer(nextPlayer) {
  playerId = nextPlayer;
  p1Button.classList.toggle("selected", playerId === "p1");
  p2Button.classList.toggle("selected", playerId === "p2");
}

function selectCalibrationScreenSide(nextSide) {
  calibrationScreenSide = nextSide;
  screenBackButton.classList.toggle("selected", calibrationScreenSide === "back");
  screenFrontButton.classList.toggle("selected", calibrationScreenSide === "front");
}

function joinRoom() {
  roomCode = roomInput.value.trim().toUpperCase();
  if (roomCode.length !== 5) {
    connectionStatus.textContent = "Enter the five-character room code.";
    return;
  }
  client.send("joinRoom", { roomCode, role: playerId });
}

async function enableMotion() {
  permissionButton.disabled = false;
  motionStatus.textContent = "Requesting motion permission...";
  sensorDebug.textContent = getSensorEnvironmentText();

  try {
    const motionPermission = await requestSensorPermission(window.DeviceMotionEvent);
    const orientationPermission = await requestSensorPermission(window.DeviceOrientationEvent);
    if (motionPermission === "denied" || orientationPermission === "denied") {
      motionStatus.textContent = "Motion permission denied";
      return;
    }
  } catch (error) {
    motionStatus.textContent = `Permission error: ${error.message}`;
    return;
  }

  await startQuaternionOrientationSensor();

  if (!motionListenersAttached) {
    window.addEventListener("devicemotion", onMotion);
    window.addEventListener("deviceorientation", onOrientation);
    window.addEventListener("deviceorientationabsolute", onOrientation);
    motionListenersAttached = true;
  }

  motionStatus.textContent = "Motion listener attached";
  clearTimeout(noMotionTimer);
  noMotionTimer = setTimeout(() => {
    if (motionEventCount === 0) {
      motionStatus.textContent = "No motion events received";
      sensorDebug.textContent = `${getSensorEnvironmentText()} | If using phone over http://192.168.x.x, Chrome may block motion sensors. Use HTTPS/ngrok or Android Chrome site permissions.`;
    }
  }, 2500);
}

// Following function is for setting up orientation using quarternions instead of euler angles.

let latestQuaternion = null;
let orientationSensor = null;

async function startQuaternionOrientationSensor() {
  if (!("AbsoluteOrientationSensor" in window)) {
    console.warn("AbsoluteOrientationSensor is not available. Falling back to DeviceOrientationEvent.");
    return false;
  }

  try {
    if (navigator.permissions?.query) {
      await Promise.all([
        navigator.permissions.query({ name: "accelerometer" }),
        navigator.permissions.query({ name: "gyroscope" }),
        navigator.permissions.query({ name: "magnetometer" })
      ]);
    }

    orientationSensor = new AbsoluteOrientationSensor({
      frequency: 60,
      referenceFrame: "device"
    });

    orientationSensor.addEventListener("reading", () => {
      const q = orientationSensor.quaternion;

      if (!q) {
        return;
      }

      latestQuaternion = {
        x: q[0],
        y: q[1],
        z: q[2],
        w: q[3]
      };
    });

    orientationSensor.addEventListener("error", (event) => {
      console.warn("Orientation sensor error:", event.error?.message || event);
    });

    orientationSensor.start();
    console.log("AbsoluteOrientationSensor started.");
    return true;
  } catch (error) {
    console.warn("Could not start AbsoluteOrientationSensor:", error);
    return false;
  }
}

function onMotion(event) {
  const now = performance.now();
  motionEventCount += 1;
  const accel = event.accelerationIncludingGravity || event.acceleration || { x: 0, y: 0, z: 0 };
  latestSample = {
    playerId,
    timestamp: Date.now(),
    accel: {
      x: Number(accel.x || 0),
      y: Number(accel.y || 0),
      z: Number(accel.z || 0)
    },
    rotation: {
      alpha: Number(event.rotationRate?.alpha || 0),
      beta: Number(event.rotationRate?.beta || 0),
      gamma: Number(event.rotationRate?.gamma || 0)
    },
    orientation: latestSample?.orientation || { alpha: 0, beta: 0, gamma: 0 },
    quaternion: latestQuaternion,
  };

  smoothedSample = smoothSample(smoothedSample, latestSample);
  hasValidMotionSample = true;
  calibrateButton.disabled = false;
  motionStatus.textContent = "Motion event received";
  sensorDebug.textContent =
    `Accel ${formatNumber(smoothedSample.accel.x)}, ${formatNumber(smoothedSample.accel.y)}, ${formatNumber(smoothedSample.accel.z)} | ` +
    `Rot ${formatNumber(smoothedSample.rotation.alpha)}, ${formatNumber(smoothedSample.rotation.beta)}, ${formatNumber(smoothedSample.rotation.gamma)}`;

  if (roomCode && now - lastSentAt > 33) {
    client.send("sensorSample", { roomCode, playerId, ...smoothedSample, quaternion: latestQuaternion});
    lastSentAt = now;
  }

  const shot = classifyShot(smoothedSample, calibration || undefined);
  if (shot && roomCode && now - lastShotAt > 520) {
    shotStatus.textContent = `${shot.shotType} ${(shot.power * 100).toFixed(0)}%`;
    client.send("shotEvent", { roomCode, source: "motion", ...shot });
    lastShotAt = now;
  }
}

function onOrientation(event) {
  orientationEventCount += 1;
  const orientation = {
    alpha: Number(event.alpha || 0),
    beta: Number(event.beta || 0),
    gamma: Number(event.gamma || 0)
  };
  latestSample = latestSample ? { ...latestSample, orientation, quaternion: latestQuaternion } : {
    playerId,
    timestamp: Date.now(),
    accel: { x: 0, y: 0, z: 0 },
    rotation: { alpha: 0, beta: 0, gamma: 0 },
    orientation,
    quaternion: latestQuaternion
  };

  if (!hasValidMotionSample) {
    sensorDebug.textContent =
      `Orientation ${formatNumber(orientation.alpha)}, ${formatNumber(orientation.beta)}, ${formatNumber(orientation.gamma)} | ` +
      `Motion events ${motionEventCount} | Orientation events ${orientationEventCount}`;
  }
}

function calibrate() {
  if (!hasValidMotionSample || !smoothedSample) {
    motionStatus.textContent = "Move phone once before calibrating.";
    return;
  }
  calibration = createCalibration(smoothedSample);
  client.send("calibrate", {
    roomCode,
    playerId,
    timestamp: Date.now(),
    screenFacing: calibrationScreenSide,
    screenFacingForward: calibrationScreenSide === "front",
    ...calibration,
    sample: smoothedSample
  });
  motionStatus.textContent = `Neutral paddle calibrated (${calibrationScreenSide === "front" ? "screen front" : "screen back"})`;
}

function sendManualShot(shotType) {
  if (!roomCode) {
    shotStatus.textContent = "Join a room before sending test shots.";
    return;
  }

  const shot = {
    roomCode,
    playerId,
    shotType,
    power: shotType === "smash" ? 1 : shotType === "lob" ? 0.65 : 0.78,
    direction: shotType === "backhand" ? -0.45 : shotType === "forehand" ? 0.45 : 0,
    spin: shotType === "lob" ? 0.2 : 0,
    source: "manual",
    timestamp: Date.now()
  };

  shotStatus.textContent = `Manual ${shotType} sent`;
  sensorDebug.textContent = "Manual fallback shot sent; motion sensors still not required.";
  client.send("shotEvent", shot);
}

async function requestSensorPermission(sensorEvent) {
  if (typeof sensorEvent?.requestPermission !== "function") return "granted";
  return sensorEvent.requestPermission();
}

function formatNumber(value) {
  return Number.isFinite(value) ? value.toFixed(1) : "0.0";
}

function getSensorEnvironmentText() {
  const secure = window.isSecureContext ? "secure" : "not secure";
  const host = window.location.host;
  const motionApi = "DeviceMotionEvent" in window ? "DeviceMotion yes" : "DeviceMotion no";
  const orientationApi = "DeviceOrientationEvent" in window ? "DeviceOrientation yes" : "DeviceOrientation no";
  return `${secure} context on ${host} | ${motionApi} | ${orientationApi}`;
}

function applyInitialQueryParams() {
  const params = new URLSearchParams(window.location.search);
  const room = params.get("room");
  if (room) {
    roomInput.value = room.trim().toUpperCase().slice(0, 5);
  }

  const player = params.get("player");
  if (player === "p1" || player === "p2") {
    selectPlayer(player);
  }
}
