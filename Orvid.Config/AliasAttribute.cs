using System;
using System.Collections.Generic;

namespace Orvid.Config
{
	/// <summary>
	/// When applied to a field or property, indicates
	/// other names that it can be configured via.
	/// </summary>
	/// <remarks>
	/// This is usually used for shortened names, or,
	/// rarely, for configuration in the end user's
	/// native language.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
	public sealed class AliasAttribute : Attribute
	{
		/// <summary>
		/// Indicates other names that this item can
		/// be configured via.
		/// </summary>
		public ICollection<string> Aliases { get; private set; }

		/// <summary>
		/// Indicates other names that this item can
		/// be configured via.
		/// </summary>
		/// <param name="aliases">Other names this item can be configured via.</param>
		public AliasAttribute(params string[] aliases)
		{
			this.Aliases = new List<string>(aliases);
		}
	}
}
