using System;
using Newtonsoft.Json;

namespace FiscalMachineStruct
{
	public class FMTax
	{
		public enum FMTaxType {
			FMTAX_UNK = -1,
			FMTAX_EXCENT = 0,
			FMTAX_BASE = 1,
			FMTAX_BASE_TAX = 2
		};

		[JsonProperty(PropertyName = "id")]
		public int id { get; internal set; } //Desde 0 en adelante, si la maquina fiscal trabaja desde 1 se debe restar/sumar 1 (Dependiendo de la operacion)

		[JsonProperty(PropertyName = "value")]
		public decimal value { get; internal set; }

		[JsonProperty(PropertyName = "type")]
		public FMTaxType type { get; internal set; }

		public FMTax ()
		{
		}

		FMTax(int _id,  decimal _value, FMTaxType _type)
		{
			id = id;
			value = _value;
			type = _type;
		}

		public decimal calculate_tax(decimal v){
			return type==FMTaxType.FMTAX_EXCENT?0m:((v*value)/100.0m);
		}
	}
}

