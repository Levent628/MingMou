; ============================================================================
; 文件：MingMou.iss
; 用途：明眸（MingMou）护眼提醒应用的安装包脚本（自包含版）
; 适用：Inno Setup 6 / 7 均可（7 对 6 脚本完全向后兼容；推荐 7.0.2+ 64 位）
;
; 特性：自包含部署——.NET 8 桌面运行时 + Windows App SDK 1.6 运行时全部随应用分发，
;       目标机器【免装任何运行时】，双击即用（安装包约 110~160MB）。
;       安装过程不做运行时检测（自包含不需要）。
;
; 前置：先构建自包含 Release（VS 里配置 Release + x64 → 重新生成解决方案）：
;   构建输出 bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\（含完整运行时）
; 然后 Inno Setup 打开本脚本 → Compile → installer\MingMouSetup.exe
;
; 说明：当前用户安装（免管理员/UAC），安装目录 %LocalAppData%\Programs\MingMou（英文目录，
;       更专业；开始菜单/应用名仍显示"明眸"），与开机自启动（HKCU 注册表 Run 键）完全兼容。
; ============================================================================

#define MyAppName "明眸"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MingMou"
#define MyAppExeName "MingMou.exe"

[Setup]
AppId={{7C4B2E1A-9D3F-4A6B-8E5C-2F1D0A3B9C7E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; 安装目录用英文（MingMou），避免中文路径带来兼容性问题与观感问题；
; 应用显示名/开始菜单名仍为"明眸"（MyAppName）
DefaultDirName={localappdata}\Programs\MingMou
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=MingMouSetup
Compression=lzma2
SolidCompression=yes
; 现代化界面：Win11 风格的 modern 向导 + 强制深色（用户选择的默认外观）。
; 深色是 WizardStyle 的外观模式参数（Inno 6.6+ 支持），没有独立的 DarkMode 指令。
; 注意：ISPP 的 Ver 是字符串，不支持 >= 数值比较（会报"Operator not applicable"），
;       故不写版本条件编译——本项目固定使用 Inno Setup 7（或 6.6+），直接写最终语法。
WizardStyle=modern dark
; 单语言应用，不弹语言选择对话框
ShowLanguageDialog=no
; 自定义左侧横幅图（PNG，modern 向导尺寸）
WizardImageFile=installer-assets\banner.png
; 卸载程序同样使用 modern + 深色
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
SetupIconFile=Assets\icon.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 打包自包含构建输出（含完整运行时：.NET 8 + Windows App SDK 1.6，目标机器免装运行时）。
; Source 指向 win-x64\ 子目录（自包含 + 非打包的构建输出带 RID 子目录）。
Source: "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "installer-assets"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
