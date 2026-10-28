using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Accepted editor state for the owner's history. Pending text, errors and source receipts are excluded.</summary>
public sealed record AcceptedFieldValue(ParameterSetting Setting,bool ReferenceMode,ConstantSetting? ConstantBackup);
