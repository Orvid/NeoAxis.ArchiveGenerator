using System;
using System.ComponentModel;

namespace Orvid.Config
{
	/// <summary>
	/// When applied to a property, indicates that
	/// no inherited attributes should be processed
	/// when configuring this item.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)] // Fields can't be overriden.
	public sealed class IgnoreAncestryAttribute : Attribute
	{
		/// <summary>
		/// If true, only config attributes will be ignored,
		/// component model attributes, such as <see cref="DescriptionAttribute"/>
		/// and <see cref="DefaultValueAttribute"/> will still
		/// be inherited.
		/// </summary>
		public bool ConfigOnly { get; private set; }

		/// <summary>
		/// Indicates that no inherited attributes
		/// should be processed when configuring this
		/// item.
		/// </summary>
		/// <param name="configOnly">Specifies if only config attributes should be ignored.</param>
		public IgnoreAncestryAttribute(bool configOnly = true)
		{
			this.ConfigOnly = configOnly;
		}
	}
}
