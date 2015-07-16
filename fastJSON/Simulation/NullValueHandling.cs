using System;

namespace Newtonsoft.Json
{
	public enum NullValueHandling
	{
		/// <summary>
		/// Include null values when serializing and deserializing objects.
		/// </summary>
		Include = 0,
		/// <summary>
		/// Ignore null values when serializing and deserializing objects.
		/// </summary>
		Ignore = 1
	}
}

