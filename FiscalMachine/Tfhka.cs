using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

namespace PoSFiscalMachine
{
    /// <summary>
    /// Representa una Librería de Clase para Protocolo de Comunicación Directo
    /// </summary>
    public class Tfhka
    {
        #region Variables Globales

        private SerialPort Port = new SerialPort();
		private char STX = (char)0x02, ETX = (char)0x03, ENQ = (char)0x05, ACK = (char)0x06, NACK = (char)0x15, LRC = ' ';
		private string comPort;
		private bool statusPort = false;
		private bool IS_DEBUG = false;
		private bool CTS = false;
		private byte[] lastResponse = null;
		private byte[] lastResponseStatus = null;
		private bool existsResponse = false;
		private static Mutex mutex = new Mutex();
        #endregion

        #region Constructor
        /// <summary>
        /// Inicializa una nueva instancia de la clase Tfhka, Conecta y define el puerto a manejar en la clase
        /// </summary>
        /// <param name="Puerto">Nombre del Puerto Serial</param>
        public Tfhka(string Puerto, bool _IS_DEBUG = false)
		{
			IS_DEBUG = _IS_DEBUG;
			comPort = Puerto;
            OpenFpctrl(Puerto);
        }
        #endregion

        #region Propiedades
        /// <summary>
        /// Retorna el nombre del Puerto
        /// </summary>
        public string ComPort
        {
            get { return comPort; }
		}
		public bool ExistsResponse
		{
			get { return existsResponse; }
		}
		public byte[] LastResponse
		{
			get { return lastResponse; }
		}
        /// <summary>
        /// Retorna  si el puerto esta Abierto  ó Cerrado
		/// </summary>
		public bool StatusPort
		{
			get { return statusPort; }
		}

		public byte[] LastResponseStatus {
			get {
				return lastResponseStatus;
			}
		}

        #endregion

        #region Métodos Privados
        /// <summary>
        /// Método Exclusivo para los métodos SendCmd y SendCmd_Archivos
        /// </summary>
        /// <param name="cmd">Comando o trama</param>
		private void Do_XOR(string cmd)
        {
            char[] vector;
            string A = "", B = "";
            int N1 = 0, RX = 0, IND = 0;
            vector = cmd.ToCharArray();
            for (int i = 0; i < vector.Length; i++)
            {
                int p = (int)vector[i];
                A = p.ToString();
                N1 = p;

                if (B != "")
                    RX = RX ^ N1;
                else
                    RX = N1;

                B = A;

                ++IND;
            }

            int ival = RX ^ 03;
            LRC = (char)ival;

        }
        
        /// <summary>
        /// Consulta y hace el intento de trabajr con las señales CTS y RTS antes de ejecutar metodos de escrituras
        /// </summary>
        private void ManipulaCTS_RTS()
        {
			if (IS_DEBUG)
				return;
			if (!Port.IsOpen)
				OpenFpctrl(ComPort);

			try
            {

                Port.RtsEnable = true;
                Wait_CTS();

            }
            catch (Exception e5)
            {
				CloseFpctrl();
            }

        }
        /// <summary>
        /// Metodo para esperar que el la señal CTS se ponga en true
        /// </summary>
        private void Wait_CTS()
		{
			if (IS_DEBUG)
				return;
            try
            {
                long time = 0, time2 = 0, diff = 0;
                DateTime calendario = DateTime.Now;

                time = calendario.Ticks;
                Thread.Sleep(10);
                do
                {
                    DateTime calendario2 = DateTime.Now;
                    time2 = calendario2.Ticks;
                    diff = time2 - time;
                    CTS = Port.CtsHolding;
                }
				while (!CTS && diff < 3 * 10000000);//10000000 Ticks=1seg
            }
			catch (Exception e)
			{
				CloseFpctrl();
            }
		}

		private void ForceSetBaudRate(string portName, int baudRate)
		{
			if (IS_DEBUG)
				return;
			if (Type.GetType ("Mono.Runtime") == null) return; //It is not mono === not linux! 
			string arg = String.Format("-F {0} speed {1}",portName , baudRate);
			var proc = new Process
			{
				EnableRaisingEvents = false,
				StartInfo = {FileName = @"stty", Arguments = arg}
			};
			proc.Start();
			proc.WaitForExit();
		}

        #endregion

        #region Métodos Públicos
        /// <summary>
        /// Metodo para la Configuración del Puerto
        /// </summary>
        /// <param name="Puerto">Nombre del Puerto Serial</param>
        public bool OpenFpctrl(string Puerto)
		{
			Program.PrintLog("Abriendo puerto "+Puerto+"...");
			if (IS_DEBUG) {
				statusPort = true;
				Program.PrintLog("Resultado "+Puerto+": "+(statusPort?"Abierto":"Cerrado"));
				return true;
			}
            try
            {
                Port.PortName = Puerto;
                Port.BaudRate = 9600;
                Port.DataBits = 8;
                Port.StopBits = StopBits.One;
                Port.Parity = Parity.Even;
                Port.ReadBufferSize = 256;
                Port.WriteBufferSize = 256;
				Port.Encoding = Encoding.ASCII;
				Port.Handshake = Handshake.XOnXOff;
				Port.ReadTimeout = 5000; 
				Port.WriteTimeout = 5000;
				Port.Open();
				ForceSetBaudRate(Puerto, 9600);
				statusPort = Port.IsOpen;

				Program.PrintLog("Resultado "+Puerto+": "+(statusPort?"Abierto":"Cerrado"));

				return statusPort;
            }
			catch (System.IO.IOException e)
			{
				Program.PrintLog("OpenFpctrl IOException: "+e.ToString());
				statusPort = false;
                return false;
			}
			catch (ArgumentException e)
			{
				Program.PrintLog("OpenFpctrl ArgumentException: "+e.ToString());
				statusPort = false;
				return false;
			}
			catch (Exception e)
			{
				Program.PrintLog("OpenFpctrl Exception: "+e.ToString());
				statusPort = false;
				return false;
			}
		}

        /// <summary>
        /// Metodo para Cerrar el Puerto serie
        /// </summary>
        public void CloseFpctrl()
		{
			if (IS_DEBUG)
				return;
			Program.PrintLog("Cerrando puerto...");
			try{ Port.Close (); Port.Dispose(); }catch {}
			Port = new SerialPort();
            statusPort = false;
        }

		public bool Reconnect()
		{
			Program.PrintLog("Reconectando...");
			CloseFpctrl ();
			return OpenFpctrl (comPort);
		}

        /// <summary>
        /// Metodo para el Envio de Comandos
        /// </summary>
        /// <param name="cmd">Comando o trama</param>
		public bool SendCmd(string cmd, int _timeout = 5, int retry = 0)
		{
			Program.PrintLog("SendCmd: "+cmd + ", timeout: "+_timeout+"seg");
			if (IS_DEBUG)
				return true;
			mutex.WaitOne ();
			if (!StatusPort) {
				mutex.ReleaseMutex ();
				return false;
			}

			try
			{
				Port.ReadTimeout = 1000;

				existsResponse = false;

	            ManipulaCTS_RTS();

				if (CTS) {
					
					if (retry == 3) {
						Reconnect ();
						Flush ();
					}

					Port.DiscardOutBuffer();
					Port.DiscardInBuffer();

					Port.ReadTimeout = _timeout * 1000; 

					while (Port.BytesToRead > 0)
						Port.ReadChar();

					Do_XOR (cmd);

					List<byte> cmd_list = new List<byte>();

					cmd_list.Add ((byte)STX);

					for (int i = 0; i < cmd.Length; ++i)
						cmd_list.Add ((byte)cmd [i]);

					cmd_list.Add ((byte)ETX);
					cmd_list.Add ((byte)LRC);

					Port.Write(cmd_list.ToArray(), 0, cmd_list.Count);
					while (Port.BytesToWrite > 0)
						Thread.Sleep(2);

					Thread.Sleep(2);
					byte y = (byte)Port.ReadChar();

					if (y == ENQ)
					{
						byte[] a = new byte[1];
						a [0] = (byte)ACK;
						Port.Write(a, 0, 1);
						y = (byte)Port.ReadChar();
					}
					if (y == STX)
					{
						List<byte> rsp = new List<byte> ();
						rsp.Add (y);
						bool isETX = false;
						int retryByteRead = 0;
						do {
							y = (byte)Port.ReadChar();
							rsp.Add (y);

							if(isETX) 
								break;

							if(y == ETX)
								isETX = true;

							while(Port.BytesToRead == 0) {
								Thread.Sleep(10);
								if(++retryByteRead > _timeout * 100)
									break;
							}

							if(Port.BytesToRead == 0 && retryByteRead > _timeout * 100)
								break;

						} while(Port.BytesToRead > 0);

						byte[] a = new byte[1];
						a [0] = (byte)ACK;
						Port.Write(a, 0, 1);

						lastResponse = rsp.ToArray ();
						existsResponse = true;
						mutex.ReleaseMutex ();
						return true;
					}else if (y == ACK)
					{
						List<byte> rsp = new List<byte> ();
						rsp.Add (y);
						lastResponse = rsp.ToArray ();
						existsResponse = true;
						Port.RtsEnable = false;
						mutex.ReleaseMutex ();
						return true;
					}else if (y == NACK)
					{
						Thread.Sleep(500);
						Port.DiscardOutBuffer();
						Port.DiscardInBuffer();
						Port.RtsEnable = false;
						mutex.ReleaseMutex ();
						if (retry > 3) {
							Program.PrintLog("Max Retry {"+retry.ToString()+"} NACK: "+cmd);
							return false;
						}else{
							return SendCmd(cmd, _timeout, retry + 1);
						}
					}
	                else
	                {
						Port.RtsEnable = false;
						mutex.ReleaseMutex ();
						Program.PrintLog("SendCmd First BYTE Invalid: '"+y+"'");
						if (retry > 3) {
							Program.PrintLog("Max Retry {"+retry.ToString()+"} BYTE Invalid: "+cmd);
							return false;
						}else{
							return SendCmd(cmd, _timeout, retry + 1);
						}
	                }
	            }
	            else
				{
					Program.PrintLog("SendCmd CTS in false");
					Port.RtsEnable = false;

					if (retry > 3) {
						Program.PrintLog("Max Retry {"+retry.ToString()+"} CTS: "+cmd);
						mutex.ReleaseMutex ();
						return false;
					}

					Reconnect ();

					mutex.ReleaseMutex ();
					return SendCmd(cmd, _timeout, retry + 1);
	            }
			} catch(TimeoutException e) {
				if (retry > 3) {
					Program.PrintLog("Max Retry {"+retry.ToString()+"} Timeout("+_timeout.ToString()+"seg): "+cmd);
					Port.RtsEnable = false;
					mutex.ReleaseMutex ();
					return false;
				}

				Program.PrintLog("Timeout("+_timeout.ToString()+"seg): "+cmd);
				
				if (retry == 3) 
					Reconnect ();

				Flush ();

				mutex.ReleaseMutex ();

				return SendCmd(cmd, _timeout, retry + 1);
			} catch (Exception e)
			{
				Program.PrintLog("SendCmd Exception: "+e.ToString());
				if (retry > 3) {
					Program.PrintLog("Max Retry {"+retry.ToString()+"} Exception: "+cmd);
					Port.RtsEnable = false;
					mutex.ReleaseMutex ();
					return false;
				}
				if (retry == 3) 
					Reconnect ();
				Flush ();
				mutex.ReleaseMutex ();
				return SendCmd(cmd, _timeout, retry + 1);
			}
		}

		public void Flush() 
		{
			Program.PrintLog("Haciendo flush...");
			if (IS_DEBUG)
				return;
			ManipulaCTS_RTS();
			if (CTS) {
				Port.DiscardOutBuffer ();
				Port.DiscardInBuffer ();

				byte[] a = new byte[1];
				a [0] = (byte)ACK;

				try {
					Port.Write (a, 0, 1);
				} catch (Exception e1) {
				}

				Thread.Sleep (500);

				try {
					while (Port.BytesToRead > 0)
						Port.ReadChar ();
				} catch (Exception e2) {
				}
				Program.PrintLog("Flush completo.");
			} else {
				Program.PrintLog("Error en Flush, CTS en false.");
			}
		}
		/// <summary>
		/// Lee el Status y Error de la Impresora Fiscal
		/// </summary>
		public bool ReadFpStatus(int retry = 0)
		{
			if (IS_DEBUG)
				return true;
			if (retry > 3) {
				Program.PrintLog("ReadFpStatus Maxima cantidad de reintentos...");
				return false;
			}

			Program.PrintLog("ReadFpStatus STEP1");
			mutex.WaitOne ();
			if (!StatusPort) {
				mutex.ReleaseMutex ();
				return false;
			}
			Program.PrintLog("ReadFpStatus STEP2");

			ManipulaCTS_RTS();

			if (CTS)
			{
				Program.PrintLog("ReadFpStatus STEP3");
				try {
					int t = Port.BytesToRead;
					if (t != 0)
					{
						for (int i = 0; i < t; ++i)
						{
							Port.ReadChar();
						}
					}

					Port.ReadTimeout = 5000;

					byte[] e = new byte[1];
					e [0] = (byte)ENQ;
					Port.Write(e, 0, 1);

					byte y = (byte)Port.ReadChar();

					if (y == STX)
					{
						List<byte> rsp = new List<byte> ();
						rsp.Add (y);
						bool isETX = false;
						int retryByteRead = 0;
						do {
							y = (byte)Port.ReadChar();
							rsp.Add (y);

							if(isETX) 
								break;

							if(y == ETX)
								isETX = true;

							while(Port.BytesToRead == 0) {
								Thread.Sleep(10);
								if(++retryByteRead > 500)
									break;
							}

							if(Port.BytesToRead == 0 && retryByteRead > 500)
								break;
						} while(Port.BytesToRead > 0);
						lastResponse = rsp.ToArray ();
						lastResponseStatus = rsp.ToArray ();
						existsResponse = true;
						mutex.ReleaseMutex ();
						return true;
					} else {
						Program.PrintLog("ReadFpStatus STX not found, retry...");
						Port.RtsEnable = false;
						mutex.ReleaseMutex ();
						Thread.Sleep(500); // Hay que esperar un poco para poder limpiar el buffer si se embasuro...
						return ReadFpStatus (retry + 1);
					}
				} catch (TimeoutException e) {
					//  Se envía el mensaje
					Program.PrintLog("Timeout ReadFpStatus, retry...");
					Reconnect ();
					Flush ();
					mutex.ReleaseMutex ();
					Thread.Sleep(500); 
					return ReadFpStatus (retry + 1);
				} catch (Exception e)
				{
					Program.PrintLog("ReadFpStatus Exception");
					Reconnect ();
					Flush ();
					mutex.ReleaseMutex ();
					Thread.Sleep(500); 
					return ReadFpStatus (retry + 1);
				}
			}
			else
			{
				Program.PrintLog("ReadFpStatus ERROR CTS");
				Reconnect ();
				mutex.ReleaseMutex ();
				Thread.Sleep(500);
				return ReadFpStatus (retry + 1);
			}
		}
        #endregion
    }
}
