using Microsoft.Extensions.Logging;

namespace Botsome; 

public class ScopedLoggerFactory : ILoggerFactory {
	private readonly ILoggerFactory m_LoggerFactoryImplementation;
	private readonly string m_LogFormat;
	private readonly object[] m_LogFormatArguments;

	public ScopedLoggerFactory(ILoggerFactory loggerFactoryImplementation, string logFormat, params object[] logFormatArguments) {
		m_LoggerFactoryImplementation = loggerFactoryImplementation;
		m_LogFormat = logFormat;
		m_LogFormatArguments = logFormatArguments;
	}

	public void Dispose() {
		m_LoggerFactoryImplementation.Dispose();
	}

	public void AddProvider(ILoggerProvider provider) {
		m_LoggerFactoryImplementation.AddProvider(provider);
	}

	public ILogger CreateLogger(string categoryName) {
		ILogger ret = m_LoggerFactoryImplementation.CreateLogger(categoryName);
		ret.BeginScope(m_LogFormat, m_LogFormatArguments);
		return ret;
	}
}

public static class ScopedLoggerFactoryUtils {
	public static ILoggerFactory Scope(this ILoggerFactory ilf, string logFormat, params object[] logFormatArguments) {
		return new ScopedLoggerFactory(ilf, logFormat, logFormatArguments);
	}
}