using System.Net;
using Rivulet.Core;

namespace Rivulet.Http.Tests;

public class HttpOptionsTests
{
    [Fact]
    public void HttpOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new HttpOptions();

        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.RespectRetryAfterHeader.Should().BeTrue();
        options.BufferSize.Should().Be(81920);
        options.FollowRedirects.Should().BeTrue();
        options.MaxRedirects.Should().Be(50);
        options.RetriableStatusCodes.Should().Contain([
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        ]);
    }

    [Fact]
    public void HttpOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var options = new HttpOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(60),
            RespectRetryAfterHeader = false,
            BufferSize = 16384,
            FollowRedirects = false,
            MaxRedirects = 10,
            RetriableStatusCodes = [HttpStatusCode.TooManyRequests]
        };

        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.RespectRetryAfterHeader.Should().BeFalse();
        options.BufferSize.Should().Be(16384);
        options.FollowRedirects.Should().BeFalse();
        options.MaxRedirects.Should().Be(10);
        options.RetriableStatusCodes.Should().ContainSingle().Which.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void GetMergedParallelOptions_WithNullParallelOptions_ShouldUseDefaults()
    {
        var options = new HttpOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.Should().Be(HttpOptions.DefaultRetryCount); // HTTP default
        mergedOptions.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(30));
        mergedOptions.IsTransient.Should().NotBeNull();
    }

    [Fact]
    public void GetMergedParallelOptions_WithCustomParallelOptions_ShouldMergeCorrectly()
    {
        var customParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            MaxRetries = 5,
            BaseDelay = TimeSpan.FromMilliseconds(200),
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var options = new HttpOptions
        {
            ParallelOptions = customParallelOptions,
            RequestTimeout = TimeSpan.FromSeconds(45)
        };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxDegreeOfParallelism.Should().Be(10);
        mergedOptions.MaxRetries.Should().Be(5);
        mergedOptions.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        mergedOptions.ErrorMode.Should().Be(ErrorMode.CollectAndContinue);
        mergedOptions.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldHandleHttpRequestException()
    {
        var options = new HttpOptions
        {
            RetriableStatusCodes = [HttpStatusCode.ServiceUnavailable]
        };

        var mergedOptions = options.GetMergedParallelOptions();

        var httpException503 = new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);
        var httpException404 = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
        var timeoutException = new TaskCanceledException();
        var cancelledException = new OperationCanceledException();

        mergedOptions.IsTransient!.Invoke(httpException503).Should().BeTrue();
        mergedOptions.IsTransient!.Invoke(httpException404).Should().BeFalse();
        mergedOptions.IsTransient!.Invoke(timeoutException).Should().BeTrue();
        mergedOptions.IsTransient!.Invoke(cancelledException).Should().BeFalse();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldRespectUserProvidedPredicate()
    {
        var userProvidedCalled = false;
        var customParallelOptions = new ParallelOptionsRivulet
        {
            IsTransient = ex =>
            {
                userProvidedCalled = true;
                return ex is InvalidOperationException;
            }
        };

        var options = new HttpOptions
        {
            ParallelOptions = customParallelOptions
        };

        var mergedOptions = options.GetMergedParallelOptions();
        var customException = new InvalidOperationException();
        var result = mergedOptions.IsTransient!.Invoke(customException);

        result.Should().BeTrue();
        userProvidedCalled.Should().BeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_WithZeroMaxRetries_ShouldDefaultToThree()
    {
        var options = new HttpOptions
        {
            ParallelOptions = new()
            {
                MaxRetries = 0
            }
        };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.Should().Be(HttpOptions.DefaultRetryCount);
    }

    [Fact]
    public void HttpOptions_WithCallbacks_ShouldSetCorrectly()
    {
        var httpErrorCalled = false;
        var redirectCalled = false;

        var options = new HttpOptions
        {
            OnHttpErrorAsync = (_, _, _) =>
            {
                httpErrorCalled = true;
                return ValueTask.CompletedTask;
            },
            OnRedirectAsync = (_, _) =>
            {
                redirectCalled = true;
                return ValueTask.CompletedTask;
            }
        };

        options.OnHttpErrorAsync.Should().NotBeNull();
        options.OnRedirectAsync.Should().NotBeNull();

        // Verify callbacks can be invoked
        options.OnHttpErrorAsync!.Invoke(new("http://example.com"), HttpStatusCode.NotFound, new());
        options.OnRedirectAsync!.Invoke(new("http://example.com"), new("http://redirected.com"));

        httpErrorCalled.Should().BeTrue();
        redirectCalled.Should().BeTrue();
    }
}
