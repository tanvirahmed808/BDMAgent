namespace BDMAgent.Application.Abstractions;
public interface IEmailSender { Task<string> SendAsync(string to, string subject, string body, CancellationToken ct = default); }
