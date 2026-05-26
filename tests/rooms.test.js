import test from "node:test";
import assert from "node:assert/strict";
import { createRoomCode, handleDisconnect, joinRoom } from "../server/rooms.js";

function client(id) {
  return { id, roomCode: null, role: null };
}

test("host can create and join room", () => {
  const rooms = new Map();
  const host = client("host");
  const code = createRoomCode(rooms);
  const result = joinRoom(rooms, code, "host", host);
  assert.equal(result.ok, true);
  assert.equal(rooms.get(code).host.id, "host");
});

test("prevents duplicate player slots", () => {
  const rooms = new Map();
  const code = createRoomCode(rooms);
  assert.equal(joinRoom(rooms, code, "p1", client("a")).ok, true);
  const result = joinRoom(rooms, code, "p1", client("b"));
  assert.equal(result.ok, false);
});

test("disconnect clears occupied slot", () => {
  const rooms = new Map();
  const code = createRoomCode(rooms);
  const p2 = client("p2");
  joinRoom(rooms, code, "p2", p2);
  handleDisconnect(rooms, p2);
  assert.equal(rooms.get(code).p2, null);
});
