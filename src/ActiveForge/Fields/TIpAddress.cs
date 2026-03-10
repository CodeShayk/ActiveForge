using System;
using System.Diagnostics;
using System.Net;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that stores a network IP address (IPv4 or IPv6) as a <see cref="string"/>
    /// in the database, mapping to a VARCHAR / TEXT column.
    /// <para>
    /// Internally the value is kept as the dotted-decimal (or colon-hex) string form returned
    /// by <see cref="IPAddress.ToString()"/>.  The implicit conversions and
    /// <see cref="ToIPAddress()"/> helper parse that string back to a
    /// <see cref="System.Net.IPAddress"/> on demand.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/>, an <see cref="IPAddress"/> implicit conversion,
    /// or a <c>string</c> assignment (inherited from <see cref="TString"/>) to set the value.
    /// Read it back via <see cref="ToIPAddress()"/> or the implicit cast to <see cref="IPAddress"/>.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TIpAddress : TString
    {
        /// <summary>Initialises a new, null <see cref="TIpAddress"/>.</summary>
        public TIpAddress() : base() { }

        /// <summary>
        /// Initialises a new <see cref="TIpAddress"/> from a string representation of an IP address.
        /// </summary>
        /// <param name="v">The IP address in dotted-decimal or colon-hex string form.</param>
        public TIpAddress(string v) : base(v) { }

        /// <summary>
        /// Initialises a new <see cref="TIpAddress"/> from a <see cref="System.Net.IPAddress"/>.
        /// The address is stored as its canonical string representation.
        /// </summary>
        /// <param name="v">The IP address to store, or <see langword="null"/> to create a null field.</param>
        public TIpAddress(IPAddress v) : base(v?.ToString()) { }

        /// <summary>
        /// Implicitly converts a <see cref="TIpAddress"/> to a <see cref="System.Net.IPAddress"/>
        /// by parsing the stored string.  Returns <see langword="null"/> when the field is null.
        /// </summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator IPAddress(TIpAddress t)
            => t.IsNull() ? null : IPAddress.Parse(t.InnerValue);

        /// <summary>
        /// Implicitly wraps a <see cref="System.Net.IPAddress"/> in a new <see cref="TIpAddress"/>.
        /// Passing <see langword="null"/> produces a null field.
        /// </summary>
        /// <param name="v">The IP address to wrap, or <see langword="null"/>.</param>
        public static implicit operator TIpAddress(IPAddress v)
            => v == null ? new TIpAddress() : new TIpAddress(v.ToString());

        /// <summary>
        /// Parses the stored string and returns the corresponding <see cref="System.Net.IPAddress"/>.
        /// Returns <see langword="null"/> when the field is null or the string is not a valid IP address.
        /// </summary>
        public IPAddress ToIPAddress()
            => IsNull() ? null : IPAddress.TryParse(InnerValue, out var ip) ? ip : null;

        /// <summary>Returns the string token <c>"ipaddress"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription() => "ipaddress";
    }
}
