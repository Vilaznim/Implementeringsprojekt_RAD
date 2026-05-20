using System;
using System.Diagnostics;
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

		static IEnumerable<(ulong, int)> CreateStream(int n, int l)
		{
			var rnd = new Random();
			byte[] b = new byte[8];
			rnd.NextBytes(b);
			ulong a = 0UL;
			for (int i = 0; i < 8; ++i) a = (a << 8) + b[i];
			// we demand that our random number has 30 zeros on the least significant
			// bits and then a one following the assignments generator.
			a = (a | ((1UL << 31) - 1UL)) ^ ((1UL << 30) - 1UL);
			ulong x = 0UL;
			// mask = (((1UL << l) - 1UL) << 30)
			ulong mask;
			if (l >= 34)
			{
				// shifting >=64 would be undefined clamp to all ones in practice
				mask = ulong.MaxValue & (~((1UL << 30) - 1UL));
			}
			else
			{
				mask = ((1UL << l) - 1UL) << 30;
			}

			for (int i = 0; i < n/3; ++i)
			{
				x = unchecked(x + a);
				yield return (x & mask, 1);
			}
			for (int i = 0; i < (n + 1)/3; ++i)
			{
				x = unchecked(x + a);
				yield return (x & mask, -1);
			}
			for (int i = 0; i < (n + 2)/3; ++i)
			{
				x = unchecked(x + a);
				yield return (x & mask, 1);
			}
		}

		static void Main(string[] args)
		{
			int n = args.Length > 0 ? int.Parse(args[0]) : 200000;
			int l = args.Length > 1 ? int.Parse(args[1]) : 16;

			Console.WriteLine($"Running benchmark n={n}, l={l}");

			// prepare multiply-shift parameter a (odd 64-bit)
			var rnd = new Random();
			ulong a_ms = 0UL;
			byte[] buf = new byte[8];
			rnd.NextBytes(buf);
			for (int i = 0; i < 8; ++i) a_ms = (a_ms << 8) + buf[i];
			a_ms |= 1UL; // make it odd

			// prepare multiply-mod-prime parameters a and b
			BigInteger a_mp = 0;
			BigInteger b_mp = 0;
			// generate 89-bit randoms
			byte[] raw = new byte[12]; // 96 bits
			rnd.NextBytes(raw);
			for (int i = 0; i < 12; ++i) a_mp = (a_mp << 8) + raw[i];
			a_mp &= MersenneMask;
			rnd.NextBytes(raw);
			for (int i = 0; i < 12; ++i) b_mp = (b_mp << 8) + raw[i];
			b_mp &= MersenneMask;

			// warmup
			Console.WriteLine("Warming up");
			foreach (var _ in CreateStream(1000, l)) { }

			// multiply-shift benchmark
			var sw = Stopwatch.StartNew();
			ulong sum_ms = 0UL;
			foreach (var (x, d) in CreateStream(n, l))
			{
				sum_ms += MultiplyShift(a_ms, x, l);
			}
			sw.Stop();
			Console.WriteLine($"Multiply-Shift: sum={sum_ms} time={sw.ElapsedMilliseconds}ms");

			// multiply-mod-prime benchmark
			sw.Restart();
			ulong sum_mp = 0UL;
			foreach (var (x, d) in CreateStream(n, l))
			{
				sum_mp += MultiplyModPrime(a_mp, b_mp, x, l);
			}
			sw.Stop();
			Console.WriteLine($"Multiply-Mod-Prime: sum={sum_mp} time={sw.ElapsedMilliseconds}ms");
		}
        
		static (BigInteger, long) ComputeQuadraticSum(IEnumerable<(ulong, int)> stream, Func<ulong, int> hash, int l)
		{
			var ht = new HashTableChaining(hash, l);
			var sw = Stopwatch.StartNew();
			foreach (var (x, d) in stream)
			{
				ht.Increment(x, d);
			}
			sw.Stop();
			BigInteger S = BigInteger.Zero;
			foreach (var e in ht.Entries())
			{
				BigInteger v = new BigInteger(e.Value);
				S += v * v;
			}
			return (S, sw.ElapsedMilliseconds);
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
