namespace IISManager.Shared.Constants;

public static class SignalRConstants
{
    public static class Hubs
    {
        public const string Agent = "/hubs/agent";
        public const string Deployment = "/hubs/deployment";
        public const string Monitoring = "/hubs/monitoring";
    }

    public static class AgentMethods
    {
        public const string ExecuteCommand = "ExecuteCommand";
        public const string Ping = "Ping";
    }

    public static class PortalMethods
    {
        public const string ReportHeartbeat = "ReportHeartbeat";
        public const string ReportProgress = "ReportProgress";
        public const string ReportLogLine = "ReportLogLine";
        public const string ReportDeploymentCompleted = "ReportDeploymentCompleted";
        public const string ReportIISState = "ReportIISState";
        public const string ReportCommandResult = "ReportCommandResult";
    }

    public static class ClientMethods
    {
        public const string OnLogLine = "OnLogLine";
        public const string OnProgress = "OnProgress";
        public const string OnStatusChange = "OnStatusChange";
        public const string OnServerHealthUpdate = "OnServerHealthUpdate";
        public const string OnServerStatusChange = "OnServerStatusChange";
        public const string OnNotification = "OnNotification";
        public const string OnIISStateUpdate = "OnIISStateUpdate";
    }

    public static class Groups
    {
        public static string Deployment(int deploymentId) => $"deployment-{deploymentId}";
        public static string Server(int serverId) => $"server-{serverId}";
        public const string AllAdmins = "admins";
        public const string AllUsers = "all-users";
    }

    public const string AgentApiKeyHeader = "X-Agent-ApiKey";
    public const string ServerIdHeader = "X-Server-Id";
    public const int HeartbeatIntervalSeconds = 30;
    public const int ConnectionTimeoutSeconds = 90;
}
