using System;
using System.Diagnostics;
using System.Net;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TIpAddress : TString
    {
        public TIpAddress() : base() { }
        public TIpAddress(string v) : base(v) { }
        public TIpAddress(IPAddress v) : base(v?.ToString()) { }

        public static implicit operator IPAddress(TIpAddress t)
            => t.IsNull() ? null : IPAddress.Parse(t.InnerValue);
        public static implicit operator TIpAddress(IPAddress v)
            => v == null ? new TIpAddress() : new TIpAddress(v.ToString());

        public IPAddress ToIPAddress()
            => IsNull() ? null : IPAddress.TryParse(InnerValue, out var ip) ? ip : null;

        public override string GetTypeDescription() => "ipaddress";
    }
}
