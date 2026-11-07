# 托管程序集反编译（按职责命名）

- 来源：本机 `D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\` 下的 `<Plugin>Cs.dll`
- 工具：`ilspycmd`（ICSharpCode.Decompiler）
- 用途：内部对照，用于确认参数模型、结果模型、工具入口与显示配置
- 边界：**只有托管包装，不含算法本体**。算法在原生 `<Plugin>.dll`（无托管元数据），需汇编级分析
- 不分发：反编译产物仅为内部逆向对照，不对外分发，也不作为产品源码

| 工具卡 | 原厂程序集 | SHA-256 | 职责目录 | .cs 文件数 | 抓取时间 |
|---|---|---|---|---|---|
| `imvsblobfindmodu` | `IMVSBlobFindModuCs.dll` | `3106AF2E9344A972…` | `BlobFind/` | 5 | 2026-11-05 21:29 |
| `imvscalipermodu` | `IMVSCaliperModuCs.dll` | `EBADE8A3714A524B…` | `Caliper/` | 5 | 2026-11-05 21:29 |
| `imvscirclefindmodu` | `IMVSCircleFindModuCs.dll` | `21E5C345FFB92921…` | `CircleFind/` | 5 | 2026-11-05 21:29 |
| `imvsfastfeaturematchmodu` | `IMVSFastFeatureMatchModuCs.dll` | `7139D13E81F41D5A…` | `FastFeatureMatch/` | 8 | 2026-11-05 21:29 |
| `imvslinefindmodu` | `IMVSLineFindModuCs.dll` | `E31FEA7302D5AD52…` | `LineFind/` | 5 | 2026-11-05 21:29 |
