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
    class FcmConnection : IFcmConnection
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
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 60);
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);

            sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls13 | SslProtocols.Tls12, false).ConfigureAwait(false);
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            CancellationToken linkedToken = CreateLinkedToken(cancellationToken);

            await sslStream.WriteAsync(data, 0, data.Length, linkedToken).ConfigureAwait(false);
        }

        public void Send(byte[] data)
        {
            sslStream.Write(data, 0, data.Length);
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
            catch (SocketException)
            {

            }
            catch (OperationCanceledException)
            {

            }
        }

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

    interface IFcmConnection : IDisposable
    {
        event EventHandler<DataReceivedEventArgs> DataReceived;
        Task SendAsync(byte[] data, CancellationToken cancellationToken);
        void Send(byte[] data);
        Task ConnectAsync();
        Task ReceiveAsync(CancellationToken cancellationToken);
    }
}
