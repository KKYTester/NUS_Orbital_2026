export function createRealtimeClient() {
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  const socket = new WebSocket(`${protocol}://${location.host}`);
  const handlers = new Map();

  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    for (const handler of handlers.get(message.type) || []) handler(message.payload);
  });

  return {
    socket,
    on(type, handler) {
      if (!handlers.has(type)) handlers.set(type, new Set());
      handlers.get(type).add(handler);
    },
    send(type, payload = {}) {
      const sendNow = () => socket.send(JSON.stringify({ type, payload }));
      if (socket.readyState === WebSocket.OPEN) sendNow();
      else socket.addEventListener("open", sendNow, { once: true });
    }
  };
}
