using System;
using fastJSON;

namespace Newtonsoft.Json
{
	/// <summary>
	/// Json.NET API emulation
	/// </summary>
	public class JsonConvert
	{
		public JsonConvert ()
		{
		}

		public static string SerializeObject(object value)
		{
			return SerializeObject(value, Formatting.None, (JsonSerializerSettings)null);
		}

		public static string SerializeObject(object value, Formatting formatting)
		{
			return SerializeObject(value, formatting, (JsonSerializerSettings)null);
		}

		public static string SerializeObject(object value, JsonSerializerSettings settings)
		{
			return SerializeObject(value, Formatting.None, settings);
		}

		public static string SerializeObject(object value, Formatting formatting, JsonSerializerSettings settings)
		{
			var jsonParams = new JSONParameters { 
				UseExtensions = false,
				EnableAnonymousTypes = true,
				SerializeNullValues = false,
				WithoutDynamicMethodsGeneration = true
			};
				
			return JSON.ToJSON (value, jsonParams);
		}

		public static T DeserializeObject<T>(string value)
		{
			return DeserializeObject<T>(value, (JsonSerializerSettings)null);
		}

		public static T DeserializeObject<T>(string value, JsonSerializerSettings settings)
		{
			var jsonParams = new JSONParameters { 
				UseExtensions = false,
				EnableAnonymousTypes = true,
				SerializeNullValues = false,
				WithoutDynamicMethodsGeneration = true
			};

			return JSON.ToObject<T> (value, jsonParams);
		}
	}
}

