using System;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps a CLR <see cref="string"/> intended to hold HTML markup,
    /// mapping to a TEXT / VARCHAR(MAX) (or equivalent) database column.
    /// <para>
    /// Identical in storage and behaviour to <see cref="TString"/> but carries a distinct
    /// type description (<c>"htmlstring"</c>) so that the ORM and UI layers can apply
    /// HTML-aware rendering or validation rather than treating the value as plain text.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the <c>string</c> implicit conversion
    /// inherited from <see cref="TString"/> to assign a value, and <see cref="TField.GetValue()"/>
    /// or implicit cast to <c>string</c> to read it back.
    /// </para>
    /// </summary>
    [Serializable]
    public class THtmlString : TString
    {
        /// <summary>Initialises a new, empty <see cref="THtmlString"/>.</summary>
        public THtmlString() : base() { }

        /// <summary>Initialises a new <see cref="THtmlString"/> with the specified HTML <paramref name="value"/>.</summary>
        /// <param name="value">The initial HTML string content.</param>
        public THtmlString(string value) : base(value) { }

        /// <summary>Returns the string token <c>"htmlstring"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription() => "htmlstring";
    }
}
