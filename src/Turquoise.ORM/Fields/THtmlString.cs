using System;

namespace Turquoise.ORM
{
    /// <summary>String field intended to hold HTML content.</summary>
    [Serializable]
    public class THtmlString : TString
    {
        public THtmlString() : base() { }
        public THtmlString(string value) : base(value) { }

        public override string GetTypeDescription() => "htmlstring";
    }
}
