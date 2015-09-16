// Copyright (C) 2015 Tom Deseyn. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal enum ReceiverType
    {
        XhrPolling,
        XhrStreaming,
        HtmlFile
    }
}
