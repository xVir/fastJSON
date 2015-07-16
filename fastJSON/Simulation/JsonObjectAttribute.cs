using System;

namespace Newtonsoft.Json
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false)]
	public class JsonObjectAttribute : Attribute
	{
		public JsonObjectAttribute ()
		{
		}
	}
}

