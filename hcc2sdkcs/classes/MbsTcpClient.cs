using System.Net.Sockets;

namespace Sensia.HCC2.SDK.Classes
{
    public class MbsTcpClient : TcpClient 
    {
        public bool IsDisposed { get; set; }
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}