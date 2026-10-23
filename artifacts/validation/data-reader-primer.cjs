module.exports=()=>`<section class="primer" aria-labelledby="primer-title">
<h2>先用这张表认清：软件里究竟存了什么？</h2><p>看“内容”和“用途”即可；不需要先认识代码类型。同一张图像可能被多处使用，不代表每处都复制了一张。</p><div class="data-overview"><table><thead><tr><th>数据</th><th>里面是什么（例子）</th><th>从哪里来 → 交给谁</th><th>是否保存到方案</th></tr></thead><tbody>
<tr><th>流程配置</th><td>读图 → 阈值 → 显示，以及节点之间的连线</td><td>画布编辑 → 运行准备、方案文件</td><td>保存设计配置</td></tr>
<tr><th>工具参数</th><td>阈值100、图像文件路径、ROI范围</td><td>参数面板 → 本轮执行节点</td><td>保存配置；运行值另算</td></tr>
<tr><th>取值关系（绑定）</th><td>“阈值工具的图像，取自读图节点的输出”</td><td>绑定编辑 → 运行时查找对应输出</td><td>保存取值关系，不保存所指的像素</td></tr>
<tr><th>本轮输入</th><td>本次实际使用的阈值100和输入图像</td><td>配置副本、上游结果、设备 → 当前工具</td><td>不是方案配置本身</td></tr>
<tr><th>本轮结果</th><td>执行成功与否、测量值、处理后的图像</td><td>工具 → 本轮结果缓存 → 后续工具和界面</td><td>不能当作设计配置混存</td></tr>
<tr><th>显示内容</th><td>底图、圆线框、结果文字、结果树各行</td><td>本轮结果 → 主图、结果树、分析面板</td><td>显示内容与显示设置要区分</td></tr>
<tr><th>状态与日志</th><td>正在运行、耗时、错误原因</td><td>执行状态和诊断事件 → 状态栏、日志面板</td><td>不属于工具输出；日志存储另有路径</td></tr>
</tbody></table></div><p class="plain-rule"><b>最关键的关系：</b>配置说“怎么做”，绑定说“去哪里取”，本轮输入是“实际拿到的材料”，结果是“做出来的东西”，界面再把结果画出来。</p><h2 id="primer-title">先看一个例子：读图 → 阈值处理 → 显示结果</h2>
<p>下面用“阈值设为100”解释数据结构。名称和数值是教学示例，不是产品运行记录，也不是完整的文件格式。</p>
<div class="object-cards">
<article><span class="kind">① 编辑时保存的东西</span><h3>配置：记住你想怎么处理</h3><dl><dt>流程里有哪些节点？</dt><dd>读图节点A、阈值节点B</dd><dt>B的阈值是多少？</dt><dd>100</dd><dt>B的图像从哪里来？</dt><dd>记录“从A的图像输出取”，这里不存图像像素。</dd></dl><p class="plain-rule">配置像一张配方：记设置和取值位置。</p><details><summary>对应代码叫什么？</summary><p>Workflow / WorkflowNodeBase 保存流程和节点；ParamSetting、ParameterBindingDefinition 描述参数来源。SourceNodeId 指向来源节点；SourceProperty 指向输出属性；SourceFieldId 记录字段身份。</p><a href="#D01">看配置源码</a> · <a href="#D02">看绑定源码</a></details></article>
<article><span class="kind">② 点击运行后准备的东西</span><h3>本轮输入：这次实际用什么</h3><dl><dt>本轮用哪个阈值？</dt><dd>从配置复制出来的100。</dd><dt>本轮处理哪张图？</dt><dd>A本轮产出的图像，按绑定找到后交给B。</dd><dt>界面改成150会怎样？</dt><dd>配置副本用于隔开编辑和执行；数组、图像等引用对象仍要分别检查共享规则。</dd></dl><p class="plain-rule">运行输入像领到的材料：不只是配方上的地址。</p><details><summary>对应代码叫什么？</summary><p>ToolParameterSnapshot 复制配置；运行解析得到的值放入 RuntimeParameterSnapshot。后者复制字典容器，不会因此复制每张图像。</p><a href="#D03">看复制边界</a> · <a href="#D05">看进入算法的路径</a></details></article>
<article><span class="kind">③ 工具执行后留下的东西</span><h3>结果：算出了什么，交给谁</h3><dl><dt>B有没有执行成功？</dt><dd>节点执行结果记录成功或失败。</dd><dt>输出存在哪里？</dt><dd>本轮上下文按节点ID保存结果，后续节点再来读取。</dd><dt>图像和结果树是一份吗？</dt><dd>都来自结果，但像素、图形和树里的文字是不同的显示内容。</dd></dl><p class="plain-rule">结果像成品：下游和界面都可能还在使用。</p><details><summary>对应代码叫什么？</summary><p>WorkflowContext 通过 SetNodeResult / GetNodeResult 存取节点结果；ToolResults 描述工具输出。读取结果返回引用，不代表生成了一份独立副本。</p><a href="#D04">看结果保存</a> · <a href="#D06">看结果显示</a></details></article>
</div>
<aside class="reader-verdict"><b>现在可以确定什么？</b> 配置、运行输入、运行结果是三种不同用途的数据；不能混成一张字段表。<br><b>还没完全核实什么？</b> 各类图像在下游、显示和历史都用完后，由谁最终释放。不能把“界面清空”当作“内存已释放”。</aside>
<h2>接下来按你关心的问题看</h2><p>点下面六个步骤，查看“是什么、谁传给谁、是否共用、哪里还缺证据”。代码类型和文件路径默认收起。</p>
</section>`;
