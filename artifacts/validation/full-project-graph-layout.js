function fullProjectLayout(nodes, mode) {
    const pos = new Map(), bands = [];
    if (mode === 'overview') {
        const fixed = {
            'SunEyeVision.UI': [530, 65],
            'SunEyeVision.Workflow': [310, 225],
            'SunEyeVision.Plugin.Infrastructure': [850, 225],
            '__tools__': [530, 385],
            'SunEyeVision.Plugin.SDK': [130, 545],
            'CommonControl': [650, 545],
            'SunEyeVision.Core': [1150, 545],
            'SunEyeVision.DeviceDriver': [110, 705],
            'SunEyeVision.HalAdapter': [465, 705],
            'SunEyeVision.CvAdapter': [820, 705],
            'SunEyeVision.CvEngine': [1175, 705]
        };
        nodes.forEach(n => pos.set(n.id, fixed[n.id] || [1290, 65]));
        bands.push(
            { x: 20, y: 15, w: 1200, h: 130, label: '应用宿主 · 1工程' },
            { x: 20, y: 175, w: 1570, h: 130, label: '流程执行与扩展装配 · 2工程' },
            { x: 20, y: 335, w: 1570, h: 130, label: '工具插件层 · 23工程 / 24项能力，默认合并；具体工具不是独立架构层' },
            { x: 20, y: 495, w: 1570, h: 130, label: '公共合同、控件与基础 · 3工程；SDK运行合同与UI扩展尚需分界' },
            { x: 20, y: 655, w: 1570, h: 130, label: '设备与算法后端 · 4工程' },
            { x: 1260, y: 15, w: 330, h: 130, label: '辅助工程 · 不计产品架构层' }
        );
        return { pos, bands, height: 825 };
    }
    const tools = nodes.filter(n => n.kind === 'tool');
    const core = nodes.filter(n => n.kind === 'core');
    const aux = nodes.filter(n => n.kind === 'auxiliary');
    tools.forEach((n, i) => pos.set(n.id, [30 + (i % 6) * 260, 70 + Math.floor(i / 6) * 100]));
    const coreY = tools.length ? 100 + Math.ceil(tools.length / 6) * 100 : 65;
    core.forEach((n, i) => pos.set(n.id, [30 + (i % 5) * 310, coreY + 40 + Math.floor(i / 5) * 180]));
    const auxY = coreY + Math.ceil(core.length / 5) * 180 + 65;
    aux.forEach((n, i) => pos.set(n.id, [30 + i * 310, auxY + 40]));
    if (tools.length) bands.push({ x: 15, y: 15, w: 1580, h: coreY - 30, label: `工具插件层 · ${tools.length}个同层工程展开，不是${tools.length}个架构层` });
    if (core.length) bands.push({ x: 15, y: coreY, w: 1580, h: Math.ceil(core.length / 5) * 180 + 25, label: '核心工程明细 · 这里按工程展开，不把工程行数当职责层数' });
    if (aux.length) bands.push({ x: 15, y: auxY, w: 1580, h: 125, label: '辅助工程 · 不参与产品业务层次比较' });
    return { pos, bands, height: aux.length ? auxY + 150 : coreY + Math.ceil(core.length / 5) * 180 + 70 };
}
