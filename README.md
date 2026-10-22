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

Season bonuses are **+4%** by default, which keeps every strategy under 100% RTP. Starting the server
with `HAPPY_HOUR=1` restores the pitch-demo bonuses (+25% butterfly, +10% cocoa). Screens show
whatever the server really pays, and the game footer then reads **HAPPY HOUR (demo)**.

Every round is **provably fair**: the server publishes a SHA-256 seal of the outcome before anyone
can cash out, and opens it after the cat is distracted. Press **Q** to see the seal and its checks.

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

## Repository layout

| Path | What |
|---|---|
| `Assets/KittyCrash/` | The game: scene `Scenes/KittyCrash.unity`, scripts, art, audio |
| `KittyAPI/` | Python/FastAPI server (git submodule, see its README). Decides crash points, seasons, bonuses and payouts |
| `start_server.bat` | Double-click to start the v2 server on port 8001 |

## Requirements

- Unity **6000.6.5f1** (Unity 6)
- For server mode: Anaconda/Miniconda with the `hackathon` env
  (`conda env create -f KittyAPI\environment.yml`)

Fresh clone: `git clone --recurse-submodules <this repo>`, or run `git submodule update --init` in an
existing clone.

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

## Controls

| Key | Action |
|---|---|
| Space / Enter | Place bet while betting is open; cash out while the cat walks |
| C | Show / hide crowd mode (QR code and phone players) |
| N | Choose the next walk's season (host / testing) |
| F | Next festival look |
| Q | Show / hide the quantum box: the round's fairness seal and its hash / math / qubit checks |
| M | Mute / unmute music |
| W | Open / close the wallet (server mode) |

Shortcuts are ignored while you type in a text field.

## Known limits

- Phones poll the server about 3 times a second (no WebSockets yet).
- Wallets are still anonymous: anyone who asks for a new session gets a new 50-point wallet. A
  profile adds a nickname and avatar, and the cashier QR code proves which wallet to top up, but there
  are no logins.
- The 19+ check is self-attested (no ID verification).
- The game's wallet panel is built at runtime with plain UI widgets. It works, but it isn't yet
  styled to match the pixel-art HUD.
- Plain HTTP on the local network only; no hosting or HTTPS yet.
- The server's health check still names the game "Quantum Cat Crash".
- With `HAPPY_HOUR=1` (demo only) the spring butterfly and winter 2.00x strategies return more than
  100% to the player (about 119% and 105%, `KittyAPI\tests\sim_rtp.py --happy-hour`). Never run it
  with real money.
- Editor menu **Kitty Crash > Install Schrödinger Box + Meow Cash-Out** still exists although
  voice cash-out was removed.

## Credits

- Cat sprites (Pet Cats pack). TODO: I will fill in author and licence myself
- Plants (KebapGuy pack). TODO: I will fill in author and licence myself
- Font (Fredoka). TODO: I will fill in author and licence myself
- Music (Season Cycle). TODO: I will fill in author and licence myself
- Sound effects (generated in-house). TODO: I will fill in author and licence myself
