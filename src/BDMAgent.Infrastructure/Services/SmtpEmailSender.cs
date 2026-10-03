using System.Net;
using System.Net.Mail;
using BDMAgent.Application.Abstractions;
using Microsoft.Extensions.Configuration;
namespace BDMAgent.Infrastructure.Services;
public sealed class SmtpEmailSender(IConfiguration cfg) : IEmailSender
{
    public async Task<string> SendAsync(string to,string subject,string body,CancellationToken ct=default)
    {
        var host=cfg["Email:SmtpHost"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var user=cfg["Email:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var pass=cfg["Email:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var from=cfg["Email:From"] ?? user;
        if(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(pass)||string.IsNullOrWhiteSpace(from)) throw new InvalidOperationException("SMTP is not configured.");
        using var client=new SmtpClient(host, int.TryParse(cfg["Email:SmtpPort"],out var p)?p:587){EnableSsl=true,Credentials=new NetworkCredential(user,pass)};
        using var msg=new MailMessage(from,to,subject,body);
        await client.SendMailAsync(msg,ct); return Guid.NewGuid().ToString("N");
    }
}
