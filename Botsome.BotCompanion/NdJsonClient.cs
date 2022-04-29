using System.Runtime.CompilerServices;

namespace ChessBot;

public class NdJsonClient {
	private readonly HttpClient m_Http;

	public NdJsonClient(HttpClient http) {
		m_Http = http;
	}

	public IAsyncEnumerable<string> StreamLinesAsync(HttpRequestMessage hrm, bool skipEmptyLines, CancellationToken cancellationToken = default) => StreamLinesAsync(hrm, skipEmptyLines, s => s, cancellationToken);
	public async IAsyncEnumerable<T> StreamLinesAsync<T>(HttpRequestMessage hrm, bool skipEmptyLines, Func<string, T> selector, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		using HttpResponseMessage response = await m_Http.SendAsync(hrm, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var reader = new StreamReader(stream);
		while (await reader.ReadLineAsync() is { } next) {
			if (!(skipEmptyLines && string.IsNullOrWhiteSpace(next))) {
				yield return selector(next);
			}
		}
	}
}
