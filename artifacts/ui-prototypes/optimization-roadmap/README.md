# 优化总看板

唯一维护源：docs/collaboration/OPTIMIZATION_BACKLOG.json。当前清单非全量问题审计，不能计算全产品完成率。

更新清单后在仓库根目录运行：

    node artifacts/ui-prototypes/optimization-roadmap/build.cjs
    node artifacts/ui-prototypes/optimization-roadmap/check.cjs

build只读取清单生成HTML及build-manifest；页面使用内嵌数据，离线file协议可用，不写回JSON。check仅验页面/清单结构，不构建或运行产品。来源SHA为收集时快照，后续代码变化需重核证据。

B1未批准产品实施。未来测试结果/角色接单状态由主控收回后更新清单；报告回传不等于自动验收。
