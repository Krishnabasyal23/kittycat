# Catino

A crash game where the "crash" is a cat getting distracted. You bet, the cat starts walking, the
multiplier climbs, and you cash out before something (a butterfly, a fish, a laser dot, a nap)
catches its eye. Up to 60 people in the room can join from their phones by scanning a QR code and
bet on the same cat. Four seasons each change one rule:

| Season | Rule |
|---|---|
| Spring | **Butterfly Catch**: cash out while the golden butterfly is on screen for +25% |
| Summer | **Summer Zoomies**: 40% chance the cat sprints and the multiplier climbs faster |
| Fall | **Leaf Storm**: gusts of leaves hide the multiplier |
| Winter | **Hot Cocoa**: slow walk; cash out at 2.00x or more for +10% |

The game uses virtual credits only.

## Repository layout

| Path | What |
|---|---|
| `Assets/KittyCrash/` | The game: scene `Scenes/KittyCrash.unity`, scripts, art, audio |
| `KittyAPI/` | Python/FastAPI server (separate git repo, see its README). Decides crash points, seasons, bonuses and payouts |
| `start_server.bat` | Double-click to start the server on port 8000 |

## Requirements

- Unity **6000.6.5f1** (Unity 6)
- For server mode: Anaconda/Miniconda with the `hackathon` env
  (`conda env create -f KittyAPI\environment.yml`)

## Run offline (no server)

1. Open the project in Unity and open `Assets/KittyCrash/Scenes/KittyCrash.unity`.
2. Press **Play**. The scene ships with **Use Api** unticked, so rounds run on `LocalBackend`, an
   offline copy of the server rules with its own practice balance.

## Run with the server

1. Double-click `start_server.bat` (or see `KittyAPI\README.md`). Wait for
   `Uvicorn running on http://0.0.0.0:8000`.
2. In Unity, find the Round Manager (Hierarchy search: `t:RoundManager`), tick **Use Api** and
   check **Base Url** is `http://127.0.0.1:8000`.
3. Press **Play**. Press **C** to show the crowd QR code; phones on the same Wi-Fi scan it to open
   `/crowd`. Run `pip install segno` in the env if the QR code does not appear.

## Controls

| Key | Action |
|---|---|
| Space / Enter | Place bet while betting is open; cash out while the cat walks |
| C | Show / hide crowd mode (QR code and phone players) |
| N | Choose the next walk's season (host / testing) |
| F | Next festival look |
| Q | Show / hide the quantum box panel |
| M | Mute / unmute music |

## Known limits

- Phones poll the server about 3 times a second (no WebSockets yet); crowd players and their
  credits live in server memory and reset when the server restarts.
- Plain HTTP on the local network only; no hosting or HTTPS yet.
- The server's health check still names the game "Quantum Cat Crash".
- Season bonuses are generous: the RTP simulation (`KittyAPI\tests\sim_rtp.py`) shows the spring
  butterfly and winter 2.00x strategies return more than 100% to the player.
- Editor menu **Kitty Crash > Install Schrödinger Box + Meow Cash-Out** still exists although
  voice cash-out was removed.

## Credits

- Cat sprites (Pet Cats pack). TODO: I will fill in author and licence myself
- Plants (KebapGuy pack). TODO: I will fill in author and licence myself
- Font (Fredoka). TODO: I will fill in author and licence myself
- Music (Season Cycle). TODO: I will fill in author and licence myself
- Sound effects (generated in-house). TODO: I will fill in author and licence myself
