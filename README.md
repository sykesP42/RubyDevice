# RubyDevice

**RubyDevice** 是一款现代化的 Windows 输入设备管理工具，基于 WinUI 3 构建。它允许用户查看、管理和控制连接到计算机的所有输入设备（键盘、鼠标、触控板）。

## 功能特性

- **设备枚举** - 自动检测并列出系统中所有 HID 输入设备，显示详细信息（制造商、VID/PID、连接类型）
- **设备控制** - 一键启用或禁用特定输入设备，无需管理员权限
- **安全确认机制** - 两阶段确认流程，防止误操作和意外锁定
- **使用统计** - 追踪设备使用时间，提供可视化统计图表
- **多语言支持** - 内置中英文界面切换
- **多主题支持** - 5 种主题样式（浅色、深色、海洋、森林、日落）

## 系统要求

- Windows 10 版本 1809 (17763) 或更高
- .NET 9.0 Runtime

## 构建项目

```bash
dotnet build -c Debug -p:Platform=x64
dotnet run -c Debug -p:Platform=x64
```

## 数据存储

所有配置与统计数据都保存在 `%AppData%\RubyDevice\` 目录下：

| 文件 | 内容 |
| --- | --- |
| `device_data.json` | 设备名称与备注缓存 |
| `usage_data.json` | 每日使用记录（活跃时长 / 启用时长） |
| `tracking_settings.json` | 各设备的追踪开关与保留天数 |
| `timer_settings.json` | 自动恢复定时器的上次超时设置 |

删除这些文件即可恢复到初始状态。

## 项目结构

```
RubyDevice/
├── App.xaml.cs          应用入口，注册全局异常处理
├── MainWindow.xaml.cs   主窗口、导航与 Raw Input 注册
├── Core/                设备枚举与输入拦截（Raw Input API + 低级键盘/鼠标钩子）
├── Models/              数据模型与持久化结构
├── Services/            本地化、主题、通知、使用统计、设备监听、数据导出
├── ViewModels/          MVVM 视图模型
├── Pages/               各功能页面
├── Controls/            自定义确认与倒计时对话框
├── Helpers/             通用工具方法
├── Themes/              5 套主题资源字典
└── tests/               单元测试
```

## 技术栈

- WinUI 3 (Windows App SDK 1.5)
- .NET 9.0
- C# / XAML
- MVVM 架构

## 许可证

MIT License
