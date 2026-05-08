namespace NexaPay.Application.Common.Interfaces
{
    /// <summary>
    /// Märker en MediatR-request som känslig.
    /// LoggingBehavior loggar request-detaljer utelämnade för dessa typer,
    /// så att lösenord och andra hemliga fält inte hamnar i loggarna.
    /// </summary>
    public interface ISensitiveRequest { }
}
