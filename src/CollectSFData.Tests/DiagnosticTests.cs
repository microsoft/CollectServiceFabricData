// Licensed under the MIT License.

using CollectSFData.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace CollectSFData.Tests
{
    /// <summary>
    /// Diagnostic tests to understand validation failures
    /// </summary>
    [
        Collection("TestAuditCollection")
    ]
    public class DiagnosticTests
    {
        private readonly ITestOutputHelper _output;

        public DiagnosticTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Debug_ValidateTime_WithEmptyTimes()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var now = DateTimeOffset.Now;
            var startTime = now.AddHours(-2).UtcDateTime; // DefaultStartTimeHours = -2
            var endTime = now.UtcDateTime;
            
            // The ConfigurationOptions constructor sets default timestamps
            // We need to mock ConvertToUtcTime for any timestamp format that might be generated
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(It.IsAny<string>()))
                            .Returns<string>(timeString => 
                            {
                                _output.WriteLine($"ConvertToUtcTime called with: '{timeString}'");
                                // Try to parse the actual timestamp
                                if (DateTimeOffset.TryParse(timeString, out var parsed))
                                {
                                    var utc = parsed.UtcDateTime;
                                    // Return start time if it's roughly the start time, end time if roughly end time
                                    var timeDiffStart = Math.Abs((utc - startTime).TotalMinutes);
                                    var timeDiffEnd = Math.Abs((utc - endTime).TotalMinutes);
                                    _output.WriteLine($"Parsed UTC: {utc}, StartDiff: {timeDiffStart}min, EndDiff: {timeDiffEnd}min");
                                    return timeDiffStart < timeDiffEnd ? startTime : endTime;
                                }
                                return DateTime.MinValue;
                            });
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                StartTimeStamp = null,
                EndTimeStamp = null
            };

            _output.WriteLine($"StartTimeStamp: '{config.StartTimeStamp}'");
            _output.WriteLine($"EndTimeStamp: '{config.EndTimeStamp}'");
            _output.WriteLine($"HasValue(StartTimeStamp): {config.HasValue(config.StartTimeStamp)}");
            _output.WriteLine($"HasValue(EndTimeStamp): {config.HasValue(config.EndTimeStamp)}");

            // Act
            bool result = config.ValidateTime();

            // Assert
            _output.WriteLine($"ValidateTime result: {result}");
            result.Should().BeTrue();
        }

        [Fact]
        public void Debug_ValidateTime_WithValidTimes()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var startTime = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var endTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            
            // Set up mocks to handle the actual timestamp formats that will be used
            mockTimeProvider.Setup(x => x.ConvertToUtcTime("01/01/2023 05:00 -05:00")).Returns(startTime);
            mockTimeProvider.Setup(x => x.ConvertToUtcTime("01/01/2023 07:00 -05:00")).Returns(endTime);
            mockTimeProvider.Setup(x => x.ConvertToUtcTime("2023-01-01T10:00:00")).Returns(startTime);
            mockTimeProvider.Setup(x => x.ConvertToUtcTime("2023-01-01T12:00:00")).Returns(endTime);
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object);
            
            config.StartTimeStamp = "01/01/2023 05:00 -05:00";
            config.EndTimeStamp = "01/01/2023 07:00 -05:00";

            _output.WriteLine($"StartTimeStamp: '{config.StartTimeStamp}'");
            _output.WriteLine($"EndTimeStamp: '{config.EndTimeStamp}'");
            _output.WriteLine($"StartTimeUtc: {config.StartTimeUtc}");
            _output.WriteLine($"EndTimeUtc: {config.EndTimeUtc}");
            _output.WriteLine($"HasValue(StartTimeStamp): {config.HasValue(config.StartTimeStamp)}");
            _output.WriteLine($"HasValue(EndTimeStamp): {config.HasValue(config.EndTimeStamp)}");

            // Act
            bool result = config.ValidateTime();

            // Assert
            _output.WriteLine($"ValidateTime result: {result}");
            result.Should().BeTrue();
        }
    }
}