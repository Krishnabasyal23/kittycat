using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KittyCrash
{
    /// <summary>
    /// Talks to the Catino FastAPI server (KittyAPI) (main app with /api/... routes).
    /// Set Base Url to wherever the server runs (default: same PC, port 8000).
    /// The session id is remembered between plays; if the server restarted, a new session is created.
    /// </summary>
    public class ApiBackend : MonoBehaviour, ICrashBackend
    {
        [Tooltip("e.g. http://127.0.0.1:8000  (or http://192.168.x.x:8000 for another PC on your network)")]
        public string baseUrl = "http://127.0.0.1:8000";
        [Tooltip("Starting a round runs the quantum RNG, which can take a few seconds.")]
        public float requestTimeout = 20f;
        [Tooltip("Must match GROWTH_RATE in crash_engine.py. Overwritten automatically if /api/health returns growth_rate.")]
        public float growthRate = CrashMath.DefaultGrowthRate;
        [Tooltip("Read automatically from /api/health (warmup_seconds). Stays 0 if the server doesn't have the warm-up patch, so client and server never disagree.")]
        public float warmupSeconds = 0f;
        public float minBet = 1f;
        public float maxBet = 500f;
        public bool rememberSession = true;
        [Tooltip("Web build: talk to the server that served the page (http(s)://host:port of the game page) instead of Base Url. " +
                 "Works when the FastAPI server also hosts the WebGL build (KittyAPI/web).")]
        public bool usePageAddressInBrowser = true;
        /// <summary>GROWTH_RATE in crash_engine.py when the server doesn't report it.</summary>
        public const float ServerDefaultGrowthRate = 0.18f;
        public bool logRequests;

        public string RngMode { get; private set; }
        /// <summary>Server version from /api/health (shown in the footer), null until connected.</summary>
        public string ServerVersion { get; private set; }
        /// <summary>e.g. "HAPPY HOUR (demo)" when the server runs demo bonuses, otherwise null.</summary>
        public string Promo { get; private set; }
        public string SessionId => sessionId;

        public string Name => "API " + baseUrl + (string.IsNullOrEmpty(RngMode) ? "" : $" ({RngMode})");
        public float GrowthRate => growthRate;
        public float WarmupSeconds => warmupSeconds;
        public decimal MinBet => (decimal)minBet;
        public decimal MaxBet => (decimal)maxBet;

        const string SessionKey = "KittyCrash_ApiSession";
        string sessionId;

        // ── JSON shapes (field names match the Python responses) ──
        [Serializable] class HealthDto { public string status; public string game; public string version; public string rng_mode; public float growth_rate; public float warmup_seconds = -1f; public string min_bet; public string max_bet; public string promo; }
        [Serializable] class SessionDto { public string session_id; public string balance; }
        [Serializable] class BalanceDto { public string balance; }
        [Serializable] class WagerDto { public string wager; }
        [Serializable] class StartDto { public string round_id; public string status; public string wager; public string balance; public float multiplier; public string distraction; public string commitment; public float growth_rate; public float warmup_seconds = -1f; }        [Serializable] class StateDto { public string status; public float multiplier; public string distraction; public string payout; }
        [Serializable] class CashDto { public string status; public float multiplier; public string payout; public string balance; }
        [Serializable] class ResultDto { public string status; public float crash_point; public string distraction; public string payout; public QuantumProof proof; }
        [Serializable] class ErrorDto { public string detail; }

        class Response
        {
            public long code;
            public string text;
            public string error;
            public bool Ok => error == null;
            public bool SessionLost => code == 404 && text != null && text.Contains("Session not found");
        }

        // ── ICrashBackend ─────────────────────────────

        public void Connect(Action<BalanceResult> done) => StartCoroutine(ConnectRoutine(done));

        IEnumerator ConnectRoutine(Action<BalanceResult> done)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (usePageAddressInBrowser && !string.IsNullOrEmpty(Application.absoluteURL))
            {
                try { baseUrl = new Uri(Application.absoluteURL).GetLeftPart(UriPartial.Authority); }
                catch (Exception) { /* keep the Inspector value */ }
            }
#endif
            var r = new Response();
            yield return Send("GET", "/api/health", null, false, r);
            if (!r.Ok) { done(new BalanceResult { ok = false, error = r.error }); yield break; }

            var health = Parse<HealthDto>(r.text);
            if (health != null)
            {
                RngMode = health.rng_mode;
                ServerVersion = health.version;
                Promo = string.IsNullOrEmpty(health.promo) ? null : health.promo;
                if (health.growth_rate > 0f) growthRate = health.growth_rate;
                else
                {
                    // Older server without growth_rate in /api/health: assume its built-in default,
                    // otherwise the cat/curve would run at a different speed than the real round.
                    growthRate = ServerDefaultGrowthRate;
                    Debug.LogWarning($"[KittyCrash] Server didn't report growth_rate - assuming {ServerDefaultGrowthRate}. Update main.py from the kit to fix.");
                }
                float mb, xb;
                if (float.TryParse(health.min_bet, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mb) && mb > 0f) minBet = mb;
                if (float.TryParse(health.max_bet, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out xb) && xb > 0f) maxBet = xb;
                if (health.warmup_seconds >= 0f) warmupSeconds = health.warmup_seconds;
            }

            if (rememberSession) sessionId = PlayerPrefs.GetString(SessionKey, "");

            if (!string.IsNullOrEmpty(sessionId))
            {
                r = new Response();
                yield return Send("GET", "/api/player/balance", null, true, r);
                if (r.Ok)
                {
                    var b = Parse<BalanceDto>(r.text);
                    done(new BalanceResult { ok = true, balance = BackendUtil.ParseMoney(b?.balance) });
                    yield break;
                }
                if (!r.SessionLost) { done(new BalanceResult { ok = false, error = r.error }); yield break; }
                sessionId = null; // server forgot us (restart) → make a new session
            }

            r = new Response();
            yield return Send("POST", "/api/sessions", "{}", false, r);
            if (!r.Ok) { done(new BalanceResult { ok = false, error = r.error }); yield break; }

            var s = Parse<SessionDto>(r.text);
            if (s == null || string.IsNullOrEmpty(s.session_id))
            {
                done(new BalanceResult { ok = false, error = "Bad /api/sessions response" });
                yield break;
            }
            sessionId = s.session_id;
            if (rememberSession) { PlayerPrefs.SetString(SessionKey, sessionId); PlayerPrefs.Save(); }
            done(new BalanceResult { ok = true, balance = BackendUtil.ParseMoney(s.balance) });
        }

        /// <summary>Forget the saved session; next Connect makes a fresh one (a new wallet with the 50-point starting grant).</summary>
        public void ForgetSession()
        {
            sessionId = null;
            PlayerPrefs.DeleteKey(SessionKey);
        }

        public void StartRound(decimal wager, Action<StartRoundResult> done)
        {
            string body = JsonUtility.ToJson(new WagerDto { wager = BackendUtil.FormatMoney(wager) });
            Request("POST", "/api/rounds", body, r =>
            {
                if (!r.Ok) { done(Fail<StartRoundResult>(r)); return; }
                var d = Parse<StartDto>(r.text);
                // Each round has its own speed (seasons / Summer Rush) and warm-up: follow the server exactly.
                if (d != null && d.growth_rate > 0f) growthRate = d.growth_rate;
                if (d != null && d.warmup_seconds >= 0f) warmupSeconds = d.warmup_seconds;
                done(new StartRoundResult
                {
                    ok = d != null,
                    error = d == null ? "Bad /api/rounds response" : null,
                    roundId = d?.round_id,
                    status = BackendUtil.ParseStatus(d?.status),
                    balance = BackendUtil.ParseMoney(d?.balance),
                    distraction = Clean(d?.distraction),
                    commitment = d?.commitment,
                });
            });
        }

        public void GetState(string roundId, Action<RoundStateResult> done)
        {
            Request("GET", $"/api/rounds/{roundId}/state", null, r =>
            {
                if (!r.Ok) { done(Fail<RoundStateResult>(r)); return; }
                var d = Parse<StateDto>(r.text);
                done(new RoundStateResult
                {
                    ok = d != null,
                    error = d == null ? "Bad state response" : null,
                    status = BackendUtil.ParseStatus(d?.status),
                    multiplier = d?.multiplier ?? 1f,
                    distraction = Clean(d?.distraction),
                    payout = BackendUtil.ParseMoney(d?.payout),
                });
            });
        }

        public void CashOut(string roundId, Action<CashOutResult> done)
        {
            Request("POST", $"/api/rounds/{roundId}/cashout", "{}", r =>
            {
                if (!r.Ok) { done(Fail<CashOutResult>(r)); return; }
                var d = Parse<CashDto>(r.text);
                done(new CashOutResult
                {
                    ok = d != null,
                    error = d == null ? "Bad cashout response" : null,
                    status = BackendUtil.ParseStatus(d?.status),
                    multiplier = d?.multiplier ?? 0f,
                    payout = BackendUtil.ParseMoney(d?.payout),
                    balance = BackendUtil.ParseMoney(d?.balance),
                });
            });
        }

        public void GetResult(string roundId, Action<RoundOutcome> done)
        {
            Request("GET", $"/api/rounds/{roundId}/result", null, r =>
            {
                if (!r.Ok) { done(Fail<RoundOutcome>(r)); return; }
                var d = Parse<ResultDto>(r.text);
                done(new RoundOutcome
                {
                    ok = d != null,
                    error = d == null ? "Bad result response" : null,
                    status = BackendUtil.ParseStatus(d?.status),
                    crashPoint = d?.crash_point ?? 0f,
                    distraction = Clean(d?.distraction),
                    payout = BackendUtil.ParseMoney(d?.payout),
                    proof = d != null && d.proof != null && d.proof.IsValid ? d.proof : null,
                });
            });
        }

        public void GetBalance(Action<BalanceResult> done)
        {
            Request("GET", "/api/player/balance", null, r =>
            {
                if (!r.Ok) { done(Fail<BalanceResult>(r)); return; }
                var d = Parse<BalanceDto>(r.text);
                done(new BalanceResult { ok = d != null, balance = BackendUtil.ParseMoney(d?.balance) });
            });
        }

        // ── HTTP plumbing ─────────────────────────────

        void Request(string method, string path, string body, Action<Response> done)
            => StartCoroutine(RequestRoutine(method, path, body, done));

        IEnumerator RequestRoutine(string method, string path, string body, Action<Response> done)
        {
            var r = new Response();
            yield return Send(method, path, body, true, r);
            done(r);
        }

        IEnumerator Send(string method, string path, string body, bool withSession, Response res)
        {
            string url = baseUrl.TrimEnd('/') + path;
            using (var req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (method == "POST")
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}"));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (withSession && !string.IsNullOrEmpty(sessionId))
                    req.SetRequestHeader("X-Session-ID", sessionId);
                req.timeout = Mathf.CeilToInt(requestTimeout);

                if (logRequests) Debug.Log($"[KittyCrash API] → {method} {path} {body}");
                yield return req.SendWebRequest();

                res.code = req.responseCode;
                res.text = req.downloadHandler != null ? req.downloadHandler.text : null;

                if (req.result == UnityWebRequest.Result.ConnectionError)
                    res.error = $"Can't reach the API at {baseUrl} — is the server running? ({req.error})";
                else if (res.code < 200 || res.code >= 300)
                    res.error = ExtractDetail(res.text) ?? $"HTTP {res.code}";

                if (logRequests) Debug.Log($"[KittyCrash API] ← {res.code} {res.text}");
            }
        }

        T Fail<T>(Response r) where T : BackendResult, new()
        {
            if (r.SessionLost) ForgetSession();
            return new T { ok = false, error = r.error, sessionLost = r.SessionLost };
        }

        static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch { return null; }
        }

        static string ExtractDetail(string json)
        {
            var e = Parse<ErrorDto>(json);
            if (e != null && !string.IsNullOrEmpty(e.detail)) return e.detail;
            if (json != null && json.Contains("\"msg\"")) return "Invalid request (check the bet amount)";
            return null;
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
