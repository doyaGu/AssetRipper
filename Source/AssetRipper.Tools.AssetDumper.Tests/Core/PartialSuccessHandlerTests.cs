using AssetRipper.Tools.AssetDumper.Core;
using FluentAssertions;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Core;

/// <summary>
/// Tests for PartialSuccessResult and ExceptionHandlerExtensions.
/// </summary>
public class PartialSuccessHandlerTests
{
    #region PartialSuccessResult Basic Tests

    [Fact]
    public void PartialSuccessResult_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new PartialSuccessResult();

        // Assert
        result.TotalItems.Should().Be(0);
        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AddSuccess_ShouldIncrementSuccessCount()
    {
        // Arrange
        var result = new PartialSuccessResult();

        // Act
        result.AddSuccess();
        result.AddSuccess();

        // Assert
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public void AddError_ShouldIncrementFailureCountAndAddToErrors()
    {
        // Arrange
        var result = new PartialSuccessResult();
        var exception = new Exception("Test error");

        // Act
        result.AddError("item1", exception);

        // Assert
        result.FailureCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Item.Should().Be("item1");
        result.Errors[0].Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void AddError_WithCriticalFlag_ShouldMarkAsCritical()
    {
        // Arrange
        var result = new PartialSuccessResult();

        // Act
        result.AddError("critical-item", new Exception("Critical"), isCritical: true);

        // Assert
        result.Errors[0].IsCritical.Should().BeTrue();
    }

    [Fact]
    public void AddSkipped_ShouldIncrementSkippedCountAndAddToErrors()
    {
        // Arrange
        var result = new PartialSuccessResult();

        // Act
        result.AddSkipped("item1", "Not supported");

        // Assert
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].IsSkipped.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Not supported");
    }

    #endregion

    #region Status Properties Tests

    [Fact]
    public void IsCompleteSuccess_ShouldBeTrueWhenNoFailuresOrSkips()
    {
        // Arrange
        var result = new PartialSuccessResult();
        result.AddSuccess();
        result.AddSuccess();

        // Assert
        result.IsCompleteSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsCompleteSuccess_ShouldBeFalseWhenHasFailures()
    {
        // Arrange
        var result = new PartialSuccessResult();
        result.AddSuccess();
        result.AddError("item", new Exception());

        // Assert
        result.IsCompleteSuccess.Should().BeFalse();
    }

    [Fact]
    public void HasAnySuccess_ShouldBeTrueWhenAtLeastOneSuccess()
    {
        // Arrange
        var result = new PartialSuccessResult();
        result.AddSuccess();
        result.AddError("item", new Exception());

        // Assert
        result.HasAnySuccess.Should().BeTrue();
    }

    [Fact]
    public void IsCompleteFailure_ShouldBeTrueWhenNoSuccessesButHasItems()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 5 };
        result.AddError("item1", new Exception());
        result.AddError("item2", new Exception());

        // Assert
        result.IsCompleteFailure.Should().BeTrue();
    }

    [Fact]
    public void SuccessRate_ShouldCalculateCorrectPercentage()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 10 };
        for (int i = 0; i < 7; i++) result.AddSuccess();
        for (int i = 0; i < 3; i++) result.AddError($"item{i}", new Exception());

        // Assert
        result.SuccessRate.Should().BeApproximately(70.0, 0.01);
    }

    [Fact]
    public void SuccessRate_ShouldReturnZeroWhenNoItems()
    {
        // Arrange
        var result = new PartialSuccessResult();

        // Assert
        result.SuccessRate.Should().Be(0);
    }

    #endregion

    #region GetErrorCode Tests

    [Fact]
    public void GetErrorCode_ShouldReturnSuccess_WhenCompleteSuccess()
    {
        // Arrange
        var result = new PartialSuccessResult();
        result.AddSuccess();

        // Act
        var errorCode = result.GetErrorCode();

        // Assert
        errorCode.Should().Be(ErrorCode.Success);
    }

    [Fact]
    public void GetErrorCode_ShouldReturnProcessingFailed_WhenCompleteFailure()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 3 };
        result.AddError("item1", new Exception());
        result.AddError("item2", new Exception());

        // Act
        var errorCode = result.GetErrorCode();

        // Assert
        errorCode.Should().Be(ErrorCode.ProcessingFailed);
    }

    [Fact]
    public void GetErrorCode_ShouldReturnProcessingFailed_WhenHasCriticalErrors()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 3 };
        result.AddSuccess();
        result.AddError("critical", new Exception("Critical"), isCritical: true);

        // Act
        var errorCode = result.GetErrorCode();

        // Assert
        errorCode.Should().Be(ErrorCode.ProcessingFailed);
    }

    [Fact]
    public void GetErrorCode_ShouldReturnPartialSuccess_WhenSomeSuccessAndNoCriticalErrors()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 5 };
        result.AddSuccess();
        result.AddSuccess();
        result.AddError("item", new Exception(), isCritical: false);

        // Act
        var errorCode = result.GetErrorCode();

        // Assert
        errorCode.Should().Be(ErrorCode.PartialSuccess);
    }

    #endregion

    #region LogSummary Tests

    [Fact]
    public void LogSummary_ShouldNotThrow_WithCompleteSuccess()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 5 };
        for (int i = 0; i < 5; i++) result.AddSuccess();

        // Act
        Action act = () => result.LogSummary("Test Operation");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogSummary_ShouldNotThrow_WithPartialSuccess()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 10 };
        for (int i = 0; i < 7; i++) result.AddSuccess();
        for (int i = 0; i < 3; i++) result.AddError($"item{i}", new Exception($"Error {i}"));

        // Act
        Action act = () => result.LogSummary("Test Operation");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogSummary_ShouldNotThrow_WithCompleteFailure()
    {
        // Arrange
        var result = new PartialSuccessResult { TotalItems = 5 };
        for (int i = 0; i < 5; i++) result.AddError($"item{i}", new Exception());

        // Act
        Action act = () => result.LogSummary("Test Operation");

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ProcessWithRecovery Tests

    [Fact]
    public void ProcessWithRecovery_ShouldProcessAllItems_WhenNoErrors()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var processed = new List<int>();

        // Act
        var result = ExceptionHandlerExtensions.ProcessWithRecovery(
            items,
            item => processed.Add(item),
            item => item.ToString());

        // Assert
        result.TotalItems.Should().Be(5);
        result.SuccessCount.Should().Be(5);
        result.FailureCount.Should().Be(0);
        processed.Should().Equal(items);
    }

    [Fact]
    public void ProcessWithRecovery_ShouldContinueAfterError_WhenContinueOnErrorIsTrue()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var processed = new List<int>();

        // Act
        var result = ExceptionHandlerExtensions.ProcessWithRecovery(
            items,
            item =>
            {
                if (item == 3) throw new Exception("Error on 3");
                processed.Add(item);
            },
            item => item.ToString(),
            continueOnError: true);

        // Assert
        result.TotalItems.Should().Be(5);
        result.SuccessCount.Should().Be(4);
        result.FailureCount.Should().Be(1);
        processed.Should().Equal(new[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void ProcessWithRecovery_ShouldStopAfterError_WhenContinueOnErrorIsFalse()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var processed = new List<int>();

        // Act
        var result = ExceptionHandlerExtensions.ProcessWithRecovery(
            items,
            item =>
            {
                if (item == 3) throw new Exception("Error on 3");
                processed.Add(item);
            },
            item => item.ToString(),
            continueOnError: false);

        // Assert
        result.TotalItems.Should().Be(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        processed.Should().Equal(new[] { 1, 2 });
    }

    [Fact]
    public void ProcessWithRecovery_ShouldAbortAfterMaxConsecutiveErrors()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var result = ExceptionHandlerExtensions.ProcessWithRecovery(
            items,
            item => throw new Exception($"Error on {item}"),
            item => item.ToString(),
            continueOnError: true,
            maxConsecutiveErrors: 3);

        // Assert
        result.TotalItems.Should().Be(3);
        result.FailureCount.Should().Be(3);
    }

    [Fact]
    public void ProcessWithRecovery_ShouldResetConsecutiveErrorCount_OnSuccess()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        var result = ExceptionHandlerExtensions.ProcessWithRecovery(
            items,
            item =>
            {
                // Fail on 1, 2, succeed on 3, fail on 4, 5, succeed on 6, fail on 7, 8
                if (item == 1 || item == 2 || item == 4 || item == 5 || item == 7 || item == 8)
                    throw new Exception($"Error on {item}");
            },
            item => item.ToString(),
            continueOnError: true,
            maxConsecutiveErrors: 3);

        // Assert - Should process all items because successes reset the consecutive count
        result.TotalItems.Should().Be(8);
        result.SuccessCount.Should().Be(2); // Items 3 and 6
        result.FailureCount.Should().Be(6);
    }

    #endregion

    #region ProcessWithRecoveryAsync Tests

    [Fact]
    public async Task ProcessWithRecoveryAsync_ShouldProcessAllItems_WhenNoErrors()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var processed = new List<int>();

        // Act
        var result = await ExceptionHandlerExtensions.ProcessWithRecoveryAsync(
            items,
            async item =>
            {
                await Task.Delay(1);
                processed.Add(item);
            },
            item => item.ToString());

        // Assert
        result.TotalItems.Should().Be(5);
        result.SuccessCount.Should().Be(5);
        result.FailureCount.Should().Be(0);
        processed.Should().Equal(items);
    }

    [Fact]
    public async Task ProcessWithRecoveryAsync_ShouldContinueAfterError()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var processed = new List<int>();

        // Act
        var result = await ExceptionHandlerExtensions.ProcessWithRecoveryAsync(
            items,
            async item =>
            {
                await Task.Delay(1);
                if (item == 3) throw new Exception("Error on 3");
                processed.Add(item);
            },
            item => item.ToString(),
            continueOnError: true);

        // Assert
        result.TotalItems.Should().Be(5);
        result.SuccessCount.Should().Be(4);
        result.FailureCount.Should().Be(1);
        processed.Should().Equal(new[] { 1, 2, 4, 5 });
    }

    #endregion

    #region OperationError Tests

    [Fact]
    public void OperationError_ShouldStoreTimestamp()
    {
        // Arrange
        var result = new PartialSuccessResult();
        var before = DateTime.UtcNow;

        // Act
        result.AddError("item", new Exception("Test"));
        var after = DateTime.UtcNow;

        // Assert
        var error = result.Errors[0];
        error.Timestamp.Should().BeOnOrAfter(before);
        error.Timestamp.Should().BeOnOrBefore(after);
    }

    #endregion
}
