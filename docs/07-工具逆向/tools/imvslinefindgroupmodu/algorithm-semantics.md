# 直线查找组合 VM 算法语义

- 工具 ID：imvslinefindgroupmodu
- 组装类别：compound-control
- 优先级：P4
- 当前状态：inventory-only
- 语义状态：unknown（仅当下列步骤均有原厂证据后才可改为 source-confirmed 或 runtime-confirmed）

## 证据入口

- 尚未恢复工具专属源码/算法证据。

## 执行语义链

| 步骤 | VM 原厂证据 | 当前结论 |
|---|---|---|
| 工具创建/初始化 | 待恢复具体构造函数、注册表和资源绑定 | unknown |
| 参数读取与条件显隐 | 待恢复参数对象、绑定路径和触发器 | unknown |
| 输入/ROI/变量准备 | 待恢复输入节点、所有权和 reader 生命周期 | unknown |
| Process/ProcessAlg/原生桥 | 待恢复函数体、调用约定和返回码 | unknown |
| 分支、失败与取消 | 待恢复谓词、错误码、停止路径 | unknown |
| 结果写回与判定 | 待恢复结果结构、状态码和判定链 | unknown |
| 图形/文本显示发布 | 待恢复结果视图、DisplayType 和通知链 | unknown |
| 保存恢复 | 待恢复序列化键、版本迁移和重绑 | unknown |
| DeInit/Destroy/释放 | 待恢复对象所有权、reader lease 和释放顺序 | unknown |

## 规则

此文件只接受原厂反编译源码、资源、汇编/ABI或受控运行证据。产品意图、截图或自研实现不能单独证明 VM 算法语义。
