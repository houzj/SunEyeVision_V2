# If 原厂动态控件静态证据摘要

目标程序集：`IfBranchModuleControl.dll`

原厂资源模板路径：`datatemplate/paramusercontroldatatemplate.xaml`

原厂行模板的列结构：

1. `RealNameA`
2. `FilterSelecter`
3. `Floatbetween`
4. `State`
5. `RunModuleId`
6. 删除按钮

容器模板使用 `ItemsControl` 绑定 `ItemsA`，底部使用类型下拉框绑定 `SelectTypeIndex`，加号按钮绑定 `AddCommand`。容器滚轮事件绑定 `PreviewMouseWheelCommand`。

原厂动态行保存格式：

```text
name:min:max:state:runModuleId;
```

原厂 `GetParam` 从 `IfBranchText` 读取 UTF-8 字节并按分号、冒号解析。原厂 `SetParamUnit` 遍历可见动态行，将名称、范围、状态和运行模块 ID重新拼成该格式并写回 `IfBranchText`。

原厂动态类型：

- `int` 范围 `-999999999..999999999`
- `float` 范围 `-100000..100000`
- `state` 为 `OK=1`、`NG=2`
- 未选择运行模块为 `-1`
- 类型下拉选项为 `int`、`float`

原厂动态模块提供 `AddIO`、`RemoveIO`、`InitSource`、`GetParam`。本方已将 `IfBranchText` 以 `IfFields.BranchText` 内部设置投影接入编解码和持久化，且没有把它登记为算法数据输入端口。动态行专属面板、动态源选择和动态端口仍未完成，因此该摘要不能当作已完成的 If 同构实现。
