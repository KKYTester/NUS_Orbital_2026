export const MATCH_POINT = 7;

export function createMatchState() {
  return {
    p1: 0,
    p2: 0,
    server: "p1",
    phase: "waiting",
    winner: null,
    rally: 0
  };
}

export function startRally(state) {
  return { ...state, phase: "rally", rally: state.rally + 1 };
}

export function awardPoint(state, playerId) {
  const next = {
    ...state,
    [playerId]: state[playerId] + 1,
    server: playerId,
    phase: "point",
    winner: null
  };

  if (next[playerId] >= MATCH_POINT && next[playerId] - next[opponent(playerId)] >= 2) {
    next.phase = "finished";
    next.winner = playerId;
  }

  return next;
}

export function opponent(playerId) {
  return playerId === "p1" ? "p2" : "p1";
}
