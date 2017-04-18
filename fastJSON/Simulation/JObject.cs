using System;
using System.Collections.Generic;


namespace Newtonsoft.Json.Linq
{
    public class JObject
    {
		private readonly Dictionary<string,object> values;

        public JObject()
        {
			values = new Dictionary<string,object> ();
        }

		public JObject(Dictionary<string,object> values)
		{
			this.values = new Dictionary<string, object> (values);
		}	

        public string SelectToken(string name)
        {
			if (values.ContainsKey(name)) {
				var stringValue = Convert.ToString (values [name]);
				return stringValue;
			}
            return null;
        }

		public object SelectTokenAsObject(string name)
		{
			if (values.ContainsKey(name)) {
				return values[name];
			}
			return null;
		}
    }
}

