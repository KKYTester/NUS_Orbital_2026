export const SHOT_TYPES = Object.freeze({
  FOREHAND: "forehand",
  BACKHAND: "backhand",
  LOB: "lob",
  SMASH: "smash"
});

const DEFAULT_CALIBRATION = {
  neutralOrientation: { alpha: 0, beta: 0, gamma: 0 },
  baselineAccel: { x: 0, y: 0, z: 0 }
};

export function magnitude(vector) {
  return Math.sqrt(Object.values(vector).reduce((sum, value) => sum + value ** 2, 0));
}

export function smoothSample(previous, next, alpha = 0.35) {
  if (!previous) return next;
  return {
    ...next,
    accel: lerpVector(previous.accel, next.accel, alpha),
    rotation: lerpVector(previous.rotation, next.rotation, alpha),
    orientation: lerpVector(previous.orientation, next.orientation, alpha)
  };
}

export function classifyShot(sample, calibration = DEFAULT_CALIBRATION) {
  const accelDelta = {
    x: sample.accel.x - calibration.baselineAccel.x,
    y: sample.accel.y - calibration.baselineAccel.y,
    z: sample.accel.z - calibration.baselineAccel.z
  };
  const orientationDelta = {
    alpha: angleDelta(sample.orientation.alpha, calibration.neutralOrientation.alpha),
    beta: sample.orientation.beta - calibration.neutralOrientation.beta,
    gamma: sample.orientation.gamma - calibration.neutralOrientation.gamma
  };
  const accelStrength = magnitude(accelDelta);
  const rotationStrength = magnitude(sample.rotation);

  if (accelStrength < 10.8 && rotationStrength < 95) return null;

  const power = clamp((accelStrength - 7) / 22 + rotationStrength / 520, 0.25, 1);
  const direction = clamp(orientationDelta.gamma / 55 + accelDelta.x / 28, -1, 1);
  const spin = clamp(sample.rotation.gamma / 360, -1, 1);

  let shotType = accelDelta.x >= 0 ? SHOT_TYPES.FOREHAND : SHOT_TYPES.BACKHAND;
  if (accelStrength > 22 && accelDelta.z < -7) shotType = SHOT_TYPES.SMASH;
  else if (orientationDelta.beta < -18 && accelDelta.z > 3) shotType = SHOT_TYPES.LOB;

  return {
    playerId: sample.playerId,
    shotType,
    power,
    direction,
    spin,
    timestamp: sample.timestamp
  };
}

export function createCalibration(sample) {
  return {
    neutralOrientation: { ...sample.orientation },
    baselineAccel: { ...sample.accel }
  };
}

function lerpVector(a, b, alpha) {
  return Object.fromEntries(
    Object.keys(b).map((key) => [key, a[key] + (b[key] - a[key]) * alpha])
  );
}

function angleDelta(angle, baseline) {
  let diff = angle - baseline;
  while (diff > 180) diff -= 360;
  while (diff < -180) diff += 360;
  return diff;
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}
