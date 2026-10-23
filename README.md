# Catino

A crash game where the "crash" is a cat getting distracted. You bet, the cat starts walking, the
multiplier climbs, and you cash out before something (a butterfly, a fish, a laser dot, a nap)
catches its eye. Up to 60 people in the room can join from their phones by scanning a QR code and
bet on the same cat. Four seasons each change one rule:

| Season | Rule |
|---|---|
| Spring | **Butterfly Catch**: cash out while the golden butterfly is on screen for a bonus |
| Summer | **Summer Zoomies**: 40% chance the cat sprints and the multiplier climbs faster |
| Fall | **Leaf Storm**: gusts of leaves hide the multiplier |
| Winter | **Hot Cocoa**: slow walk; cash out at 2.00x or more for a bonus |

While betting, the forecast card shows the next walk's season and the two after it. The server rolls
those seasons in advance and uses them in that order, so the forecast is never a guess.

Season bonuses are **+4%** by default, which keeps every strategy under 100% RTP. Starting the server
with `HAPPY_HOUR=1` restores the bigger demo bonuses (+25% butterfly, +10% cocoa). Screens show
whatever the server really pays, and the game footer then reads **HAPPY HOUR (demo)**.

Every round is **provably fair**, and the proof lives in **Schrödinger's Box**: the server publishes a SHA-256
seal of its secret before anyone can cash out, and the box stays shut until the cat is "observed" (distracted).
While kitty stretches, every player's device adds a random **seed**, and the crash point comes from the server's
value XOR a hash of all the seeds, so neither the server nor a player can steer it. The small box badge under MENU
shows the seal; press **Q** (or click the badge) to open the box and see its checks: seal, seeds, math and qubits.

Server mode uses **points** in a persistent wallet, never cash. Every player (PC or phone) starts
with 50 points once. Each server day there are 20 free points. In `DEMO_MODE` the server also
offers **simulated** top-ups of any amount from $1 to $100 (8 points per $1, up to $200 a day) and a
$5 parking bundle (60 points, once a day); no money moves. A ticket code from a demo ticket is worth
25 points, once a day. See `KittyAPI\README.md` for the
wallet rules. Offline practice keeps its own local balance.

**Play safe.** In server mode the game first asks you to confirm you're **19 or older**: the
server refuses bets and purchases until you do. The **WALLET** and **PLAY SAFE** buttons (or **W**)
open:
- the wallet: free points, demo packs with a simulated checkout and a required "no real money"
  checkbox, ticket entry, profile and cashier code, receipts and history;
- the play-safe tools:
  - a daily spend limit (lowering applies now, raising waits 24 hours);
  - a reality check every 15, 30 or 60 minutes;
  - a cool-off break and self-exclusion. Neither can be shortened.

The phones have the same tools on the crowd page.

**Safe play** (title screen, menu, phone menu) explains in plain words how the game works and how to
keep it fun, opens the tools above, and lists ConnexOntario (1-866-531-2600, or text CONNEX to
247247, free, 24/7) and the 9-8-8 crisis line. Re-check the numbers before any public use.

## Repository layout

| Path | What |
|---|---|
| `Assets/KittyCrash/` | The game: scene `Scenes/KittyCrash.unity`, scripts, art, audio |
| `KittyAPI/` | Python/FastAPI server (git submodule, see its README). Decides crash points, seasons, bonuses and payouts |
| `start_server.bat` | Double-click to start the v2 server on port 8001 (room demo on the Wi-Fi) |
| `start_online.bat` | One click for a live demo: a Cloudflare tunnel plus the server in public mode with the house table on, prints the public address |
| `start_tunnel.bat`, `start_public.bat` | The same in two steps (cloudflared or ngrok tunnel, then the server), see "Run online" |
| `site/` | A static landing page (video, screenshots, links). Host it on Vercel, Cloudflare Pages or GitHub Pages |

## Requirements

- Unity **6000.6.5f1** (Unity 6)
- For server mode: Anaconda/Miniconda with the `hackathon` env
  (`conda env create -f KittyAPI\environment.yml`)

Fresh clone: `git clone --recurse-submodules <this repo>`, or run `git submodule update --init` in an
existing clone.

## Quick start (Windows)

1. Install Unity 6000.6.5f1 and, for server mode, Anaconda with the `hackathon` env (see Requirements).
2. Clone with `git clone --recurse-submodules <this repo>`.
3. Offline: open `Assets/KittyCrash/Scenes/KittyCrash.unity` and press Play.
4. With the server: double-click `start_server.bat`, tick **Use Api** on the Round Manager, press Play,
   then press **C** and scan the QR code with a phone on the same Wi-Fi.
5. Over the internet: see "Run online" below.

## Run offline (no server)

1. Open the project in Unity and open `Assets/KittyCrash/Scenes/KittyCrash.unity`.
2. Press **Play**. The scene ships with **Use Api** unticked, so rounds run on `LocalBackend`, an
   offline copy of the server rules with its own practice balance.

## Run with the server

1. Double-click `start_server.bat` (or see `KittyAPI\README.md`). Wait for
   `Uvicorn running on http://0.0.0.0:8001`. Settings and local secrets
   (`DEMO_MODE=1` for simulated purchases, `TICKET_SECRET`, `QR_SECRET`) go in `KittyAPI\.env.local`,
   one `KEY=value` per line. That file is never committed.
2. In Unity, run **Kitty Crash > Server > Use v2 Server (port 8001)** once and save the scene
   (Ctrl+S). Then find the Round Manager (Hierarchy search: `t:RoundManager`) and tick **Use Api**.
   **Use v1 Server (port 8000)** switches back for the v1 demo.
3. Press **Play**. The footer (bottom right) shows the game and server versions. Press **C** to show
   the crowd QR code; phones on the same Wi-Fi scan it to open
   `/crowd`. Run `pip install segno` in the env if the QR code does not appear.

Pages that only open on the server computer (`http://127.0.0.1:8001/...`):
- `/cashier`: scan or type a player's cashier code and record a simulated counter top-up.
- `/operator`: live RTP, bets, players, top-ups, points, safe-play counts, daily charts, CSV export.

## Run online (for a live demo)

Phones anywhere can join through a tunnel that gives the server a public https address. Nothing is installed
on the phones and no money is involved (points only).

**Fastest:** double-click `start_online.bat`. It downloads cloudflared once, opens the tunnel, starts the server in public mode
and prints the address. Phones open `<address>/crowd`; the web game is at `<address>/play` once you have built it (Kitty Crash >
Build > WebGL for the server). The server's own house table runs rounds while phones are watching, so no game window is needed.

If the Vercel command line is set up on this PC, it also updates the landing page (`site/`, catino.vercel.app) so its
**Play in your browser** and **Join on your phone** buttons point at this run, and switches the page back to "offline" when you
stop. The page reads `live.json`, which the launcher rewrites for each deploy; the committed copy stays empty.

By hand:

1. Install `cloudflared` (no account) or `ngrok` (free account).
2. Double-click `start_tunnel.bat` and copy the `https://...` address it prints.
3. Double-click `start_public.bat` and paste the address when asked. It starts the server in **public mode**
   on this computer only: the cashier and operator pages close, only this game can host, and each visitor is rate limited.
4. In Unity use the v2 server (`http://127.0.0.1:8001`), tick **Use Api**, press Play and press **C**. The QR now holds the
   public address.

Container hosts (Render, Fly.io), a VM with Caddy, backups and a pre-demo checklist: `KittyAPI\docs\DEPLOY.md`.

## Controls

| Key | Action |
|---|---|
| Space / Enter | Place bet while betting is open; cash out while the cat walks |
| C | Show / hide crowd mode (QR code and phone players) |
| N | Choose the next walk's season (host / testing) |
| F | Next festival look |
| Q | Show / hide Schrödinger's Box: the round's seal and its seal / seeds / math / qubit checks |
| M | Mute / unmute music |
| W | Open / close the wallet (server mode) |
| Esc | Open / close the menu (wallet, safe play, panels, how to play, settings, demo reset) |
| F1 | How to play: a picture tour with no words (Left / Right or the arrows to step, Esc to close) |
| F5 | Demo reset: start over as a new player (asks first; offline, or a server in `DEMO_MODE`) |

Shortcuts are ignored while you type in a text field or while a menu is open.

The game opens on a title screen (Play, How to play, Settings, Safe play). The first time you press
Play, a short picture tour shows the game without words: bet, the walk, cash out in time, and what
happens when the cat gets distracted. The written rules are in the menu under **RULES**. Settings has the language
(English / French), mute, volume, the classic layout, the title screen on/off and online/offline play. The demo reset gives
a fresh wallet with the starting points and no breaks or limits; on the server the old wallet and
its history stay untouched.

## Contributing

`main` is the trunk — it's always current and is what the repo link shows by default. Branch off
`main`, open a PR back into `main`, and delete the branch once it's merged (`git push origin --delete
<branch>`, or tick the box on the GitHub merge screen). Don't let merged branches pile up.

## Known limits

- Phones get live updates over a WebSocket (polling only as a fallback). The game screen itself
  still polls the server, which runs on the same computer.
- Wallets are still anonymous: anyone who asks for a new session gets a new 50-point wallet. A
  profile adds a nickname and avatar, and the cashier QR code proves which wallet to top up, but there
  are no logins.
- The 19+ check is self-attested (no ID verification).
- The wallet panel and menu are built at runtime. They use the HUD's rounded cards, colours and header
  font, but no hand-drawn pixel art.
- Plain HTTP on the local network by default. A tunnel in public mode gives https for a live demo; there is no always-on host yet, and a
  hosted table still needs the game on a PC to open rounds.
- With `HAPPY_HOUR=1` (demo only) the spring butterfly and winter 2.00x strategies return more than
  100% to the player (about 119% and 105%, `KittyAPI\tests\sim_rtp.py --happy-hour`). Never run it
  with real money.

## Credits

- Cat sprites (Pet Cats pack). https://assetstore.unity.com/packages/2d/characters/pet-cats-pixel-art-pack-248340
- Plants (KebapGuy pack). https://assetstore.unity.com/publishers/158912
- Font (Fredoka). https://fonts.google.com/specimen/Fredoka
- Music (Season Cycle). https://marketplace.unity.com/packages/audio/music/orchestral/gomesda-cozy-spring-free-orchestral-mini-pack-377670

## License

The code in this repository is released under the [MIT License](LICENSE). The art, music and fonts listed under
Credits belong to their authors and keep their own licences; the MIT License does not cover them.
