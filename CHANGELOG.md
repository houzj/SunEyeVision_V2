# 变更日志

本文件记录每个发布版本的变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)，渠道字段见版本真源 `version.json`（rule-022）。

> 维护约定：每次发布前由 `installer/Pack-Release.ps1` 的使用者在本文件顶部追加该版本条目；
> 草稿可用 `git log <上一个 tag>..HEAD --oneline` 生成。**未发布的改动写入 `[Unreleased]`。**

## [Unreleased]

## [1.0.0.1] - 2026-09-18

> 渠道：alpha　|　提交：1eb5aad　|　版本号由 `installer/Pack-Release.ps1 -AutoVersion` 依据提交历史自动推导

### Added

- **版本号单一真源** `version.json`（rule-022）：版本 / 渠道 / 序号 / 发布日期 / 方案文件格式版本集中定义，提升版本只改一处。
- **打包脚本** `installer/Pack-Release.ps1`：一条命令完成 `构建 → 校验 → 打包 → 发布清单`；
  支持 `-FromSource`（读真源）、`-Coexist`（内测版与正式版并存）、`-Tag`（打 tag），
  并在打包前做 4 项硬校验（SemVer 格式、commit == HEAD、硬编码版本字面量扫描、源码不新于产物）。
- **图标构建脚本** `installer/Build-Icons.ps1`：从源图生成多尺寸 ICO（16/24/32/48/64/128/256）。
- **安装器注册 `.solution` 文件关联**（`[Registry]`，HKCU）：安装后即显示应用图标、可双击打开，不再依赖"程序首次运行"。
- **治理**：rule-022《版本号单一真源》、Skill《版本发布》。

### Changed

- `installer/SunEyeVision.iss`：版本号**参数化**（原硬编码 `1.0.0` 与载荷路径改为可注入；由 `Pack-Release.ps1` 生成 `.generated/version.iss` 注入，避免命令行传中文的编码问题）。
- `src/UI/Icons/app_logo.ico`：由单尺寸 256 重制为 **7 尺寸**（小图标视图不再依赖系统缩放）。
- `src/UI/Icons/solution.ico`：重制为 7 尺寸，**与软件图标同源同尺寸**。

### Fixed

- 解决方案文件图标与软件图标不一致（原 `solution.ico` 为占位图且仅 4 尺寸）。
- 安装包/程序集版本无单一真源、且曾在界面与安装包之间不一致。

### Known Issues

- `src/UI/Shell/Windows/SplashScreen.xaml:85`（`版本 2.0.0`）、`AboutWindow.xaml:37`（`v1.0.0.0`）、
  `SolutionConfigurationDialog.xaml.cs:53`（取 `AssemblyVersion`）仍为硬编码/取错字段 ⇒ **待改读 `AppVersionInfo`**
  （属 `src/UI` 禁区，需 Codex 或授权）。在此之前，打包脚本需显式传 `-AllowHardcodedVersion` 才能放行。
- `FileAssociationService.IsRegistered()` 仅比对 ProgID ⇒ 关联路径**不会自愈**（换安装目录后仍指向旧路径）；安装器注册可覆盖新装用户，程序侧自愈待改（`src/UI`）。
- 历史 `.solution` 方案文件在已迁移工具（ImageThreshold/ImageFilter/BlobAnalysis/Morphology）上**无法反序列化**
  （缺格式迁移器；`SchemaVersion` 机制待落地）。

### 交付物

- 安装包：`SunEyeVision-Setup-1.0.0.1-alpha-x64.exe`
- 发布清单：`release-manifest-1.0.0.1.json`
- SHA256：`8C929C0D59382A2A1D0C7B4F2D8E0B37455C8E2B3A7D34B5B0DFD6CA977886CA`
- 载荷：308 个文件 / 608.7 MB


## [1.0.1.0] - 2026-09-18

> 渠道：alpha　|　提交：49a59e4　|　本条目由 `installer/Pack-Release.ps1 -AutoVersion` 依据提交历史自动生成

### 新增（feat）

-（无）

### 修复与优化（fix / perf）

- fix(installer): 补齐安装包版本资源与 .solution 文件关联，图标改用应用图标

### 其他（docs / chore / refactor / test / build）

- docs(release): 新增内测版发布与版本管理最终方案
- chore(rules): 新增 rule-022 版本单一真源并落地版本发布技能
- chore(release): 引入版本单一真源与自动推导发布流水线

### 交付物

- 安装包：`SunEyeVision-Setup-1.0.1.0-alpha-x64.exe`
- 发布清单：`release-manifest-1.0.1.0.json`
- SHA256：`50E8FF171A752F4E5C75F8A2285E7225BB3F691ADBC29F5BB9B184B4509ED3EE`
- 载荷：308 个文件 / 608.7 MB


## [2.0.0.1] - 2026-09-19

> 渠道：alpha　|　提交：7c49b36　|　本条目由 `installer/Pack-Release.ps1 -AutoVersion` 依据提交历史自动生成

### 新增（feat）

- feat(ui): 全局变量绑定与执行快照适配
- feat(workflow): 执行链路接入参数绑定与运行快照
- feat(sdk): 参数契约与运行快照落地
- feat(workflow): 运行请求、依赖图、资源租约与工具执行画像
- feat(ui): 运行态输入快照与执行租约，连续执行统一由宿主驱动

### 修复与优化（fix / perf）

- fix(installer): 补齐安装包版本资源与 .solution 文件关联，图标改用应用图标
- fix(installer): 安装包图标改用应用程序图标并补 UTF-8 BOM

### 其他（docs / chore / refactor / test / build）

- refactor(release): 打包改为只读真源、构建号递增与单产物保证
- docs(release): 版本管理再设计与单产物方案
- docs: 新增参数契约与运行快照方案及架构审查文档
- refactor(tools): 工具参数纯值化与契约清单迁移
- test(e2e): 新增确定性灰度测试图用于阈值与面积判据
- docs(test): 新增全局变量运行快照端到端验证执行记录
- chore(release): 记录 1.0.1.0 内测版发布产物
- docs(release): 新增内测版发布与版本管理最终方案
- chore(rules): 新增 rule-022 版本单一真源并落地版本发布技能
- chore(release): 引入版本单一真源与自动推导发布流水线
- docs(方案设计): 新增参数契约与工具插件分层最终方案（总纲）
- docs(方案设计): A/B 子方案标注主从关系并上收待确认事项
- docs(方案设计): 更新方案文档，补充实测证据与判据落地依据
- docs(方案设计): 目标参数与全局变量绑定UI优化方案定稿（含可绑目标判据v3）
- docs(方案设计): 目标参数与全局变量绑定UI优化方案更新为最终版
- docs(方案设计): 新增目标参数与全局变量绑定UI优化方案
- docs(collab): 记录安装包重编译结果、图标验证并更正两处判断
- docs(workflow): 工作流运行引擎重构设计与审计文档
- docs(collab): 记录 Release 重建与安装包重打包结果
- chore(release): 生成 1.0.0 安装包并记录产物
- chore(collab): 落地 CodeBuddy 协作规则 rule-021 与协作文档
- docs: 更新验收总计划运行时项进展
- build(installer): 打包脚本与 HALCON 托管程序集部署

### 交付物

- 安装包：`SunEyeVision-Setup-2.0.0.1-alpha-x64.exe`
- 发布清单：`release-manifest-2.0.0.1.json`
- SHA256：`1ECCC8FCC22D75AC510B655897B332AA9B16750EE7BF0B487C1D3DA399F17C0B`
- 载荷：308 个文件 / 608.7 MB


## 版本索引

| 版本 | 渠道 | 日期 | 说明 |
|---|---|---|---|
| （待发布）1.0.0-alpha.1 | alpha | 2026-09-18 | 首个内测版：发布链路与版本真源落地 |



