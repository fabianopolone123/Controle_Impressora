namespace PrintControl.Host.Models;

public sealed class PrintControlOptions
{
    public string ApiKey { get; set; } = "";
    public string DatabasePath { get; set; } = @"C:\\ProgramData\\PrintControl\\Host\\prints.db";
    public string ListenUrls { get; set; } = "http://0.0.0.0:5080";
    public string DashboardUser { get; set; } = "admin";
    public string DashboardPassword { get; set; } = "change-me";
}
