# 外部工具模板

当前模板按 SunEyeVision 新合同构建，使用 `PluginDevKit/sdk/net9.0` 中的发布 DLL。

- `RuntimePlugin.Template` 提供 `IToolDefinition<TSettings,TOutput>` 和 `IToolSession` 样例。
- `WpfTool.Template` 提供 `IToolPresentationFactory` 和只读结果视图样例。
- 每个模板都包含 `PluginContractAttribute(6,0)`，宿主会按合同版本准入。

模板工程只依赖发布 DLL。将模板目录复制到干净目录后执行 `dotnet build -c Release`，不需要主仓库源码。
