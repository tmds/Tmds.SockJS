// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;

namespace Tmds.SockJS
{
    class JsonString
    {
        private byte[] _array;
        private int _offset;
        private int _endOffset;

        public JsonString(byte[] buffer, int startIndex, int endIndex)
        {
            _array = buffer;
            _offset = startIndex;
            _endOffset = endIndex;
        }

        public int Decode(ArraySegment<byte> destination)
        {
            int readOffset = _offset;
            int writeOffset = destination.Offset;
            int writEndOffset = writeOffset + destination.Count;
            while ((readOffset < _endOffset) && (writeOffset < writEndOffset))
            {
                byte b = _array[readOffset++];
                bool escape = b == (byte)'\\';
                if (escape)
                {
                    int remaining = _endOffset - readOffset;
                    if (remaining < 1)
                    {
                        throw new ArgumentException("Json string cannot terminate with escape character ('\\')");
                    }
                    b = _array[readOffset++];
                    switch (b)
                    {
                        case (byte)'b': b = (byte)'\b'; break;
                        case (byte)'f': b = (byte)'\f'; break;
                        case (byte)'n': b = (byte)'\n'; break;
                        case (byte)'r': b = (byte)'\r'; break;
                        case (byte)'t': b = (byte)'\t'; break;
                        case (byte)'u':
                            {
                                remaining = _endOffset - readOffset;
                                if (remaining < 4)
                                {
                                    throw new ArgumentException("Json string \\u escape sequence must be followed by 4 hexadecimal digits.");
                                }
                                int b1 = readHex(_array[readOffset++]);
                                int b2 = readHex(_array[readOffset++]);
                                int b3 = readHex(_array[readOffset++]);
                                int b4 = readHex(_array[readOffset++]);
                                b = (byte)((b1 << 4) + b2);
                                if ((b & 0xfc) == 0xd8)
                                {
                                    int _a = 0;
                                    int _b = 0;
                                    int _c = 0;
                                    remaining = _endOffset - readOffset;
                                    if ((remaining < 6) ||
                                        (_array[readOffset++] != (byte)'\\') ||
                                        (_array[readOffset++] != (byte)'u'))
                                    {
                                        throw new ArgumentException("Json string \\ud8-db escape sequence must be followed by another escape sequence.");
                                    }
                                    int b5 = readHex(_array[readOffset++]);
                                    int b6 = readHex(_array[readOffset++]);
                                    b = (byte)((b5 << 4) + b6);
                                    if ((b & 0xfc) != 0xdc)
                                    {
                                        throw new ArgumentException("Json string \\ud8-db escape sequence must be followed by \\udc-df escape sequence.");
                                    }
                                    int b7 = readHex(_array[readOffset++]);
                                    int b8 = readHex(_array[readOffset++]);
                                    _c = 1 + ((b2 & 0x3) << 2) + (b3 >> 2);
                                    _b = ((b3 & 0x3) << 6) + (b4 << 2) + (b6 & 0x3);
                                    _a = (byte)((b7 << 4) + b8);

                                    destination.Array[writeOffset++] = (byte)(0xf0 + (_c >> 2));
                                    destination.Array[writeOffset++] = (byte)(0x80 + ((_c & 0x3) << 4) + ((_b & 0xf0) >> 4));
                                    destination.Array[writeOffset++] = (byte)(0x80 + ((_b & 0xf) << 2) + ((_a & 0xc0) >> 6));
                                    b = (byte)(0x80 + (_a & 0x3f));
                                }
                                else
                                {
                                    int u = (b << 8) + ((b3 << 4) + b4);
                                    if (u < 0x80)
                                    {
                                        b = (byte)u;
                                    }
                                    else if (u < 0x800)
                                    {
                                        destination.Array[writeOffset++] = (byte)(0xc0 + (u >> 6));
                                        b = (byte)(0x80 + (u & 0x3f));
                                    }
                                    else
                                    {
                                        destination.Array[writeOffset++] = (byte)(0xe0 + (u >> 12));
                                        destination.Array[writeOffset++] = (byte)(0x80 + ((u & 0xfff) >> 6));
                                        b = (byte)(0x80 + (u & 0x3f));
                                    }
                                }
                            }
                            break;
                        case (byte)'\"':
                        case (byte)'\\':
                        case (byte)'/':
                            break;
                        default:
                            throw new ArgumentException(string.Format("Json string escape sequence cannot start with 0x{0:x2}", (int)b));
                    }
                }
                destination.Array[writeOffset++] = b;
            };
            _offset = readOffset;
            return writeOffset - destination.Offset;
        }

        private int readHex(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                return b - (byte)'0';
            }
            if (b >= (byte)'a' && b <= (byte)'f')
            {
                return 10 + b - (byte)'a';
            }
            if (b >= (byte)'A' && b <= (byte)'F')
            {
                return 10 + b - (byte)'A';
            }
            throw new ArgumentException(string.Format("Json string unicode escape sequence contains invalid hexadecimal character 0x{0:x2}", (int)b));
        }

        public bool IsEmpty
        {
            get
            {
                return _offset >= _endOffset;
            }
        }
    }
}
