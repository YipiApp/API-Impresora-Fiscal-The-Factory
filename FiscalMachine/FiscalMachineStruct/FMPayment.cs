using System;
using Newtonsoft.Json;

namespace FiscalMachineStruct
{
	public class FMPayment
	{
		[JsonProperty(PropertyName = "id")]
		public int id { get; internal set; } //Desde 1 en adelante, si la maquina fiscal trabaja desde 0 se debe restar/sumar 1 (Dependiendo de la operacion)

		[JsonProperty(PropertyName = "name")]
		public string name { get; internal set; }

		public FMPayment (int _id, string _name)
		{
			id = _id;
			name = _name;
		}

	}
}

