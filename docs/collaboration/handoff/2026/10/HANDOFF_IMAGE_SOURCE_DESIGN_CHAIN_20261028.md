# 图像源参数页同组装链交接

- 基线提交：3aef1aa1；分支codex/workflow-toolbox-dataflow；87f4唯一写者root。
- 范围：仅新增Plugin.Wpf指定ImageSources视觉、工具Wpf设计/实际组装与UI源适配；未动共享合同/C14/Blob/Group/主仓Release。
- 真实差异：原设计附加到内层DeclaredSettingsView直接Bind，绕外层_source/Changed；plainStrip无法随相机模式切换，不能视为真实独立设计。
- 实现：ImageSourceInputView抽现有源条、NativeInputFactory枚举、状态与管理按钮同一视觉。运行适配仍持唯一Runtime draft投影、正式源Session、当前设备枚举/状态计时器及管理动作。设计适配使用同Runtime factory/draft和只读session，不连接相机、不导入或执行；相机管理禁用。
- 外层ImageSourceParameterEditor公开Bind供构造和设计同链，先解绑旧Changed，prepare/accept捕获同代次/实际draft/source身份；关闭使旧回调失效。Editor继续唯一拥有附着InputContent的Dispose，不增加第二所有权。默认本地设计与ImageSourceCameraDesignView均使用同真实编辑器，删除内层附加样例路径。
- 验证：五C#纯语法、两XAML XML与diff检查通过；未语义编译、WPF真实显示、VS实际设计器或人工验收。需下一主仓集中UI/full构建及同源图像源轨验证；不能套用旧45项成绩。
- 待验：两态真实Designer、标量/source双向Changed、重绑关闭旧回调拒绝、InputContent一次释放、深浅/宽窄/DPI/禁用状态；硬件另列。完整结果Shell/C14新合同接入仍另在同目标内，不以本单宣称目标完成。
