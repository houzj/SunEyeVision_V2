---
name: 方案调研
description: >
  方案调研技能。定位为「方案设计」的前置阶段。
  出方案前必须先完成三类调研：源码实现调研（成熟开源项目如何实现同类问题）、
  架构方案调研（业界 3+ 框架的官方文档与设计思路）、
  代码库现状调研（当前项目的真实上下文与已有约束）。
  调研结论必须有可追溯引用（URL+锚点+源码行号），
  输出独立调研报告沉淀到 .codebuddy/research/ 目录，可跨方案复用。
  然后移交「方案设计」技能基于调研报告出方案。

  Trigger keywords: 方案调研/调研/竞品调研/技术调研/行业对标/源码调研/
  业界对标/技术选型调研/可行性调研/参考实现

  DO NOT trigger on: 简单编码、bug 修复、单文件改动、纯文档阅读。
---

# 方案调研 — 方案设计的前置阶段

> 本技能是「方案设计」的前置依赖，避免方案设计陷入"以现有实现为基线做调研"的死循环。
> 调研必须从"成熟开源项目 + 官方文档"开始，而非从"我们的实现"开始。
> 任何"先出方案再说"、"以我们的实现为基线"、"调研做不做都行"的判断均视为违规。

## 目的

解决方案设计阶段的 3 类典型陷阱：

1. **以自己为基线** — 看到现有实现就以为找到了"标准做法"，没去查成熟项目怎么做的
2. **泛泛而谈** — 引用"业界最佳实践"但没说哪家、哪份文档、什么特征
3. **调研不沉淀** — 每次方案设计都从零搜一遍，浪费 token 也无法复用

工作流产出：

- **调研报告**（独立 Markdown 文件）— 沉淀到 `.codebuddy/research/<topic>.md`
- **可跨方案复用** — 同主题后续方案直接引用本报告，无需重做调研
- **可追溯引用** — 每个结论带 URL+锚点 或 path/file.cs:行号

---

## 工作流 — 五阶段调研

### ━━━ Phase R0: 调研需求定义（强制前置）━━━

> **在搜索任何资料之前，先想清楚要查什么、为什么查、查到什么程度。**

#### Step 1 — 拆解调研主题

按顺序回答：

1. **方案设计的核心问题是什么？** — 用户的功能性视角原话
2. **这个问题在业界属于哪个领域？** — 通用 Agent 框架 / 协议层 / WPF 桌面 / 搜索/抓取 / 数据库 / 错误处理 / ...
3. **调研要回答的关键问题清单** — 列出 3~7 个 yes/no 或可对比的问题
4. **调研完成判据** — 满足什么条件可以停止调研

示例：

```markdown
## 调研主题：RAG 混合检索（BM25 + 向量）架构

**核心问题**: BM25 与向量检索的融合方式（并行 vs 串行，权重如何分配）

**领域**: 搜索/检索 / RAG

**关键问题清单**:
1. Elasticsearch / Qdrant / Weaviate / Milvus 等向量库是否支持 hybrid search 原语？
2. 融合算法 RRF（Reciprocal Rank Fusion）的标准实现是什么？
3. BM25 与向量是否应该并行执行？
4. 倒排索引（inverted index）的标准预计算结构是什么？
5. 主流 RAG 框架（LangChain / LlamaIndex / Haystack）怎么做 hybrid search？

**完成判据**:
- 找到 3+ 主流框架的 hybrid search 实现源码或官方文档
- 每种实现的关键参数（并行/串行、融合算法、权重）有可追溯引用
- 形成"业界做法"对比表
```

#### Step 2 — 调研分类判定

按主题判定本调研属于哪一类（可多选）：

| 调研类型 | 何时触发 | 关键产出 |
|---------|---------|---------|
| **源码实现调研** | 想知道"成熟项目具体怎么写" | 源码片段 + 行号 + 注释 |
| **架构方案调研** | 想知道"业界主流方案有哪些" | 框架对比表 + 关键设计决策 |
| **代码库现状调研** | 想知道"我们项目里现在是怎么做的" | path/file.cs:行号 + 上下文 |
| **官方文档调研** | 想知道"标准/规范是怎么定义的" | URL+锚点 + 原文摘录 |

> **本项目的方案设计 skill 中 Phase 1（代码库现状调研）会复用本技能的"代码库现状调研"子模块。**

#### Step 3 — 调研资源列表

列出本次要访问的资源池：

```markdown
## 调研资源池

### 源码资源
- LangChain: github.com/langchain-ai/langchain → langchain/retrievers/ 目录
- LlamaIndex: github.com/run-llama/llama_index → llama-index-core/retrievers/
- Haystack: github.com/deepset-ai/haystack → haystack/components/retrievers/
- Elasticsearch: github.com/elastic/elasticsearch → server/src/main/java/org/elasticsearch/search/

### 文档资源
- Elasticsearch hybrid search 文档：elastic.co/guide/...
- Qdrant prefetch 文档：qdrant.tech/documentation/...
- LangChain4j RRF 文档：docs.langchain4j.dev/...

### 项目现状资源
- search_content "BM25" → 找到 LocalKnowledgeBase.cs
- search_content "FuseRrf" → 找到融合函数位置
```

---

### ━━━ Phase R1: 源码实现调研（第一优先）━━━

> **核心原则**：先看成熟项目怎么写，再看文档怎么说，最后看我们怎么做。
> 顺序不能颠倒 — 颠倒就回到"以我们为基线"的陷阱。

#### Step 1 — 候选项目选择

按主题选 3+ 个成熟项目：

| 领域 | 候选项目 |
|------|---------|
| RAG / 检索 | LangChain / LlamaIndex / Haystack / Vespa / Weaviate |
| 向量数据库 | Qdrant / Milvus / Weaviate / Pinecone / Chroma |
| 搜索引擎 | Elasticsearch / OpenSearch / Meilisearch / Typesense |
| WPF / .NET 桌面 | Avalonia UI / CommunityToolkit.Mvvm / Prism / Caliburn.Micro |
| Agent 框架 | Anthropic Agent SDK / LangGraph / CrewAI / AutoGen |
| 错误处理 / 重试 | Polly / Resilience4j / Tenacity |
| 日志框架 | Serilog / NLog / log4net / Microsoft.Extensions.Logging |
| 协议 | MCP / OpenAI Function Calling / Anthropic Tool Use |

#### Step 2 — 源码定位与阅读

对每个候选项目：

1. 用 `web_fetch` 或 `web_search` 定位关键文件路径
2. 用 `web_fetch` 拉取源码（GitHub raw URL 或 raw.githubusercontent.com）
3. 精读关键类/方法，记录：
   - 入口方法签名
   - 关键算法/数据结构
   - 并发模型（并行/串行）
   - 错误处理
   - 配置/参数

#### Step 3 — 源码引用格式

每个引用必须包含：

```markdown
- [来源] github.com/langchain-ai/langchain/blob/master/libs/community/langchain_community/retrievers/bm25.py:42-78
  - **做法**: BM25Retriever 使用 rank_bm25 库，初始化时一次性建好倒排索引
  - **关键点**: from_documents() 中预处理 tokenize + 计算 IDF，避免查询时重复算
  - **对我们的启示**: 倒排索引预计算 vs 查询时 O(N) 计算的差异
```

#### Step 4 — 源码引用 5 元素

每条源码引用必须包含 5 个元素：

1. **URL 或 path** — 唯一可定位
2. **行号范围** — 不是整文件
3. **做法摘要** — 一句话说清这段代码做了什么
4. **关键决策** — 为什么这样写（设计意图）
5. **对方案的启示** — 借鉴/不借鉴/折中

---

### ━━━ Phase R2: 架构方案调研（第二优先）━━━

> 当源码调研不能完全回答"业界标准做法"时，必须辅以官方文档与权威博客。

#### Step 1 — 主流框架官方文档

对每个候选项目：

1. 找到官方文档对应章节（不是泛泛的官网）
2. 记录**章节锚点**（#anchor 或 URL 片段）
3. 摘录**关键定义/原话**（不是 LLM 转述）
4. 记录**对我们的启示**

#### Step 2 — 权威博客/论文补充

- 优先：项目官方博客、作者发表的设计文章
- 次优：Google Research / Microsoft Research / 各厂技术博客
- 谨慎：Medium / 知乎 / CSDN — 仅作辅助参考，必须二次确认

#### Step 3 — 文档引用格式

```markdown
- [来源] https://www.elastic.co/guide/en/elasticsearch/reference/current/rrf.html
  - **标题**: Reciprocal Rank Fusion
  - **关键定义**: RRF combines rankings by assigning 1/(k+rank) to each document, then summing across rankings. Default k=60.
  - **对我们的启示**: 我们的 FuseRrf 用 1/(60+rank) 是标准做法，参数无需调整
```

---

### ━━━ Phase R3: 代码库现状调研（第三优先）━━━

> **这是"我们当前怎么做的"调研**，用于在出方案时明确起点和约束。
> 注意：这是调研的最后一步，不是第一步。

#### Step 1 — 项目内搜索策略

| 调研维度 | 搜索方法 | 目的 |
|---------|---------|------|
| 涉及文件定位 | search_file / search_content | 确定修改波及的文件 |
| 类/接口定义 | search_content | 找到类定义、接口签名、基类 |
| 调用链 | search_content 搜索调用方 | 确认修改的影响范围 |
| XAML 绑定 | search_content | 了解 UI 绑定和资源引用 |
| Rule 规则 | search_content | 确认 Rule 文件中的约束 |
| 现有 Skill | search_file / search_content | 复用现有技能模式 |
| 同类 bug 模式 | search_content pattern 搜索 | 方案设计 Phase 0.5 用 |

#### Step 2 — 引用格式

```markdown
- [来源] AgentWpf/Services/LocalKnowledgeBase.cs:293-338 — SearchAsync 当前实现
- [来源] AgentWpf/Services/LocalKnowledgeBase.cs:514-602 — FuseRrf 与 ScoreBm25 实现
```

> **重要**：项目 .gitignore 第 174 行 /dev/ 屏蔽整个构建输出目录。
> 涉及 dev/ 下任何文件操作，必须用 `execute_command` + PowerShell（Get-ChildItem 模式）。
> 源码文件（.cs/.xaml/.csproj）不在 gitignore 范围内，search_file 仍有效。

---

### ━━━ Phase R4: 调研报告输出（强制）━━━

> **核心原则**：调研必须形成独立可复用的报告，不能只在对话里过一遍。

#### Step 1 — 报告保存位置

```
.codebuddy/research/
├── README.md                              # 调研报告索引
├── 2026-07-16_rag-hybrid-search.md        # 主题_日期.md
├── 2026-07-12_kb-search-perf.md
├── ...
```

#### Step 2 — 报告模板

```markdown
# 调研报告：<主题>

- **调研日期**: YYYY-MM-DD
- **调研者**: <Agent 名 / 人工>
- **关联方案**: <方案名（如有）>
- **状态**: 进行中 / 已完成 / 已过时

## 1. 调研背景

<为什么做这个调研，要回答什么核心问题>

## 2. 关键问题清单

1. ...
2. ...

## 3. 源码实现调研

### 3.1 项目 A（如 LangChain）
- [来源] github.com/.../bm25.py:42-78
  - 做法: ...
  - 关键决策: ...
  - 启示: ...

### 3.2 项目 B（如 LlamaIndex）
...

## 4. 架构方案调研

### 4.1 官方文档对比
| 框架 | 文档章节 | 关键做法 | URL |
|------|---------|---------|-----|
| ... | ... | ... | ... |

## 5. 代码库现状

### 5.1 当前实现
- [来源] path/file.cs:行号 — 说明

### 5.2 现有约束
- 约束 1: ...
- 约束 2: ...

## 6. 业界做法对比表

| 维度 | 方案 A | 方案 B | 方案 C | 我们当前 |
|------|--------|--------|--------|---------|
| 并行/串行 | 并行 | 串行 | 并行 | 串行 |
| 融合算法 | RRF | 线性加权 | RRF | RRF |
| 倒排索引 | 预计算 | 查询时算 | 预计算 | 查询时算 |
| ... | ... | ... | ... | ... |

## 7. 结论与建议

### 7.1 业界共识
- 共识 1: ...
- 共识 2: ...

### 7.2 业界分歧
- 分歧 1: ...

### 7.3 对方案的硬性约束
- 约束 1: <必须满足>
- 约束 2: <应当满足>

### 7.4 后续行动
- [ ] 行动 1 — 移交「方案设计」技能
- [ ] 行动 2 — 更新 README.md 索引

## 8. 参考资料

### 源码引用
- [1] github.com/.../bm25.py:42-78 — LangChain BM25Retriever 实现
- [2] github.com/.../base.py:120-180 — LlamaIndex BaseRetriever

### 文档引用
- [3] https://www.elastic.co/guide/.../rrf.html — RRF 标准定义
- [4] https://qdrant.tech/.../hybrid-search/ — Qdrant hybrid search 文档

### 项目内引用
- [5] AgentWpf/Services/LocalKnowledgeBase.cs:293-338 — 当前 SearchAsync 实现
```

#### Step 3 — README 索引更新

每次新增报告，必须更新 `.codebuddy/research/README.md`：

```markdown
# 调研报告索引

| 日期 | 主题 | 关联方案 | 状态 |
|------|------|---------|------|
| 2026-07-16 | RAG 混合检索架构 | 知识库查询卡死 | 已完成 |
| 2026-07-12 | 知识库搜索性能 | 知识库查询卡死 | 已完成 |

## 主题分类索引

### RAG / 检索
- 2026-07-16 RAG 混合检索架构
```

---

## 自检清单（输出前必跑）

```
□ R0. 调研主题、关键问题清单、完成判据已明确
□ R1. 源码实现调研 3+ 成熟项目，每条引用含 URL+行号+做法+决策+启示
□ R2. 架构方案调研 3+ 官方文档/权威来源，每条引用含 URL+锚点+原话
□ R3. 代码库现状调研覆盖定义/实现/调用方/UI绑定/同类 pattern
□ R4. 调研报告已沉淀到 .codebuddy/research/<topic>.md
□ R5. README.md 索引已更新
□ R6. 业界做法对比表已输出
□ R7. 结论与建议章节有"对方案的硬性约束"小节
□ R8. 调研报告可独立被方案设计技能引用，不依赖对话上下文
```

任一项未通过 → 补充调研，不要移交方案设计。

---

## 移交方案设计

调研报告完成后：

1. 在对话中告知用户"调研报告已生成于 .codebuddy/research/<topic>.md"
2. 移交「方案设计」技能，方案设计应：
   - 直接读取调研报告作为 Phase 0.7（业界对标）和 Phase 1（信息调研）的输入
   - 引用格式如 `[调研报告 §3.1] LangChain BM25Retriever 实现`
   - 避免重复做已沉淀的调研

## 与方案设计技能的关系

```
┌────────────────────┐
│  本技能（方案调研） │  ← 前置：先做调研
└──────────┬─────────┘
           │ 输出调研报告
           ▼
┌────────────────────┐
│  方案设计          │  ← 基于调研报告出方案
└──────────┬─────────┘
           │ 输出方案报告
           ▼
┌────────────────────┐
│  方案实施          │  ← 执行方案
└──────────┬─────────┘
           │ 输出实施结果
           ▼
┌────────────────────┐
│  方案测试          │  ← 全链路验证
└────────────────────┘
```

## 注意事项

### 避免的陷阱

1. **以自己为基线** — 看到现有实现就以为找到了标准做法。必须先看成熟项目。
2. **泛泛引用** — "业界最佳实践"、"参考了某框架" → 违规。必须说哪家、哪份文档、什么特征。
3. **调研不沉淀** — 每次方案都从零搜一遍。必须输出独立报告，可跨方案复用。
4. **调研被方案设计绑架** — 方案设计进入"先出方案再说"循环。调研是独立前置阶段，不依附于方案设计。
5. **只搜英文** — 中文技术社区、GitHub README 中文版、官方文档中文版都可作为补充来源。

### 与"方案设计"内部调研的边界

| 职责 | 方案调研（本技能） | 方案设计 |
|------|------------------|---------|
| 调研起点 | 成熟项目 + 官方文档 | 调研报告 + 项目现状 |
| 核心产出 | 独立调研报告 | 方案报告 |
| 决策重点 | 业界共识 + 分歧 | 方案推荐 + 长期 ROI |
| 复用范围 | 跨方案可复用 | 单方案专用 |

## 参考

- skill:方案设计 — 调研完成后移交方案设计
- skill:方案实施 — 方案通过后执行
- skill:方案测试 — 实施完成后全链路验证
- rule:agent-025 — 方案输出规则（最佳实践导向）
- rule:agent-024 — 测试分层规则
- rule:agent-022 — 禁止防御性编程
