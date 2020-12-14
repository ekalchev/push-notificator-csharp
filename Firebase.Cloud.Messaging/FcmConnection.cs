using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging
{
    class FcmConnection : IDisposable
    {
        private const string host = "mtalk.google.com";
        private const int port = 5228;
        private TcpClient client;
        private SslStream sslStream;
        private byte[] buffer = new byte[2048];
        private CancellationTokenSource cts = new CancellationTokenSource();

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public async Task ConnectAsync()
        {
            client = new TcpClient(host, port);
            SetKeepAlive(client.Client, 60 * 1000, 60 * 1000); //TODO: test keep alive

            sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls, false).ConfigureAwait(false);
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            CancellationToken linkedToken = CreateLinkedToken(cancellationToken);

            await sslStream.WriteAsync(data, 0, data.Length, linkedToken).ConfigureAwait(false);
        }

        private CancellationToken CreateLinkedToken(CancellationToken cancellationToken)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;
        }

        public async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            CancellationToken linkedToken = CreateLinkedToken(cancellationToken);

            try
            {
                while (linkedToken.IsCancellationRequested == false)
                {
                    int bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length, linkedToken).ConfigureAwait(false);

                    if (bytesRead > 0 && DataReceived != null)
                    {
                        DataReceived.Invoke(this, new DataReceivedEventArgs(buffer, bytesRead));
                    }

                    // it seems that if the underlying socket is disconnected no exception is thrown
                    if (client.Connected == false)
                    {
                        throw new SocketException(1);
                    }
                }
            }
            catch(SocketException)
            {
                
            }
            catch(OperationCanceledException)
            {

            }
        }

        /// <summary>
        ///     Sets the Keep-Alive values for the current tcp connection
        /// </summary>
        /// <param name="socket">Current socket instance</param>
        /// <param name="keepAliveInterval">Specifies how often TCP repeats keep-alive transmissions when no response is received. TCP sends keep-alive transmissions to verify that idle connections are still active. This prevents TCP from inadvertently disconnecting active lines.</param>
        /// <param name="keepAliveTime">Specifies how often TCP sends keep-alive transmissions. TCP sends keep-alive transmissions to verify that an idle connection is still active. This entry is used when the remote system is responding to TCP. Otherwise, the interval between transmissions is determined by the value of the keepAliveInterval entry.</param>
        public static void SetKeepAlive(Socket socket, uint keepAliveInterval, uint keepAliveTime)
        {
            var keepAlive = new TcpKeepAlive
            {
                onoff = 1,
                keepaliveinterval = keepAliveInterval,
                keepalivetime = keepAliveTime
            };
            int size = Marshal.SizeOf(keepAlive);
            IntPtr keepAlivePtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(keepAlive, keepAlivePtr, true);
            var buffer = new byte[size];
            Marshal.Copy(keepAlivePtr, buffer, 0, size);
            Marshal.FreeHGlobal(keepAlivePtr);
            socket.IOControl(IOControlCode.KeepAliveValues, buffer, null);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TcpKeepAlive
        {
            internal uint onoff;
            internal uint keepalivetime;
            internal uint keepaliveinterval;
        };

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private bool disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    cts.Dispose();
                    sslStream?.Dispose();
                    client?.Dispose();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
