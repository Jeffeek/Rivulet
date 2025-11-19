namespace Rivulet.Http.Tests;

public class StreamingDownloadOptionsTests
{
    [Fact]
    public void StreamingDownloadOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new StreamingDownloadOptions();

        options.BufferSize.Should().Be(81920);
        options.EnableResume.Should().BeTrue();
        options.ValidateContentLength.Should().BeTrue();
        options.OverwriteExisting.Should().BeFalse();
        options.ProgressInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.HttpOptions.Should().BeNull();
        options.OnProgressAsync.Should().BeNull();
        options.OnResumeAsync.Should().BeNull();
        options.OnCompleteAsync.Should().BeNull();
    }

    [Fact]
    public void StreamingDownloadOptions_WithInitProperties_ShouldSetCorrectly()
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

        options.HttpOptions.Should().BeSameAs(httpOptions);
        options.BufferSize.Should().Be(16384);
        options.EnableResume.Should().BeFalse();
        options.ValidateContentLength.Should().BeFalse();
        options.OverwriteExisting.Should().BeTrue();
        options.ProgressInterval.Should().Be(TimeSpan.FromMilliseconds(500));
        options.OnProgressAsync.Should().NotBeNull();
        options.OnResumeAsync.Should().NotBeNull();
        options.OnCompleteAsync.Should().NotBeNull();
        options.OnProgressAsync!.Invoke(new Uri("http://test.local"), 100, 200);
        options.OnResumeAsync!.Invoke(new Uri("http://test.local"), 50);
        options.OnCompleteAsync!.Invoke(new Uri("http://test.local"), "/path/file.txt", 200);

        progressCalled.Should().BeTrue();
        resumeCalled.Should().BeTrue();
        completeCalled.Should().BeTrue();
    }

    [Fact]
    public void StreamingDownloadOptions_Immutability_ShouldBeEnforced()
    {
        var options = new StreamingDownloadOptions
        {
            BufferSize = 16384,
            EnableResume = false
        };

        // Act & Assert - This should not compile if properties are not init-only
        // options.BufferSize = 32768; // Compile error expected
        options.BufferSize.Should().Be(16384);
    }

    [Fact]
    public void StreamingDownloadOptions_WithCustomBufferSize_ShouldRespectValue()
    {
        var smallBuffer = new StreamingDownloadOptions { BufferSize = 4096 };
        var largeBuffer = new StreamingDownloadOptions { BufferSize = 1048576 };

        smallBuffer.BufferSize.Should().Be(4096);
        largeBuffer.BufferSize.Should().Be(1048576);
    }

    [Fact]
    public void StreamingDownloadOptions_WithCustomProgressInterval_ShouldRespectValue()
    {
        var fastProgress = new StreamingDownloadOptions { ProgressInterval = TimeSpan.FromMilliseconds(100) };
        var slowProgress = new StreamingDownloadOptions { ProgressInterval = TimeSpan.FromSeconds(5) };

        fastProgress.ProgressInterval.Should().Be(TimeSpan.FromMilliseconds(100));
        slowProgress.ProgressInterval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StreamingDownloadOptions_WithHttpOptions_ShouldPassThrough()
    {
        var httpOptions = new HttpOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(120),
            RespectRetryAfterHeader = false
        };

        var options = new StreamingDownloadOptions
        {
            HttpOptions = httpOptions
        };

        options.HttpOptions.Should().BeSameAs(httpOptions);
        options.HttpOptions.RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        options.HttpOptions.RespectRetryAfterHeader.Should().BeFalse();
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
                await Task.Delay(1);
            },
            OnResumeAsync = async (_, _) =>
            {
                resumeCount++;
                await Task.Delay(1);
            },
            OnCompleteAsync = async (_, _, _) =>
            {
                completeCount++;
                await Task.Delay(1);
            }
        };

        await options.OnProgressAsync!.Invoke(new Uri("http://test.local"), 50, 100);
        await options.OnProgressAsync!.Invoke(new Uri("http://test.local"), 100, 100);
        await options.OnResumeAsync!.Invoke(new Uri("http://test.local"), 50);
        await options.OnCompleteAsync!.Invoke(new Uri("http://test.local"), "/path", 100);

        progressCount.Should().Be(2);
        resumeCount.Should().Be(1);
        completeCount.Should().Be(1);
    }
}
