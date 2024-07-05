using System;
using System.Security.Cryptography;
using System.Text;
namespace MCForgeUpdater
{
    public abstract class BaseWebSocket : INetSocket, INetProtocol
    {
        public bool conn, upgrade;
        public bool readingHeaders = true;
        public static string ComputeKey(string rawKey)
        {
            string key = rawKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            SHA1 sha = SHA1.Create();
            byte[] raw = sha.ComputeHash(Encoding.ASCII.GetBytes(key));
            return Convert.ToBase64String(raw);
        }
        public abstract void OnGotAllHeaders();
        public abstract void OnGotHeader(string name, string value);
        public void ProcessHeader(string raw)
        {
            if (raw.Length == 0) OnGotAllHeaders();
            int sep = raw.IndexOf(':');
            if (sep == -1) return;
            string name = raw.Substring(0, sep);
            string value = raw.Substring(sep + 1).Trim();
            if (name.CaselessEq("Connection"))
            {
                conn = value.CaselessContains("Upgrade");
            }
            else if (name.CaselessEq("Upgrade"))
            {
                upgrade = value.CaselessEq("websocket");
            }
            else
            {
                OnGotHeader(name, value);
            }
        }
        public int ReadHeaders(byte[] buffer, int bufferLen)
        {
            int i;
            for (i = 0; i < bufferLen - 1;)
            {
                int end = -1;
                for (int j = i; j < bufferLen - 1; j++)
                {
                    if (buffer[j] != '\r' || buffer[j + 1] != '\n') continue;
                    end = j; break;
                }
                if (end == -1) break;
                string value = Encoding.ASCII.GetString(buffer, i, end - i);
                ProcessHeader(value);
                i = end + 2;
            }
            return i;
        }
        public int state, opcode, frameLen, maskRead, frameRead;
        public byte[] mask = new byte[4], frame;
        public const int state_header1 = 0;
        public const int state_header2 = 1;
        public const int state_extLen1 = 2;
        public const int state_extLen2 = 3;
        public const int state_mask = 4;
        public const int state_data = 5;
        public const int OPCODE_CONTINUED = 0;
        public const int OPCODE_TEXT = 1;
        public const int OPCODE_BINARY = 2;
        public const int OPCODE_DISCONNECT = 8;
        public const int FIN = 0x80;
        public const int REASON_NORMAL = 1000;
        public const int REASON_INVALID_DATA = 1003;
        public const int REASON_EXCESSIVE_SIZE = 1009;
        public int GetDisconnectReason()
        {
            if (frameLen < 2) return REASON_NORMAL;
            return (frame[0] << 8) | frame[1];
        }
        public void DecodeFrame()
        {
            for (int i = 0; i < frameLen; i++)
            {
                frame[i] ^= mask[i & 3];
            }
            switch (opcode)
            {
                case OPCODE_CONTINUED:
                case OPCODE_BINARY:
                case OPCODE_TEXT:
                    if (frameLen == 0) return;
                    HandleData(frame, frameLen);
                    break;
                case OPCODE_DISCONNECT:
                    Disconnect(GetDisconnectReason()); break;
                default:
                    Disconnect(REASON_INVALID_DATA); break;
            }
        }
        public int ProcessData(byte[] data, int offset, int len)
        {
            switch (state)
            {
                case state_header1:
                    if (offset >= len) break;
                    opcode = data[offset++] & 0x0F;
                    state = state_header2;
                    goto case state_header2;
                case state_header2:
                    if (offset >= len) break;
                    int flags = data[offset] & 0x7F;
                    maskRead = 0x80 - (data[offset] & 0x80);
                    offset++;
                    if (flags == 127)
                    {
                        Disconnect(REASON_EXCESSIVE_SIZE);
                        return len;
                    }
                    else if (flags == 126)
                    {
                        state = state_extLen1;
                        goto case state_extLen1;
                    }
                    else
                    {
                        frameLen = flags;
                        state = state_mask;
                        goto case state_mask;
                    }
                case state_extLen1:
                    if (offset >= len) break;
                    frameLen = data[offset++] << 8;
                    state = state_extLen2;
                    goto case state_extLen2;

                case state_extLen2:
                    if (offset >= len) break;
                    frameLen |= data[offset++];
                    state = state_mask;
                    goto case state_mask;
                case state_mask:
                    for (; maskRead < 4; maskRead++)
                    {
                        if (offset >= len) return offset;
                        mask[maskRead] = data[offset++];
                    }
                    maskRead = 0;
                    state = state_data;
                    goto case state_data;
                case state_data:
                    if (frame == null || frameLen > frame.Length) frame = new byte[frameLen];
                    int copy = Math.Min(len - offset, frameLen - frameRead);

                    Buffer.BlockCopy(data, offset, frame, frameRead, copy);
                    offset += copy; frameRead += copy;

                    if (frameRead == frameLen)
                    {
                        DecodeFrame();
                        frameRead = 0;
                        state = state_header1;
                    }
                    break;
            }
            return offset;
        }
        int INetProtocol.ProcessReceived(byte[] buffer, int bufferLen)
        {
            int offset = 0;
            if (readingHeaders)
            {
                offset = ReadHeaders(buffer, bufferLen);
                if (readingHeaders) return offset;
            }

            while (offset < bufferLen)
            {
                offset = ProcessData(buffer, offset, bufferLen);
            }
            return offset;
        }
        public static byte[] WrapDisconnect(int reason)
        {
            byte[] packet = new byte[4];
            packet[0] = OPCODE_DISCONNECT | FIN;
            packet[1] = 2;
            packet[2] = (byte)(reason >> 8);
            packet[3] = (byte)reason;
            return packet;
        }
        public void Disconnect() { Disconnect(REASON_NORMAL); }
        public void Disconnect(int reason)
        {
            try
            {
                SendRaw(WrapDisconnect(reason), SendFlags.Synchronous);
            }
            catch
            {
            }
            OnDisconnected(reason);
        }
        public abstract void OnDisconnected(int reason);
        public abstract void HandleData(byte[] data, int len);
        public abstract void SendRaw(byte[] data, SendFlags flags);
    }
    public abstract class ServerWebSocket : BaseWebSocket
    {
        bool version;
        string verKey;
        void AcceptConnection()
        {
            const string fmt =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: {0}\r\n" +
                "Sec-WebSocket-Protocol: ClassiCube\r\n" +
                "\r\n";
            string key = ComputeKey(verKey);
            string headers = string.Format(fmt, key);
            SendRaw(Encoding.ASCII.GetBytes(headers), SendFlags.None);
            readingHeaders = false;
        }
        public override void OnGotAllHeaders()
        {
            if (conn && upgrade && version && verKey != null)
            {
                AcceptConnection();
            }
            else
            {
                Close();
            }
        }
        public override void OnGotHeader(string name, string value)
        {
            if (name.CaselessEq("Sec-WebSocket-Version"))
            {
                version = value.CaselessEq("13");
            }
            else if (name.CaselessEq("Sec-WebSocket-Key"))
            {
                verKey = value;
            }
        }
        public static byte[] WrapData(byte[] data)
        {
            int headerLen = data.Length >= 126 ? 4 : 2;
            byte[] packet = new byte[headerLen + data.Length];
            packet[0] = OPCODE_BINARY | FIN;

            if (headerLen > 2)
            {
                packet[1] = 126;
                packet[2] = (byte)(data.Length >> 8);
                packet[3] = (byte)data.Length;
            }
            else
            {
                packet[1] = (byte)data.Length;
            }
            Buffer.BlockCopy(data, 0, packet, headerLen, data.Length);
            return packet;
        }
    }
    public abstract class ClientWebSocket : BaseWebSocket
    {
        public string path = "/";
        string verKey;
        const string key = "xTNDiuZRoMKtxrnJDWyLmA==";
        void AcceptConnection()
        {
            readingHeaders = false;
        }
        public override void OnGotAllHeaders()
        {
            if (conn && upgrade && verKey == ComputeKey(key))
            {
                AcceptConnection();
            }
            else
            {
                Close();
            }
        }
        public override void OnGotHeader(string name, string value)
        {
            if (name.CaselessEq("Sec-WebSocket-Accept"))
            {
                verKey = value;
            }
        }
        public static byte[] WrapData(byte[] data)
        {
            int headerLen = data.Length >= 126 ? 4 : 2;
            byte[] packet = new byte[headerLen + 4 + data.Length];
            packet[0] = OPCODE_TEXT | FIN;

            if (headerLen > 2)
            {
                packet[1] = 126;
                packet[2] = (byte)(data.Length >> 8);
                packet[3] = (byte)data.Length;
            }
            else
            {
                packet[1] = (byte)data.Length;
            }
            packet[1] |= 0x80;
            Buffer.BlockCopy(data, 0, packet, headerLen + 4, data.Length);
            return packet;
        }
        public override void Send(byte[] buffer, SendFlags flags)
        {
            SendRaw(WrapData(buffer), flags);
        }
        public void WriteHeader(string header)
        {
            SendRaw(Encoding.ASCII.GetBytes(header + "\r\n"), SendFlags.None);
        }
        public virtual void WriteCustomHeaders() { }
        public override void Init()
        {
            WriteHeader("GET " + path + " HTTP/1.1");
            WriteHeader("Upgrade: websocket");
            WriteHeader("Connection: Upgrade");
            WriteHeader("Sec-WebSocket-Version: 13");
            WriteHeader("Sec-WebSocket-Key: " + key);
            WriteCustomHeaders();
            WriteHeader("");
        }
    }
}