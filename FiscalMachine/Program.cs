using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.IO.Ports;

namespace PoSFiscalMachine
{
    class Program
	{
		static public void PrintLog(string log) {
			DateTime now = DateTime.Now;
			Console.WriteLine (now.ToString ("yyyy-MM-dd HH:mm:ss") + ": " + log);
		}
        static void Main(string[] args)
        {
			if (args.Length != 3) {
				Program.PrintLog ("Argumentos Invalidos: ./program <server_port> <hash_key> <is_debug>");
				return;
			}

			string seek = args[1];
			bool IS_DEBUG = Convert.ToInt32(args[2]) == 1;
			PosServer PoS = new PosServer(Convert.ToInt32(args[0]), seek);

			FiscalMachine fm = null;
			Tfhka Tf = null;
			int num_retry = 0;
			while (true) {

				PoS.lockMutex ();
				if (Tf != null && Tf.StatusPort && Tf.ReadFpStatus ()) {
					if (!IS_DEBUG) {
						Program.PrintLog ("PING OK: " + fm.Fm_serial);
					}
					PoS.unlockMutex ();
					num_retry = 0;
					Thread.Sleep (15000);
					continue;
				} else if (Tf != null) {
					Tf.CloseFpctrl ();
					fm = null;
				}

				if (num_retry > 3) {
					PoS.unlockMutex ();
					PoS.stopServer ();
					Program.PrintLog("Salida forzada por maxima cantidad de intentos de conexion a la Maquina Fiscal");
					return; 
					// Se forza cerrar la APP para que se reinicie el proceso.
					// Esto funciona aveces cuando la maquina fiscal queda guindada.
				}
				++num_retry;

				PoS.Fm = null;
				if (IS_DEBUG) {
					Tf = new Tfhka ("COMTEST1", true);
					fm = new FiscalMachine (Tf, true);
					PoS.Fm = fm;
				} else {
					Program.PrintLog ("Check ports");
					string[] ports = SerialPort.GetPortNames ();

					Program.PrintLog ("Ports fonud: " + ports.Length);

					for (int i = 0; i < ports.Length && fm == null; ++i) {
						Program.PrintLog ("Probando Puerto: " + ports [i]);
						Tf = new Tfhka (ports [i]);
						if (Tf.StatusPort) {
							fm = new FiscalMachine (Tf, false);
							if (fm.Fm_vat != null && fm.Fm_taxes != null && fm.Fm_vat != "" && fm.Fm_taxes.Count >= 3) {
								PoS.Fm = fm;
								break;
							}
							Tf.CloseFpctrl ();
							fm = null;
						}
						Tf = null;
					}
				}
				PoS.unlockMutex ();

				if (fm == null)
					Thread.Sleep (5000);
			}
        }
    }
}
