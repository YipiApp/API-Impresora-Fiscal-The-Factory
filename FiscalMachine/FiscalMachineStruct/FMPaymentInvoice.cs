using System;
using Newtonsoft.Json;

namespace FiscalMachineStruct
{
	public class FMPaymentInvoice
	{
		[JsonProperty(PropertyName = "payment")]
		public FMPayment payment { get; internal set; }

		[JsonProperty(PropertyName = "amount")]
		public decimal amount { get; internal set; }

		[JsonProperty(PropertyName = "bank_name")]
		public string bank_name { get; internal set; }

		[JsonProperty(PropertyName = "point_of_sale")]
		public string point_of_sale { get; internal set; }

		[JsonProperty(PropertyName = "reference")]
		public string reference { get; internal set; }

		[JsonProperty(PropertyName = "customer_name")]
		public string customer_name { get; internal set; }

		[JsonProperty(PropertyName = "customer_vat")]
		public string customer_vat { get; internal set; }

		public FMPaymentInvoice ()
		{
		}
	}
}

