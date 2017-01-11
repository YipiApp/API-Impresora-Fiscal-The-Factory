using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PoSFiscalMachine;

namespace FiscalMachineStruct
{
	public class FMInvoice
	{
		[JsonProperty(PropertyName = "id_invoice")]
		public int id_invoice { get; internal set; }

		[JsonProperty(PropertyName = "subtotal")]
		public decimal subtotal { get; internal set; }

		[JsonProperty(PropertyName = "subtotal_tax")]
		public decimal subtotal_tax { get; internal set; }

		[JsonProperty(PropertyName = "total")]
		public decimal total { get; internal set; }

		[JsonProperty(PropertyName = "vendor")]
		public FMVendor vendor { get; internal set; }

		[JsonProperty(PropertyName = "id_virtual_invoice")]
		public string id_virtual_invoice { get; internal set; }

		[JsonProperty(PropertyName = "customer_name")]
		public string customer_name { get; internal set; }

		[JsonProperty(PropertyName = "customer_address")]
		public string customer_address { get; internal set; }

		[JsonProperty(PropertyName = "customer_phone")]
		public string customer_phone { get; internal set; }

		[JsonProperty(PropertyName = "customer_vat")]
		public string customer_vat { get; internal set; }

		[JsonProperty(PropertyName = "customer_vendor")]
		public string customer_vendor { get; internal set; }

		[JsonProperty(PropertyName = "customer_sale")]
		public string customer_sale { get; internal set; }

		[JsonProperty(PropertyName = "payments")]
		public List<FMPaymentInvoice> payments { get; internal set; }

		[JsonProperty(PropertyName = "items")]
		public List<FMInvoiceItem> items { get; internal set; }

		public FMInvoice ()
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
				//decimal iTotal = items[i].total;
				//decimal iTax = items[i].tax.calculate_tax(iTotal);
				//total -= iTax + iTotal;
				total -= items[i].total;
			}

			total = Math.Round(total, 2);
			Program.PrintLog ("Diff Pay-Inv:  >"+total);

			if(total < 0 ){
				Program.PrintLog ("Invalid INV Total: "+total);
				return false;
			}

			if(vendor.id<=0) {
				Program.PrintLog ("Invalid INV Vendor: >"+vendor.id);
				return false;
			}

			return true;
		}
		public void set_items_doc(List <FMInvoiceItem> v){
			items = v;
			subtotal = 0m;
			subtotal_tax = 0m;
			total = 0m;
			for(int i=0; i<items.Count;++i){
				subtotal += items[i].total;
				subtotal_tax += items[i].tax.calculate_tax(items[i].total);
			}
			total = Math.Round(subtotal + subtotal_tax, 2);
		}

		public void add_item_doc(FMInvoiceItem item){
			items.Add (item);

			subtotal += item.total;
			subtotal_tax += item.tax.calculate_tax (item.total);
			total = subtotal + subtotal_tax;
		}

		public void set_payments_doc(List <FMPaymentInvoice> v){
			payments = v;
		}

		public void add_payment_doc(FMPaymentInvoice p){
			payments.Add (p);
		}
	}
}

