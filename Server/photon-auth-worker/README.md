# Photon authentication for Phantom Warrior

Stops other clients from using the game's Photon App ID. Every player signs in with
**Unity Authentication**. Photon then asks this worker whether the player's token is genuine
before it lets them connect.

```
Game ──(Unity access token)──▶ Photon ──▶ this worker ──▶ checks the token against Unity's public keys
                                   ◀── ResultCode 1 + UserId (or rejected) ──┘
```

The worker accepts a token only when all of these are true:
- it is signed by Unity (RS256, checked against Unity's published public keys);
- it was issued by `player-auth.services.api.unity.com` and has not expired;
- it belongs to **your** Unity project;
- its player ID matches the one the client sent, and that player is not banned.

Photon uses the player ID from the token as the player's Photon UserId.

## One-time setup (in this order)

### 1. Unity: link the project and turn on Authentication
1. In the Unity Editor, open **Edit → Project Settings → Services**. Link the project to a
   Unity Cloud project, or create one.
2. In the Unity Cloud Dashboard, open your project's **Player Authentication** settings and
   check that **Anonymous** sign-in is on (it is on by default).
3. Copy the **Project ID**. Step 2 needs it.

Unity installs the `com.unity.services.authentication` package automatically (it's listed in
`Packages/manifest.json`).

### 2. Cloudflare: deploy this worker (free plan is enough)
```bash
cd Server/photon-auth-worker
node --test                       # 13 tests should pass
# Edit wrangler.toml: set UNITY_PROJECT_ID to the ID you copied
npx wrangler login
npx wrangler deploy               # prints https://phantom-warrior-photon-auth.<you>.workers.dev
npx wrangler secret put PHOTON_SHARED_SECRET   # optional but recommended: paste a long random string
```

### 3. Photon dashboard: require authentication
In the Photon dashboard, open the app whose ID is in `PhotonServerSettings` (the game's only
Photon app; its Chat, Voice and Fusion IDs are empty). Go to **Manage → Authentication**:
1. Add a **Custom Server** provider. Set the **Authentication URL** to the worker URL from step 2.
2. If you set `PHOTON_SHARED_SECRET`, add a key/value pair: key `secret`, value = the same string.
3. Turn **Allow anonymous clients to connect** **OFF**. Without this, a client with no token can
   still get in.
4. Turn **Reject all clients if not able to connect to the authentication service** **ON**.

If you later add Photon Chat, Voice or Fusion, give each of those apps the same
Authentication settings.

## Checking it works
- In the game, the main menu connects normally. Photon's dashboard shows your players by their
  Unity player IDs.
- The worker's logs (`npx wrangler tail`) show each rejected attempt with its reason.
- To see the protection working, set `Require Authentication` off on the `Launcher` component
  (MainMenu scene) and press Play. With "allow anonymous" off, Photon now refuses the
  connection. Turn it back on afterwards.

## Banning a player
Add their Unity player ID to `BANNED_PLAYER_IDS` in `wrangler.toml` (comma-separated), then run
`npx wrangler deploy`. It takes effect the next time they connect.
