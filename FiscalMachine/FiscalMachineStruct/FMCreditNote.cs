using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PoSFiscalMachine;

namespace FiscalMachineStruct
{
	public class FMCreditNote : FMInvoice
	{
		[JsonProperty(PropertyName = "id_credit_note")]
		public int id_credit_note { get; internal set; }

		[JsonProperty(PropertyName = "id_virtual_credit_note")]
		public string id_virtual_credit_note { get; internal set; }

		public FMCreditNote ()
		{
		}

		public bool isValid() {
			if(customer_name.Trim().Length<=0 || customer_address.Trim().Length<=0 || customer_vat.Trim().Length==0 || customer_phone.Trim().Length<=0) {
				Program.PrintLog ("Customer Invalid: Name: >"+customer_name+"< Addr: >"+customer_address+"< Vat: >"+customer_vat+"< Phone: >"+customer_phone+"<");
				return false;
			}
			if(customer_vendor.Trim().Length<=0 || customer_sale.Trim().Length<=0) {
				Program.PrintLog ("Customer Invalid: customer_vendor: >"+customer_vendor+"< customer_sale: >"+customer_sale);
				return false;
			}

			decimal total = 0;

			for(int i=0;i<payments.Count;++i)
				total += payments[i].amount;

			for(int i=0;i<items.Count;++i) {
				decimal iTotal = items[i].total;
				decimal iTax = items[i].tax.calculate_tax(iTotal);
				total -= iTax + iTotal;
			}

			total = Math.Round(total, 2);
			Program.PrintLog ("Diff Pay-cn:  >"+total);


			if(total > 0 ){
				Program.PrintLog ("Invalid CN Total: "+total);
				return false;
			}

			if(vendor.id<=0) {
				Program.PrintLog ("Invalid CN Vendor: >"+vendor.id);
				return false;
			}
			return true;
		}

	}
}

