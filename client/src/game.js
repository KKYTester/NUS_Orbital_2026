import { awardPoint, createMatchState, opponent, startRally } from "../../shared/scoring.js";
import { createRealtimeClient } from "./socket.js";

const canvas = document.querySelector("#court");
const ctx = canvas.getContext("2d");
const roomCodeLabel = document.querySelector("#roomCode");
const connectionsLabel = document.querySelector("#connections");
const scoreLabel = document.querySelector("#score");
const phaseLabel = document.querySelector("#phase");
const lastShotLabel = document.querySelector("#lastShot");
const restartButton = document.querySelector("#restartButton");
const client = createRealtimeClient();

const state = {
  roomCode: "",
  match: createMatchState(),
  ball: { x: 550, y: 340, vx: 260, vy: -80, radius: 13 },
  players: {
    p1: { x: 120, y: 340, targetY: 340, connected: false, color: "#f0c85a" },
    p2: { x: 980, y: 340, targetY: 340, connected: false, color: "#8fd9ff" }
  },
  lastTime: performance.now(),
  feedback: "Waiting for controllers"
};

client.socket.addEventListener("open", () => client.send("createRoom"));
client.on("roomCreated", ({ roomCode }) => {
  state.roomCode = roomCode;
  roomCodeLabel.textContent = roomCode;
  phaseLabel.textContent = "Ask phones to open /controller and join this room.";
});
client.on("roomState", (room) => {
  state.players.p1.connected = room.p1;
  state.players.p2.connected = room.p2;
  connectionsLabel.textContent = `P1 ${room.p1 ? "on" : "off"} | P2 ${room.p2 ? "on" : "off"}`;
  if (room.p1 && room.p2 && state.match.phase === "waiting") {
    state.match = startRally(state.match);
    phaseLabel.textContent = "Rally started";
  }
});
client.on("shotEvent", applyShot);
client.on("sensorSample", () => {
  // Sensor samples are intentionally used by the phone for shot detection only.
  // In-game player movement remains automatic so the user focuses on swing timing.
});
client.on("restartMatch", restartMatch);
restartButton.addEventListener("click", () => {
  restartMatch();
  client.send("restartMatch", { roomCode: state.roomCode });
});

requestAnimationFrame(loop);

function loop(now) {
  const dt = Math.min((now - state.lastTime) / 1000, 0.033);
  state.lastTime = now;
  update(dt);
  draw();
  requestAnimationFrame(loop);
}

function update(dt) {
  if (state.match.phase === "finished") return;

  updateAutomaticPlayerTargets();

  for (const player of Object.values(state.players)) {
    player.y += (player.targetY - player.y) * Math.min(1, dt * 8);
  }

  state.ball.x += state.ball.vx * dt;
  state.ball.y += state.ball.vy * dt;

  if (state.ball.y < 78 || state.ball.y > 602) {
    state.ball.y = clamp(state.ball.y, 78, 602);
    state.ball.vy *= -0.92;
    state.feedback = "Ball bounce";
  }

  if (state.ball.x < 58) scorePoint("p2");
  if (state.ball.x > 1042) scorePoint("p1");
  updateHud();
}

function updateAutomaticPlayerTargets() {
  state.players.p1.targetY = getAutoTargetY("p1");
  state.players.p2.targetY = getAutoTargetY("p2");
}

function getAutoTargetY(playerId) {
  const player = state.players[playerId];
  const ballComingTowardPlayer =
    (playerId === "p1" && state.ball.vx < 0) ||
    (playerId === "p2" && state.ball.vx > 0);
  const ballOnPlayerHalf =
    (playerId === "p1" && state.ball.x < 550) ||
    (playerId === "p2" && state.ball.x > 550);

  if (ballComingTowardPlayer || ballOnPlayerHalf) {
    return clamp(state.ball.y, 120, 560);
  }

  return clamp(340 + (state.ball.y - 340) * 0.25, 160, 520);
}

function applyShot(shot) {
  const player = state.players[shot.playerId];
  if (!player || state.match.phase === "finished") return;
  const nearX = Math.abs(state.ball.x - player.x) < 100;
  const nearY = Math.abs(state.ball.y - player.y) < 105;

  if (!nearX || !nearY) {
    state.feedback = `${shot.playerId.toUpperCase()} mistimed ${shot.shotType}`;
    lastShotLabel.textContent = state.feedback;
    return;
  }

  const side = shot.playerId === "p1" ? 1 : -1;
  const baseSpeed = 330 + shot.power * 280;
  state.ball.vx = side * baseSpeed;
  state.ball.vy = shot.shotType === "lob"
    ? -260
    : shot.shotType === "smash"
      ? 230
      : shot.direction * 280 + shot.spin * 70;
  state.ball.x = player.x + side * 58;
  state.feedback = `${shot.playerId.toUpperCase()} ${shot.shotType}`;
  lastShotLabel.textContent = `${state.feedback} power ${(shot.power * 100).toFixed(0)}%`;
}

function scorePoint(playerId) {
  state.match = awardPoint(state.match, playerId);
  state.feedback = `${playerId.toUpperCase()} scores`;
  resetBall(opponent(playerId));
  updateHud();
  client.send("gameState", {
    roomCode: state.roomCode,
    score: { p1: state.match.p1, p2: state.match.p2 },
    ball: state.ball,
    players: state.players,
    phase: state.match.phase
  });
}

function resetBall(servingTo) {
  const direction = servingTo === "p1" ? -1 : 1;
  state.ball.x = 550;
  state.ball.y = 340;
  state.ball.vx = 220 * direction;
  state.ball.vy = Math.random() > 0.5 ? 90 : -90;
}

function restartMatch() {
  state.match = startRally(createMatchState());
  resetBall("p2");
  state.feedback = "Match restarted";
  updateHud();
}

function updateHud() {
  scoreLabel.textContent = `${state.match.p1} - ${state.match.p2}`;
  phaseLabel.textContent = state.match.winner
    ? `${state.match.winner.toUpperCase()} wins match`
    : state.feedback;
}

function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = "#255f52";
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  ctx.strokeStyle = "#f6f0de";
  ctx.lineWidth = 6;
  ctx.strokeRect(58, 78, 984, 524);
  ctx.beginPath();
  ctx.moveTo(550, 78);
  ctx.lineTo(550, 602);
  ctx.moveTo(58, 340);
  ctx.lineTo(1042, 340);
  ctx.stroke();

  ctx.fillStyle = "rgba(0,0,0,0.16)";
  ctx.fillRect(540, 78, 20, 524);

  for (const [id, player] of Object.entries(state.players)) {
    ctx.fillStyle = player.connected ? player.color : "#65706c";
    ctx.fillRect(player.x - 14, player.y - 72, 28, 144);
    ctx.fillStyle = "#f9f5e8";
    ctx.font = "700 20px system-ui";
    ctx.fillText(id.toUpperCase(), player.x - 16, player.y - 88);
  }

  ctx.fillStyle = "#f0c85a";
  ctx.beginPath();
  ctx.arc(state.ball.x, state.ball.y, state.ball.radius, 0, Math.PI * 2);
  ctx.fill();
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}
