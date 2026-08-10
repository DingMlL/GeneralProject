# 🏭 GeneralProject.Transport

> **工业物联网通信框架 — .NET 8**
>
> 从物理通道到业务调用的全链路工业通信解决方案

---

## 📖 目录

- [项目简介](#项目简介)
- [技术亮点](#技术亮点)
- [项目结构](#项目结构)
- [技术架构](#技术架构)
- [核心能力](#核心能力)
- [已接入设备](#已接入设备)
- [测试工具](#测试工具)
- [快速使用](#快速使用)
- [技术栈](#技术栈)

---

## 项目简介

**GeneralProject.Transport** 是一套面向工业物联网场景的 **.NET 8 通信框架**，覆盖从物理通道、协议解析、设备管理到业务调用的全链路需求。

框架采用**分层架构设计**，**系统层与设备层完全分离**，支持多物理通道、多设备共享、双队列优先级调度、主动上报路由等工业通信核心能力，并内置 AI 辅助开发工作流，帮助团队快速接入新设备。

---

## 技术亮点

| 亮点 | 说明 |
|:---:|:---|
| 🎯 **特性驱动** | 设备代理标注 `[ProtocolParser]`，框架自动创建解析器实例，新增协议不改框架代码 |
| 🔒 **防重入状态机** | 基于 `Interlocked.Exchange` 原子操作，超时回调和数据回调只触发一次 |
| ⚡ **双队列优先级** | 高优先级队列 + 普通优先级队列，紧急命令插队发送 |
| 🎯 **精准匹配** | matchKey（设备地址 + 功能码）精准匹配，解决 FIFO 模式下主动上报顶替正常响应的问题 |
| 🤖 **AI 就绪** | 完整 XML 注释，AI 可根据协议文档自动生成设备接入代码，新设备接入从 **2-3 天** 缩短至 **20-30 分钟** |
| ⚙️ **配置驱动** | 支持 URI / 键值对 / 推断格式，一行字符串完成通道创建 |

---

## 项目结构

```
GeneralProject.Transport/                    # 核心框架（系统层）
├── Channels/                                # 物理通道（串口/TCP/UDP）
├── Server/                                  # TCP 服务端
├── Parser/                                  # 协议解析器接口 + 特性标注
├── Proxy/                                   # 设备代理基类
├── Core/                                    # 核心调度引擎
├── Manager/                                 # 全局设备管理器
├── Factory/                                 # 配置解析工厂
└── Extensions/                              # DI 扩展

GeneralProject.Transport.Devices.Renke/      # 设备层（独立项目）
├── RenkeParser.cs                           # ModBus RTU 协议解析器
├── TemperatureHumidityProxy.cs              # 温湿度变送器（地址 1）
├── AirConditionerControllerProxy.cs         # 空调控制器（地址 2）
└── MicrowaveDetectorProxy.cs                # 微波探测器（地址 3）

GeneralProject.UIDemo/                       # WinForm 测试工具
├── Program.cs
├── Form1.cs
└── Form1.Designer.cs
```

---

## 技术架构

```
┌─────────────────────────────────────────┐
│         业务层（WinForm / Service）        │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│     设备层（Devices/）← 独立项目，按需引用   │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  核心调度层（Core/）← ConnectionQueue      │
│                + 防重入状态机              │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│   协议层（Parser/）← IProtocolParser      │
│                + 特性驱动                 │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│  物理通道层（Channels/）← ICommChannel     │
│            + 串口 / TCP / UDP             │
└─────────────────────────────────────────┘
```

---

## 核心能力

| 功能 | 说明 |
|:---:|:---|
| 🔌 **物理通道** | 串口（RS-232/485）、TCP 客户端、TCP 服务端、UDP |
| 🔗 **一连接多设备** | 单条总线挂载多个设备，通过设备地址区分 |
| 📜 **协议解析** | `IProtocolParser` 接口 + `[ProtocolParser]` 特性驱动 |
| ⚡ **双队列调度** | 高/普通双队列，配置命令优先发送 |
| 🎯 **精准匹配** | matchKey（地址+功能码）精准匹配请求-响应 |
| 📡 **主动上报** | 上报与响应隔离，通过设备地址精准路由 |
| ⚙️ **配置驱动** | 支持 URI / 键值对 / 推断格式 |
| 🧩 **DI 集成** | `AddTransport()` 扩展方法，一键注册 |
| 🤖 **AI 辅助** | XML 注释 + 标准化接口，AI 可自动生成接入代码 |

---

## 已接入设备

> **仁科系列设备**

| 设备 | 协议 | 地址 | 功能 |
|:---|:---:|:---:|:---|
| 🌡️ 温湿度变送器 | ModBus RTU | 1 | 温度/湿度读取 |
| ❄️ 空调控制器 | ModBus RTU | 2 | 温湿度读取、制冷/制热/关机控制、学习指令（自定义 1-29） |
| 📡 微波探测器 | ModBus RTU | 3 | 报警状态、延时/持续时间配置 |

---

## 测试工具

**GeneralProject.UIDemo** 提供 WinForm 可视化测试界面：

| 功能 | 说明 |
|:---:|:---|
| 🔌 **连接管理** | 支持串口（`COM3`）/ TCP（`192.168.1.113:8234`）切换 |
| 📊 **数据监控** | 三设备温湿度、状态、报警实时显示 |
| ❄️ **空调控制** | 制冷/制热/关机发射，自定义指令发射（1-29） |
| 📚 **空调学习** | 学习制冷/制热/关机/自定义指令（超时 5 秒） |
| 🔄 **轮询读取** | 勾选后自动循环读取，间隔 1-60 秒可调 |

---

## 快速使用

### 1. DI 注册

```csharp
builder.Services.AddTransport();
```

### 2. 获取设备（一行代码）

```csharp
var device = DeviceManager.Instance.GetOrCreateDevice<TemperatureHumidityProxy>(
    connectionId: "COM3_BUS",
    deviceId: "1",
    connectionConfig: "serial://COM3:9600"
);
```

### 3. 使用设备

```csharp
float temp = await device.ReadTemperatureAsync();
```

---

## 技术栈

- **.NET 8**
- **System.IO.Ports** / **System.Net.Sockets**
- **Microsoft.Extensions.Hosting** / **DependencyInjection**
- **WinForms**（测试工具）

---

<div align="center">

**Made with ❤️ for Industrial IoT**

</div>
