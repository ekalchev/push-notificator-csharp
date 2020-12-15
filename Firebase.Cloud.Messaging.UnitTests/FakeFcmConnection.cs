using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.UnitTests
{
    class FakeFcmConnection : IFcmConnection
    {
        private byte[] buffer = new byte[2048];
        private readonly Func<CancellationToken, Task> receiveAction;

        public event EventHandler<byte[]> DataSent;
        public event EventHandler Connected;

        public FakeFcmConnection(Func<CancellationToken, Task> receiveAction)
        {
            this.receiveAction = receiveAction;
        }

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public Task ConnectAsync()
        {
            Connected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            await receiveAction(cancellationToken);
        }

        public void Send(byte[] data)
        {
            DataSent?.Invoke(this, data);
        }

        public Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            DataSent?.Invoke(this, data);
            return Task.CompletedTask;
        }

        public void SimulateDataReceived(byte[] data)
        {
            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
            DataReceived?.Invoke(this, new DataReceivedEventArgs(buffer, data.Length));
        }
    }
}
