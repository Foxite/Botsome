namespace Botsome.Coordinator; 

public sealed class DisposalNotifyingStream<T> : Stream {
	private readonly Stream m_BaseStream;
	private readonly T m_Identifier;

	public override bool CanRead => m_BaseStream.CanRead;
	public override bool CanSeek => m_BaseStream.CanSeek;
	public override bool CanWrite => m_BaseStream.CanWrite;
	public override long Length => m_BaseStream.Length;

	public override long Position {
		get => m_BaseStream.Position;
		set => m_BaseStream.Position = value;
	}

	public event EventHandler<T>? Disposing;

	public DisposalNotifyingStream(Stream baseStream, T identifier) {
		m_BaseStream = baseStream;
		m_Identifier = identifier;
	}

	public override int Read(byte[] buffer, int offset, int count) => m_BaseStream.Read(buffer, offset, count);
	public override void Write(byte[] buffer, int offset, int count) => m_BaseStream.Write(buffer, offset, count);
	public override long Seek(long offset, SeekOrigin origin) => m_BaseStream.Seek(offset, origin);
	public override void SetLength(long value) => m_BaseStream.SetLength(value);
	public override void Flush() => m_BaseStream.Flush();

	protected override void Dispose(bool disposing) {
		Disposing?.Invoke(this, m_Identifier);
		base.Dispose(disposing);
	}

	public override ValueTask DisposeAsync() {
		Disposing?.Invoke(this, m_Identifier);
		return base.DisposeAsync();
	}
}