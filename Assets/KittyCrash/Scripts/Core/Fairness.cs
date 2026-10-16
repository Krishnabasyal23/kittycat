using System;
using System.Globalization;
using System.Text;

namespace KittyCrash
{
    /// <summary>
    /// The fairness proof the server reveals after a round (see KittyAPI fairness.py).
    /// Field names match the JSON so JsonUtility can fill it directly.
    /// </summary>
    [Serializable]
    public class QuantumProof
    {
        public string scheme;          // "value" (full proof) or "crash" (hash only)
        public string commitment;      // sha256("{round_id}:{value}:{salt}") published at round start
        public string salt;
        public string value;           // the random integer the crash point came from (as text: it can be big)
        public int bits;               // 36 = 6 qubits x 6 samples
        public int qubits;             // 6
        public string source;          // "hybrid" (quantum XOR classical), "secure", "secure_fallback", "offline"
        public int[] quantum_samples;  // the measured qubit registers, e.g. [43,20,22,48,11,16]
        public string classical_mask;  // hybrid only: value = concat(samples) XOR mask
        public double house_edge;
        public double max_multiplier;
        public double crash_point_exact;

        public bool IsValid => !string.IsNullOrEmpty(commitment) && !string.IsNullOrEmpty(salt);
        public bool HasQubits => quantum_samples != null && quantum_samples.Length > 0;
    }

    public class FairnessResult
    {
        public bool hashOk;
        public bool? mathOk;
        public bool? quantumOk;
        public double u;
        public double recomputedCrash;
        public long value;
        public long combinedQubits;
        public long mask;
        public bool AllOk => hashOk && mathOk != false && quantumOk != false;
    }

    public static class Fairness
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Checks a revealed proof exactly like the server's fairness.verify().</summary>
        public static FairnessResult Verify(string roundId, QuantumProof p, double crashPoint)
        {
            var r = new FairnessResult();
            if (p == null || !p.IsValid) return r;
            r.hashOk = string.Equals(Sha256Hex($"{roundId}:{p.value}:{p.salt}"), p.commitment, StringComparison.OrdinalIgnoreCase);

            if (p.scheme == "value" && long.TryParse(p.value, NumberStyles.Integer, Inv, out long v) && p.bits > 0 && p.bits < 63)
            {
                r.value = v;
                r.u = (v + 0.5) / Math.Pow(2.0, p.bits);
                double crash = (1.0 - p.house_edge) / r.u;
                r.recomputedCrash = Math.Max(1.0, Math.Min(crash, p.max_multiplier > 1 ? p.max_multiplier : 100.0));
                double target = p.crash_point_exact > 0 ? p.crash_point_exact : crashPoint;
                r.mathOk = Math.Abs(r.recomputedCrash - target) < 1e-6 * Math.Max(1.0, target);

                if (p.HasQubits && p.qubits > 0)
                {
                    long combined = 0;
                    foreach (int s in p.quantum_samples) combined = (combined << p.qubits) | (long)s;
                    long mask = 0;
                    if (!string.IsNullOrEmpty(p.classical_mask)) long.TryParse(p.classical_mask, NumberStyles.Integer, Inv, out mask);
                    r.combinedQubits = combined;
                    r.mask = mask;
                    r.quantumOk = (combined ^ mask) == v;
                }
            }
            return r;
        }

        /// <summary>A proof for offline practice / spectator rounds (classical randomness, same commit-reveal idea).</summary>
        public static QuantumProof MakeOffline(string roundId, Random rng, double houseEdge, double maxMultiplier, out double u)
        {
            const int bits = 36;
            long value = ((long)rng.Next(1 << 18) << 18) | (long)rng.Next(1 << 18);
            u = (value + 0.5) / Math.Pow(2.0, bits);
            var salt = new StringBuilder();
            for (int i = 0; i < 16; i++) salt.Append(rng.Next(256).ToString("x2"));
            string vs = value.ToString(Inv);
            var p = new QuantumProof
            {
                scheme = "value",
                salt = salt.ToString(),
                value = vs,
                bits = bits,
                qubits = 6,
                source = "offline",
                house_edge = houseEdge,
                max_multiplier = maxMultiplier,
            };
            p.commitment = Sha256Hex($"{roundId}:{vs}:{p.salt}");
            double crash = (1.0 - houseEdge) / u;
            p.crash_point_exact = Math.Max(1.0, Math.Min(crash, maxMultiplier));
            return p;
        }

        // ── tiny SHA-256 (works on every platform, incl. WebGL) ──

        static readonly uint[] K =
        {
            0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,
            0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
            0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,
            0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
            0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,
            0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
        };

        static uint R(uint x, int n) => (x >> n) | (x << (32 - n));

        public static string Sha256Hex(string text)
        {
            byte[] msg = Encoding.UTF8.GetBytes(text);
            long bitLen = (long)msg.Length * 8;
            int padLen = ((msg.Length + 9 + 63) / 64) * 64;
            var data = new byte[padLen];
            Array.Copy(msg, data, msg.Length);
            data[msg.Length] = 0x80;
            for (int i = 0; i < 8; i++) data[padLen - 1 - i] = (byte)(bitLen >> (8 * i));

            uint h0 = 0x6a09e667, h1 = 0xbb67ae85, h2 = 0x3c6ef372, h3 = 0xa54ff53a, h4 = 0x510e527f, h5 = 0x9b05688c, h6 = 0x1f83d9ab, h7 = 0x5be0cd19;
            var w = new uint[64];
            for (int chunk = 0; chunk < padLen; chunk += 64)
            {
                for (int i = 0; i < 16; i++)
                    w[i] = (uint)(data[chunk + i * 4] << 24 | data[chunk + i * 4 + 1] << 16 | data[chunk + i * 4 + 2] << 8 | data[chunk + i * 4 + 3]);
                for (int i = 16; i < 64; i++)
                {
                    uint s0 = R(w[i - 15], 7) ^ R(w[i - 15], 18) ^ (w[i - 15] >> 3);
                    uint s1 = R(w[i - 2], 17) ^ R(w[i - 2], 19) ^ (w[i - 2] >> 10);
                    w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
                }
                uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;
                for (int i = 0; i < 64; i++)
                {
                    uint S1 = R(e, 6) ^ R(e, 11) ^ R(e, 25);
                    uint ch = (e & f) ^ (~e & g);
                    uint t1 = unchecked(h + S1 + ch + K[i] + w[i]);
                    uint S0 = R(a, 2) ^ R(a, 13) ^ R(a, 22);
                    uint mj = (a & b) ^ (a & c) ^ (b & c);
                    uint t2 = unchecked(S0 + mj);
                    h = g; g = f; f = e; e = unchecked(d + t1); d = c; c = b; b = a; a = unchecked(t1 + t2);
                }
                unchecked { h0 += a; h1 += b; h2 += c; h3 += d; h4 += e; h5 += f; h6 += g; h7 += h; }
            }
            var sb = new StringBuilder(64);
            foreach (uint x in new[] { h0, h1, h2, h3, h4, h5, h6, h7 }) sb.Append(x.ToString("x8"));
            return sb.ToString();
        }
    }
}
