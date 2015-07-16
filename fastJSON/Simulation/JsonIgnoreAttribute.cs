using System;

namespace Newtonsoft.Json
{
	[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class JsonIgnoreAttribute : Attribute
	{
		
	}
}

