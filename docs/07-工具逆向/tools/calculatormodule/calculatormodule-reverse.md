# 变量计算 工具逆向主文档

- 稳定工具 ID：calculatormodule
- 原厂工具：CalculatorModule
- 当前状态：inventory-only；本轮补齐资源/UI/字段证据

## 已确认
- 输入字段：
- 输出字段：ModuRunTime, ResultShow, ModuStatus, %var0%
- 参数：
- 控件：CalculatorPro, command#restvalue
 - 资源与 SHA-256：resources.json

## 结果与生命周期边界
- Process/ProcessAlg、结果写回、判定、显示发布、保存恢复、reader/释放均未取得工具专属函数体，保持 unknown。
- UI 预览为 XML 字段结构示意，不是原厂桌面截图或运行时等价证明。

## 附件
evidence.json、parameters.json、resources.json、algorithm-semantics.md、ui-preview.json、ui-preview.svg。
