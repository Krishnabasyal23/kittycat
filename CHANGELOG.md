# Changelog

All notable changes to the Catino Unity project. Server changes are listed in
`KittyAPI/CHANGELOG.md`. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [2.0.0-beta.3] - 2026-10-22

### Added
- The RULES page now states the house edge (4%, 96% back on average) in English and French, with the number read from the server
  (`/api/health` `house_edge_pct`). The landing page has an "odds, in the open" section.
- `start_public.bat` turns the simulated checkout back on for the public demo (labelled simulated, no real money).
- The landing page shows **Play in your browser** and **Join on your phone** only while the live demo is reachable (it reads
  `live.json` and pings the server), otherwise an offline notice. `start_online.bat` updates it by itself and sets it back to
  offline on exit.
- `start_online.bat`: tunnel and public server in one click, house table on. `start_public.bat` now turns the house table on.
- Web game: **Kitty Crash > Build > WebGL for the server (/play)** builds the game into `KittyAPI\web\play`; the server serves it at
  `/play`. In the browser it plays online from the start (`WebGlOnline`). On a public server the Crowd screen (key C) shows where
  phones join instead of trying to host.
- Online demo: `start_tunnel.bat` (cloudflared or ngrok) and `start_public.bat` (the server in public mode on this
  computer only). The README has a "Run online" section and a quick start. Server hardening and the hosting guide
  are in KittyAPI (`docs/DEPLOY.md`).
- `site/`: a static landing page (video, screenshots, links) for Vercel, Cloudflare Pages or GitHub Pages.
- Schrödinger's Box (the fairness seal, renamed; Q is unchanged) with player seeds: after the commitment is shown,
  the game sends its own random seed during the walk's lead-in and the opened box checks the seeds (and that
  ours was counted) next to the seal, the math and the qubits. French included. The panel is taller for the two
  new lines. Needs server 2.0.0-beta.2+ with migration 0010; offline practice has no seeds.
- The fairness seal (badge + Q proof panel) is now on screen in every game: it installs itself at runtime.
  MENU can hide the badge; Q always opens the proof. Its texts follow the French setting.
- `UiStyle`: the wallet panel and the menu use the HUD's rounded cards, colours, button tints, header font
  and orange accent edge.

### Fixed
- The seal never appeared: its file was named `QuantamBox.cs` while the class is `QuantumBox`, so Unity could
  not attach it. Renamed (asset ID kept). The old **Install Fairness Seal Panel** menu is gone (not needed).

## [2.0.0-beta.2] - 2026-10-22

### Added
- Picture tour (`PictureTour.cs`): how to play in four animated cards with no words, using the
  game's own art (your cat, the fish, the paw, the mood faces). Opens on the first Play, from How to
  play and with F1. The written rules moved to MENU > RULES.
- Server 2.0.0-beta.2: audit log, safe bonus tuning on the dashboard, WebSockets for the phones.

## [2.0.0-beta.1] - 2026-10-22

First beta: every v2 plan item is in (wallet, cashier, title and menu, clean layout, safe play,
operator dashboard, three-season forecast, English / French).

### Added
- French now covers the whole game screen: distracted banner, distraction names, graph captions and
  markers, season pill and festival names, forecast card and season banners (rule texts come from the
  server in French), mood face, crowd mode, cat skin picker, error lines and the wallet panel
  (including common server messages). `Loc.E` translates whole on-screen texts; `LocText` keeps fixed
  labels in step when the language changes.

### Fixed
- The title screen is fully opaque (the round's banner no longer shows through it).
- Editor menu renamed to **Kitty Crash > Install Fairness Seal Panel (Q)** (the old name mentioned a
  removed voice cash-out).

## [2.0.0-alpha.12] - 2026-10-22

### Added
- English / French strings (`Loc.cs`) for the title screen, menu pages, bet card and status line.
  Settings > Language switches and remembers it. Wallet panel and server rule texts stay English.

## [2.0.0-alpha.11] - 2026-10-22

Covers client 2.0.0-alpha.5 to alpha.11 (Phase 4 and the v2 polish items).

### Added (client 2.0.0-alpha.11, server 2.0.0-alpha.11)
- The forecast card shows the next walk's season and the two after it ("THEN: ...").
- README: forecast, cashier and operator pages, screenshot capture.

### Server 2.0.0-alpha.10 (no client change)
- Operator dashboard at `/operator` on the server computer.

### Added (client 2.0.0-alpha.9, server 2.0.0-alpha.9)
- SAFE PLAY (title screen and menu): plain-language advice, a button to the limits, reality check
  and breaks, and helplines (ConnexOntario 1-866-531-2600 or text CONNEX to 247247; 9-8-8). Numbers
  checked on connexontario.ca and 988.ca on 2026-10-22.

### Changed (client 2.0.0-alpha.8, server 2.0.0-alpha.8)
- Clean game screen by default: multiplier, cat, bet controls, balance, season and forecast. Graph,
  history, wallet buttons, crowd pill and leaderboard, mood face and the server switch are in the
  menu. Settings > Classic layout brings the old screen back.
- Classic layout: the crowd leaderboard moves below the wallet buttons; the version footer moves
  to the bottom centre so it no longer touches the graph card.
- Editor menu **Kitty Crash > Capture Screenshots**: saves title, clean, menu and classic shots at
  1920x1080 and 1366x768 to `.tmp/shots`.
- Phone page: menu sheet, folding "This walk" and "Leaderboard".

### Added (client 2.0.0-alpha.7)
- Title screen (Play, How to play, Settings, Safe play, Quit), MENU button and Esc menu.
- Settings: mute, volume, classic layout, title screen on/off, online/offline play (saved).
- Demo reset (menu or F5, asks first): a fresh player with a new wallet; server mode only in DEMO_MODE.
- Clean layout groundwork (`HudLayout`): graph, history, wallet buttons, crowd leaderboard and mood
  face can be hidden and opened from the menu.

### Changed (server v2.0.0-alpha.6)
- Ticket bonus: one typed code field, one ticket a day, generic message for any code that doesn't
  verify, entry pauses after three wrong codes. `start_server.bat` mentions `KittyAPI\.env.local`.

### Added (Phase 4, server v2.0.0-alpha.5)
- Wallet panel: any-amount demo top-up ($1-$100, 8 points per $1, daily cap shown).
- Profile page: nickname, avatar, stats and a single-use cashier QR code with its counter code.

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

Frozen v1 release.
