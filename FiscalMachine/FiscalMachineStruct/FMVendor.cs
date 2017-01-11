using System;
using Newtonsoft.Json;

namespace FiscalMachineStruct
{
	public class FMVendor
	{
		[JsonProperty(PropertyName = "id")]
		public int id { get; internal set; }

		[JsonProperty(PropertyName = "name")]
		public string name { get; internal set; }

		public FMVendor ()
		{
		}
	}
}

