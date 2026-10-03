namespace BDMAgent.Domain.Entities;
public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Campaign";
    public string Countries { get; set; } = "UAE,Germany,UK,USA";
    public string Technologies { get; set; } = ".NET,Angular,PHP,WordPress,Azure,APIs";
    public int MinimumScore { get; set; } = 55;
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Capabilities { get; set; } = ".NET/C#, Angular, PHP, WordPress, API integration, cloud modernization and dedicated developers";
    public string FollowUpDays { get; set; } = "3,7,14";
    public bool AutoSendFollowUps { get; set; }
    public int DailySendLimit { get; set; } = 20;
}
