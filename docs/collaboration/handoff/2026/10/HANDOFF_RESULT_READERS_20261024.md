# 冻结值按需读取有限片

- 基线：a45f8d6ae59064faaa66ed168408d3d2e282259c。唯一主树writer Codex。
- 原任务：FT-J02/J03/L01公共结果三投影依赖，不新增批次/程序集策略/Manager。
- 修改：Contracts新增DeclaredValueReader；InputValues.Read(portId)、RecordValue.Read(fieldId)、SequenceValue.Read(index)在已冻结值上导航。声明不变，Get/CopyValues原复制行为不变。不暴露原始数组、object或borrowed图像。
- 准确API：DeclaredValueReader.Declaration、IsMissing、IsNull、Count（Sequence）、GetElement(index)、GetField(fieldId)、GetScalar<T>()（bool/long/double/string）。缺失与显式null分开；非声明字段拒绝；数组按单元素读，不全Clone。
- 验证：result-readers-01/foundation-build01.log编译成功；foundation-report01.json真实51通过0失败0blocked。新增大100000元素页读取256项分配小于256KiB，输入原数组变动及旧Get副本变动不影响冻结reader；嵌套数组、边界、missing/null控通过。用于验证按页行为，非正式性能验收。
- 资源：reader是借用的只读视图，资源分支无直接获取能力。资源聚合根的存活/关闭仍须指定run reader lease桥；不得把这一纯值API计资源完整。
- 下一步：独立presentation片只消费本已提交API；主线继续正式Schema/指定run/资源桥和真实Blob，不停交接。binding-production-03已固定，不写回。
