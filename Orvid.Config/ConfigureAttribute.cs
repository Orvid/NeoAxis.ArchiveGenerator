using System;

namespace Orvid.Config
{
	/// <summary>
	/// When applied to a field or property, indicates
	/// that it should be configured.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class ConfigureAttribute : Attribute
	{
		/// <summary>
		/// Indicates that this item should be configured.
		/// </summary>
		public ConfigureAttribute()
		{
		}
	}
}
