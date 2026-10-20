# Changelog

All notable changes to the Catino Unity project. Server changes are listed in
`KittyAPI/CHANGELOG.md`. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased] - develop-v2

### Added (Phase 3, server v2.0.0-alpha.4)
- Wallet and play-safe panel in the game (WALLET / PLAY SAFE buttons, key W), built at runtime:
  19+ gate, daily points, demo packs with a simulated checkout (generic methods, required
  checkbox, receipts), mock ticket entry, history, daily spend limit, reality check, cool-off,
  self-exclusion, out-of-points page.

### Changed (Phase 3)
- Game shortcuts (Space/Enter, C, F, M, N, Q, W) are ignored while a text field has focus.
- The balance is labelled points and shown without decimals.
- Crowd mode no longer opens a fresh server session when the PC runs out of points. The
  out-of-points page replaces it.
- Footer shows `v2.0.0-alpha.4`. KittyAPI pointer moved to server `v2.0.0-alpha.4`.

### Added (Phase 2, server v2.0.0-alpha.3)
- Editor menu **Kitty Crash > Server > Use v2 Server (port 8001)** / **Use v1 Server (port 8000)**
  to point the scene's Api Backend at either server without hand-editing the scene.

### Changed (Phase 2)
- `start_server.bat` starts the v2 server on port 8001 with its own database
  (`KittyAPI\data\catino-v2.db`). The v1 demo keeps port 8000 and its file.
- Server mode uses persistent point wallets (50-point start, whole-point bets and payouts).
- Footer shows `v2.0.0-alpha.3`. KittyAPI pointer moved to server `v2.0.0-alpha.3`.

### Added (Phase 1b, server v2.0.0-alpha.2)
- Version footer (bottom right): game version, server version or "offline", and the server's
  promo line ("HAPPY HOUR (demo)"). Installs itself at runtime; no scene or prefab change.
- `.gitmodules` entry for `KittyAPI`, so fresh clones can fetch the server.

### Changed (Phase 1b)
- Season banners and rule hints show the bonus size the server reports (+4% by default) instead
  of a hard-coded +25% / +10%.
- KittyAPI pointer moved to server `v2.0.0-alpha.2` (fairness seal with `/verify`, safe bonuses).

### Added (Phase 1a, server v2.0.0-alpha.1)
- `start_server.bat`: double-click start of the server on port 8000 with the `hackathon` conda env,
  with clear messages when conda or the env is missing or the port is taken.
- README with how to run offline and with the server, controls, known limits and credits.
- This changelog.

### Changed
- KittyAPI pointer moved to server `v2.0.0-alpha.1`.
- `.gitignore` covers secrets, keys, certificates, databases and scratch folders; duplicate
  `Assets.zip` line removed.

## [1.0-pitch]

Frozen release shown at the pitch.
