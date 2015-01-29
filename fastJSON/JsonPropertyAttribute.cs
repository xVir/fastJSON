using System;

namespace fastJSON
{
	/// <summary>
	/// Json property attribute.
	/// 
	/// Based on the same class from Newtonsoft.Json
	/// https://github.com/JamesNK/Newtonsoft.Json
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
	public class JsonPropertyAttribute : Attribute
	{
		public JsonPropertyAttribute ()
		{
		}

		public JsonPropertyAttribute(string propertyName)
		{
			PropertyName = propertyName;
		}

		/// <summary>
		/// Gets or sets the name of the property.
		/// </summary>
		/// <value>The name of the property.</value>
		public string PropertyName { get; set; }

	}
}

