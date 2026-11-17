---
name: 逆向对标
description: 原厂（VisionMaster）逆向对标技能。用于任何"和原厂对齐"的需求：节点/画布交互、参数页、状态语义、视觉样式、数据结构。强制先取证再改：证据分级（反编译源码行号 > 原厂配置文件 > 原厂实机截图），证据写入对照文档，按证据实施，实测回归。触发场景：对标原厂、逆向分析、原厂怎么实现、样式/交互/状态不一致、抄原厂、对照差异、执行防呆、节点形状、端口、连线、框选、拖拽。
---

# 逆向对标技能

把"看起来像"变成"证据证明它是"。**任何对标改动，先取证，再动手。**

## 1 触发场景

- 用户指某处"和原厂不一样"（样式/交互/状态/文案/流程）
- 需要产出原厂对照清单或差异表
- 需要判断某个行为"原厂到底怎么做"
- 正在实现某个原厂已有功能（画布、节点、参数页、执行、结果呈现）

## 2 铁律（违反即返工）

1. **无证据不修改**。没有源码/配置级证据时，只能提出取证请求，不能改代码。
2. **截图只是线索**。目测像素差异只能用于"发现差异"，定性必须落到源码行号。
3. **不把推断写成结论**。文档里区分"已证实 / 待取证"。
4. **证据与代码同批提交**（commit 里含文档或注释中的行号引用）。
5. **不靠用户喂图找问题**。要自己用"源码规格 → 逐项比对 → 产出差异"的方式主动找。

## 3 证据来源（本仓库固定路径）

| 级别 | 来源 | 路径 |
|---|---|---|
| ① | 原厂反编译源码（优先） | `docs/07-工具逆向/decompiled/` |
| ② | 原厂配置文件 | `C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\<模块>\` 下的 `ToolItemInfo.xml`（`displayType`）、`*AlgorithmTab.xml`、`*Cs.xml`；`decompiled/ColorFun/colorstyle*.xaml`（色值）；`decompiled/Lang/zh-cn.xaml`（文案） |
| ③ | 原厂实机截图 | 运行原厂程序，agent-desktop 截图 |

反编译目录里与画布最相关的文件：

```
WpfControls/base/diagramitemtemplate.xaml        节点 5 套模板 + 全部状态触发器
WpfControls/VMControls.WPF.Base/DiagramItem.xaml.cs    节点容器：拖动/端口/菜单
WpfControls/VMControls.WPF.Base/ItemsPage.cs           画布 ListView：框选/落点/空白点击
WpfControls/VMControls.WPF.Base/DiagramPage.xaml.cs    框选面板、批量移动、寻路
WpfControls/VMControls.WPF.Base/DiagramArea.xaml.cs    画布外壳、缩放、锁定
WpfControls/VMControls.WPF.Base/Connector.xaml(.cs)    端口
WpfControls/VMControls.WPF.Base/Connection.xaml.cs     连线
Controls/VM.Controls.Core/UIElementAdorner.cs          Adorner 定位机制（SetOffsets/GetDesiredTransform）
base/connection/connectionstyle.xaml                   连线样式
base/rectdragpanalbasestyle.xaml                       框选虚线框样式
```

## 4 五步流程

```
1 取证    grep 到具体行号 / 读配置项 / （必要时）截原厂实机图
2 定基线  写进 docs/collaboration/acceptance/<主题>.md：证据表（文件:行号 → 行为/数值）
3 对照   逐项比对现有实现，产出"已一致 / 未实现 / 不一致"三分类清单
4 实施   一次改一项，每项都能指出对应的证据行；改完立即构建
5 回归   能自动验证的自动验证；不能的（OLE 拖拽、需手势）明确交回用户验，并说明原因
```

## 5 取证手册

### 5.1 模板 / 样式类（XAML）

```powershell
# 找某状态触发了哪些设置
Select-String -Path <xaml> -Pattern 'IsModuleFailed|IsModuleForbidden|RunState' -Context 0,14
# 提取全部 TargetName/Property/Value（可程序化生成对照表）
Select-String -Path <xaml> -Pattern 'TargetName="([^"]+)" Property="([^"]+)" Value="([^"]*)"'
# 找几何定义
Select-String -Path <xaml> -Pattern 'PathGeometry x:Key=|NormalModulePathStyle'
```

### 5.2 行为链路类（CS）

先找入口事件，再顺着调用链读，别单点看：

```powershell
Select-String -Path <cs> -Pattern 'OnMouseLeftButtonDown|PreviewMouseMove|OnDrop|IsMouseDown' -Context 0,12
```

必须确认的三件事：**谁触发、改了谁、何时提交**。

### 5.3 类型/配置类（XML）

```powershell
# 节点形状由模块声明的 displayType 决定；未声明 = DEFAULT
Get-ChildItem '<模块目录>' -Filter ToolItemInfo.xml | ForEach-Object { [xml](Get-Content $_) }
```

### 5.4 色值 / 文案

```powershell
Select-String -Path docs/07-工具逆向/decompiled/ColorFun/colorstyle1.xaml -Pattern 'Col_EditProcess_'
Select-String -Path docs/07-工具逆向/decompiled/Lang/zh-cn.xaml -Pattern 'Tab_|Lang_'
```

## 6 已验证的原厂核心机制（可直接引用，不必重查）

| 机制 | 证据 | 要点 |
|---|---|---|
| 节点模板族 | `diagramitemtemplate.xaml:1789-1848` | Style 按 `DisplayType` 切 Template（NORMAL/Square/PROCESS/GROUP/NONE） |
| 节点形状 | 同文件 `1166-1181` | 模板内按 `TargetDisplayType` 换 Path.Data；IFBRANCH 同时把高度改 40 |
| 节点状态 | 同文件 `1182-1234` | OK 绿 / NG 红 / 配置错误灰虚线 3,2 / 禁用灰渐变 / 失败米色渐变+文字0.4 / 选中橙实线 |
| 端口 | `Connector.xaml` | 20×30 命中区，5×8 橙条 #FFA719，三边描边，悬停 8.5×12 |
| 端口出现 | `diagramitemtemplate.xaml:1152-1161` | 仅节点 `IsHited && !IsLockWorkArea` 时注入 |
| 空白点击 | `ItemsPage.cs:582-592` | 只记录框选起点，**不清除选中**（重写不调 base） |
| 框选 | `ItemsPage.cs:642-651` + `DiagramPage.xaml.cs:382` | 只在松开时收集框内节点，从不改 IsSelected；框选是 CurBoxOfItems 机制 |
| 拖动预览 | `DiagramItem.xaml.cs:654-672` | 真实控件 + 0.7 VisualBrush，放进 AdornerPanel |
| 拖动位移 | `UIElementAdorner.GetDesiredTransform` | 叠加 `TranslateTransform`，`SetOffsets` 后 `AdornerLayer.Update` |
| 拖动吸附 | `DiagramItem.xaml.cs:762-788` | 3px 死区 + 5px 量化 + 只在变化时重绘 |
| 落点 | `DiagramItem.xaml.cs:727` | 直接取预览 offset，无二次取整 |
| 对齐线 | `DiagramItem.xaml.cs:855-944` | 6 组精确相等，LightGreen 1px 虚线 5,2.5，两端留 5px |
| 执行防呆 | 原厂实机截图 | 参数未配置完整 → 弹「流程:1:3 节点名 模块参数未配置完整」并中止 |

## 7 已知陷阱（真实踩过）

| 陷阱 | 表现 | 正确做法 |
|---|---|---|
| 先改后取证 | 改完再找依据，方向全错（把预览改成"连续跟随"） | 先读源码，再定实现 |
| 用我方截图当标准 | 把"颜色抽取是胶囊"当成原厂事实 | 查 `ToolItemInfo.xml` 的 displayType |
| 看错对象 | 对照 `ExecutionPortShape` 得出"端口配色不一致"，实际渲染是 `ConnectionPort` 模板 | 先确认运行时真正用的控件/模板 |
| 状态多份不同步 | `Ghosts` 与 `GhostMap` 两套状态，一处清空另一处没清 → 预览永久消失 | 生命周期收成一个对象（Ensure/Update/Dispose） |
| 每帧改视觉树 | 每帧 Remove/Add Children → 拖动卡顿 | 只建一次，之后只改变换；Adorner 层不参与画布布局 |
| 把两层压成一层 | 节点尺寸/布局塞进投影、形状塞进模板触发器，互相牵制 | 按原厂分层：模板族定布局，模板内定形状 |

## 8 交付物格式

```markdown
## 证据
| 项 | 原厂值 | 证据 |
|---|---|---|
| … | … | diagramitemtemplate.xaml:1182 |

## 对照
| 项 | 原厂 | 我方 | 判定 |
|---|---|---|---|

## 结论
已一致 / 未实现 / 不一致（并给出实施顺序）
```

## 9 完成检查

- [ ] 每条结论都有 `文件:行号`
- [ ] 文档区分"已证实 / 待取证"
- [ ] 未实现项列入清单，标注优先级
- [ ] 构建 0 错误、Release 产物已更新
- [ ] 无法自动化验证的部分，明确说明原因并交给用户验
