import { createServer } from "node:http";
import { createHash, randomUUID } from "node:crypto";
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { createRoomCode, handleDisconnect, joinRoom, rooms } from "./rooms.js";

const root = fileURLToPath(new URL("..", import.meta.url));
const publicDir = join(root, "client");
const sharedDir = join(root, "shared");
const port = Number(process.env.PORT || 3000);
const unityEvents = new Map();
let nextUnityEventId = 1;

const mime = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml"
};

const server = createServer(async (req, res) => {
  if (!req.url) return send(res, 400, "Bad request");
  const url = new URL(req.url, `http://${req.headers.host}`);

  if (url.pathname === "/health") {
    return sendJson(res, 200, { ok: true });
  }

  if (url.pathname === "/unity/create-room") {
    const roomCode = createRoomCode(rooms);
    ensureUnityEventQueue(roomCode);
    return sendJson(res, 200, { roomCode });
  }

  if (url.pathname === "/unity/events") {
    const roomCode = String(url.searchParams.get("roomCode") || "").trim().toUpperCase();
    const after = Number(url.searchParams.get("after") || 0);
    const events = (unityEvents.get(roomCode) || []).filter((event) => event.id > after);
    return sendJson(res, 200, { events });
  }

  const route = url.pathname === "/" ? "/index.html" : url.pathname;
  const baseDir = route.startsWith("/shared/") ? sharedDir : publicDir;
  const relativeRoute = route.startsWith("/shared/") ? route.replace("/shared/", "/") : route;
  const filePath = normalize(join(baseDir, relativeRoute));

  if (!filePath.startsWith(baseDir) || !existsSync(filePath)) {
    return send(res, 404, "Not found");
  }

  try {
    const data = await readFile(filePath);
    res.writeHead(200, { "Content-Type": mime[extname(filePath)] || "application/octet-stream" });
    res.end(data);
  } catch {
    send(res, 500, "Could not read file");
  }
});

const sockets = new Map();

server.on("upgrade", (req, socket) => {
  if (req.headers.upgrade?.toLowerCase() !== "websocket") {
    socket.destroy();
    return;
  }

  const key = req.headers["sec-websocket-key"];
  const accept = createHash("sha1")
    .update(`${key}258EAFA5-E914-47DA-95CA-C5AB0DC85B11`)
    .digest("base64");

  socket.write([
    "HTTP/1.1 101 Switching Protocols",
    "Upgrade: websocket",
    "Connection: Upgrade",
    `Sec-WebSocket-Accept: ${accept}`,
    "",
    ""
  ].join("\r\n"));

  const client = { id: randomUUID(), socket, roomCode: null, role: null };
  sockets.set(client.id, client);

  socket.on("data", (buffer) => {
    for (const message of decodeFrames(buffer)) {
      handleMessage(client, message);
    }
  });

  socket.on("close", () => cleanup(client));
  socket.on("error", () => cleanup(client));
});

function send(res, status, body) {
  res.writeHead(status, { "Content-Type": "text/plain; charset=utf-8" });
  res.end(body);
}

function sendJson(res, status, body) {
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(body));
}

function handleMessage(client, raw) {
  let event;
  try {
    event = JSON.parse(raw);
  } catch {
    return sendEvent(client, "error", { message: "Invalid JSON message." });
  }

  if (event.type === "createRoom") {
    const roomCode = createRoomCode(rooms);
    joinRoom(rooms, roomCode, "host", client);
    sendEvent(client, "roomCreated", { roomCode });
    broadcastRoom(roomCode, "roomState", publicRoom(roomCode));
    return;
  }

  if (event.type === "joinRoom") {
    const result = joinRoom(rooms, event.payload?.roomCode, event.payload?.role, client);
    if (!result.ok) return sendEvent(client, "joinRejected", { reason: result.reason });
    sendEvent(client, "joinedRoom", { roomCode: client.roomCode, role: client.role });
    broadcastRoom(client.roomCode, "roomState", publicRoom(client.roomCode));
    return;
  }

  if (!client.roomCode) return sendEvent(client, "error", { message: "Join a room first." });

  if (event.type === "sensorSample") {
    enqueueUnitySensorEvent(client.roomCode, event.payload);
    broadcastHosts(client.roomCode, "sensorSample", event.payload);
  }

  if (event.type === "calibrate") {
    broadcastHosts(client.roomCode, "calibrate", event.payload);
  }

  if (event.type === "shotEvent") {
    enqueueUnityEvent(client.roomCode, event.payload);
    broadcastHosts(client.roomCode, "shotEvent", event.payload);
  }

  if (event.type === "gameState") {
    broadcastControllers(client.roomCode, "gameState", event.payload);
  }

  if (event.type === "restartMatch") {
    broadcastRoom(client.roomCode, "restartMatch", {});
  }
}

function ensureUnityEventQueue(roomCode) {
  if (!unityEvents.has(roomCode)) {
    unityEvents.set(roomCode, []);
  }
}

function enqueueUnityEvent(roomCode, shot) {
  ensureUnityEventQueue(roomCode);
  const queue = unityEvents.get(roomCode);
  queue.push({
    id: nextUnityEventId++,
    eventType: "shot",
    playerId: shot.playerId,
    shotType: shot.shotType,
    source: shot.source || "unknown",
    power: Number(shot.power || 0.75),
    direction: Number(shot.direction || 0),
    spin: Number(shot.spin || 0),
    timestamp: Number(shot.timestamp || Date.now())
  });
  if (queue.length > 100) {
    queue.splice(0, queue.length - 100);
  }
}

function enqueueUnitySensorEvent(roomCode, sample) {
  ensureUnityEventQueue(roomCode);
  const queue = unityEvents.get(roomCode);
  queue.push({
    id: nextUnityEventId++,
    eventType: "sensor",
    playerId: sample.playerId,
    timestamp: Number(sample.timestamp || Date.now()),
    accelX: Number(sample.accel?.x || 0),
    accelY: Number(sample.accel?.y || 0),
    accelZ: Number(sample.accel?.z || 0),
    rotationAlpha: Number(sample.rotation?.alpha || 0),
    rotationBeta: Number(sample.rotation?.beta || 0),
    rotationGamma: Number(sample.rotation?.gamma || 0),
    orientationAlpha: Number(sample.orientation?.alpha || 0),
    orientationBeta: Number(sample.orientation?.beta || 0),
    orientationGamma: Number(sample.orientation?.gamma || 0)
  });
  if (queue.length > 150) {
    queue.splice(0, queue.length - 150);
  }
}

function cleanup(client) {
  sockets.delete(client.id);
  const roomCode = client.roomCode;
  handleDisconnect(rooms, client);
  if (roomCode) broadcastRoom(roomCode, "roomState", publicRoom(roomCode));
}

function publicRoom(roomCode) {
  const room = rooms.get(roomCode);
  if (!room) return { roomCode, host: false, p1: false, p2: false };
  return {
    roomCode,
    host: Boolean(room.host),
    p1: Boolean(room.p1),
    p2: Boolean(room.p2)
  };
}

function broadcastHosts(roomCode, type, payload) {
  const host = rooms.get(roomCode)?.host;
  if (host) sendEvent(host, type, payload);
}

function broadcastControllers(roomCode, type, payload) {
  const room = rooms.get(roomCode);
  if (!room) return;
  for (const role of ["p1", "p2"]) {
    if (room[role]) sendEvent(room[role], type, payload);
  }
}

function broadcastRoom(roomCode, type, payload) {
  const room = rooms.get(roomCode);
  if (!room) return;
  for (const role of ["host", "p1", "p2"]) {
    if (room[role]) sendEvent(room[role], type, payload);
  }
}

function sendEvent(client, type, payload) {
  if (client.socket.destroyed) return;
  client.socket.write(encodeFrame(JSON.stringify({ type, payload })));
}

function decodeFrames(buffer) {
  const messages = [];
  let offset = 0;

  while (offset + 2 <= buffer.length) {
    const second = buffer[offset + 1];
    const masked = (second & 0x80) === 0x80;
    let length = second & 0x7f;
    offset += 2;

    if (length === 126) {
      length = buffer.readUInt16BE(offset);
      offset += 2;
    } else if (length === 127) {
      length = Number(buffer.readBigUInt64BE(offset));
      offset += 8;
    }

    const mask = masked ? buffer.subarray(offset, offset + 4) : null;
    if (masked) offset += 4;

    const payload = buffer.subarray(offset, offset + length);
    offset += length;

    const data = Buffer.alloc(payload.length);
    for (let i = 0; i < payload.length; i += 1) {
      data[i] = mask ? payload[i] ^ mask[i % 4] : payload[i];
    }
    messages.push(data.toString("utf8"));
  }

  return messages;
}

function encodeFrame(text) {
  const payload = Buffer.from(text);
  const header = [];
  header.push(0x81);
  if (payload.length < 126) {
    header.push(payload.length);
  } else if (payload.length < 65536) {
    header.push(126, payload.length >> 8, payload.length & 255);
  } else {
    header.push(127, 0, 0, 0, 0, ...Buffer.alloc(4));
  }
  return Buffer.concat([Buffer.from(header), payload]);
}

server.listen(port, "0.0.0.0", () => {
  console.log(`Court Smasherz running at http://localhost:${port}`);
  console.log("Open /game on the laptop and /controller on each phone.");
});
