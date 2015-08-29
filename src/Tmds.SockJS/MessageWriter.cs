using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    class MessageWriter
    {
        private const byte ReverseSolidusByte = (byte)'\\';
        private const byte QuotationMarkByte = (byte)'\"';
        private const byte LastControlByte = 0x1f;
        private static readonly byte[][] ControlEscape;
        private static readonly byte[] HtmlFileSendMessagesStart;
        private static readonly byte[] HtmlFileSendMessagesEnd;

        static MessageWriter()
        {
            ControlEscape = new byte[LastControlByte + 1][];
            ControlEscape[(int)'\b'] = Encoding.UTF8.GetBytes("\\b");
            ControlEscape[(int)'\f'] = Encoding.UTF8.GetBytes("\\f");
            ControlEscape[(int)'\n'] = Encoding.UTF8.GetBytes("\\n");
            ControlEscape[(int)'\r'] = Encoding.UTF8.GetBytes("\\r");
            ControlEscape[(int)'\t'] = Encoding.UTF8.GetBytes("\\t");
            for (int i = 0; i <= LastControlByte; i++)
            {
                if (ControlEscape[i] == null)
                {
                    ControlEscape[i] = Encoding.UTF8.GetBytes(string.Format("\\u00{0:x2}", i));
                }
            }
            HtmlFileSendMessagesStart = Encoding.UTF8.GetBytes("<script>\np(");
            HtmlFileSendMessagesEnd = Encoding.UTF8.GetBytes(");\n</script>\r\n");
        }

        private MemoryStream _ms;

        private MessageWriter()
        {
            _ms = new MemoryStream();
        }
        private void WriteInt(int i)
        {
            var bytes = Encoding.UTF8.GetBytes(i.ToString());
            _ms.Write(bytes, 0, bytes.Length);
        }
        private void WriteArrayStart()
        {
            _ms.WriteByte((byte)'[');
        }
        private void WriteArrayNext()
        {
            _ms.WriteByte((byte)',');
        }
        private void WriteArrayEnd()
        {
            _ms.WriteByte((byte)']');
        }
        private void WriteNewline()
        {
            _ms.WriteByte((byte)'\n');
        }
        private void WriteChar(char c)
        {
            _ms.WriteByte((byte)c);
        }
        private void WriteJsonString(ArraySegment<byte> segment)
        {
            _ms.WriteByte(QuotationMarkByte);
            int copyOffset = segment.Offset;
            int startOffset = segment.Offset;
            int endOffset = segment.Offset + segment.Count;
            int copyCount = 0;
            for (int offset = startOffset; offset <= endOffset;)
            {
                bool end = (offset == endOffset);
                bool escape = false;
                byte b = 0;
                if (!end)
                {
                    b = segment.Array[offset++];
                    escape = (b == QuotationMarkByte) || (b == ReverseSolidusByte) || (b <= LastControlByte);
                }
                if (end || escape)
                {
                    _ms.Write(segment.Array, copyOffset, copyCount);
                    if (end)
                    {
                        break;
                    }
                    if (escape)
                    {
                        if (b <= LastControlByte)
                        {
                            _ms.Write(ControlEscape[b], 0, ControlEscape[b].Length);
                            copyOffset = offset;
                            copyCount = 0;
                        }
                        else
                        {
                            _ms.WriteByte(ReverseSolidusByte);
                            copyOffset = offset - 1;
                            copyCount = 1;
                        }
                    }
                }
                else
                {
                    copyCount++;
                }
            }
            _ms.WriteByte(QuotationMarkByte);
        }
        private void WriteCloseMessage(WebSocketCloseStatus status, string description)
        {
            WriteChar('c');
            WriteArrayStart();
            WriteInt((int)status);
            WriteArrayNext();
            WriteJsonString(new ArraySegment<byte>(Encoding.UTF8.GetBytes(description)));
            WriteArrayEnd();
            WriteNewline();
        }
        private void WriteSends(List<PendingSend> sends)
        {
            WriteChar('a');
            WriteArrayStart();
            for (int j = 0; j < sends.Count; j++)
            {
                var send = sends[j];
                WriteJsonString(send.Buffer);
                if (j != (sends.Count - 1))
                {
                    WriteArrayNext();
                }
            }
            WriteArrayEnd();
            WriteNewline();
        }
        private void WriteSockJSWebSocketSend(ArraySegment<byte> buffer)
        {
            WriteChar('a');
            WriteArrayStart();
            WriteJsonString(buffer);
            WriteArrayEnd();
        }
        private void WriteBytes(byte[] bytes)
        {
            _ms.Write(bytes, 0, bytes.Length);
        }
        private byte[] ToArray()
        {
            return _ms.ToArray();
        }
        private ArraySegment<byte> GetSegment()
        {
#if DNXCORE50
            ArraySegment<byte> segment;
            _ms.TryGetBuffer(out segment);
            return segment;
#else
            return new ArraySegment<byte>(_ms.GetBuffer(), 0, (int)_ms.Length);
#endif
        }

        public static ArraySegment<byte> CreateSockJSWebSocketSendMessage(ArraySegment<byte> buffer)
        {
            MessageWriter writer = new MessageWriter();
            writer.WriteSockJSWebSocketSend(buffer);
            return writer.GetSegment();
        }

        public static ArraySegment<byte> CreateSendsMessage(ReceiverType type, List<PendingSend> sends)
        {
            MessageWriter writer = new MessageWriter();
            writer.WriteSends(sends);
            ArraySegment<byte> sendsSegment = writer.GetSegment();

            if (type != ReceiverType.HtmlFile)
            {
                return sendsSegment;
            }
            else
            {
                MessageWriter htmlFileWriter = new MessageWriter();
                htmlFileWriter.WriteBytes(HtmlFileSendMessagesStart);
                htmlFileWriter.WriteJsonString(new ArraySegment<byte>(sendsSegment.Array, sendsSegment.Offset, sendsSegment.Count - 1));
                htmlFileWriter.WriteBytes(HtmlFileSendMessagesEnd);
                return htmlFileWriter.GetSegment();
            }
        }

        public static ArraySegment<byte> CreateCloseMessage(ReceiverType type, byte[] closeArray)
        {
            return new ArraySegment<byte>(closeArray);
        }

        public static byte[] CreateCloseBuffer(WebSocketCloseStatus status, string description)
        {
            MessageWriter writer = new MessageWriter();
            writer.WriteCloseMessage(status, description);
            return writer.ToArray();
        }
    }
}
