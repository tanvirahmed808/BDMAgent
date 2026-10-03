namespace BDMAgent.Domain.Entities;
public sealed class IntegrationAccount
{
 public string Id { get; set; } = "Google";
 public string ProtectedRefreshToken { get; set; } = "";
 public string Email { get; set; } = "";
 public DateTime? LastSyncUtc { get; set; }
}
public sealed class Suppression
{
 public string Email { get; set; } = "";
 public string Reason { get; set; } = "";
 public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
