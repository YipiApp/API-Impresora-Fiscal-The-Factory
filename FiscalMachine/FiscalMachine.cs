using System;
using System.Collections.Generic;
using System.Text;
using FiscalMachineStruct;
using System.Threading;

namespace PoSFiscalMachine
{
	public class FiscalMachine
	{
		public enum FMStatus {
			FMS_SUCCESS 				= 0,
			FMS_PAPERLESS 				= 0x00000001, // Error de Papel / Impresora sin papel
			FMS_DISPLAY_ERROR 			= 0x00000002, // Impresora / Error de Display
			FMS_TIMEOUT 				= 0x00000004, // Impresora offline / desconectada/error de timeout
			FMS_PRINT_BUFFER_FULL		= 0x00000008, // Buffer de impresión lleno
			FMS_NET_BUFFER_FULL			= 0x00000010, // Error en la Comunicación
			FMS_LRC_ERROR 				= 0x00000020, // Error LRC (Checksum)
			FMS_DATE_EMPTY 				= 0x00000040, // La fecha no ha sido programada todavía
			FMS_MEMORY_SEMIFULL 		= 0x00000080, // Memoria Fiscal casi llena
			FMS_MEMORY_FULL				= 0x00000100, // Memoria Fiscal llena
			FMS_MEMORY_ERROR			= 0x00000200, // Error en la memoria fiscal
			FMS_FISCAL_ERROR			= 0x00000400, // Error Fiscal
			FMS_COMMAND_INVALID			= 0x00000800, // Comando Inválido / Error en formato de comando
			FMS_VENDOR_INVALID			= 0x00001000, // Cajero no asignado
			FMS_TAX_INVALID				= 0x00002000, // Impuesto inválido
			FMS_VALUE_ERROR				= 0x00004000, // Valor Inválido /  error de desbordamiento de totales
			FMS_BUSY_FISCAL				= 0x00008000, // En transacción fiscal
			FMS_BUSY_NOFISCAL			= 0x00010000, // En transacción no fiscal
			FMS_BUSY_FISCAL_AUDITED		= 0x00020000, // Impresor fiscal esta fiscalizado
			FMS_UNUSED0					= 0x00040000, // Sin usar
			FMS_UNUSED1					= 0x00080000, // Sin usar
			FMS_UNUSED2					= 0x00100000, // Sin usar
			FMS_UNUSED3					= 0x00200000, // Sin usar
			FMS_UNUSED4					= 0x00400000, // Sin usar
			FMS_UNUSED5					= 0x00800000, // Sin usar
			FMS_UNUSED6					= 0x01000000, // Sin usar
			FMS_UNUSED7					= 0x02000000, // Sin usar
			FMS_UNUSED8					= 0x04000000, // Sin usar
			FMS_UNUSED9					= 0x08000000, // Sin usar
			FMS_UNUSED10				= 0x10000000, // Sin usar
			FMS_UNKNOWN					= 0x20000000, // Sin usar
			FMS_NOT_CONNECT				= 0x40000000//, // Maquina fiscal desconectada.
		};

		private int last_report_z;
		private int last_invoice;
		private int last_credit_note;
		private int count_header_footer;
		private int count_tax = 0;
		private decimal daily_sale;
		private uint status_fm;
		private bool IS_DEBUG = false;

		private string fm_vat;
		private string fm_serial;

		private List<string> fm_header = new List<string>();
		private List<string> fm_footer = new List<string>();
		private List<FMVendor> fm_vendors = new List<FMVendor>();
		private List<FMTax> fm_taxes = new List<FMTax>();
		private List<FMPayment> fm_payments = new List<FMPayment>();

		Tfhka fm;

		const string TAX0 = " ";
		const string TAX1 = "!";
		const string TAX2 = "\"";
		const string TAX3 = "#";
		const string CN_TAX0  = "0";
		const string CN_TAX1  = "1";
		const string CN_TAX2  = "2";
		const string CN_TAX3  = "3";
		const byte CANCEL_FT = 0xA2;
		const int MAX_SET_HEADER_FOOTER = 10;
		const int MAX_SET_TAX = 10;

		private FMStatus status = FMStatus.FMS_UNKNOWN;

		public Tfhka Fm {
			get {
				return fm;
			}
			set {
				fm = value;
				fm.SendCmd("7", 3, 4); //cancela la factura si quedo alguna pendiente
				reloadLastFiscalPrints (true, true, true, true);
			}
		}

		public int Last_report_z {
			get {
				return last_report_z;
			}
		}

		public int Last_invoice {
			get {
				return last_invoice;
			}
		}

		public int Last_credit_note {
			get {
				return last_credit_note;
			}
		}

		public int Count_header_footer {
			get {
				return count_header_footer;
			}
		}

		public int Count_tax {
			get {
				return count_tax;
			}
		}

		public decimal Daily_sale {
			get {
				return daily_sale;
			}
		}

		public string Fm_vat {
			get {
				return fm_vat;
			}
		}

		public string Fm_serial {
			get {
				return fm_serial;
			}
		}

		public List<string> Fm_header {
			get {
				return fm_header;
			}
		}

		public List<string> Fm_footer {
			get {
				return fm_footer;
			}
		}

		public List<FMVendor> Fm_vendors {
			get {
				return fm_vendors;
			}
		}

		public List<FMTax> Fm_taxes {
			get {
				return fm_taxes;
			}
		}

		public List<FMPayment> Fm_payments {
			get {
				return fm_payments;
			}
		}

		public FMStatus Status {
			get {
				return status;
			}
		}

		public FiscalMachine (PoSFiscalMachine.Tfhka _fm, bool _IS_DEBUG = false)
		{
			fm = _fm;
			IS_DEBUG = _IS_DEBUG;

			fm.SendCmd ("7", 3);

			reloadLastFiscalPrints (true, true, true, true);
		}


		static List<string> getErrorStatus(uint status) {
			List<string> ret = new List<string>();
			if(status == (uint)FMStatus.FMS_SUCCESS)                   ret.Add("Success");
			if((status & (uint)FMStatus.FMS_PAPERLESS) > 0)                  ret.Add("Error de Papel");
			if((status & (uint)FMStatus.FMS_DISPLAY_ERROR) > 0)              ret.Add("Error de Display");
			if((status & (uint)FMStatus.FMS_TIMEOUT) > 0)                    ret.Add("Impresora offline");
			if((status & (uint)FMStatus.FMS_PRINT_BUFFER_FULL) > 0)          ret.Add("Buffer de impresión lleno");
			if((status & (uint)FMStatus.FMS_NET_BUFFER_FULL) > 0)            ret.Add("Error en la Comunicación");
			if((status & (uint)FMStatus.FMS_LRC_ERROR) > 0)                  ret.Add("Error LRC (Checksum)");
			if((status & (uint)FMStatus.FMS_DATE_EMPTY) > 0)                 ret.Add("La fecha no ha sido programada todavía");
			if((status & (uint)FMStatus.FMS_MEMORY_SEMIFULL) > 0)            ret.Add("Memoria Fiscal casi llena");
			if((status & (uint)FMStatus.FMS_MEMORY_FULL) > 0)                ret.Add("Memoria Fiscal llena");
			if((status & (uint)FMStatus.FMS_MEMORY_ERROR) > 0)               ret.Add("Error en la memoria fiscal");
			if((status & (uint)FMStatus.FMS_COMMAND_INVALID) > 0)            ret.Add("Comando Inválido");
			if((status & (uint)FMStatus.FMS_VENDOR_INVALID) > 0)             ret.Add("Cajero no asignado");
			if((status & (uint)FMStatus.FMS_TAX_INVALID) > 0)                ret.Add("Impuesto inválido");
			if((status & (uint)FMStatus.FMS_VALUE_ERROR) > 0)                ret.Add("Valor Inválido");
			if((status & (uint)FMStatus.FMS_BUSY_FISCAL) > 0)                ret.Add("En transacción fiscal");
			if((status & (uint)FMStatus.FMS_BUSY_NOFISCAL) > 0)				ret.Add ("En transacción no fiscal");
			if((status & (uint)FMStatus.FMS_BUSY_FISCAL_AUDITED) > 0)        ret.Add("Impresor fiscal esta fiscalizado");
			if((status & (uint)FMStatus.FMS_UNKNOWN) > 0)                    ret.Add("Error no manejado o desconocido");

			if(ret.Count == 0 && status != 0) 
				ret.Add("Unknown");

			return ret;
		}

		private string[] splitDecimalSTR(decimal d) {
			char[] delimiters = new char[] { '.', ',' };
			string[] price = d.ToString().Split(delimiters);

			string[] ret = new string[2];
			ret [0] = price [0];
			if (price.Length == 2)
				ret [1] = price [1];
			else
				ret [1] = "0";

			return ret;
		}

		private string gen_number_fm(int v, bool aling_left, uint digits) {
			return gen_number_fm (v.ToString (), aling_left, digits);
		}

		private string gen_number_fm(string v, bool aling_left, uint digits) {
			string result = "";
			if (aling_left) {
				result += v;
				for (int i = v.Length; i < digits; ++i)
					result += "0";
			} else {
				for (int i = 0; i < digits - v.Length; ++i)
					result += "0";
				result += v;
			}
			return result;
		}
		public string SafeSubstring(string text, int start, int length = -1)
		{
			if (text.Length <= start || length == 0)
				return "";

			if (length<0 || text.Length - start <= length)
				return text.Substring(start);

			return text.Substring(start, length);
		}

		private DateTime UnixTimeStampToDateTime( UInt64 unixTimeStamp )
		{
			// Unix timestamp is seconds past epoch
			System.DateTime dtDateTime = new DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc);
			dtDateTime = dtDateTime.AddSeconds( (double) unixTimeStamp ).ToLocalTime();
			return dtDateTime;
		}

		public bool reloadLastFiscalPrints(bool last_invoice_and_z = true, bool get_last_credit_note = false, bool get_total_day = false, bool get_taxes = false) {
			bool valid = false;
			if (last_invoice_and_z) {
				if (fm.SendCmd ("S1")) {
					if (IS_DEBUG) {
						last_invoice = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
						last_report_z = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
						fm_vat = "J-00000000-0";
						fm_serial = "Z1B8005054";
						valid = true;
					} else {
						byte[] result = fm.LastResponse;
						string result_str = Encoding.UTF8.GetString (result, 0, result.Length);
						string[] result_s = result_str.Split ('\n');
						if (result_s.Length >= 10) {
							last_invoice = Convert.ToInt32 (result_s [2]);
							last_report_z = Convert.ToInt32 (result_s [6]);
							fm_vat = result_s [8].ToUpper ();
							fm_serial = result_s [9].ToUpper ();
							valid = true;
						}
					}
				}
			}

			if (get_last_credit_note) {
				if (fm.SendCmd("U0X")) {
					if (IS_DEBUG) {
						last_credit_note = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
						valid = true;
					} else {
						byte[] result = fm.LastResponse;
						string result_str = Encoding.UTF8.GetString (result, 0, result.Length);
						string[] result_s = result_str.Split ('\n');
						if (result_s.Length > 20)
							last_credit_note = Convert.ToInt32 (result_s [19]);

						valid = true;
					}
				}
			}

			if (get_taxes) {
				if (fm.SendCmd ("S3")) {
					if (IS_DEBUG) {
						FMTax tax1 = new FMTax ();
						tax1.value = 12;
						tax1.type = (FMTax.FMTaxType)((int)1);
						tax1.id = 1;

						FMTax tax2 = new FMTax ();
						tax2.value = 10;
						tax2.type = (FMTax.FMTaxType)((int)1);
						tax2.id = 2;

						FMTax tax3 = new FMTax ();
						tax3.value = 12;
						tax3.type = (FMTax.FMTaxType)((int)1);
						tax3.id = 3;

						List<FMTax> t = new List<FMTax> ();
						t.Add (tax1);
						t.Add (tax2);
						t.Add (tax3);

						fm_taxes = t;
						valid = true;
					} else {
						byte[] result = fm.LastResponse;
						string result_str = SafeSubstring (Encoding.UTF8.GetString (result, 0, result.Length), 3);
						string[] result_s = result_str.Split ('\n'); 

						if (result_s.Length > 3) {
							FMTax tax1 = new FMTax ();
							ulong full_t1 = Convert.ToUInt64 (result_s [0]); 
							tax1.value = Convert.ToDecimal (full_t1 % 10000) / 100.0m;
							tax1.type = (FMTax.FMTaxType)((int)(full_t1 / 10000));
							tax1.id = 1;

							FMTax tax2 = new FMTax ();
							ulong full_t2 = Convert.ToUInt64 (result_s [1]); 
							tax2.value = Convert.ToDecimal (full_t2 % 10000) / 100.0m;
							tax2.type = (FMTax.FMTaxType)((int)(full_t2 / 10000));
							tax2.id = 2;

							FMTax tax3 = new FMTax ();
							ulong full_t3 = Convert.ToUInt64 (result_s [2]); 
							tax3.value = Convert.ToDecimal (full_t3 % 10000) / 100.0m;
							tax3.type = (FMTax.FMTaxType)((int)(full_t3 / 10000));
							tax3.id = 3;

							List<FMTax> t = new List<FMTax> ();
							t.Add (tax1);
							t.Add (tax2);
							t.Add (tax3);

							fm_taxes = t;

							valid = true;
						}
					}
				}
			}

			if (get_total_day) {
				if (fm.SendCmd ("S4")) {
					if (IS_DEBUG) {
						daily_sale = 0;
						valid = true;
					} else {
						byte[] result = fm.LastResponse;
						string result_str = Encoding.UTF8.GetString (result, 0, result.Length);
						string[] result_s = result_str.Split ('\n'); 
						for (int i = 0; i < result_s.Length - 1; ++i) {
							if (i == 0)
								daily_sale = Convert.ToDecimal (SafeSubstring (result_s [i], 3)) / 100.0m;
							else
								daily_sale += Convert.ToDecimal (result_s [i]) / 100.0m;
						}
						valid = true;
					}
				}
			}

			return valid;
		}

		public uint get_status_code() {
			uint ret = (uint)FMStatus.FMS_SUCCESS;
			if (IS_DEBUG)
				return ret;
			if (fm.LastResponseStatus != null || fm.ReadFpStatus ()) {
				byte[] status = fm.LastResponseStatus;
				byte st_aux = status [1];
				byte er = status [2];
				byte st = (byte)(st_aux & ((byte)0xFB));
				if ((er & 0x6C) == 0x6C)
					ret = ret | (uint)FMStatus.FMS_MEMORY_FULL;
				if ((er & 0x64) == 0x64)
					ret = ret | (uint)FMStatus.FMS_MEMORY_ERROR;
				//if ((er & 0x48) == 0x48)
				//	ret = ret | (uint)FMStatus.FMS_MEMORY_ERROR;
				if ((er & 0x60) == 0x60)
					ret = ret | (uint)FMStatus.FMS_FISCAL_ERROR;
				if ((er & 0x5C) == 0x5C)
					ret = ret | (uint)FMStatus.FMS_COMMAND_INVALID;
				if ((er & 0x58) == 0x58)
					ret = ret | (uint)FMStatus.FMS_VENDOR_INVALID;
				if ((er & 0x70) == 0x70)
					ret = ret | (uint)FMStatus.FMS_DATE_EMPTY;
				if ((er & 0x54) == 0x54)
					ret = ret | (uint)FMStatus.FMS_TAX_INVALID;
				if ((er & 0x50) == 0x50)
					ret = ret | (uint)FMStatus.FMS_VALUE_ERROR;// Comando Invalido/Valor Invalido
				if ((er & 0x43) == 0x43)
					ret = ret | (uint)FMStatus.FMS_PAPERLESS;
				if ((er & 0x42) == 0x42)
					ret = ret | (uint)FMStatus.FMS_PAPERLESS;
				if ((er & 0x41) == 0x41)
					ret = ret | (uint)FMStatus.FMS_PAPERLESS;
				if ((st_aux & 0x04) == 0x04)
					ret = ret | (uint)FMStatus.FMS_NET_BUFFER_FULL;
				if (er == 128)
					ret = ret | (uint)FMStatus.FMS_UNKNOWN;//CTS en falso
				if (er == 137)
					ret = ret | (uint)FMStatus.FMS_TIMEOUT;//No hay respuesta
				if (er == 144)
					ret = ret | (uint)FMStatus.FMS_LRC_ERROR;
				if (er == 114)
					ret = ret | (uint)FMStatus.FMS_BUSY_FISCAL;
				if ((st & 0x41) == 0x41)
					ret = ret | (uint)FMStatus.FMS_BUSY_FISCAL;
				if ((st & 0x42) == 0x42)
					ret = ret | (uint)FMStatus.FMS_BUSY_NOFISCAL;
			} else {
				ret = ret | (uint)FMStatus.FMS_TIMEOUT;
				ret = ret | (uint)FMStatus.FMS_UNKNOWN;
			}

			status_fm = ret;

			return status_fm;
		}

		public string[] get_status() {
			return getErrorStatus (get_status_code()).ToArray ();
		}

		//Retorna el Numero de Factura generado. -1 en caso de error.
		public int generate_invoice(FMInvoice invoice, bool show_barcode, string barcode = null) {

			if (!reloadLastFiscalPrints ()) {
				return -1;
			}

			int linv = last_invoice;

			fm.SendCmd("7", 3, 4); //cancela la factura si quedo alguna pendiente, solo se intenta una vez.

			bool ready = fm.SendCmd ("5" + String.Format ("{0:00000}", invoice.vendor.id));

			if (!ready)
				return -1;

			ready =	fm.SendCmd ("i01Razon Social: " + SafeSubstring(invoice.customer_name, 0, 35)) &&
			        fm.SendCmd ("i02R.I.F./C.I.:  " + SafeSubstring(invoice.customer_vat, 0, 35)) &&
			        fm.SendCmd ("i03Direccion:    " + SafeSubstring(invoice.customer_address, 0, 35)) &&
			        fm.SendCmd ("i04Telefono:     " + SafeSubstring(invoice.customer_phone, 0, 35)) &&
			        fm.SendCmd ("i05Caja:         " + SafeSubstring(invoice.customer_sale, 0, 35)) &&
			        fm.SendCmd ("i06Vendedor:     " + SafeSubstring(invoice.customer_vendor, 0, 35));

			if (!ready) {
				fm.SendCmd("7", 3, 4); //cancela la factura
				fm.SendCmd ("6");
				return -1;
			}

			bool ready_comment = true;
			for (int i = 0; i < invoice.items.Count && ready; ++i) {
				FMInvoiceItem item = invoice.items [i];
				string cmd = "";

				if (item.tax.type == FMTax.FMTaxType.FMTAX_EXCENT || item.tax.value < 0.01m) {
					cmd = TAX0;
				} else {
					switch (item.tax.id) {
					case 1:
						cmd += TAX1;
						break;
					case 2:
						cmd += TAX2;
						break;
					case 3:
						cmd += TAX3;
						break;
					default:
						Program.PrintLog ("Error catastrofico, el ID es Invaldo! Como es posible :S");
						fm.SendCmd ("7"); //cancela la factura
						return -1;
					}
				}

				decimal real_price = Math.Round (item.price, 2);
				decimal real_discount = Math.Round (item.discount, 2);
				decimal real_count = Math.Round (item.count, 3);

				string[] price = splitDecimalSTR(real_price);
				string[] count =  splitDecimalSTR(real_count);
				string[] discount =  splitDecimalSTR(real_discount);
				cmd += 	gen_number_fm (price [0], false, 8) + gen_number_fm (price [1], true, 2) +
					    gen_number_fm (count [0], false, 5) + gen_number_fm (count [1], true, 3) +
				       SafeSubstring(item.name, 0, 25);
				//sprintf(&tmp[1], "%08s%02s%05s%03s%s", price_1.toStdString().c_str(), price_2.toStdString().c_str(), count_1.toStdString().c_str(), count_2.toStdString().c_str(), item.get_name().mid(0, 25).toStdString().c_str());

				ready = fm.SendCmd (cmd, 10);
				if (ready && ready_comment) {
					if (ready_comment && item.desc1 != null && item.desc1.Trim ().Length > 0)
						ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc1.Trim (), 0, 45));
					if (ready_comment && item.desc2 != null && item.desc2.Trim ().Length > 0)
						ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc2.Trim (), 0, 45));
					if (ready_comment && item.desc3 != null && item.desc3.Trim ().Length > 0)
						ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc3.Trim (), 0, 45));
					if (ready_comment && item.reference != null && item.reference.Trim ().Length > 0)
						ready_comment = fm.SendCmd ("@Ref.: " + SafeSubstring (item.reference.Trim (), 0, 39));
				}
			}

			if (!ready) {
				fm.SendCmd("7", 3, 4); //cancela la factura
				fm.SendCmd ("6");
				return -1;
			}

			if (show_barcode) {
				ulong code = (ulong)last_invoice + 1;
				if(barcode != null && Convert.ToUInt64(barcode) > 0)
					code = Convert.ToUInt64(barcode);
				string cmd = "y" + gen_number_fm (code.ToString(), false, 12);
				//sprintf(tmp, "y%012lld", code);

				//No se verifica ya que algunas impresoras no soportan esta opcion.
				fm.SendCmd (cmd, 5, 4);
			}
			decimal pay_amount_acum = 0;
			for (int i = 0; i < invoice.payments.Count && ready; ++i) {

				if (pay_amount_acum >= invoice.total)
					break; //Esto evita que la maquina fiscal se cuelgue...

				FMPaymentInvoice pay = invoice.payments [i];
				string cmd = "2";
				decimal pay_amount = Math.Round (pay.amount, 2);
				pay_amount_acum += pay_amount;
				string[] pay_amount_str = splitDecimalSTR(pay_amount);
				cmd += gen_number_fm (pay.payment.id, false, 2) + gen_number_fm (pay_amount_str [0], false, 10) + gen_number_fm (pay_amount_str [1], true, 2);
				//sprintf(tmp, "2%02d%010s%02s", pay.get_payment().get_id(), amount_1.toStdString().c_str(), amount_2.toStdString().c_str());
				ready = ready && fm.SendCmd (cmd, 10);
			}

			if (!ready) {
				fm.SendCmd("7", 3, 4); //cancela la factura
				fm.SendCmd ("6");
				return -1;
			}

			fm.SendCmd ("6");

			if (reloadLastFiscalPrints ()) {
				if (linv < last_invoice) {
					return last_invoice;
				}
			}
			return ++linv;
		}

		//Retorna el Numero de la nota de credito generado. -1 en caso de error.
		public int generate_credit_note(FMCreditNote credit_note) {

			if (!reloadLastFiscalPrints (false, true)) {
				return -1;
			}

			fm.SendCmd("7", 3, 4); //cancela la factura si quedo alguna pendiente

			if (credit_note.payments.Count != 1)
				return -1;

			bool ready = fm.SendCmd ("5" + String.Format ("{0:00000}", credit_note.vendor.id));

			if (!ready)
				return -1;

			int lcn = last_credit_note;

			ready =	fm.SendCmd ("i01Razon Social: " + SafeSubstring(credit_note.customer_name, 0, 35)) &&
			        fm.SendCmd ("i02R.I.F./C.I.:  " + SafeSubstring(credit_note.customer_vat, 0, 35)) &&
			        fm.SendCmd ("i03Direccion:    " + SafeSubstring(credit_note.customer_address, 0, 35)) &&
			        fm.SendCmd ("i04Telefono:     " + SafeSubstring(credit_note.customer_phone, 0, 35)) &&
			    	fm.SendCmd ("i05Factura:      " + credit_note.id_invoice) &&
			        fm.SendCmd ("i06Caja:         " + SafeSubstring(credit_note.customer_sale, 0, 35));

			if (!ready) {
				fm.SendCmd("7", 3, 4); //cancela la factura
				fm.SendCmd ("6");
				return -1;
			}

			for (int i = 0; i < credit_note.items.Count && ready; ++i) {
				FMInvoiceItem item = credit_note.items [i];
				string cmd = "d";

				if (item.tax.type == FMTax.FMTaxType.FMTAX_EXCENT || item.tax.value < 0.01m) {
					cmd += CN_TAX0;
				} else {
					switch (item.tax.id) {
					case 1:
						cmd += CN_TAX1;
						break;
					case 2:
						cmd += CN_TAX2;
						break;
					case 3:
						cmd += CN_TAX3;
						break;
					default:
						Program.PrintLog ("Error catastrofico, el ID es Invaldo! Como es posible :S");
						fm.SendCmd("7", 3, 4); //cancela la factura
						return -1;
					}
					decimal real_price = Math.Round (item.price, 2);
					decimal real_discount = Math.Round (item.discount, 2);
					decimal real_count = Math.Round (item.count, 3);

					string[] price = splitDecimalSTR(real_price);
					string[] count =  splitDecimalSTR(real_count);
					string[] discount =  splitDecimalSTR(real_discount);
					cmd += 	gen_number_fm (price [0], false, 8) + gen_number_fm (price [1], true, 2) +
						gen_number_fm (count [0], false, 5) + gen_number_fm (count [1], true, 3) +
					       SafeSubstring(item.name, 0, 25);
					//sprintf(&tmp[1], "%08s%02s%05s%03s%s", price_1.toStdString().c_str(), price_2.toStdString().c_str(), count_1.toStdString().c_str(), count_2.toStdString().c_str(), item.get_name().mid(0, 25).toStdString().c_str());

					ready = fm.SendCmd (cmd, 10);
					// Los comentarios en nota de credito definitivamente dan muchos problemas
					// Se prefirio quitarlos para agilizar la caja.
					/*if (ready && ready_comment) {
						if (ready_comment && item.desc1 != null && item.desc1.Trim ().Length > 0)
							ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc1.Trim (), 0, 45));
						if (ready_comment && item.desc2 != null && item.desc2.Trim ().Length > 0)
							ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc2.Trim (), 0, 45));
						if (ready_comment && item.desc3 != null && item.desc3.Trim ().Length > 0)
							ready_comment = fm.SendCmd ("@" + SafeSubstring (item.desc3.Trim (), 0, 45));
						if (ready_comment && item.reference != null && item.reference.Trim ().Length > 0)
							ready_comment = fm.SendCmd ("@Ref.: " + SafeSubstring (item.reference.Trim (), 0, 39));
					}*/
				}
			}

			if (ready) {
				//for (int i = 0; i < credit_note.payments.Count && ready; ++i) {
				FMPaymentInvoice pay = credit_note.payments [0];
				string cmd = "f";
				decimal pay_amount = Math.Round (pay.amount, 2) + 1.0m;
				string[] pay_amount_str = splitDecimalSTR (pay_amount);
				cmd += gen_number_fm (pay.payment.id, false, 2) + gen_number_fm (pay_amount_str [0], false, 10) + gen_number_fm (pay_amount_str [1], true, 2);
				ready = fm.SendCmd (cmd, 10, 3); //Aveces no sirve con f, se intenta con 1 (Pago completo Forma 1) 
				if (!ready) { //Aveces no sirve con f, se intenta con 1 (Pago completo Forma 2)
					cmd = "1";
					cmd += gen_number_fm (pay.payment.id, false, 2);
					ready = fm.SendCmd (cmd, 10, 3);
				}
				if (!ready) { //Aveces no sirve con f ni 1, se intenta con 2 (Pago parcial)
					cmd = "2";
					cmd += gen_number_fm (pay.payment.id, false, 2) + gen_number_fm (pay_amount_str [0], false, 10) + gen_number_fm (pay_amount_str [1], true, 2);
					ready = fm.SendCmd (cmd, 10, 3);
				}

				if (!ready && reloadLastFiscalPrints(false, true)) {
					if (lcn < last_credit_note) {
						return last_credit_note;
					}
				}
			}
			//}
				
			if (!ready) {
				fm.SendCmd("7", 3, 4); //cancela la factura
				fm.SendCmd ("6");
				return -1;
			}

			fm.SendCmd ("6");

			return reloadLastFiscalPrints(false, true)?last_credit_note:++lcn;
		}

		public bool open_drawer(FMPaymentInvoice[] fixed_drawer) {
			if(fixed_drawer.Length>0) {
				bool ready = true;
				for(int i=0;i<fixed_drawer.Length && ready;++i) {
					FMPaymentInvoice op = fixed_drawer[i];
					bool output = op.amount<0.0m;
					decimal amount = op.amount;
					if(output)
						amount = -amount;

					string[] p_amount = splitDecimalSTR(amount);

					string cmd = "9" +(output?"0":"1") + gen_number_fm (op.payment.id, false, 2) + gen_number_fm (p_amount[0], false, 10) + gen_number_fm (p_amount[1], true, 2);
					//sprintf(tmp, "9%d%02d%010d%02d", output?0:1, op.get_payment().get_id(), amount_1, amount_2);

					ready = ready && fm.SendCmd(cmd);
				}
				if(ready)
					return fm.SendCmd("t");

				return false;
			}else{
				return fm.SendCmd("0");
			}
		}

		public int generate_report_z() {
			//if (reloadLastFiscalPrints (false, false, true, false) && Daily_sale < 0.01m)
			//	return -1;

			while (!fm.SendCmd ("I0Z", 30))
				Thread.Sleep (500);

			Thread.Sleep (3000); // Esta espera es necesaria, puede fallar el cierre si no se hace...

			return reloadLastFiscalPrints()?last_report_z:++last_report_z;

			//return -1;
		}

		public bool generate_report_x() {
			while (!fm.SendCmd ("I0X", 30))
				Thread.Sleep (100);

			Thread.Sleep (3000); // Esta espera es necesaria, puede fallar el cierre si no se hace...

			return true;
		}

		public bool abort_fiscal_print() {
			return fm.SendCmd("7");
		}

		public bool print_copy_last_invoice() {
			return print_copy_invoice(last_invoice, last_invoice);
		}

		public bool print_copy_last_credit_note() {
			return print_copy_credit_note(last_credit_note, last_credit_note);
		}

		public bool print_copy_last_report_z() {
			return print_copy_z(last_report_z, last_report_z);
		}

		public bool print_copy_z(int id_min, int id_max) {
			if(id_min>id_max || id_min<0) 
				return false;

			return fm.SendCmd ("RZ" + gen_number_fm (id_min, false, 7) + gen_number_fm (id_max, false, 7), (id_max - id_min + 1) * 10);
		}

		public bool print_copy_invoice(int id_min, int id_max) {
			if(id_min>id_max || id_min<0) 
				return false;

			return fm.SendCmd ("RF" + gen_number_fm (id_min, false, 7) + gen_number_fm (id_max, false, 7), (id_max - id_min + 1) * 10);
		}

		public bool print_copy_credit_note(int id_min, int id_max) {
			if(id_min>id_max || id_min<0) 
				return false;

			return fm.SendCmd ("RC" + gen_number_fm (id_min, false, 7) + gen_number_fm (id_max, false, 7), (id_max - id_min + 1) * 10);
		}

		public bool print_copy_z_date(int min_date, int max_date) {
			if(min_date>max_date || min_date<=0) 
				return false;
			DateTime mi_date = UnixTimeStampToDateTime ((ulong)min_date);
			DateTime ma_date = UnixTimeStampToDateTime ((ulong)max_date);
			return fm.SendCmd ("Rz" + gen_number_fm (mi_date.Year%100, false, 2) + gen_number_fm (mi_date.Month, false, 2) + gen_number_fm (mi_date.Day, false, 2)
				+ gen_number_fm (ma_date.Year%100, false, 2) + gen_number_fm (ma_date.Month, false, 2) + gen_number_fm (ma_date.Day, false, 2), 30);
		}

		public bool print_copy_invoice_date(int min_date, int max_date) {
			if(min_date>max_date || min_date<=0) 
				return false;
			DateTime mi_date = UnixTimeStampToDateTime ((ulong)min_date);
			DateTime ma_date = UnixTimeStampToDateTime ((ulong)max_date);
			return fm.SendCmd ("Rf" + gen_number_fm (mi_date.Year%100, false, 2) + gen_number_fm (mi_date.Month, false, 2) + gen_number_fm (mi_date.Day, false, 2)
				+ gen_number_fm (ma_date.Year%100, false, 2) + gen_number_fm (ma_date.Month, false, 2) + gen_number_fm (ma_date.Day, false, 2), 30);
		}

		public bool print_copy_credit_note_date(int min_date, int max_date) {
			if(min_date>max_date || min_date<=0) 
				return false;
			DateTime mi_date = UnixTimeStampToDateTime ((ulong)min_date);
			DateTime ma_date = UnixTimeStampToDateTime ((ulong)max_date);
			return fm.SendCmd ("Rc" + gen_number_fm (mi_date.Year%100, false, 2) + gen_number_fm (mi_date.Month, false, 2) + gen_number_fm (mi_date.Day, false, 2)
				+ gen_number_fm (ma_date.Year%100, false, 2) + gen_number_fm (ma_date.Month, false, 2) + gen_number_fm (ma_date.Day, false, 2), 30);
		}

		public bool print(string[] p) {
			bool ready = false;
			for(int i=0;i<p.Length;++i) {
				ready = fm.SendCmd("800"+SafeSubstring(p[i], 0, 56)) || ready;
			}
			ready = fm.SendCmd("810 ") || ready; // Termina impresion no fiscal con "810 "
			return ready;

		}

		public bool print_copy_last_print() {
			return fm.SendCmd("RU00000000000000", 15);
		}


		//Peligro!!!> Este comando podrá ser ejecutado N veces como máximo de por vida
		public bool set_header_footer(string[] header, string[] footer) {
			if(header.Length<=0 || header.Length>8 || footer.Length<=0 || footer.Length>8)
				return false;

			if(count_header_footer>=MAX_SET_HEADER_FOOTER) {
				Program.PrintLog ("Se supero el maximo de set head/foot: "+count_header_footer);
				return false;
			}

			bool ready = true;

			for(int i=0;i<header.Length && ready;++i) {
				ready = fm.SendCmd ("PH" + gen_number_fm (i, false, 2) + SafeSubstring(header [i].Trim(), 0, 56));
		    }

			for(int i=0;i<footer.Length && ready;++i) {
				ready = fm.SendCmd ("PH" + gen_number_fm (91+i, false, 2) + SafeSubstring(header [i].Trim(), 0, 56));
		    }

			if(ready) 
				count_header_footer++;

			return ready;
		}		

		public bool set_display_message(string header, string footer) {
			bool ready = fm.SendCmd ("cU" + SafeSubstring(header.Trim(), 0, 20));
			ready = ready && fm.SendCmd ("cL" + SafeSubstring(footer.Trim(), 0, 20));
			return ready;
		}

		public bool set_display_commercial(string msg) {
			bool ready = fm.SendCmd ("PI" + SafeSubstring(msg.Trim(), 0, 50));
			return ready;
		}		

		public bool show_display_commercial() {
			fm.SendCmd ("a");
			return fm.SendCmd ("b");
		}		

		public bool set_taxes(FMTax[] taxes) {
			if(count_tax>=MAX_SET_TAX) return false;
			if(taxes.Length != 3) return false;

			decimal tax1 = Math.Round (taxes[0].value, 2);
			string[] tax1_str = splitDecimalSTR(tax1);

			decimal tax2 = Math.Round (taxes[1].value, 2);
			string[] tax2_str = splitDecimalSTR(tax2);

			decimal tax3 = Math.Round (taxes[2].value, 2);
			string[] tax3_str = splitDecimalSTR(tax3);

			bool result = fm.SendCmd ("PT" + ((int)taxes [0].type).ToString () + gen_number_fm (tax1_str [0], false, 2) + gen_number_fm (tax1_str [1], true, 2)
				+ ((int)taxes [1].type).ToString () + gen_number_fm (tax2_str [0], false, 2) + gen_number_fm (tax2_str [1], true, 2)
				+ ((int)taxes [2].type).ToString () + gen_number_fm (tax3_str [0], false, 2) + gen_number_fm (tax3_str [1], true, 2))

				&&
				fm.SendCmd ("Pt") //Esto confirma el cambio de impuestos...
				;
			reloadLastFiscalPrints(false, false, false, true);
			return result;
			//sprintf(buffer, "PT%d%02d%02d%d%02d%02d%d%02d%02d%", taxes[0].get_type(), tax0_1, tax0_2
			//	, taxes[1].get_type(), tax1_1, tax1_2
			//	, taxes[2].get_type(), tax2_1, tax2_2);
		}

		public bool set_date(int[] date) {
			if(date.Length != 6) return false;

			return fm.SendCmd ("PG"
				+ 			(date[0]<10?"0":"")+date[0].ToString() //DD
				+ 			(date[1]<10?"0":"")+date[1].ToString() //MM
				+ 			(date[2]<10?"0":"")+date[2].ToString() //YY
			) && fm.SendCmd ("PF"
				+ 			(date[3]<10?"0":"")+date[3].ToString() //HH
				+ 			(date[4]<10?"0":"")+date[4].ToString() //MM
				+ 			(date[5]<10?"0":"")+date[5].ToString() //SS
			);
		}

		public bool set_vendors(FMVendor[] vendors) {
			if(vendors.Length <= 0 || vendors.Length>12) return false;

			bool ready = true;
			for(int i=0;i<vendors.Length && ready;++i) {
				ready = fm.SendCmd ("PC" + gen_number_fm (vendors [i].id, false, 2) + gen_number_fm (vendors [i].id, false, 5) + SafeSubstring(vendors [i].name.Trim (), 0, 12));
				//sprintf(buffer, "PC%02d%05d%s", vendors[i].get_id(), vendors[i].get_id(), vendors[i].get_name().toStdString().c_str());
			}

			return ready;
		}


		public bool set_payments(FMPayment[] payments) {
			if(payments.Length <= 0 || payments.Length>12) return false;

			bool ready = true;
			for(int i=0;i<payments.Length && ready;++i) {
				ready = fm.SendCmd ("PE" + gen_number_fm (payments [i].id, false, 2) + SafeSubstring(payments [i].name.Trim (), 0, 12));
				//sprintf(buffer, "PE%02d%s", payments[i].get_id(), payments[i].get_name().toStdString().c_str());
			}

			return ready;
		}

	}
}

