using System;
using System.Text.RegularExpressions;

namespace Orvid.Config
{
	/// <summary>
	/// When applied to a field or property, specifies
	/// the regex that the value must match in order to
	/// be valid.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class ValidationRegexAttribute : Attribute
	{
		/// <summary>
		/// The pattern that the value must match to be valid.
		/// </summary>
		public string ValidationRegexPattern { get; private set; }

		/// <summary>
		/// The options to use when running the regex match.
		/// </summary>
		public RegexOptions ValidationRegexOptions { get; private set; }

		/// <summary>
		/// A <see cref="Regex"/> created from the <see cref="ValidationRegexPattern"/>
		/// using the <see cref="ValidationRegexOptions"/>.
		/// </summary>
		public Regex ValidationRegex
		{
			get { return new Regex(ValidationRegexPattern, ValidationRegexOptions); }
		}

		/// <summary>
		/// Specifies the regex which a value must match in order
		/// to be valid.
		/// </summary>
		/// <param name="pattern">The regex's pattern.</param>
		/// <param name="patternOptions">The options to use when performing the regex match.</param>
		public ValidationRegexAttribute(string pattern, RegexOptions patternOptions = RegexOptions.Singleline)
		{
			this.ValidationRegexPattern = pattern;
			this.ValidationRegexOptions = patternOptions;
		}
	}
}
