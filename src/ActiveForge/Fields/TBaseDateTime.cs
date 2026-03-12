using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class for all date and time field types
    /// (<see cref="TDate"/>, <see cref="TDateTime"/>, and their UTC/Local variants).
    /// <para>
    /// Wraps a CLR <see cref="DateTime"/> value with the standard
    /// <see cref="TField"/> null-tracking semantics.  Concrete subclasses choose
    /// which portion of the <see cref="DateTime"/> is significant (date-only, full
    /// datetime, UTC, local) and supply the appropriate
    /// <see cref="TField.GetTypeDescription"/> and
    /// <see cref="TField.GetUnderlyingType"/> implementations.
    /// </para>
    /// <para>
    /// When the field is in the null state, <c>InnerValue</c> is
    /// <see cref="DateTime.MinValue"/>.  Callers should always check
    /// <see cref="TField.IsNull()"/> before interpreting the value.
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class TBaseDateTime : TField, IComparable
    {
        /// <summary>
        /// Backing store for the <see cref="DateTime"/> value.  Defaults to
        /// <see cref="DateTime.MinValue"/> when the field is in the null state.
        /// </summary>
        protected DateTime InnerValue;

        /// <summary>
        /// Gets or sets the underlying <see cref="DateTime"/> value.  The getter calls
        /// <see cref="TField.CheckValidity"/> and throws
        /// <see cref="PersistenceException"/> if the field is null.
        /// External callers should use the concrete subclass's implicit cast operator
        /// or <see cref="GetValue()"/> instead.
        /// </summary>
        protected DateTime Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        /// <summary>
        /// Initialises a new date/time field in the null state with
        /// <c>InnerValue</c> set to <see cref="DateTime.MinValue"/>.
        /// </summary>
        protected TBaseDateTime() { InnerValue = DateTime.MinValue; }

        /// <summary>
        /// Returns the underlying <see cref="DateTime"/> value boxed as an
        /// <see cref="object"/>.  When the field is null, returns
        /// <see cref="DateTime.MinValue"/>.
        /// </summary>
        public override object GetValue() => InnerValue;

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/>.  Accepts another
        /// <see cref="TBaseDateTime"/> subclass (copies its inner value directly),
        /// a plain <see cref="DateTime"/>, or any object that
        /// <see cref="Convert.ToDateTime(object)"/> can coerce.
        /// <para>
        /// Concrete subclasses (such as <see cref="TDate"/>) may override this method
        /// to strip or normalise portions of the value (e.g. removing the time component).
        /// </para>
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TBaseDateTime tb) InnerValue = tb.InnerValue;
            else if (value is DateTime dt) InnerValue = dt;
            else                           InnerValue = Convert.ToDateTime(value);
        }

        /// <summary>
        /// Sets the field to <paramref name="value"/> and clears any previously recorded
        /// conversion error.
        /// </summary>
        /// <param name="value">The <see cref="DateTime"/> value to store.</param>
        public void SetValue(DateTime value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Sets the null state of the field.  When <paramref name="isNull"/> is
        /// <c>true</c>, <c>InnerValue</c> is reset to <see cref="DateTime.MinValue"/>.
        /// </summary>
        /// <param name="isNull"><c>true</c> to null the field; <c>false</c> to un-null it.</param>
        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = DateTime.MinValue;
        }

        /// <summary>
        /// Returns the default string representation of <c>InnerValue</c> using the
        /// current thread's culture.  Null-state fields return the string
        /// representation of <see cref="DateTime.MinValue"/>.
        /// </summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this field with <paramref name="obj"/> using chronological ordering.
        /// <para>
        /// When comparing two <see cref="TBaseDateTime"/> instances:
        /// null-state sorts before non-null (null is considered the earliest possible
        /// date).  Two null-state fields compare as equal.
        /// </para>
        /// When comparing with a non-<see cref="TBaseDateTime"/> value, delegates to
        /// <see cref="DateTime.CompareTo(object)"/> after converting via
        /// <see cref="Convert.ToDateTime(object)"/>.
        /// </summary>
        /// <param name="obj">The value to compare against.</param>
        /// <returns>
        /// A negative integer, zero, or a positive integer indicating whether this
        /// field is earlier than, equal to, or later than <paramref name="obj"/>.
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj is TBaseDateTime other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return InnerValue.CompareTo(other.InnerValue);
            }
            return InnerValue.CompareTo(Convert.ToDateTime(obj));
        }

        /// <summary>
        /// Returns the underlying <see cref="DateTime"/> value.  Equivalent to an
        /// explicit cast; useful when the caller has a reference typed as
        /// <see cref="TBaseDateTime"/> rather than a concrete subclass.
        /// </summary>
        /// <returns>The wrapped <see cref="DateTime"/> value.</returns>
        public DateTime ToDateTime() => InnerValue;

        /// <summary>
        /// Gets the year component of the stored <see cref="DateTime"/> value.
        /// Returns the year from <see cref="DateTime.MinValue"/> when the field is null.
        /// </summary>
        public int Year  => InnerValue.Year;

        /// <summary>
        /// Gets the month component (1–12) of the stored <see cref="DateTime"/> value.
        /// Returns the month from <see cref="DateTime.MinValue"/> when the field is null.
        /// </summary>
        public int Month => InnerValue.Month;

        /// <summary>
        /// Gets the day-of-month component (1–31) of the stored <see cref="DateTime"/> value.
        /// Returns the day from <see cref="DateTime.MinValue"/> when the field is null.
        /// </summary>
        public int Day   => InnerValue.Day;
    }
}
