export const rooms = new Map();

export function createRoomCode(existingRooms) {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  do {
    code = Array.from({ length: 5 }, () => alphabet[Math.floor(Math.random() * alphabet.length)]).join("");
  } while (existingRooms.has(code));
  existingRooms.set(code, { host: null, p1: null, p2: null });
  return code;
}

export function joinRoom(existingRooms, roomCode, role, client) {
  const code = String(roomCode || "").trim().toUpperCase();
  if (!["host", "p1", "p2"].includes(role)) return { ok: false, reason: "Unknown role." };
  if (!existingRooms.has(code)) return { ok: false, reason: "Room does not exist." };

  const room = existingRooms.get(code);
  if (room[role] && room[role].id !== client.id) {
    return { ok: false, reason: `${role.toUpperCase()} is already connected.` };
  }

  handleDisconnect(existingRooms, client);
  room[role] = client;
  client.roomCode = code;
  client.role = role;
  return { ok: true };
}

export function handleDisconnect(existingRooms, client) {
  if (!client.roomCode || !client.role) return;
  const room = existingRooms.get(client.roomCode);
  if (room && room[client.role]?.id === client.id) {
    room[client.role] = null;
  }
  client.roomCode = null;
  client.role = null;
}
