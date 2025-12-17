namespace Rivulet.Http.Tests;

public class StreamingDownloadOptionsTests
{
    [Fact]
    public void StreamingDownloadOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new StreamingDownloadOptions();

        options.BufferSize.ShouldBe(81920);
        options.EnableResume.ShouldBeTrue();
        options.ValidateContentLength.ShouldBeTrue();
        options.OverwriteExisting.ShouldBeFalse();
        options.ProgressInterval.ShouldBe(TimeSpan.FromSeconds(1));
        options.HttpOptions.ShouldBeNull();
        options.OnProgressAsync.ShouldBeNull();
        options.OnResumeAsync.ShouldBeNull();
        options.OnCompleteAsync.ShouldBeNull();
    }

    [Fact]
    public async Task StreamingDownloadOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var httpOptions = new HttpOptions();
        var progressCalled = false;
        var resumeCalled = false;
        var completeCalled = false;

        var options = new StreamingDownloadOptions
        {
            HttpOptions = httpOptions,
            BufferSize = 16384,
            EnableResume = false,
            ValidateContentLength = false,
            OverwriteExisting = true,
            ProgressInterval = TimeSpan.FromMilliseconds(500),
            OnProgressAsync = (_, _, _) =>
            {
                progressCalled = true;
                return ValueTask.CompletedTask;
            },
            OnResumeAsync = (_, _) =>
            {
                resumeCalled = true;
                return ValueTask.CompletedTask;
            },
            OnCompleteAsync = (_, _, _) =>
            {
                completeCalled = true;
                return ValueTask.CompletedTask;
            }
        };

        options.HttpOptions.ShouldBeSameAs(httpOptions);
        options.BufferSize.ShouldBe(16384);
        options.EnableResume.ShouldBeFalse();
        options.ValidateContentLength.ShouldBeFalse();
        options.OverwriteExisting.ShouldBeTrue();
        options.ProgressInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.OnProgressAsync.ShouldNotBeNull();
        options.OnResumeAsync.ShouldNotBeNull();
        options.OnCompleteAsync.ShouldNotBeNull();
        await options.OnProgressAsync!.Invoke(new("http://test.local"), 100, 200);
        await options.OnResumeAsync!.Invoke(new("http://test.local"), 50);
        await options.OnCompleteAsync!.Invoke(new("http://test.local"), "/path/file.txt", 200);

        progressCalled.ShouldBeTrue();
        resumeCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
    }

    [Fact]
    public void StreamingDownloadOptions_Immutability_ShouldBeEnforced()
    {
        var options = new StreamingDownloadOptions { BufferSize = 16384, EnableResume = false };

        // Act & Assert - This should not compile if properties are not init-only
        // options.BufferSize = 32768; // Compile error expected
        options.BufferSize.ShouldBe(16384);
    }

    [Fact]
    public void StreamingDownloadOptions_WithCustomBufferSize_ShouldRespectValue()
    {
        var smallBuffer = new StreamingDownloadOptions { BufferSize = 4096 };
        var largeBuffer = new StreamingDownloadOptions { BufferSize = 1048576 };

        smallBuffer.BufferSize.ShouldBe(4096);
        largeBuffer.BufferSize.ShouldBe(1048576);
    }

    [Fact]
    public void StreamingDownloadOptions_WithCustomProgressInterval_ShouldRespectValue()
    {
        var fastProgress = new StreamingDownloadOptions { ProgressInterval = TimeSpan.FromMilliseconds(100) };
        var slowProgress = new StreamingDownloadOptions { ProgressInterval = TimeSpan.FromSeconds(5) };

        fastProgress.ProgressInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
        slowProgress.ProgressInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StreamingDownloadOptions_WithHttpOptions_ShouldPassThrough()
    {
        var httpOptions = new HttpOptions { RequestTimeout = TimeSpan.FromSeconds(120), RespectRetryAfterHeader = false };

        var options = new StreamingDownloadOptions { HttpOptions = httpOptions };

        options.HttpOptions.ShouldBeSameAs(httpOptions);
        options.HttpOptions.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(120));
        options.HttpOptions.RespectRetryAfterHeader.ShouldBeFalse();
    }

    [Fact]
    public async Task StreamingDownloadOptions_CallbackChaining_ShouldWorkCorrectly()
    {
        var progressCount = 0;
        var resumeCount = 0;
        var completeCount = 0;

        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = async (_, _, _) =>
            {
                progressCount++;
                await Task.Delay(1, CancellationToken.None);
            },
            OnResumeAsync = async (_, _) =>
            {
                resumeCount++;
                await Task.Delay(1, CancellationToken.None);
            },
            OnCompleteAsync = async (_, _, _) =>
            {
                completeCount++;
                await Task.Delay(1, CancellationToken.None);
            }
        };

        await options.OnProgressAsync!.Invoke(new("http://test.local"), 50, 100);
        await options.OnProgressAsync!.Invoke(new("http://test.local"), 100, 100);
        await options.OnResumeAsync!.Invoke(new("http://test.local"), 50);
        await options.OnCompleteAsync!.Invoke(new("http://test.local"), "/path", 100);

        progressCount.ShouldBe(2);
        resumeCount.ShouldBe(1);
        completeCount.ShouldBe(1);
    }
}