using System;

namespace ArchiveGenerator
{
	public sealed class Tuple<T, T2>
	{
		private T valA;
		public T ValA
		{
			get { return valA; }
			set { valA = value; }
		}
		private T2 valB;
		public T2 ValB
		{
			get { return valB; }
			set { valB = value; }
		}

		public Tuple(T valA, T2 valB)
		{
			this.valA = valA;
			this.valB = valB;
		}

		public override bool Equals(object obj)
		{
			if (Object.ReferenceEquals(this, obj)) return true;
			if (!(obj is Tuple<T, T2>)) return false;
			return ((Tuple<T, T2>)obj).ValA.Equals(this.ValA) && ((Tuple<T, T2>)obj).ValB.Equals(this.ValB);
		}

		public override int GetHashCode()
		{
			return valA.GetHashCode() ^ valB.GetHashCode();
		}

		public override string ToString()
		{
			return "{ " + valA + ", " + valB + " }";
		}
	}
}
