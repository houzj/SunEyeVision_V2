# G0 前置条件状态（2026-11-21）

## 已完成

- 原厂 VisionMaster 运行中，参数窗口「1 轮廓匹配」已打开（`w-6495078`，420×500）。
- 参数窗口四 tab 齐全（基本参数/特征模板/运行参数/结果显示），有「执行」「连续执行」按钮。
- 原厂 DLL 哈希已锁定：
  - `IMVSContourMatchModu.dll` = `56156A8707FA92CE758485772BEB18A5289CB97599533D86897A9391817FCF64`
  - `IMVSContourMatchModuControl.dll` = `67F4FA218C54EC9203DE8E38D21AF14A0F55C9054A079791026F84F430FAD461`
  - `IMVSContourMatchModuCs.dll` = `AEE906888A4C9CF6DF5917828665DEE83B1EA06C02F3A02E214CD9787FB96B80`
- 证据：`ORIGINAL-current.png`（参数窗口）、`ORIGINAL-featuretab.png`（特征模板 tab）、`current-param-window.json`。

## 前置条件已满足（用户 2026-11-21 配置 test2）

用户已配置 `C:\Users\Administrator\Desktop\test2.sol`（SHA256 `3EE3721841712CDC5CF6C74A9280B82AF2416771B4294E849DE107225AB6F05F`）：
- 流程：0图像源 → 1轮廓匹配
- 模板：`新建模板1` 已训练并勾选加入搜索列表（见 `G0-param-open.png`）
- 图像：`2.jpg`（序列 2/12），1280×960

G0 已执行并落盘，见 `G0-default/README.md`。

## 下一步

验证字段映射与坐标口径（角度单位存疑 -0.0059），再进入 G1/G2/G4/G3。
