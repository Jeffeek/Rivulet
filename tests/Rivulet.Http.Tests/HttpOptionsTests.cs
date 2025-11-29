using System.Net;
using Rivulet.Core;

namespace Rivulet.Http.Tests;

public class HttpOptionsTests
{
    [Fact]
    public void HttpOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new HttpOptions();

        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.RespectRetryAfterHeader.ShouldBeTrue();
        options.BufferSize.ShouldBe(81920);
        options.FollowRedirects.ShouldBeTrue();
        options.MaxRedirects.ShouldBe(50);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.RequestTimeout);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.TooManyRequests);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.InternalServerError);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.BadGateway);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.ServiceUnavailable);
        options.RetriableStatusCodes.ShouldContain(HttpStatusCode.GatewayTimeout);
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

        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        options.RespectRetryAfterHeader.ShouldBeFalse();
        options.BufferSize.ShouldBe(16384);
        options.FollowRedirects.ShouldBeFalse();
        options.MaxRedirects.ShouldBe(10);
        options.RetriableStatusCodes.ShouldHaveSingleItem().ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void GetMergedParallelOptions_WithNullParallelOptions_ShouldUseDefaults()
    {
        var options = new HttpOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.ShouldBe(HttpOptions.DefaultRetryCount); // HTTP default
        mergedOptions.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        mergedOptions.IsTransient.ShouldNotBeNull();
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

        mergedOptions.MaxDegreeOfParallelism.ShouldBe(10);
        mergedOptions.MaxRetries.ShouldBe(5);
        mergedOptions.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
        mergedOptions.ErrorMode.ShouldBe(ErrorMode.CollectAndContinue);
        mergedOptions.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(45));
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

        mergedOptions.IsTransient!.Invoke(httpException503).ShouldBeTrue();
        mergedOptions.IsTransient!.Invoke(httpException404).ShouldBeFalse();
        mergedOptions.IsTransient!.Invoke(timeoutException).ShouldBeTrue();
        mergedOptions.IsTransient!.Invoke(cancelledException).ShouldBeFalse();
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

        result.ShouldBeTrue();
        userProvidedCalled.ShouldBeTrue();
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

        mergedOptions.MaxRetries.ShouldBe(HttpOptions.DefaultRetryCount);
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

        options.OnHttpErrorAsync.ShouldNotBeNull();
        options.OnRedirectAsync.ShouldNotBeNull();

        // Verify callbacks can be invoked
        options.OnHttpErrorAsync!.Invoke(new("http://example.com"), HttpStatusCode.NotFound, new());
        options.OnRedirectAsync!.Invoke(new("http://example.com"), new("http://redirected.com"));

        httpErrorCalled.ShouldBeTrue();
        redirectCalled.ShouldBeTrue();
    }
}
