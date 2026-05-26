import test from "node:test";
import assert from "node:assert/strict";
import { awardPoint, createMatchState, MATCH_POINT, startRally } from "../shared/scoring.js";

test("starts a fresh match in waiting phase", () => {
  assert.deepEqual(createMatchState(), {
    p1: 0,
    p2: 0,
    server: "p1",
    phase: "waiting",
    winner: null,
    rally: 0
  });
});

test("starts a rally and increments rally counter", () => {
  const state = startRally(createMatchState());
  assert.equal(state.phase, "rally");
  assert.equal(state.rally, 1);
});

test("awards point and gives server to scorer", () => {
  const state = awardPoint(createMatchState(), "p2");
  assert.equal(state.p2, 1);
  assert.equal(state.server, "p2");
  assert.equal(state.phase, "point");
});

test("requires two-point margin to finish match", () => {
  let state = { ...createMatchState(), p1: MATCH_POINT - 1, p2: MATCH_POINT - 1 };
  state = awardPoint(state, "p1");
  assert.equal(state.phase, "point");
  assert.equal(state.winner, null);
  state = awardPoint(state, "p1");
  assert.equal(state.phase, "finished");
  assert.equal(state.winner, "p1");
});
