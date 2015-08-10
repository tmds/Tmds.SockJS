using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tmds.SockJS.Tests
{
    class Info
    {
        public bool websocket { get; set; }
        public bool cookie_needed { get; set; }
        public IList<string> origins { get; set; }
        public int entropy { get; set; }
    }
}
