// Run: node --test   (from Server/photon-auth-worker). No dependencies needed (Node 20+).
import { test, before } from "node:test";
import assert from "node:assert/strict";

const ISSUER = "https://player-auth.services.api.unity.com";
const PROJECT = "11111111-2222-3333-4444-555555555555";
const KID = "test-key";
let privateKey, otherPrivateKey, worker;

const b64url = (bytes) => Buffer.from(bytes).toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
const enc = (obj) => b64url(new TextEncoder().encode(JSON.stringify(obj)));

async function sign(claims, { key = privateKey, kid = KID, alg = "RS256" } = {}) {
  const head = enc({ alg, kid, typ: "JWT" });
  const body = enc(claims);
  const sig = await crypto.subtle.sign({ name: "RSASSA-PKCS1-v1_5" }, key, new TextEncoder().encode(head + "." + body));
  return head + "." + body + "." + b64url(new Uint8Array(sig));
}

const now = () => Math.floor(Date.now() / 1000);
const goodClaims = (over = {}) => ({ iss: ISSUER, sub: "player-1", project_id: PROJECT, exp: now() + 3600, iat: now(), ...over });
const env = { UNITY_PROJECT_ID: PROJECT, BANNED_PLAYER_IDS: "banned-player" };

async function call(body, query = "", e = env) {
  const req = new Request("https://auth.example/" + query, { method: "POST", body: JSON.stringify(body), headers: { "content-type": "application/json" } });
  const res = await worker.default.fetch(req, e);
  return res.json();
}

before(async () => {
  const gen = () => crypto.subtle.generateKey(
    { name: "RSASSA-PKCS1-v1_5", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" }, true, ["sign", "verify"]);
  const pair = await gen();
  privateKey = pair.privateKey;
  otherPrivateKey = (await gen()).privateKey;
  const jwk = await crypto.subtle.exportKey("jwk", pair.publicKey);

  // Serve our test key as "Unity's" JWKS.
  globalThis.fetch = async (url) => {
    assert.equal(String(url), ISSUER + "/.well-known/jwks.json");
    return new Response(JSON.stringify({ keys: [{ ...jwk, kid: KID, alg: "RS256", use: "sig" }] }));
  };
  worker = await import("../src/index.js");
});

test("valid token is accepted and UserId comes from the token", async () => {
  const r = await call({ token: await sign(goodClaims()), userId: "player-1" });
  assert.deepEqual(r, { ResultCode: 1, UserId: "player-1" });
});

test("project id via aud upid: is accepted", async () => {
  const r = await call({ token: await sign(goodClaims({ project_id: undefined, aud: ["upid:" + PROJECT] })) });
  assert.equal(r.ResultCode, 1);
});

test("missing token -> invalid parameters", async () => {
  assert.equal((await call({})).ResultCode, 3);
});

test("token signed by another key is rejected", async () => {
  const r = await call({ token: await sign(goodClaims(), { key: otherPrivateKey }) });
  assert.deepEqual(r, { ResultCode: 2, Message: "Invalid token" });
});

test("tampered payload is rejected", async () => {
  const t = (await sign(goodClaims())).split(".");
  t[1] = enc(goodClaims({ sub: "someone-else" }));
  assert.equal((await call({ token: t.join(".") })).ResultCode, 2);
});

test("alg none / HS256 is rejected", async () => {
  const t = enc({ alg: "none", kid: KID }) + "." + enc(goodClaims()) + ".";
  assert.equal((await call({ token: t })).ResultCode, 2);
});

test("expired token is rejected", async () => {
  const r = await call({ token: await sign(goodClaims({ exp: now() - 3600 })) });
  assert.equal(r.Message, "Token expired");
});

test("token from another Unity project is rejected", async () => {
  const r = await call({ token: await sign(goodClaims({ project_id: "other-project" })) });
  assert.equal(r.Message, "Token is for another project");
});

test("wrong issuer is rejected", async () => {
  const r = await call({ token: await sign(goodClaims({ iss: "https://evil.example" })) });
  assert.equal(r.Message, "Wrong issuer");
});

test("client claiming a different user id is rejected", async () => {
  const r = await call({ token: await sign(goodClaims()), userId: "player-2" });
  assert.equal(r.Message, "Player mismatch");
});

test("banned player is rejected", async () => {
  const r = await call({ token: await sign(goodClaims({ sub: "banned-player" })) });
  assert.equal(r.Message, "This account is banned");
});

test("shared secret, when configured, must match", async () => {
  const e = { ...env, PHOTON_SHARED_SECRET: "s3cret" };
  const token = await sign(goodClaims());
  assert.equal((await call({ token }, "?secret=wrong", e)).ResultCode, 2);
  assert.equal((await call({ token }, "?secret=s3cret", e)).ResultCode, 1);
});

test("unconfigured project id refuses everyone", async () => {
  const r = await call({ token: await sign(goodClaims()) }, "", { UNITY_PROJECT_ID: "REPLACE_WITH_YOUR_UNITY_PROJECT_ID" });
  assert.equal(r.ResultCode, 2);
});
