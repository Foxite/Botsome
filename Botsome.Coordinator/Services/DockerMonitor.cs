using Docker.DotNet;

namespace Botsome.Coordinator; 

public class DockerMonitor {
	private readonly DockerClient m_Docker;
	
	public DockerMonitor(DockerClient docker) {
		m_Docker = docker;
		
		// TODO receive notification when docker a container changes
		// Nginx-proxy does this using docker-gen, which is in go. https://github.com/nginx-proxy/docker-gen/blob/cfd2934f0fb8d1a872d6651bdf7acf2fb97e886c/internal/generator/generator.go#L88
		// It seems to respond to SIGHUP, so find out why it gets SIGHUP.
	}
}
