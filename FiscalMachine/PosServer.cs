using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using FiscalMachineStruct;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PoSFiscalMachine
{
	public class PosServer
	{
		enum ERROR_FM {
			EFM_INVALID_JSON = 1,
			EFM_FISCAL_MACHINE_NOT_FOUND,
			EFM_HASH_INVALID,
			EFM_HASH_EMPTY,
			EFM_COMMAND_EMPTY,
			EFM_SERIAL_EMPTY,
			EFM_UNK_ERROR,
			EFM_FISCAL_MACHINE_NOT_RESPOND,

			EFM_COMMAND_NOT_FOUND = 100,
			EFM_COMMAND_INVALID_ARGS,
			EFM_COMMAND_INVALID_HEADER_FOOTER,
			EFM_COMMAND_INVALID_PRINT,
			EFM_COMMAND_INVALID_INVOICE,
			EFM_COMMAND_INVALID_CREDIT_NOTE,
			EFM_COMMAND_INVALID_PAYMENT
		};

		private String seekHashSecurity;
		private TcpListener tcpListener;
		private Thread listenThread;
		private FiscalMachine fm;
		private static Mutex mutex = new Mutex();

		public FiscalMachine Fm {
			get {
				return fm;
			}
			set {
				fm = value;
			}
		}

		public void lockMutex() {
			mutex.WaitOne();
		}

		public void unlockMutex() {
			mutex.ReleaseMutex();
		}

		private string respondError(ERROR_FM err_num, string err_str) {
			return "{'error':"+(int)err_num+",'errorSTR':'"+err_str.Replace("'","\\'")+"'}";
		}

		public string CalculateMD5Hash(string input)
		{
			// step 1, calculate MD5 hash from input
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hash = md5.ComputeHash(inputBytes);

			// step 2, convert byte array to hex string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++)
			{
				sb.Append(hash[i].ToString("X2"));
			}
			return sb.ToString();
		}

		public PosServer (int port, string seek)
		{
			seekHashSecurity = seek;
			fm = null;
			tcpListener = new TcpListener(IPAddress.Any, port);
			listenThread = new Thread(new ThreadStart(ListenForClients));
			listenThread.Start();
		}

		public void stopServer ()
		{
			try {
				tcpListener.Stop();
			}catch{
				Program.PrintLog ("Error in stopServer Stop tcpListener");
			}
			try {
				listenThread.Abort();
				listenThread.Join();
			}catch{
				Program.PrintLog ("Error in stopServer Stop listenThread");
			}
		}

		private void ListenForClients()
		{
			tcpListener.Start();
			try {
				while (true)
				{
					//blocks until a client has connected to the server
					TcpClient client = tcpListener.AcceptTcpClient();

					//create a thread to handle communication 
					//with connected client
					Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
					clientThread.Start(client);
				}
			}catch{
				Program.PrintLog ("TCP Listener Stop");
			}
		}

		private JObject proccessCommand(string command, JArray args) {
			JObject respond;
			switch (command) {
			case "get_resume":
				if (fm.reloadLastFiscalPrints (true, true, true, true)) {
					uint status_resume = fm.get_status_code ();
					string[] status_str = fm.get_status ();
					respond = new JObject (
						new JProperty ("error", 0),
						new JProperty ("result", new JObject (
							new JProperty ("status", status_resume),
							new JProperty ("status_str", status_str),
							new JProperty ("last_invoice", fm.Last_invoice),
							new JProperty ("last_report_z", fm.Last_report_z),
							new JProperty ("last_credit_note", fm.Last_credit_note),
							new JProperty ("daily_sale", fm.Daily_sale),
							new JProperty ("taxes", JToken.FromObject (fm.Fm_taxes))
						))
					);
				} else {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_FISCAL_MACHINE_NOT_RESPOND),
						new JProperty ("errorSTR", "FM Not Respond")
					);
				}
				break;
			case "get_vat":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Fm_vat)
				);


				break;
			case "get_serial":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Fm_serial)
				);


				break;
			case "get_model":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", "Bixolon") //No se pudo implementar una manera de hacer esto...
				);


				break;
			case "get_brand":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", "The Factory")
				);


				break;
			case "get_last_invoice_id":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Last_invoice)
				);

				break;
			case "get_last_credit_note_id":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Last_credit_note)
				);

				break;
			case "get_last_report_z_id":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Last_report_z)
				);

				break;
			case "get_count_header_footer":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Count_header_footer) //NO se encontro una manera de inicializar esto, se asume en 0 cada vez que se inicia el programa
				);

				break;
			case "get_count_tax":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Count_tax) //NO se encontro una manera de inicializar esto, se asume en 0 cada vez que se inicia el programa
				);

				break;
			case "get_daily_sale":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.Daily_sale)
				);


				break;
			case "get_status":
				uint status_s = fm.get_status_code();
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", status_s),
					new JProperty("result_str", JToken.FromObject(fm.get_status()))
				);

				break;
			case "get_header":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", JToken.FromObject (fm.Fm_header)) //NO se encontro una manera de inicializar esto
				);

				break;
			case "get_footer":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", JToken.FromObject (fm.Fm_footer)) //NO se encontro una manera de inicializar esto
				);

				break;
			case "check_status":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.get_status_code()),
					new JProperty("result_datail", fm.get_status())
				);

				break;
			case "abort_fiscal_print":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.abort_fiscal_print())
				);

				break;
			case "generate_invoice":

				if (args.Count < 1) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				JToken tk = args [0];
				Program.PrintLog ("Factura: "+tk.ToString ());
				FMInvoice inv = JsonConvert.DeserializeObject<FMInvoice> (tk.ToString ());
				bool show_barcode = false;
				string barcode = "0";
				if (!inv.isValid ()) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_INVOICE),
						new JProperty ("errorSTR", "Invalid Invoice")
					);
					break;
				}

				if (args.Count > 1)
					show_barcode = args [1].ToObject<bool> ();

				if (args.Count > 2)
					barcode = args [2].ToObject<string> ();

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.generate_invoice(inv, show_barcode, barcode))
				);


				break;
			case "generate_credit_note":

				if (args.Count != 1) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				FMCreditNote cn = args [0].ToObject<FMCreditNote> ();
				if (!cn.isValid ()) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_CREDIT_NOTE),
						new JProperty ("errorSTR", "Invalid Credit Note")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.generate_credit_note(cn))
				);

				break;
			case "open_drawer":

				if (args.Count < 1) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				List<FMPaymentInvoice> fixed_drawer = new List<FMPaymentInvoice>();
				for (int i = 0; i < args.Count; ++i)
					fixed_drawer.Add (args [i].ToObject<FMPaymentInvoice> ());

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.open_drawer(fixed_drawer.ToArray()))
				);
				break;
			case "generate_report_x":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.generate_report_x())
				);

				break;
			case "generate_report_z":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.generate_report_z())
				);

				break;
			case "print_copy_last_invoice":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_last_invoice())
				);

				break;
			case "print_copy_last_credit_note":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_last_credit_note())
				);

				break;
			case "print_copy_last_report_z":

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_last_report_z())
				);

				break;
			case "print_copy_last_print":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_last_print())
				);

				break;
			case "print_copy_z":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_z(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print_copy_z_date":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_z_date(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print_copy_invoice":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_invoice(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print_copy_invoice_date":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_invoice_date(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print_copy_credit_note":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_credit_note(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print_copy_credit_note_date":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print_copy_credit_note_date(args[0].ToObject<int>(), args[1].ToObject<int>()))
				);

				break;
			case "print":
				if (args.Count < 1) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				string[] list = args.ToObject<string[]> ();

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.print(list))
				);
				break;
			case "set_display_message":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				string header = args [0].ToObject<string> ();
				string footer = args [1].ToObject<string> ();

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_display_message(header, footer))
				);
				break;
			case "set_display_commercial":
				if (args.Count != 1) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				string msg = args [0].ToObject<string> ();

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_display_commercial(msg))
				);
				break;
			case "show_display_commercial":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.show_display_commercial ())
				);
				break;
			case "set_header_footer":
				if (args.Count != 2) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}

				string[] headers = args [0].ToObject<string[]> ();
				string[] footers = args [1].ToObject<string[]> ();

				if (headers.Length < 1 || footers.Length > 8) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Header o Footer empty!")
					);
					break;
				}

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_header_footer(headers, footers))
				);
				break;
			case "set_vendors":
				if (args.Count < 1 || args.Count > 12) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}
				List<FMVendor> vendors = new List<FMVendor>();
				for (int i = 0; i < args.Count; ++i)
					vendors.Add (args [i].ToObject<FMVendor> ());

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_vendors(vendors.ToArray()))
				);

				break;
			case "set_payments":
				if (args.Count < 1 || args.Count > 12) {
					respond = new JObject (
						new JProperty ("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty ("errorSTR", "Invalid Args")
					);
					break;
				}
				List<FMPayment> payments = new List<FMPayment>();
				for (int i = 0; i < args.Count; ++i)
					payments.Add (args [i].ToObject<FMPayment> ());

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_payments(payments.ToArray()))
				);

				break;
			case "set_taxes":
				if (args.Count != 3) {
					respond = new JObject(
						new JProperty("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty("errorSTR", "Invalid Args")
					);
					break;
				}
				List<FMTax> taxes = new List<FMTax>();
				taxes.Add(args[0].ToObject<FMTax>());
				taxes.Add(args[1].ToObject<FMTax>());
				taxes.Add(args[2].ToObject<FMTax>());

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_taxes(taxes.ToArray()))
				);

				break;
			case "set_date":
				if (args.Count != 6) {
					respond = new JObject(
						new JProperty("error", (int)ERROR_FM.EFM_COMMAND_INVALID_ARGS),
						new JProperty("errorSTR", "Invalid Args")
					);
					break;
				}
				List<int> date = new List<int>();
				date.Add(args[0].ToObject<int>());
				date.Add(args[1].ToObject<int>());
				date.Add(args[2].ToObject<int>());
				date.Add(args[3].ToObject<int>());
				date.Add(args[4].ToObject<int>());
				date.Add(args[5].ToObject<int>());

				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", fm.set_date(date.ToArray()))
				);

				break;
			case "get_taxes":
				respond = new JObject(
					new JProperty("error", 0),
					new JProperty("result", JToken.FromObject (fm.Fm_taxes))
				);
				break;
			default:
				respond = new JObject(
					new JProperty("error", (int)ERROR_FM.EFM_COMMAND_NOT_FOUND),
					new JProperty("errorSTR", "Command not found: "+command)
				);
				break;
			}
			return respond;
		}

		private void HandleClientComm(object client)
		{
			Program.PrintLog ("Nuevo Cliente");
			TcpClient tcpClient = (TcpClient)client;
			NetworkStream clientStream = tcpClient.GetStream();
			//StreamReader sr = new StreamReader(clientStream, Encoding.UTF8);
			ASCIIEncoding encoder = new ASCIIEncoding();

			byte[] message = new byte[40960];
			string response = "";
			int bytesRead;

			while (true)
			{
				bytesRead = 0;

				try
				{
					//blocks until a client sends a message
					bytesRead = clientStream.Read(message, 0, 40960);
				}
				catch
				{
					//a socket error has occured
					break;
				}

				if (bytesRead == 0)
				{
					//the client has disconnected from the server
					break;
				}

				//message has successfully been received
				response += encoder.GetString(message, 0, bytesRead);
				try {
					string rsp = "";
					JObject o = JObject.Parse(response);
					if(o.Count>0){
						Program.PrintLog ("Nuevo Mensaje: " + response);
						response = "";
						string serial = (string)o["serial"];
						string hash = (string)o["hash"];
						string command = (string)o["command"];
						if(serial.Length == 0) {
							rsp = respondError(ERROR_FM.EFM_SERIAL_EMPTY, "Serial is Empty");
						}else if(command.Length == 0){
							rsp = respondError(ERROR_FM.EFM_COMMAND_EMPTY, "Command is Empty");
						}else if(hash.Length == 0){
							rsp = respondError(ERROR_FM.EFM_HASH_EMPTY, "Hash is Empty");
						} else {
							string md5Hash = CalculateMD5Hash(serial.ToUpper()+command.ToUpper()+seekHashSecurity).ToUpper();
							if(hash.ToUpper()!=md5Hash) {
								rsp = respondError(ERROR_FM.EFM_HASH_INVALID, "Invalid Hash");
							}else{
								lockMutex();
								if(fm == null || !fm.Fm.StatusPort || serial != fm.Fm_serial) {
									rsp = respondError(ERROR_FM.EFM_FISCAL_MACHINE_NOT_FOUND, "Fiscal Machine " + serial + " not found.");
								} else {
									try{
										JArray args = o["args"] as JArray;
										o.Add("respond", proccessCommand(command, args));
										rsp = o.ToString();
									}catch{
										rsp = respondError(ERROR_FM.EFM_UNK_ERROR, "Fiscal Machine generic error.");
									}
								}
								unlockMutex();
							}
						}
					}else{
						rsp = respondError(ERROR_FM.EFM_INVALID_JSON, "JSON Invalid");
					}
					Program.PrintLog ("Respuesta: " + rsp);

					byte[] b_rsp = Encoding.ASCII.GetBytes(rsp+"\n");
					clientStream.Write(b_rsp, 0, b_rsp.Length);
				}catch (Exception e){
					Program.PrintLog("Error... JObject Exception: "+e.ToString());
				}
			}
			tcpClient.Close();
			Program.PrintLog ("Cliente Desconectado");
		}
	}
}

