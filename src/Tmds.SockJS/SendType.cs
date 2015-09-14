// Copyright (C) 2015 Tom Deseyn
// Licensed under GNU LGPL, Version 2.1. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tmds.SockJS
{
    internal enum RawSendType
    {
        Header,
        Close,
        Other
    }
}
