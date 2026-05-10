using System.Text.Json.Serialization;

namespace IISManager.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandType
{
    DeployApplication,
    RollbackDeployment,
    BackupApplication,
    StartWebsite,
    StopWebsite,
    RestartWebsite,
    DeleteWebsite,
    CreateWebsite,
    RestartApplicationPool,
    StopApplicationPool,
    StartApplicationPool,
    CreateApplicationPool,
    DeleteApplicationPool,
    RecycleApplicationPool,
    GetIISStatus,
    GetServerHealth,
    SyncIISState
}
