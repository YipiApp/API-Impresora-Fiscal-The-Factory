using System;
using Newtonsoft.Json;

namespace FiscalMachineStruct
{
	public class FMInvoiceItem
	{
		[JsonProperty(PropertyName = "is_refound")]
		public bool is_refound { get; internal set; }

		[JsonProperty(PropertyName = "price")]
		public decimal price { get; internal set; }

		[JsonProperty(PropertyName = "count")]
		public decimal count { get; internal set; }

		[JsonProperty(PropertyName = "discount")]
		public decimal discount { get; internal set; }

		[JsonProperty(PropertyName = "total")]
		public decimal total { get; internal set; }

		[JsonProperty(PropertyName = "name")]
		public string name { get; internal set; }

		[JsonProperty(PropertyName = "desc1")]
		public string desc1 { get; internal set; }

		[JsonProperty(PropertyName = "desc2")]
		public string desc2 { get; internal set; }

		[JsonProperty(PropertyName = "desc3")]
		public string desc3 { get; internal set; }

		[JsonProperty(PropertyName = "reference")]
		public string reference { get; internal set; }

		[JsonProperty(PropertyName = "tax")]
		public FMTax tax { get; internal set; }

		public FMInvoiceItem ()
		{

		}

		void set_count_item(decimal v) { 
			count = v;
			total = (count * price) - discount;
		}

		void set_price_item(decimal v) { 
			price = v;
			total = (count * price) - discount;
		}

		void set_discount_item(decimal v) {
			discount = v;
			total = (count * price) - discount; 
		}
	}
}

