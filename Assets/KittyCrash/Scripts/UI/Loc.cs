using System.Collections.Generic;
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// English / French strings for the main screens (title, menu pages, bet card, status line).
    /// Loc.T("key") or Loc.F("key", args) for text with {0} slots. The language is saved (Settings > Language).
    /// A missing key shows the key itself, so gaps are easy to spot.
    /// </summary>
    public static class Loc
    {
        public enum Lang { En, Fr }

        const string Key = "kc_lang";
        static bool loaded;
        static Lang lang;

        public static Lang Current
        {
            get { if (!loaded) { loaded = true; lang = PlayerPrefs.GetInt(Key, 0) == 1 ? Lang.Fr : Lang.En; } return lang; }
            set { loaded = true; lang = value; PlayerPrefs.SetInt(Key, value == Lang.Fr ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var pair)) return key;
            return Current == Lang.Fr ? pair.fr : pair.en;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        static readonly Dictionary<string, (string en, string fr)> Table = new Dictionary<string, (string, string)>
        {
            // title screen
            ["title.tagline"] = ("Bet points, watch the cat walk, cash out before it gets distracted.",
                                 "Misez des points, regardez le chat marcher, encaissez avant qu'il soit distrait."),
            ["title.play"] = ("PLAY", "JOUER"),
            ["title.howto"] = ("HOW TO PLAY", "COMMENT JOUER"),
            ["title.settings"] = ("SETTINGS", "PARAMÈTRES"),
            ["title.safe"] = ("SAFE PLAY", "JEU RESPONSABLE"),
            ["title.quit"] = ("QUIT", "QUITTER"),
            ["title.footer"] = ("Points only, never money.  Players must be 19+.  v{0}",
                                "Des points seulement, jamais d'argent.  19 ans et plus.  v{0}"),

            // menu
            ["menu.button"] = ("MENU", "MENU"),
            ["menu.title"] = ("Menu", "Menu"),
            ["menu.wallet"] = ("WALLET", "PORTEFEUILLE"),
            ["menu.show"] = ("Show on screen (this session)", "Afficher à l'écran (cette session)"),
            ["menu.graph"] = ("Multiplier graph", "Graphique du multiplicateur"),
            ["menu.history"] = ("Round history", "Historique des tours"),
            ["menu.leaderboard"] = ("Crowd leaderboard", "Classement de la salle"),
            ["menu.mood"] = ("Mood face", "Humeur du chat"),
            ["menu.crowd"] = ("CROWD MODE (PHONES JOIN WITH A QR CODE)", "MODE SALLE (LES TÉLÉPHONES REJOIGNENT PAR CODE QR)"),
            ["menu.reset"] = ("DEMO RESET", "RÉINITIALISER (DÉMO)"),
            ["menu.titlescreen"] = ("TITLE SCREEN", "ÉCRAN TITRE"),
            ["menu.back"] = ("BACK TO THE GAME", "RETOUR AU JEU"),
            ["common.back"] = ("BACK", "RETOUR"),
            ["common.cancel"] = ("Cancel", "Annuler"),
            ["common.on"] = ("ON", "OUI"),
            ["common.off"] = ("OFF", "NON"),
            ["wallet.needsServer"] = ("The wallet needs the game server. Switch to online play in Settings.",
                                      "Le portefeuille a besoin du serveur de jeu. Passez au jeu en ligne dans les Paramètres."),

            // how to play
            ["howto.title"] = ("How to play", "Comment jouer"),
            ["howto.1"] = ("1.  While betting is open, choose how many points to bet and press BET (or Space).",
                           "1.  Pendant les mises, choisissez combien de points miser et appuyez sur MISER (ou Espace)."),
            ["howto.2"] = ("2.  The cat starts walking and the multiplier climbs.",
                           "2.  Le chat se met à marcher et le multiplicateur monte."),
            ["howto.3"] = ("3.  Press CASH OUT (or Space) before the cat gets distracted. You win bet x multiplier.",
                           "3.  Appuyez sur ENCAISSER (ou Espace) avant que le chat soit distrait. Vous gagnez mise x multiplicateur."),
            ["howto.4"] = ("4.  If the cat gets distracted first, the bet is lost. Auto cash-out can cash out for you.",
                           "4.  Si le chat est distrait avant, la mise est perdue. L'encaissement auto peut encaisser pour vous."),
            ["howto.5"] = ("5.  Each season changes one rule. The forecast while betting shows what's coming.",
                           "5.  Chaque saison change une règle. Pendant les mises, la prévision montre ce qui s'en vient."),
            ["howto.points"] = ("Points are not money and can't be cashed out. Take breaks: Safe play is in the menu.",
                                "Les points ne sont pas de l'argent et ne s'encaissent pas. Prenez des pauses : le jeu responsable est dans le menu."),
            ["howto.keys"] = ("Keys:  Space bet / cash out  ·  Esc menu  ·  W wallet  ·  C crowd mode  ·  M music  ·  F1 help  ·  F5 demo reset",
                              "Touches :  Espace miser / encaisser  ·  Échap menu  ·  W portefeuille  ·  C mode salle  ·  M musique  ·  F1 aide  ·  F5 réinitialiser"),

            // settings
            ["settings.title"] = ("Settings", "Paramètres"),
            ["settings.mute"] = ("Mute all sound", "Couper tout le son"),
            ["settings.volume"] = ("Volume  {0}%", "Volume  {0} %"),
            ["settings.classic"] = ("Classic layout (everything on screen)", "Disposition classique (tout à l'écran)"),
            ["settings.titleAtStart"] = ("Show the title screen at start", "Afficher l'écran titre au démarrage"),
            ["settings.online"] = ("Play online (game server)", "Jouer en ligne (serveur de jeu)"),
            ["settings.language"] = ("Language: English", "Langue : français"),

            // safe play
            ["safe.title"] = ("Safe play", "Jeu responsable"),
            ["safe.points"] = ("Catino is a game. Points have no cash value and can't be cashed out.",
                               "Catino est un jeu. Les points n'ont aucune valeur en argent et ne s'encaissent pas."),
            ["safe.1"] = ("-  Decide how long you'll play before you start, and stop when the time is up.",
                          "-  Décidez combien de temps vous jouerez avant de commencer, et arrêtez quand le temps est écoulé."),
            ["safe.2"] = ("-  Each walk is random. The next walk doesn't know about the last one, so chasing losses never helps.",
                          "-  Chaque promenade est aléatoire. La suivante ne dépend pas de la précédente : courir après ses pertes ne sert à rien."),
            ["safe.3"] = ("-  On average the game pays back less than you bet. Playing longer doesn't make a win more likely.",
                          "-  En moyenne, le jeu redonne moins que ce que vous misez. Jouer plus longtemps ne rend pas un gain plus probable."),
            ["safe.4"] = ("-  Take breaks. If playing stops being fun, or you feel you have to play, stop.",
                          "-  Prenez des pauses. Si le jeu n'est plus amusant, ou si vous sentez que vous devez jouer, arrêtez."),
            ["safe.tools"] = ("Your tools (server mode): a daily spend limit, a reality check reminder, a cool-off break and self-exclusion. Breaks and self-exclusion can't be shortened.",
                              "Vos outils (mode serveur) : une limite de mise quotidienne, un rappel de durée de jeu, une pause et l'auto-exclusion. Les pauses et l'auto-exclusion ne peuvent pas être raccourcies."),
            ["safe.open"] = ("LIMITS, REALITY CHECK AND BREAKS", "LIMITES, RAPPELS ET PAUSES"),
            ["safe.help"] = ("Need to talk to someone? ConnexOntario: call 1-866-531-2600 or text CONNEX to 247247 (free, 24/7, includes gambling support).",
                             "Besoin de parler à quelqu'un? ConnexOntario : appelez le 1-866-531-2600 ou textez CONNEX au 247247 (gratuit, 24 h sur 24, 7 jours sur 7, aide pour le jeu incluse)."),
            ["safe.crisis"] = ("In crisis: call or text 9-8-8 (Canada, 24/7). In danger: call 9-1-1.",
                               "En crise : appelez ou textez le 9-8-8 (Canada, 24/7). En danger : composez le 9-1-1."),

            // demo reset
            ["reset.title"] = ("Demo reset", "Réinitialisation (démo)"),
            ["reset.ask"] = ("Start over as a new demo player? You get a fresh wallet with the starting points, and breaks and limits on this computer are cleared.",
                             "Recommencer comme nouveau joueur de démo? Vous obtenez un nouveau portefeuille avec les points de départ, et les pauses et limites sur cet ordinateur sont effacées."),
            ["reset.server"] = ("The old wallet and its history stay on the server.", "L'ancien portefeuille et son historique restent sur le serveur."),
            ["reset.offline"] = ("This resets the offline practice balance.", "Cela remet à zéro le solde de pratique hors ligne."),
            ["reset.yes"] = ("YES, START OVER", "OUI, RECOMMENCER"),
            ["reset.done"] = ("Done. You're a new player.", "C'est fait. Vous êtes un nouveau joueur."),
            ["reset.notReady"] = ("The game isn't ready yet.", "Le jeu n'est pas encore prêt."),
            ["reset.bet"] = ("Finish or cancel your bet first.", "Terminez ou annulez votre mise d'abord."),
            ["reset.walk"] = ("Wait for this walk to end, then try again.", "Attendez la fin de cette promenade, puis réessayez."),
            ["reset.demoOnly"] = ("Demo reset is only available when the server runs in demo mode.",
                                  "La réinitialisation n'est offerte que lorsque le serveur est en mode démo."),

            // game screen
            ["hud.yourBet"] = ("YOUR BET", "VOTRE MISE"),
            ["hud.points"] = ("POINTS", "POINTS"),
            ["hud.unit"] = ("points", "points"),
            ["hud.auto"] = ("Auto cash-out at", "Auto-encaisser à"),
            ["hud.graph"] = ("MULTIPLIER OVER TIME", "COURBE DU MULTIPLICATEUR"),
            ["hud.connecting"] = ("Connecting...", "Connexion..."),
            ["hud.offline"] = ("Can't reach the server - retrying in {0}s", "Serveur injoignable - nouvel essai dans {0} s"),
            ["hud.locked"] = ("Bet locked in: {0}  -  kitty starts in {1:0.0}s", "Mise confirmée : {0}  -  le chat part dans {1:0.0} s"),
            ["hud.placeBets"] = ("Place your bets!  Next walk in {0:0.0}s", "Faites vos mises!  Prochaine promenade dans {0:0.0} s"),
            ["hud.placeOne"] = ("Place a bet to send kitty walking", "Misez pour faire marcher le chat"),
            ["hud.starting"] = ("Kitty is stretching...  rolling the quantum dice", "Le chat s'étire...  on lance les dés quantiques"),
            ["hud.warming"] = ("Kitty is warming up...  multiplier starts in {0:0.0}s", "Le chat se réchauffe...  le multiplicateur part dans {0:0.0} s"),
            ["hud.walked"] = ("Kitty has walked {0:0.0} m", "Le chat a marché {0:0.0} m"),
            ["hud.walking"] = ("Kitty is walking...", "Le chat marche..."),
            ["hud.sniff"] = ("   ...sniff sniff", "   ...snif snif"),
            ["hud.cashedAt"] = ("You cashed out at {0:0.00}x - watch where kitty stops!", "Encaissé à {0:0.00}x - regardez où le chat s'arrête!"),
            ["hud.watching"] = ("Just watching this round - bets open when kitty gets distracted", "Vous regardez ce tour - les mises ouvrent quand le chat est distrait"),
            ["hud.distracted"] = ("DISTRACTED by {0}!", "DISTRAIT par {0}!"),
            ["hud.cancelBet"] = ("CANCEL BET", "ANNULER LA MISE"),
            ["hud.bet"] = ("BET", "MISER"),
            ["hud.betIn"] = ("BET   ({0}s)", "MISER   ({0} s)"),
            ["hud.startingBtn"] = ("STARTING...", "DÉPART..."),
            ["hud.watchingBtn"] = ("WATCHING...", "EN OBSERVATION..."),
            ["hud.won"] = ("WON  +{0}", "GAGNÉ  +{0}"),
            ["hud.cashingOut"] = ("CASHING OUT...", "ENCAISSEMENT..."),
            ["hud.warmingBtn"] = ("WARMING UP  {0:0.0}s", "ÉCHAUFFEMENT  {0:0.0} s"),
            ["hud.cashOut"] = ("CASH OUT  {0}", "ENCAISSER  {0}"),
            ["hud.betsSoon"] = ("BETS OPEN SOON", "MISES À VENIR"),
            ["hud.lost"] = ("LOST  -{0}", "PERDU  -{0}"),
            ["hud.offlineBtn"] = ("OFFLINE", "HORS LIGNE"),
        };
    }
}
