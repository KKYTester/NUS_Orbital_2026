import test from "node:test";
import assert from "node:assert/strict";
import { classifyShot, createCalibration, magnitude, smoothSample } from "../shared/motion.js";

const calibration = {
  neutralOrientation: { alpha: 0, beta: 0, gamma: 0 },
  baselineAccel: { x: 0, y: 0, z: 9.8 }
};

function sample(overrides) {
  return {
    playerId: "p1",
    timestamp: 1,
    accel: { x: 0, y: 0, z: 9.8 },
    rotation: { alpha: 0, beta: 0, gamma: 0 },
    orientation: { alpha: 0, beta: 0, gamma: 0 },
    ...overrides
  };
}

test("classifies a forehand swing", () => {
  const shot = classifyShot(sample({
    accel: { x: 16, y: 2, z: 10 },
    rotation: { alpha: 0, beta: 140, gamma: 60 }
  }), calibration);
  assert.equal(shot.shotType, "forehand");
});

test("classifies a backhand swing", () => {
  const shot = classifyShot(sample({
    accel: { x: -16, y: 2, z: 10 },
    rotation: { alpha: 0, beta: 140, gamma: -60 }
  }), calibration);
  assert.equal(shot.shotType, "backhand");
});

test("classifies a smash from strong downward motion", () => {
  const shot = classifyShot(sample({
    accel: { x: 8, y: 3, z: -18 },
    rotation: { alpha: 90, beta: 260, gamma: 40 }
  }), calibration);
  assert.equal(shot.shotType, "smash");
});

test("ignores weak noisy motion", () => {
  const shot = classifyShot(sample({
    accel: { x: 1, y: 1, z: 9.9 },
    rotation: { alpha: 8, beta: 4, gamma: 2 }
  }), calibration);
  assert.equal(shot, null);
});

test("creates calibration from current sample", () => {
  const baseline = sample({
    accel: { x: 1, y: 2, z: 10 },
    orientation: { alpha: 12, beta: -6, gamma: 31 }
  });
  assert.deepEqual(createCalibration(baseline), {
    neutralOrientation: baseline.orientation,
    baselineAccel: baseline.accel
  });
});

test("computes magnitude for alpha beta gamma rotation objects", () => {
  assert.equal(magnitude({ alpha: 3, beta: 4, gamma: 12 }), 13);
});

test("smooths incoming motion values without blocking real samples", () => {
  const previous = sample({
    accel: { x: 0, y: 0, z: 0 },
    rotation: { alpha: 0, beta: 0, gamma: 0 },
    orientation: { alpha: 0, beta: 0, gamma: 0 }
  });
  const next = sample({
    accel: { x: 10, y: 20, z: 30 },
    rotation: { alpha: 10, beta: 20, gamma: 30 },
    orientation: { alpha: 10, beta: 20, gamma: 30 }
  });
  const smoothed = smoothSample(previous, next, 0.5);
  assert.deepEqual(smoothed.accel, { x: 5, y: 10, z: 15 });
  assert.deepEqual(smoothed.rotation, { alpha: 5, beta: 10, gamma: 15 });
  assert.deepEqual(smoothed.orientation, { alpha: 5, beta: 10, gamma: 15 });
});
