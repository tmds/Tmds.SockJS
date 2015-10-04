using System.Collections.Generic;

namespace Tmds.SockJS.Tests
{
    internal class Info
    {
        public bool websocket { get; set; }
        public bool cookie_needed { get; set; }
        public IList<string> origins { get; set; }
        public int entropy { get; set; }
    }
}
