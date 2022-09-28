namespace ReponoStorage;

public sealed class PartialStream : Stream
{
    public Stream BaseStream { get; }

    public long TotalLimit { get; }

    private long limit;

    public PartialStream(Stream baseStream, long totalLimit)
    {
        BaseStream = baseStream;
        TotalLimit = limit = totalLimit;
        if (!baseStream.CanRead)
            throw new ArgumentException("cannot read stream", nameof(baseStream));
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => TotalLimit;

    public override long Position
    {
        get => TotalLimit - limit;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = (int)Math.Min(count, limit);
        var read = BaseStream.Read(buffer, offset, count);
        limit -= read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var count = (int)Math.Min(buffer.Length, limit);
        var read = BaseStream.Read(buffer[..count]);
        limit -= read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = (int)Math.Min(buffer.Length, limit);
        var read = await BaseStream.ReadAsync(buffer[..count], cancellationToken);
        limit -= read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        count = (int)Math.Min(count, limit);
        var read = await BaseStream.ReadAsync(buffer, offset, count, cancellationToken);
        limit -= read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

    public override void SetLength(long value)
    => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        limit = 0;
        BaseStream.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        limit = 0;
        await BaseStream.DisposeAsync();
        await base.DisposeAsync();
    }
}