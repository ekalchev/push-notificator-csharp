using Google.Protobuf;
using McsProto;
using Firebase.Cloud.Messaging.Fcm;
using Firebase.Cloud.Messaging.Gcm;
using Firebase.Cloud.Messaging.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Firebase.Cloud.Messaging
{
    public class FcmListener : IDisposable
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        // Max # of bytes a length packet consumes. A Varint32 can consume up to 5 bytes
        // (the msb in each byte is reserved for denoting whether more bytes follow).
        // Although the protocol only allows for 4KiB payloads currently, and the socket
        // stream buffer is only of size 8KiB, it's possible for certain applications to
        // have larger message sizes. When payload is larger than 4KiB, an temporary
        // in-memory buffer is used instead of the normal in-place socket stream buffer.
        private const int kSizePacketLenMin = 1;
        private const int kSizePacketLenMax = 5;
        private const int kTagPacketLen = 1;
        private const int kVersionPacketLen = 1;
        private const string kHeartbeatIntervalSettingName = "hbping";
        private const string kLoginSettingDefaultName = "new_vc";
        // The current MCS protocol version.
        private const int kMCSVersion = 41;

        private IFcmConnection connection = new FcmConnection();
        private ProcessingState state;
        private MessageTag messageTag;
        private bool handShakeComplete;
        private long sizePacketSoFar;
        private uint messageSize;
        private MemoryStream dataStream = new MemoryStream();

        private LoginRequest loginRequest;
        private LoginResponse loginResponse;

        public event EventHandler<IMessage> MessageReceived;

        public async Task ConnectAsync()
        {
            CheckDisposed();

            connection.DataReceived += Connection_DataReceived;
            await connection.ConnectAsync();
        }

        public async Task LoginAsync(ulong androidId, ulong securityToken)
        {
            CheckDisposed();

            loginRequest = CreateLoginRequest(androidId, securityToken, Enumerable.Empty<string>());

            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteByte(kMCSVersion);

                byte[] buffer = CreateBufferForSend(memoryStream, MessageTag.kLoginRequestTag, loginRequest);

                await connection.SendAsync(buffer, cts.Token).ConfigureAwait(false);
            }
        }

        public async Task ListenAsync()
        {
            CheckDisposed();

            await connection.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }

        private void Connection_DataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            // here we are in worker thread
            dataStream.Position = dataStream.Length;
            dataStream.Write(dataReceivedEventArgs.Buffer, 0, dataReceivedEventArgs.Length);
            dataStream.Position = 0;
            ProcessData(dataStream);
        }

        private void ProcessData(Stream stream)
        {
            long minBytesNeeded = 0;

            switch (state)
            {
                case ProcessingState.MCS_VERSION_TAG_AND_SIZE:
                    minBytesNeeded = kVersionPacketLen + kTagPacketLen + kSizePacketLenMin;
                    break;
                case ProcessingState.MCS_TAG_AND_SIZE:
                    minBytesNeeded = kTagPacketLen + kSizePacketLenMin;
                    break;
                case ProcessingState.MCS_SIZE:
                    minBytesNeeded = sizePacketSoFar + 1;
                    break;
                case ProcessingState.MCS_PROTO_BYTES:
                    minBytesNeeded = messageSize;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected state: {this.state}");
            }

            if (stream.Length >= minBytesNeeded)
            {
                Debug.WriteLine($"Processing MCS data: state == {state}");

                switch (state)
                {
                    case ProcessingState.MCS_VERSION_TAG_AND_SIZE:
                        OnGotVersion(stream);
                        break;
                    case ProcessingState.MCS_TAG_AND_SIZE:
                        OnGotMessageTag(stream);
                        break;
                    case ProcessingState.MCS_SIZE:
                        OnGotMessageSize(stream);
                        break;
                    case ProcessingState.MCS_PROTO_BYTES:
                        OnGotMessageBytes(stream);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected state: {this.state}");
                }
            }
            else
            {
                Debug.WriteLine($"Socket read finished prematurely.Waiting for {minBytesNeeded - stream.Length} bytes");
            }
        }

        private void OnGotVersion(Stream stream)
        {
            int version = stream.ReadByte();
            Debug.WriteLine($"Vesion is {version}");

            if (version < kMCSVersion && version != 38)
            {
                throw new InvalidOperationException($"Got wrong version: {version}");
            }

            OnGotMessageTag(stream);
        }

        private void OnGotMessageTag(Stream stream)
        {
            messageTag = (MessageTag)stream.ReadByte();

            Debug.WriteLine($"Received proto of type {messageTag}");

            OnGotMessageSize(stream);
        }

        private byte[] CreateBufferForSend(MessageTag messageTag, IMessage message)
        {
            using (var memoryStream = new MemoryStream())
            {
                return CreateBufferForSend(memoryStream, messageTag, message);
            }
        }

        private byte[] CreateBufferForSend(MemoryStream stream, MessageTag messageTag, IMessage message)
        {
            byte[] preabmleBuffer = new byte[] { (byte)messageTag };
            stream.Write(preabmleBuffer, 0, preabmleBuffer.Length);
            message.WriteDelimitedTo(stream);
            return stream.ToArray();
        }

        private void OnGotMessageSize(Stream stream)
        {
            bool incompleteSizePacket = false;
            messageSize = 0;
            long prevByteCount = stream.UnreadBytesCount();
            long prevPosition = stream.Position;
            bool hasError = false;

            try
            {
                messageSize = CodedInputStreamInternals.ReadRawVarint32(stream);
            }
            catch(InvalidOperationException)
            {
                // TODO: this is not tested!!!!!

                if (prevByteCount >= kSizePacketLenMax)
                {
                    Debug.WriteLine("Error: Already had enough bytes, something else went wrong");
                    hasError = true;
                    throw new FcmListenerException("Received unexpected data");
                }
                else
                {
                    
                    sizePacketSoFar = prevByteCount - stream.UnreadBytesCount();
                    state = ProcessingState.MCS_SIZE;
                    incompleteSizePacket = true;
                }
            }

            stream.Position = prevPosition;
            
            if (hasError == false && incompleteSizePacket == false)
            {
                Debug.WriteLine($"Proto size:{messageSize}");
                sizePacketSoFar = 0;

                OnGotMessageBytes(stream);
            }
        }

        private void OnLoginResponseTag(LoginResponse loginResponse)
        {
            this.loginResponse = loginResponse;

            if (handShakeComplete == true)
            {
                Debug.WriteLine("Unexpected login response");
                throw new FcmListenerException("Unexpected login response");
            }
            else
            {
                if (loginResponse.Error != null)
                {
                    Debug.WriteLine(string.Format("GCM Handshake complete failed. ErrorCode: {0}, Description: {1} ", loginResponse.Error.Code, loginResponse.Error.Message));
                }
                else
                {
                    handShakeComplete = true;
                    Debug.WriteLine("GCM Handshake complete.");
                }
            }
        }

        private void OnHeartBeatPingTag(HeartbeatPing heartbeatPing)
        {
            HeartbeatAck message = (HeartbeatAck)BuildProtobufOfTag(MessageTag.kHeartbeatAckTag);
            connection.Send(CreateBufferForSend(MessageTag.kHeartbeatAckTag, message));
        }

        private void OnCloseTag(Close close)
        {
            connection.Dispose();
            connection = null;

            throw new IOException("Connection was closed by the server");
        }

        private void OnIqStanzaTag(IqStanza iqStanza)
        {
        }

        private void OnGotMessageBytes(Stream stream)
        {
            IMessage message = BuildProtobufOfTag(messageTag);
            message.MergeDelimitedFrom(stream);

            switch(messageTag)
            {
                case MessageTag.kLoginResponseTag:
                    OnLoginResponseTag((LoginResponse)message);
                    break;
                case MessageTag.kHeartbeatPingTag:
                    OnHeartBeatPingTag((HeartbeatPing)message);
                    break;
                case MessageTag.kCloseTag:
                    OnCloseTag((Close)message);
                    break;
                case MessageTag.kIqStanzaTag:
                    OnIqStanzaTag((IqStanza)message);
                    break;
                case MessageTag.kDataMessageStanzaTag:
                default:
                    MessageReceived?.Invoke(this, message);
                    break;
            }

            GetNextMessage();
        }

        private void GetNextMessage()
        {
            messageTag = 0;
            messageSize = 0;
            state = ProcessingState.MCS_TAG_AND_SIZE;
            var oldStream = dataStream;

            // release the memory for read bytes
            long unreadBytesCount = dataStream.UnreadBytesCount();

            if (unreadBytesCount > 0)
            {
                byte[] unreadBytes = new byte[unreadBytesCount];

                dataStream.Read(unreadBytes, 0, unreadBytes.Length);
                dataStream = new MemoryStream(unreadBytes.Length);
                dataStream.Write(unreadBytes, 0, unreadBytes.Length);
            }
            else
            {
                dataStream = new MemoryStream();
            }

            oldStream.Dispose();
        }

        private LoginRequest CreateLoginRequest(ulong androidId, ulong gcmSecurityToken, IEnumerable<string> receivedPersistentIds)
        {
            LoginRequest loginRequest = new LoginRequest()
            {
                AdaptiveHeartbeat = false,
                AuthService = LoginRequest.Types.AuthService.AndroidId,
                AuthToken = gcmSecurityToken.ToString(),
                Id = "chrome-63.0.3234.0",
                Domain = "mcs.android.com",
                DeviceId = $"android-{androidId.ToString("x")}",
                NetworkType = 1,
                Resource = androidId.ToString(),
                User = androidId.ToString(),
                UseRmq2 = true,
            };

            loginRequest.Setting.Add(new Setting() { Name = kLoginSettingDefaultName, Value = "1" });

#if DEBUG
            loginRequest.Setting.Add(new Setting() { Name = kHeartbeatIntervalSettingName, Value = "100" });
#endif

            foreach (var persistentid in receivedPersistentIds)
            {
                loginRequest.ReceivedPersistentId.Add(persistentid);
            }

            return loginRequest;
        }

        private IMessage BuildProtobufOfTag(MessageTag tag)
        {
            switch (tag)
            {
                case MessageTag.kHeartbeatPingTag:
                    return new HeartbeatPing();
                case MessageTag.kHeartbeatAckTag:
                    return new HeartbeatAck();
                case MessageTag.kLoginRequestTag:
                    return new LoginRequest();
                case MessageTag.kLoginResponseTag:
                    return new LoginResponse();
                case MessageTag.kCloseTag:
                    return new Close();
                case MessageTag.kIqStanzaTag:
                    return new IqStanza();
                case MessageTag.kDataMessageStanzaTag:
                    return new DataMessageStanza();
                case MessageTag.kStreamErrorStanzaTag:
                    return new StreamErrorStanza();
                default:
                    return null;
            }
        }

        private void CheckDisposed()
        {
            if (disposedValue == true) throw new ObjectDisposedException(this.GetType().Name);
        }

        private bool disposedValue = false; // To detect redundant calls

        public FcmListener()
        {
            connection = new FcmConnection();
        }

        internal FcmListener(IFcmConnection connection)
        {
            this.connection = connection;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    cts.Dispose();

                    dataStream.Dispose();

                    state = ProcessingState.MCS_VERSION_TAG_AND_SIZE;

                    if (connection != null)
                    {
                        connection.DataReceived -= Connection_DataReceived;
                        connection.Dispose();
                        connection = null;
                    }
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
