# 明眸 MingMou 👀

一款基于 WinUI 3 的 Windows 桌面护眼提醒应用：按医学推荐的节奏提醒你眨眼，缓解长时间用屏带来的视疲劳与干眼。

## 特性

- **👁 眨眼提醒**：每 25 秒提醒一次，右下角浮出**完全透明的眨眼动画**——只有眼睛可见，不挡内容、不抢焦点、不打断操作
- **🧠 智能间隔**：依据 20-20-20 法则与视屏终端（VDT）研究，随连续用眼时长动态加密提醒（25 → 20 → 15 → 10 秒）
- **🌓 深浅色主题**：跟随系统自动切换，或手动固定
- **⚙️ 可配置**：提醒间隔、暂停时长、开机自启动、托盘驻留
- **📊 今日眨眼计数**：自动持久化，跨天重置
- **🚀 自包含部署**：安装包内置全部运行时，目标机器免装任何环境

## 技术栈

| 组件 | 选型 |
|---|---|
| 框架 | WinUI 3 / Windows App SDK 1.6（非打包） |
| 语言 | C# 12 + XAML |
| 系统托盘 | H.NotifyIcon.WinUI |
| 透明窗口 | 自定义 `SystemBackdrop`（alpha=0 透明画刷） |
| 安装包 | Inno Setup 7（自包含） |

## 快速开始

### 环境要求

Windows 11 / Windows 10 1809+、Visual Studio 2022（.NET 桌面开发 + Windows App SDK）、.NET 8 SDK。

### 构建运行

1. VS 打开 `MingMou.sln`，平台选 `x64`；
2. F5 启动（非打包模式，构建即用）。

> ⚠️ WinUI 3 的 PRI 资源打包依赖 VS 的 MSBuild 任务，仅装 .NET SDK 无法命令行构建；请用 VS 或 VS 开发者命令提示符。

### 打包安装程序

1. VS 生成 **Release x64**；
2. 用 [Inno Setup 7](https://jrsoftware.org/isdl.php) 打开 `MingMou.iss` → **Compile**；
3. 得到 `installer\MingMouSetup.exe`（自包含，目标机器免装运行时）。

> ⚠️ 不要用 `dotnet publish`：WinUI 3 非打包发布不复制 `MingMou.pri`/`Assets`/`*.xbf`，打包后启动会崩。

## 使用

- 启动后最小化到**系统托盘**；双击托盘图标打开主窗口，右键可暂停/设置/退出；
- 到点提醒：右下角浮出透明眨眼小窗（2.5 秒自动消失）；
- 设置视图可调整：提醒间隔、智能间隔、暂停时长、主题、开机自启动。

## 项目结构

```
MingMou/
├── Assets/                  # 图标与视觉资产
├── Controls/                # 眨眼动画控件
├── Core/                    # 常量、日志、Win32 互操作、透明背景、自启动
├── Services/                # 提醒/空闲检测/托盘/设置服务
├── App.xaml/.cs             # 启动、主题、提醒分发
├── MainWindow.xaml/.cs      # 主窗口（主视图 + 设置视图）
├── ReminderPopupWindow.xaml/.cs  # 透明提醒小窗
├── Program.cs               # 入口（Bootstrap 引导）
├── MingMou.csproj / MingMou.iss
└── tools/               # 图标生成脚本
```

## 致谢

- [enKl03B](https://linux.do/u/enkl03b)（Linux Do）—— 透明窗口实现方案参考（[WinUI3 无边框透明窗口实现指南](https://linux.do/t/topic/1790806)）
- [WinUIEx](https://github.com/dotMorten/WinUIEx) —— `TransparentTintBackdrop` 透明背景思路
- WorkBuddy（AI 开发伙伴）—— 开发过程中的代码协作与调试

## 许可证

[GPL-3.0](LICENSE)

---

**免责声明**：提醒节奏基于公开眼科研究（20-20-20 法则、VDT 综合征）做体验优化，**非医疗建议**；如有眼部不适请及时就医。
