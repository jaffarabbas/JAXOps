namespace DeploymentTool.Models;

public enum FileFilterMode { All, ModifiedOnly, NewOnly }

public record FilterOption(FileFilterMode Mode, string Label);

public enum CompareMode { TwoFolder, SingleFolder }

public record CompareModeOption(CompareMode Mode, string Label);
