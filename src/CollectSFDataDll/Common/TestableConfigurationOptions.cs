// Licensed under the MIT License.

using System;

namespace CollectSFData.Common
{
    /// <summary>
    /// Test-friendly wrapper for ConfigurationOptions that supports dependency injection.
    /// Maintains backward compatibility while enabling mockable dependencies for testing.
    /// </summary>
    public class TestableConfigurationOptions : ConfigurationOptions
    {
        private readonly ITimeProvider _timeProvider;
        private readonly IFileSystemAbstraction _fileSystem;

        /// <summary>
        /// Initializes a new instance with injected dependencies.
        /// </summary>
        /// <param name="timeProvider">Time provider for date/time operations</param>
        /// <param name="fileSystem">File system abstraction for file operations</param>
        public TestableConfigurationOptions(
            ITimeProvider timeProvider = null,
            IFileSystemAbstraction fileSystem = null)
        {
            _timeProvider = timeProvider ?? new SystemTimeProvider();
            _fileSystem = fileSystem ?? new FileSystemAbstraction();
        }

        /// <summary>
        /// Override ConvertToUtcTime to use injected time provider.
        /// Falls back to base implementation if called during construction before dependencies are set.
        /// </summary>
        /// <param name="input">The string to parse</param>
        /// <returns>Parsed DateTime in UTC, or DateTime.MinValue if parsing fails</returns>
        public override DateTime ConvertToUtcTime(string input)
        {
            // During base constructor execution, _timeProvider may not be set yet
            if (_timeProvider == null)
            {
                return base.ConvertToUtcTime(input);
            }
            
            return _timeProvider.ConvertToUtcTime(input);
        }

        /// <summary>
        /// Gets the current time from the injected time provider.
        /// Falls back to system time if called during construction before dependencies are set.
        /// </summary>
        public DateTime CurrentTime => _timeProvider?.Now ?? DateTime.Now;

        /// <summary>
        /// Gets the current UTC time from the injected time provider.
        /// Falls back to system UTC time if called during construction before dependencies are set.
        /// </summary>
        public DateTime CurrentUtcTime => _timeProvider?.UtcNow ?? DateTime.UtcNow;

        /// <summary>
        /// Gets the injected file system abstraction for testing access.
        /// </summary>
        public IFileSystemAbstraction FileSystem => _fileSystem;
    }
}