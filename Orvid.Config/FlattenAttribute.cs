using System;

namespace Orvid.Config
{
	/// <summary>
	/// When applied to a field or property, indicates
	/// that the fields and properties in it should be
	/// configured without a prefix.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class FlattenAttribute : Attribute
	{
		/// <summary>
		/// If true, allows configuring via the prefixed
		/// version of the child items in addition to the
		/// direct version.
		/// </summary>
		public bool AllowPrefixed { get; private set; }

		/// <summary>
		/// Indicates that the fields and properties in
		/// this value should be configured without a prefix.
		/// </summary>
		/// <param name="allowPrefixed">
		/// If true, allows configuring via the prefixed version
		/// of child items in addition to the flattened version.
		/// </param>
		public FlattenAttribute(bool allowPrefixed = true)
		{
			this.AllowPrefixed = allowPrefixed;
		}
	}
}
