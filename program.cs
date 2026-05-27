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
