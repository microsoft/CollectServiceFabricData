// Licensed under the MIT License.

using System;

namespace CollectSFData.Common
{
    /// <summary>
    /// Provides time-related operations for dependency injection and testing.
    /// Abstracts DateTime.Now, DateTime.UtcNow, and time parsing to enable deterministic testing.
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>
        /// Gets the current date and time in local time zone.
        /// </summary>
        DateTime Now { get; }

        /// <summary>
        /// Gets the current date and time in UTC.
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Attempts to parse a string representation of a date and time to UTC.
        /// </summary>
        /// <param name="input">The string to parse</param>
        /// <returns>Parsed DateTime in UTC, or DateTime.MinValue if parsing fails</returns>
        DateTime ConvertToUtcTime(string input);
    }

    /// <summary>
    /// Default implementation that uses system time.
    /// </summary>
    public class SystemTimeProvider : ITimeProvider
    {
        /// <inheritdoc />
        public DateTime Now => DateTime.Now;

        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;

        /// <inheritdoc />
        public DateTime ConvertToUtcTime(string input)
        {
            try
            {
                if (DateTime.TryParse(input, out DateTime result))
                {
                    return result.Kind == DateTimeKind.Utc ? result : result.ToUniversalTime();
                }
            }
            catch
            {
                // Swallow parsing exceptions, return MinValue to indicate failure
            }

            return DateTime.MinValue;
        }
    }
}