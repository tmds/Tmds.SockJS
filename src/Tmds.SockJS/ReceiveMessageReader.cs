// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal class ReceiveMessageReader
    {
        private Stream body;

        public ReceiveMessageReader(Stream body)
        {
            this.body = body;
        }

        public async Task<List<JsonString>> ReadMessages(bool ignoreEmpty)
        {
            var messages = new List<JsonString> ();
            using (var memoryStream = new MemoryStream())
            {
                await body.CopyToAsync(memoryStream);
#if DNXCORE50
                ArraySegment<byte> segment;
                memoryStream.TryGetBuffer(out segment);
                var buffer = segment.Array;
#else
                var buffer = memoryStream.GetBuffer();
#endif
                if ((buffer.Length == 0) && (!ignoreEmpty))
                {
                    throw new Exception("Payload expected.");
                }
                bool inString = false;
                int startIndex = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    byte b = buffer[i];
                    if (!inString && b == (byte)'\"')
                    {
                        inString = true;
                        startIndex = i + 1;
                    }
                    else if (inString && b == (byte)'\\')
                    {
                        i++; // next character is escaped, ignore it
                    }
                    else if (inString && b == (byte)'\"')
                    {
                        inString = false;
                        messages.Add(new JsonString(buffer, startIndex, i));
                    }
                }
                if (inString)
                {
                    throw new Exception("Broken JSON encoding.");
                }
            }
            return messages;
        }
    }
}