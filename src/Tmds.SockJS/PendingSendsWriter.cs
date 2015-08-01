// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tmds.SockJS
{
    class PendingSendsWriter
    {
        private static readonly byte[] MessageStart;
        private static readonly byte[] MessageContinue;
        private static readonly byte[] MessageEnd;
        private const byte ReverseSolidusByte = (byte)'\\';
        private const byte QuotationMarkByte = (byte)'\"';
        private const byte LastControlByte = 0x1f;
        private static readonly byte[] Escape;
        private static readonly byte[][] ControlEscape;

        static PendingSendsWriter()
        {
            MessageStart = Encoding.UTF8.GetBytes("a[\"");
            MessageContinue = Encoding.UTF8.GetBytes("\",\"");
            MessageEnd = Encoding.UTF8.GetBytes("\"]\n");
            Escape = new[] { ReverseSolidusByte };
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
        }

        private MemoryStream _stream;

        public PendingSendsWriter()
        {
            _stream = new MemoryStream();
        }

        public ArraySegment<byte> Buffer
        {
            get
            {
#if DNXCORE50
                ArraySegment<byte> segment;
                _stream.TryGetBuffer(out segment);
                return segment;
#else
                var array = _stream.GetBuffer();
                return new ArraySegment<byte>(array, 0, (int)_stream.Length);
#endif
            }
        }

        public void WriteMessages(IList<PendingSend> sends)
        {
            for (int j = 0; j < sends.Count; j++)
            {
                var send = sends[j];
                if (j == 0)
                {
                    _stream.Write(MessageStart, 0, MessageStart.Length);
                }

                int copyOffset = send.Buffer.Offset;
                int startOffset = send.Buffer.Offset;
                int endOffset = send.Buffer.Offset + send.Buffer.Count;
                int copyCount = 0;
                for (int offset = startOffset; offset <= endOffset;)
                {
                    bool end = (offset == endOffset);
                    bool escape = false;
                    byte b = 0;
                    if (!end)
                    {
                        b = send.Buffer.Array[offset++];
                        escape = (b == QuotationMarkByte) || (b == ReverseSolidusByte) || (b <= LastControlByte);
                    }
                    if (end || escape)
                    {
                        _stream.Write(send.Buffer.Array, copyOffset, copyCount);
                        if (end)
                        {
                            break;
                        }
                        if (escape)
                        {
                            if (b <= LastControlByte)
                            {
                                _stream.Write(ControlEscape[b], 0, ControlEscape[b].Length);
                                copyOffset = offset;
                                copyCount = 0;
                            }
                            else
                            {
                                _stream.Write(Escape, 0, Escape.Length);
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

                if (j == (sends.Count - 1))
                {
                    _stream.Write(MessageEnd, 0, MessageEnd.Length);
                }
                else
                {
                    _stream.Write(MessageContinue, 0, MessageContinue.Length);
                }
            }
        }
    }
}
