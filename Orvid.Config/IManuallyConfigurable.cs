using System;
using System.Collections.Generic;

namespace Orvid.Config
{
	/// <summary>
	/// When implemented, allows a class to manually configure values
	/// if needed.
	/// </summary>
	public interface IManuallyConfigurable
	{
		/// <summary>
		/// Manually configure this object.
		/// </summary>
		/// <param name="items">The items to process.</param>
		/// <returns>Any unknown <see cref="ConfigItem"/>'s.</returns>
		ICollection<ConfigItem> ManuallyConfigure(List<ConfigItem> items);
	}
}
