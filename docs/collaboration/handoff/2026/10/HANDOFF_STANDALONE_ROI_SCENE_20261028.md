# 同链独立ROI设计与运行场景本批交接

- 基线提交：f9f60b8e；本文件所在中文提交是本批检查点，不冒称完整目标。
- 当前分支：main；工作流继续独立worktree。
- 当前执行者：Codex主线唯一产品写者，三路只读核证和评审。
- 任务目标：真实原参数/图像同Scene独立设计入口及生命周期；Blob业务冻结。
- 允许修改目录：Plugin.Wpf及配套验证、原方案。
- 已修改文件：NativeRoiScene、Design/NativeRoiScene.xaml、NativeRoiDesignData.cs三个新增文件；原数值Base/Point一次布局初始化；Editor/Viewport设计就绪与首帧；验证/原O06/Catalog。
- 已完成：真实ParamListView行、原Editor/Viewport借同Session，绑定逐代校验、异常续清理、不销毁借用会话；设计owner只依赖Contracts，不复制Blob模型或默认；SampleIndex=0矩形/1圆环/2多边形/3扇形，ExpandedParameters走原expand，四形状完整真实数字和首帧画面。
- 未完成：真实VS设计器/VM同态/物理指针及用户验收；绘制/继承/位姿、多对象复制删除粘贴与宿主历史。不是完整C08/C17或阶段一，不进入Blob第二阶段。
- 已执行构建与测试：UI006和全量002零错误；Window110=64、Field073=31、Design036=69、RuntimeEditor054=10、FullChain114=8、Components196=9共191组；正式EXE008 exit0/22可用工具15分类；8产品源增量、782发布项、86加载记录、141原来源SHA核对。证据O06_STANDALONE_ROI_SCENE_CHECKPOINT_20261028.json。
- 人工验收结果：最终036四形状PNG已逐张查看；不代真实VS/VM/用户验收。
- 已知风险或禁止改动：用户BlobParameterEditor.xaml页索引2与SHA保持不变、不提交；Catalog最终只元数据更新，产品源在最终构建后不变。保留所有失败和超时日志，Window108进程已由验证器终止；全部自有句柄终止，不推送。
- 下一步：原绘制完成在两个AddROI前统一截口，取消先失效再退委托/捕获，静态状态改每实例租约；显式Create/Replace+Affected稳定身份一次宿主接纳，选中在安装通知前完成。原Delete/Paste也先改集合后通知，必须提前路由；不复用哈希或IROIClone业务后备，不发明原Undo菜单。
