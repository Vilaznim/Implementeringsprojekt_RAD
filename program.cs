using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;

namespace Implementeringsprojekt
{
	class Program
	{
		static ulong MultiplyShift(ulong a, ulong x, int l)
		{
			unchecked
			{
				ulong prod = a * x;
				return prod >> (64 - l);
			}
		}

		static BigInteger P => (BigInteger.One << 89) - 1;

		static BigInteger MersenneMask => (BigInteger.One << 89) - 1;

		static ulong SampleOddUlong(Random rnd)
		{
			byte[] buf = new byte[8];
			rnd.NextBytes(buf);
			ulong value = 0UL;
			for (int i = 0; i < 8; ++i) value = (value << 8) + buf[i];
			return value | 1UL;
		}

		static BigInteger SampleUniformInP(Random rnd)
		{
			byte[] raw = new byte[12];
			while (true)
			{
				rnd.NextBytes(raw);
				BigInteger value = 0;
				for (int i = 0; i < 12; ++i) value = (value << 8) + raw[i];
				value &= MersenneMask;
				if (value != P)
				{
					return value;
				}
			}
		}

		static ulong MultiplyModPrime(BigInteger a, BigInteger b, ulong x, int l)
		{
			BigInteger t = a * new BigInteger(x) + b;
			// Reduce modulo p = 2^89 - 1 using Mersenne reduction
			BigInteger mask = MersenneMask;
			while (t > mask)
			{
				BigInteger low = t & mask;
				BigInteger high = t >> 89;
				t = low + high;
			}
			if (t >= P) t -= P;
			ulong result = (ulong)(t & ((BigInteger.One << l) - 1));
			return result;
		}

		// Mersenne reduction helper for p = 2^89 - 1
		static BigInteger MersenneReduce(BigInteger t)
		{
			BigInteger mask = MersenneMask;
			while (t > mask)
			{
				BigInteger low = t & mask;
				BigInteger high = t >> 89;
				t = low + high;
			}
			if (t >= P) t -= P;
			return t;
		}

		// Evaluate g(x) = a0 + a1*x + a2*x^2 + a3*x^3 (mod p) in the same
		// structure as Algorithm 1 from the assignment notes.
		static BigInteger EvaluateG(BigInteger a0, BigInteger a1, BigInteger a2, BigInteger a3, ulong x)
		{
			BigInteger bx = new BigInteger(x);
			BigInteger y = a3;
			y = y * bx + a2;
			y = (y & MersenneMask) + (y >> 89);
			if (y >= P) y -= P;
			y = y * bx + a1;
			y = (y & MersenneMask) + (y >> 89);
			if (y >= P) y -= P;
			y = y * bx + a0;
			y = (y & MersenneMask) + (y >> 89);
			if (y >= P) y -= P;
			return y;
		}

		static Func<ulong, BigInteger> MakeGFunction(BigInteger a0, BigInteger a1, BigInteger a2, BigInteger a3)
		{
			return (ulong x) => EvaluateG(a0, a1, a2, a3, x);
		}

		// Algorithm 2: derive h(x) and s(x) from the same 4-universal g(x).
		// h(x) uses the t least significant bits, and s(x) depends on the top bit.
		static (Func<ulong, ulong> h, Func<ulong, int> s) MakeCountSketchHashes(Func<ulong, BigInteger> g, int t)
		{
			if (t < 0 || t > 64) throw new ArgumentOutOfRangeException(nameof(t), "t must be between 0 and 64");
			BigInteger hMask = (BigInteger.One << t) - 1;
			return (
				(ulong x) => (ulong)(g(x) & hMask),
				(ulong x) =>
				{
					BigInteger gx = g(x);
					int bx = (int)(gx >> 88); // b = 89, so b-1 = 88
					return 1 - 2 * bx;
				}
			);
		}

		static (BigInteger, BigInteger, BigInteger, BigInteger) SampleGCoefficients(Random rnd)
		{
			return (SampleUniformInP(rnd), SampleUniformInP(rnd), SampleUniformInP(rnd), SampleUniformInP(rnd));
		}

		public static IEnumerable<Tuple<ulong, int>> CreateStream(int n, int l)
		{
			// We generate a random uint64 number .
			Random rnd = new System.Random();
			ulong a = 0UL;
			Byte[] b = new Byte[8];
			rnd.NextBytes(b);
			for (int i = 0; i < 8; ++i) {
				a = (a << 8) + (ulong)b[i];
			}
			// We demand that our random number has 30 zeros on the
			// least
			// significant bits and then a one.
			a = (a | ((1UL << 31) - 1UL)) ^ ((1UL << 30) - 1UL);
			ulong x = 0UL;
			for (int i = 0; i < n / 3; ++i) {
				x = x + a;
				yield return Tuple.Create(x & (((1UL << l) - 1UL) << 30), 1);
			}
			for (int i = 0; i < (n + 1) / 3; ++i) {
				x = x + a;
				yield return Tuple.Create(x & (((1UL << l) - 1UL) << 30), -1);
			}
			for (int i = 0; i < (n + 2) / 3; ++i) {
				x = x + a;
				yield return Tuple.Create(x & (((1UL << l) - 1UL) << 30), 1);
			}
		}

		static void Main(string[] args)
		{
			if (args.Length > 0 && string.Equals(args[0], "opgave7", StringComparison.OrdinalIgnoreCase))
			{
				int opgave7N = args.Length > 1 ? int.Parse(args[1]) : 200000;
				int opgave7DistinctL = args.Length > 2 ? int.Parse(args[2]) : GetSuggestedOpgave7DistinctL(opgave7N);
				int[] opgave7TValues = args.Length > 3 ? ParseIntList(args[3]) : BuildDefaultOpgave7TValues(opgave7DistinctL);
				int opgave7Experiments = args.Length > 4 ? int.Parse(args[4]) : 100;
				RunOpgave7(opgave7N, opgave7DistinctL, opgave7TValues, opgave7Experiments);
				return;
			}

			int n = args.Length > 0 ? int.Parse(args[0]) : 200000;
			int l = args.Length > 1 ? int.Parse(args[1]) : 16;
			int opgave3MinL = args.Length > 2 ? int.Parse(args[2]) : -1;
			int opgave3MaxL = args.Length > 3 ? int.Parse(args[3]) : -1;

			Console.WriteLine($"Running benchmark n={n}, l={l}");

			// prepare multiply-shift parameter a (odd 64-bit)
			var rnd = new Random();
			ulong a_ms = 0UL;
			byte[] buf = new byte[8];
			rnd.NextBytes(buf);
			for (int i = 0; i < 8; ++i) a_ms = (a_ms << 8) + buf[i];
			a_ms |= 1UL; // make it odd

			// prepare multiply-mod-prime parameters a and b
			BigInteger a_mp = SampleUniformInP(rnd);
			BigInteger b_mp = SampleUniformInP(rnd);

			// warmup
			Console.WriteLine("Warming up");
			foreach (var _ in CreateStream(1000, l)) { }

			// multiply-shift benchmark
			var sw = Stopwatch.StartNew();
			ulong sum_ms = 0UL;
			foreach (var item in CreateStream(n, l))
			{
				sum_ms += MultiplyShift(a_ms, item.Item1, l);
			}
			sw.Stop();
			Console.WriteLine($"Multiply-Shift: sum={sum_ms} time={sw.ElapsedMilliseconds}ms");

			// multiply-mod-prime benchmark
			sw.Restart();
			ulong sum_mp = 0UL;
			foreach (var item in CreateStream(n, l))
			{
				sum_mp += MultiplyModPrime(a_mp, b_mp, item.Item1, l);
			}
			sw.Stop();
			Console.WriteLine($"Multiply-Mod-Prime: sum={sum_mp} time={sw.ElapsedMilliseconds}ms");

			RunOpgave3(n, opgave3MinL, opgave3MaxL);
		}

		static Func<ulong, int> MakeMultiplyShiftHash(ulong a_ms, int l) => (ulong x) => (int)MultiplyShift(a_ms, x, l);

		static Func<ulong, int> MakeMultiplyModPrimeHash(BigInteger a_mp, BigInteger b_mp, int l) => (ulong x) => (int)MultiplyModPrime(a_mp, b_mp, x, l);
 
		static void RunOpgave3(int n, int minLOverride, int maxLOverride)
		{
			// timeout per algorithm run in milliseconds (detect "tager for lang tid")
			const int perRunTimeoutMs = 1800000; // 30 minutes (in milliseconds)

			Console.WriteLine("\nRunning Opgave 3 experiments (Compute S with chaining)");

			int maxL = (int)Math.Floor(Math.Log(n, 2));
			if (maxLOverride >= 1) maxL = Math.Min(maxL, maxLOverride);
			int minL = minLOverride >= 1 ? minLOverride : Math.Max(1, maxL - 6);

			var lValues = new List<int>();
			for (int L = minL; L <= maxL; ++L) lValues.Add(L);

			var rnd = new Random(42);
			byte[] buf = new byte[8];
			rnd.NextBytes(buf);
			ulong a_ms = 0;
			for (int i = 0; i < 8; i++) a_ms = (a_ms << 8) + buf[i];
			a_ms |= 1UL;

			byte[] raw = new byte[12];
			rnd.NextBytes(raw);
			BigInteger a_mp = 0;
			for (int i = 0; i < 12; i++) a_mp = (a_mp << 8) + raw[i];
			a_mp &= MersenneMask;
			rnd.NextBytes(raw);
			BigInteger b_mp = 0;
			for (int i = 0; i < 12; i++) b_mp = (b_mp << 8) + raw[i];
			b_mp &= MersenneMask;

			var lines = new List<string>
			{
				"l,2^l,n,status_shift,distinct_keys_shift,time_ms_shift,S_shift,status_mod,distinct_keys_mod,time_ms_mod,S_mod"
			};

			bool stoppedEarly = false;
			foreach (var lVal in lValues)
			{
				if ((1 << lVal) > n) break;
				Console.WriteLine($"Running l={lVal} (2^l={(1 << lVal)})...");

				Console.WriteLine($"  building streamList for n={n}...");
				var streamList = new List<Tuple<ulong, int>>();
				int nextStreamProgress = Math.Max(1, n / 4);
				int streamCount = 0;
				foreach (var p in CreateStream(n, lVal))
				{
					streamList.Add(p);
					streamCount++;
					if (streamCount >= nextStreamProgress)
					{
						Console.WriteLine($"  stream built: {streamCount}/{n} items, managed memory ~{GC.GetTotalMemory(false) / (1024 * 1024)} MB");
						nextStreamProgress += Math.Max(1, n / 4);
					}
				}

				string statusShift = "ok";
				string statusMod = "ok";
				BigInteger S_shift = 0;
				BigInteger S_mod = 0;
				long t_shift = -1;
				long t_mod = -1;
				int count_shift = -1;
				int count_mod = -1;

				try
				{
					Console.WriteLine("  computing S with multiply-shift hash...");
					var h_shift = MakeMultiplyShiftHash(a_ms, lVal);
					var taskShift = Task.Run(() => ComputeQuadraticSum(streamList, h_shift, lVal));
					if (taskShift.Wait(perRunTimeoutMs))
					{
						var resultShift = taskShift.Result;
						(S_shift, t_shift, count_shift) = resultShift;
						Console.WriteLine($"  shift done: distinct={count_shift} time={t_shift}ms S={S_shift}");
					}
					else
					{
						statusShift = "timeout";
						stoppedEarly = true;
						Console.WriteLine("  shift: timeout");
					}
				}
				catch (OutOfMemoryException)
				{
					statusShift = "oom";
					stoppedEarly = true;
					Console.WriteLine("  shift: out of memory");
				}

				try
				{
					Console.WriteLine("  computing S with multiply-mod-prime hash...");
					var h_mod = MakeMultiplyModPrimeHash(a_mp, b_mp, lVal);
					var taskMod = Task.Run(() => ComputeQuadraticSum(streamList, h_mod, lVal));
					if (taskMod.Wait(perRunTimeoutMs))
					{
						var resultMod = taskMod.Result;
						(S_mod, t_mod, count_mod) = resultMod;
						Console.WriteLine($"  mod done: distinct={count_mod} time={t_mod}ms S={S_mod}");
					}
					else
					{
						statusMod = "timeout";
						stoppedEarly = true;
						Console.WriteLine("  mod: timeout");
					}
				}
				catch (OutOfMemoryException)
				{
					statusMod = "oom";
					stoppedEarly = true;
					Console.WriteLine("  mod: out of memory");
				}

				lines.Add($"{lVal},{1 << lVal},{n},{statusShift},{count_shift},{t_shift},{S_shift},{statusMod},{count_mod},{t_mod},{S_mod}");
				if (stoppedEarly)
				{
					Console.WriteLine($"Stopping Opgave 3 early at l={lVal} because memory was exhausted.");
					break;
				}
			}

			System.IO.File.WriteAllLines("opgave3_results.csv", lines);
			Console.WriteLine("Wrote opgave3_results.csv");
			Console.WriteLine($"Opgave 3 l-range used: {minL}..{maxL}");
		}

		static (BigInteger, long, int) ComputeQuadraticSum(IEnumerable<Tuple<ulong, int>> stream, Func<ulong, int> hash, int l)
		{
			var ht = new HashTableChaining(hash, l);
			var sw = Stopwatch.StartNew();
			foreach (var item in stream)
			{
				ht.Increment(item.Item1, item.Item2);
			}
			sw.Stop();
			BigInteger S = BigInteger.Zero;
			int count = 0;
			foreach (var e in ht.Entries())
			{
				BigInteger v = new BigInteger(e.Value);
				S += v * v;
				count++;
			}
			return (S, sw.ElapsedMilliseconds, count);
		}

		// Compute Count-Sketch estimate X = sum_y C[y]^2 using provided h and s
		static (BigInteger, long) ComputeCountSketch(IEnumerable<Tuple<ulong, int>> stream, Func<ulong, ulong> h, Func<ulong, int> s, int t)
		{
			if (t < 0 || t > 30) throw new ArgumentOutOfRangeException(nameof(t), "t must be between 0 and 30 for practical array sizing");
			var cs = new CountSketch(t);
			var sw = Stopwatch.StartNew();
			foreach (var item in stream)
			{
				cs.Update(item.Item1, item.Item2, h, s);
			}
			sw.Stop();
			return (cs.EstimateX(), sw.ElapsedMilliseconds);
		}

		static (BigInteger, long) ComputeCountSketch(IEnumerable<Tuple<ulong, int>> stream, Func<ulong, BigInteger> g, int t)
		{
			if (t < 0 || t > 30) throw new ArgumentOutOfRangeException(nameof(t), "t must be between 0 and 30 for practical array sizing");
			var cs = new CountSketch(t);
			BigInteger hMask = (BigInteger.One << t) - 1;
			var sw = Stopwatch.StartNew();
			foreach (var item in stream)
			{
				BigInteger gx = g(item.Item1);
				ulong bucket = (ulong)(gx & hMask);
				int bit = (int)(gx >> 88);
				int sign = 1 - 2 * bit;
				cs.Update(bucket, sign, item.Item2);
			}
			sw.Stop();
			return (cs.EstimateX(), sw.ElapsedMilliseconds);
		}

		static int[] ParseIntList(string csv)
		{
			string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries);
			var values = new List<int>();
			foreach (var part in parts)
			{
				if (int.TryParse(part.Trim(), out int value)) values.Add(value);
			}
			return values.ToArray();
		}

		static int[] BuildDefaultOpgave7TValues(int distinctL)
		{
			if (distinctL <= 2) return new[] { 1 };
			int low = Math.Max(1, distinctL - 7);
			int mid = Math.Max(1, distinctL - 5);
			int high = Math.Max(1, distinctL - 3);
			var values = new List<int>();
			foreach (int candidate in new[] { low, mid, high })
			{
				if (!values.Contains(candidate) && candidate <= 30) values.Add(candidate);
			}
			values.Sort();
			return values.ToArray();
		}

		static int GetSuggestedOpgave7DistinctL(int fallbackN)
		{
			string path = "opgave3_results.csv";
			if (!System.IO.File.Exists(path))
			{
				return Math.Max(1, (int)Math.Floor(Math.Log(fallbackN, 2)) - 1);
			}

			string[] lines = System.IO.File.ReadAllLines(path);
			int lastOkL = -1;
			int lastSeenL = -1;
			for (int i = 1; i < lines.Length; ++i)
			{
				if (string.IsNullOrWhiteSpace(lines[i])) continue;
				string[] parts = lines[i].Split(',');
				if (parts.Length < 8) continue;
				if (!int.TryParse(parts[0], out int lValue)) continue;
				lastSeenL = Math.Max(lastSeenL, lValue);
				string statusShift = parts[3].Trim();
				string statusMod = parts[7].Trim();
				if (!string.Equals(statusShift, "ok", StringComparison.OrdinalIgnoreCase) || !string.Equals(statusMod, "ok", StringComparison.OrdinalIgnoreCase))
				{
					return Math.Max(1, lValue - 1);
				}
				lastOkL = lValue;
			}
			if (lastOkL >= 1) return lastOkL;
			if (lastSeenL >= 2) return Math.Max(1, lastSeenL - 1);
			return Math.Max(1, (int)Math.Floor(Math.Log(fallbackN, 2)) - 1);
		}

		static void RunOpgave7(int n, int distinctL, int[] tValues, int experiments)
		{
			if (distinctL < 1 || distinctL > 30) throw new ArgumentOutOfRangeException(nameof(distinctL), "distinctL must be between 1 and 30");
			if (experiments < 1) throw new ArgumentOutOfRangeException(nameof(experiments), "experiments must be positive");
			if (tValues == null || tValues.Length == 0) throw new ArgumentException("At least one t value is required", nameof(tValues));

			var streamList = new List<Tuple<ulong, int>>();
			foreach (var item in CreateStream(n, distinctL))
			{
				streamList.Add(item);
			}

			Console.WriteLine($"\nRunning Opgave 7 with n={n}, distinctL={distinctL}, distinctKeys={1 << distinctL}, experiments={experiments}");

			var exactRnd = new Random(20260528);
			ulong exactA = SampleOddUlong(exactRnd);
			var exactHash = MakeMultiplyShiftHash(exactA, distinctL);
			var exactResult = ComputeQuadraticSum(streamList, exactHash, distinctL);
			BigInteger exactS = exactResult.Item1;
			long exactTimeMs = exactResult.Item2;
			int exactDistinctKeys = exactResult.Item3;
			Console.WriteLine($"Exact S={exactS} computed in {exactTimeMs} ms with {exactDistinctKeys} distinct keys");

			var summaryLines = new List<string>
			{
				"n,distinctL,distinctKeys,experiments,t,m,exactS,exact_time_ms,mse,avg_sketch_time_ms,min_sketch_time_ms,max_sketch_time_ms,avg_sketch_time_ms_double,speedup,ch_sketch_memory_bytes,chaining_memory_est_bytes"
			};

			foreach (int t in tValues)
			{
				if (t < 0 || t > 30)
				{
					Console.WriteLine($"Skipping t={t} because it is outside the supported range 0..30");
					continue;
				}

				Console.WriteLine($"Running Count-Sketch experiments for t={t} (m={1 << t})...");
				var experimentValues = new List<BigInteger>();
				var experimentTimes = new List<long>();
				var experimentSeeds = new List<int>();
				var rawLines = new List<string> { "experiment,X,time_ms" };
				var rng = new Random(100000 + t);

				for (int experiment = 1; experiment <= experiments; ++experiment)
				{
					var coeffs = SampleGCoefficients(rng);
					var g = MakeGFunction(coeffs.Item1, coeffs.Item2, coeffs.Item3, coeffs.Item4);
					var hashes = MakeCountSketchHashes(g, t);
					var sketchResult = ComputeCountSketch(streamList, hashes.h, hashes.s, t);
					experimentValues.Add(sketchResult.Item1);
					experimentTimes.Add(sketchResult.Item2);
					experimentSeeds.Add(experiment);
					rawLines.Add($"{experiment},{sketchResult.Item1},{sketchResult.Item2}");
				}

				var sortedValues = new List<BigInteger>(experimentValues);
				sortedValues.Sort();
				var sortedLines = new List<string> { "rank,X" };
				for (int i = 0; i < sortedValues.Count; ++i)
				{
					sortedLines.Add($"{i + 1},{sortedValues[i]}");
				}

				var medianValues = new List<BigInteger>();
				var medianLines = new List<string> { "group,median" };
				var medianGroupRawLines = new List<string> { "group,observations" };
				for (int group = 0; group < 9; ++group)
				{
					var groupValues = new List<BigInteger>();
					var groupEntries = new List<string>();
					for (int i = 0; i < 11; ++i)
					{
						groupValues.Add(experimentValues[group * 11 + i]);
						groupEntries.Add(experimentValues[group * 11 + i].ToString());
					}
					groupValues.Sort();
					BigInteger median = groupValues[5];
					medianValues.Add(median);
					medianLines.Add($"{group + 1},{median}");
					medianGroupRawLines.Add($"{group + 1},[{string.Join(";", groupEntries)}]");
				}
				medianValues.Sort();
				var sortedMedianLines = new List<string> { "rank,median" };
				for (int i = 0; i < medianValues.Count; ++i)
				{
					sortedMedianLines.Add($"{i + 1},{medianValues[i]}");
				}

				double mse = 0.0;
				long minTime = long.MaxValue;
				long maxTime = long.MinValue;
				long totalTime = 0;
				for (int i = 0; i < experimentValues.Count; ++i)
				{
					double diff = (double)(experimentValues[i] - exactS);
					mse += diff * diff;
					long time = experimentTimes[i];
					if (time < minTime) minTime = time;
					if (time > maxTime) maxTime = time;
					totalTime += time;
				}
				mse /= experiments;
				double avgTime = (double)totalTime / experiments;

				string baseName = $"opgave7_n{n}_l{distinctL}_t{t}";
				System.IO.File.WriteAllLines($"{baseName}_raw.csv", rawLines);
				System.IO.File.WriteAllLines($"{baseName}_sorted.csv", sortedLines);
				System.IO.File.WriteAllLines($"{baseName}_medians.csv", medianLines);
				System.IO.File.WriteAllLines($"{baseName}_medians_sorted.csv", sortedMedianLines);
				System.IO.File.WriteAllLines($"{baseName}_groups.csv", medianGroupRawLines);

				// additional metrics for Opgave 8: speedup and memory estimates
				double avgTimeDouble = avgTime; // ms
				double speedup = avgTimeDouble > 0.0 ? (double)exactTimeMs / avgTimeDouble : double.PositiveInfinity;
				long sketchMemoryBytes = (1L << t) * 8L; // one 64-bit counter per bucket
				long chainingMemoryEst = (long)exactDistinctKeys * 32L; // rough estimate ~32 bytes per distinct key
				summaryLines.Add($"{n},{distinctL},{exactDistinctKeys},{experiments},{t},{1 << t},{exactS},{exactTimeMs},{mse.ToString(System.Globalization.CultureInfo.InvariantCulture)},{avgTime.ToString(System.Globalization.CultureInfo.InvariantCulture)},{minTime},{maxTime},{avgTimeDouble.ToString(System.Globalization.CultureInfo.InvariantCulture)},{speedup.ToString(System.Globalization.CultureInfo.InvariantCulture)},{sketchMemoryBytes},{chainingMemoryEst}");

				Console.WriteLine($"  t={t}: mse={mse.ToString(System.Globalization.CultureInfo.InvariantCulture)}, avg_time={avgTime.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms, speedup={speedup.ToString(System.Globalization.CultureInfo.InvariantCulture)}, sketch_mem={sketchMemoryBytes} bytes, chaining_mem_est={chainingMemoryEst} bytes");
			}

			System.IO.File.WriteAllLines("opgave7_summary.csv", summaryLines);
			Console.WriteLine("Wrote opgave7_summary.csv and per-t experiment CSV files");
		}
	}

	// Simple hashtabel med chaining. Nøgler er 64-bit, værdier er 64-bit signed (kan være negative).
	public class HashTableChaining
	{
		public class Entry
		{
			public ulong Key;
			public long Value;
			public Entry(ulong k, long v) { Key = k; Value = v; }
		}

		private readonly List<Entry>[] buckets;
		private readonly Func<ulong, int> hash;
		private readonly int size;

		public HashTableChaining(Func<ulong, int> hash, int l)
		{
			if (l < 0 || l > 30) throw new ArgumentOutOfRangeException(nameof(l), "l must be between 0 and 30 for array sizing");
			this.hash = hash;
			size = 1 << l;
			buckets = new List<Entry>[size];
			for (int i = 0; i < size; ++i) buckets[i] = new List<Entry>();
		}

		private List<Entry> BucketFor(ulong x)
		{
			int idx = hash(x);
			return buckets[idx];
		}

		public long Get(ulong x)
		{
			var bucket = BucketFor(x);
			for (int i = 0; i < bucket.Count; ++i)
			{
				if (bucket[i].Key == x) return bucket[i].Value;
			}
			return 0L;
		}

		public void Set(ulong x, long v)
		{
			var bucket = BucketFor(x);
			for (int i = 0; i < bucket.Count; ++i)
			{
				if (bucket[i].Key == x) { bucket[i].Value = v; return; }
			}
			bucket.Add(new Entry(x, v));
		}

		public void Increment(ulong x, long d)
		{
			var bucket = BucketFor(x);
			for (int i = 0; i < bucket.Count; ++i)
			{
				if (bucket[i].Key == x) { bucket[i].Value += d; return; }
			}
			bucket.Add(new Entry(x, d));
		}

		public IEnumerable<Entry> Entries()
		{
			foreach (var b in buckets)
			{
				foreach (var e in b) yield return e;
			}
		}
	}
}

// Count-Sketch implementation
public class CountSketch
{
	private readonly long[] C;
	private readonly int m;

	public CountSketch(int t)
	{
		if (t < 0 || t > 30) throw new ArgumentOutOfRangeException(nameof(t), "t must be between 0 and 30 for array sizing");
		m = 1 << t;
		C = new long[m];
	}

	public void Update(ulong bucketIndex, int sign, int d)
	{
		C[(int)bucketIndex] += (long)sign * d;
	}

	public void Update(ulong x, int d, Func<ulong, ulong> h, Func<ulong, int> s)
	{
		Update(h(x), s(x), d);
	}

	public BigInteger EstimateX()
	{
		BigInteger sum = BigInteger.Zero;
		for (int i = 0; i < m; ++i)
		{
			BigInteger v = new BigInteger(C[i]);
			sum += v * v;
		}
		return sum;
	}

	public void Reset()
	{
		Array.Clear(C, 0, m);
	}
}
