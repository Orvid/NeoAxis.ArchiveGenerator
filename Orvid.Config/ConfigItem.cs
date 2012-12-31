using System;

namespace Orvid.Config
{
	/// <summary>
	/// Represents a parsed config item.
	/// </summary>
	public sealed class ConfigItem
	{
		/// <summary>
		/// The name of the parsed item. This will be in lower-case
		/// and stripped of any characters requested to be stripped
		/// in the config.
		/// </summary>
		public string Name { get; private set; }
		/// <summary>
		/// The value of the parsed item.
		/// </summary>
		public string Value { get; private set; }

		/// <summary>
		/// Create a new <see cref="ConfigItem"/>.
		/// </summary>
		/// <param name="name">The name of the parsed item.</param>
		/// <param name="value">The value of the parsed item.</param>
		public ConfigItem(string name, string value)
		{
			this.Name = name;
			this.Value = value;
		}

#pragma warning disable 1591
		public override bool Equals(object obj)
		{
			if (Object.ReferenceEquals(this, obj)) return true;
			if ((object)obj == null) return false;
			if (!(obj is ConfigItem)) return false;
			return ((ConfigItem)obj).Name == this.Name && ((ConfigItem)obj).Value == this.Value;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode() ^ Value.GetHashCode();
		}

		public override string ToString()
		{
			return Name + ": " + Value;
		}
#pragma warning restore 1591
	}
}
