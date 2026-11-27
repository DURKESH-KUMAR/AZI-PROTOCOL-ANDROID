// Photon Custom Authentication for Phantom Warrior.
//
// Photon calls this endpoint for every client that connects. The client (PhotonAuth.cs) sends
// its Unity Authentication access token as POST data. We accept the client only if the token is
// a genuine, unexpired Unity token for OUR Unity project, then hand Photon the player's id.
// Photon response contract: ResultCode 1 = ok, 2 = rejected, 3 = invalid parameters.

const ISSUER = "https://player-auth.services.api.unity.com";
const JWKS_URL = ISSUER + "/.well-known/jwks.json";
const CLOCK_SKEW_SECONDS = 60;
const JWKS_REFRESH_MS = 10 * 60 * 1000; // re-fetch keys at most every 10 min (or on unknown kid, once a minute)

const keyCache = new Map(); // kid -> CryptoKey
let jwksFetchedAt = 0;

export default {
  async fetch(request, env) {
    try {
      return json(await authenticate(request, env));
    } catch (err) {
      // Never leak internals to clients; Photon shows Message to the client.
      console.error("auth error", err && err.stack ? err.stack : err);
      return json({ ResultCode: 2, Message: "Authentication service error" });
    }
  },
};

export async function authenticate(request, env) {
  const url = new URL(request.url);
  const query = Object.fromEntries(url.searchParams.entries());

  // Optional: only Photon knows this value (set as a static key/value in the Photon dashboard).
  if (env.PHOTON_SHARED_SECRET && query.secret !== env.PHOTON_SHARED_SECRET)
    return { ResultCode: 2, Message: "Unauthorized caller" };

  let body = {};
  if (request.method === "POST") {
    const text = await request.text();
    if (text) {
      try { body = JSON.parse(text); } catch { return { ResultCode: 3, Message: "Body is not JSON" }; }
    }
  }

  const token = body.token || query.token;
  const claimedUserId = body.userId || query.userId || "";
  if (!token) return { ResultCode: 3, Message: "Missing token" };

  const projectId = (env.UNITY_PROJECT_ID || "").trim();
  if (!projectId || projectId.startsWith("REPLACE"))
    return { ResultCode: 2, Message: "Server not configured (UNITY_PROJECT_ID)" };

  const claims = await verifyUnityToken(token);
  if (!claims) return { ResultCode: 2, Message: "Invalid token" };

  const now = Math.floor(Date.now() / 1000);
  if (claims.iss !== ISSUER) return { ResultCode: 2, Message: "Wrong issuer" };
  if (typeof claims.exp !== "number" || claims.exp + CLOCK_SKEW_SECONDS < now) return { ResultCode: 2, Message: "Token expired" };
  if (typeof claims.nbf === "number" && claims.nbf - CLOCK_SKEW_SECONDS > now) return { ResultCode: 2, Message: "Token not yet valid" };
  if (!belongsToProject(claims, projectId)) return { ResultCode: 2, Message: "Token is for another project" };

  const playerId = claims.sub;
  if (!playerId) return { ResultCode: 2, Message: "Token has no player" };
  if (claimedUserId && claimedUserId !== playerId) return { ResultCode: 2, Message: "Player mismatch" };

  const banned = (env.BANNED_PLAYER_IDS || "").split(",").map((s) => s.trim()).filter(Boolean);
  if (banned.includes(playerId)) return { ResultCode: 2, Message: "This account is banned" };

  // UserId returned here is authoritative: Photon uses it, not what the client sent.
  return { ResultCode: 1, UserId: playerId };
}

function belongsToProject(claims, projectId) {
  if (claims.project_id === projectId) return true;
  const aud = Array.isArray(claims.aud) ? claims.aud : claims.aud ? [claims.aud] : [];
  return aud.includes("upid:" + projectId);
}

/** Verifies an RS256 JWT against Unity's published keys. Returns the claims or null. */
export async function verifyUnityToken(token) {
  const parts = token.split(".");
  if (parts.length !== 3) return null;

  let header, claims;
  try {
    header = JSON.parse(b64urlText(parts[0]));
    claims = JSON.parse(b64urlText(parts[1]));
  } catch {
    return null;
  }
  if (header.alg !== "RS256" || !header.kid) return null; // reject alg=none / HS256 tricks

  const key = await getKey(header.kid);
  if (!key) return null;

  const ok = await crypto.subtle.verify(
    { name: "RSASSA-PKCS1-v1_5" },
    key,
    b64urlBytes(parts[2]),
    new TextEncoder().encode(parts[0] + "." + parts[1])
  );
  return ok ? claims : null;
}

async function getKey(kid) {
  const stale = Date.now() - jwksFetchedAt > JWKS_REFRESH_MS;
  if (!keyCache.has(kid) || stale) {
    // Unknown kid (Unity rotated keys) -> refresh, but at most once a minute.
    if (stale || Date.now() - jwksFetchedAt > 60 * 1000) await refreshKeys();
  }
  return keyCache.get(kid) || null;
}

async function refreshKeys() {
  const res = await fetch(JWKS_URL, { cf: { cacheTtl: 600 } });
  if (!res.ok) throw new Error("JWKS fetch failed: " + res.status);
  const { keys } = await res.json();
  keyCache.clear();
  for (const jwk of keys || []) {
    if (jwk.kty !== "RSA" || !jwk.kid) continue;
    const key = await crypto.subtle.importKey(
      "jwk",
      { kty: jwk.kty, n: jwk.n, e: jwk.e, alg: "RS256", ext: true },
      { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
      false,
      ["verify"]
    );
    keyCache.set(jwk.kid, key);
  }
  jwksFetchedAt = Date.now();
}

function b64urlBytes(s) {
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  while (s.length % 4) s += "=";
  const bin = atob(s);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

function b64urlText(s) {
  return new TextDecoder().decode(b64urlBytes(s));
}

function json(obj) {
  return new Response(JSON.stringify(obj), { headers: { "content-type": "application/json" } });
}
