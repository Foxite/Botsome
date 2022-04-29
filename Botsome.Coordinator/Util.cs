namespace Botsome.Coordinator; 

public class AsyncEnumeratorWrapper<T> : IAsyncEnumerable<T> {
	private readonly Func<CancellationToken, IAsyncEnumerator<T>> m_EnumeratorFactory;

	public AsyncEnumeratorWrapper(Func<CancellationToken, IAsyncEnumerator<T>> enumeratorFactory) {
		m_EnumeratorFactory = enumeratorFactory;
	}
	
	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) {
		return m_EnumeratorFactory(cancellationToken);
	}
}
