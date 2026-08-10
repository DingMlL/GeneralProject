<div align="center">

# GeneralProject.Transport

**工业物联网通信框架 · .NET 8**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![AI Ready](https://img.shields.io/badge/AI-Ready-FF6F00)](#ai-辅助开发工作流)

*从物理通道到业务调用的全链路工业物联网通信解决方案*

</div>

---

## 📖 目录

- [项目简介](#项目简介)
- [核心特性](#核心特性)
- [技术架构](#技术架构)
- [核心模块](#核心模块)
- [快速开始](#快速开始)
- [设备接入示例](#设备接入示例)
- [AI 辅助开发工作流](#ai-辅助开发工作流)
- [已接入设备](#已接入设备)
- [测试工具](#测试工具)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [性能指标](#性能指标)
- [后续规划](#后续规划)

---

## 项目简介

**GeneralProject.Transport** 是一个面向工业物联网场景的 .NET 通信框架，提供从**物理通道、协议解析、设备管理到业务调用**的全链路解决方案。

### 解决什么问题？

| 痛点 | 解决方案 |
|:---|:---|
| 协议碎片化，接入重复造轮子 | 标准化 `IProtocolParser` 接口，协议与业务解耦 |
| 总线复杂性（多设备/多协议混接） | 一连接多设备，通过地址区分 + 同一连接共享解析器 |
| 新设备接入周期长（2-3天） | AI 辅助生成，**20-30 分钟**完成接入 |
| 框架与业务强耦合 | 分层架构 + 特性驱动，新增设备不改框架代码 |
| 调试困难，缺乏测试工具 | WinForm 可视化测试工具 + 轮询监控 |

---

## 核心特性

| 特性 | 说明 |
|:---|:---|
| **多物理通道** | 串口（RS-232/485）、TCP 客户端、TCP 服务端、UDP |
| **一连接多设备** | 单条总线挂载多个设备，通过设备地址区分 |
| **双队列优先级** | 高优先级命令（配置）插队发送，不被轮询阻塞 |
| **精准请求匹配** | 基于 `matchKey`（地址+功能码）精准匹配，解决 FIFO 被上报顶替问题 |
| **防重入机制** | `Interlocked.Exchange` 原子操作，超时/响应只触发一次 |
| **特性驱动** | `[ProtocolParser]` 自动发现解析器，业务层零感知 |
| **配置驱动** | 一行字符串完成通道创建（`tcp://192.168.1.100:502`） |
| **双模式 API** | 异步等待（`await SendAsync`）+ 回调模式（`SendWithCallback`） |
| **DI 集成** | `AddTransport()` 扩展方法，无缝接入 .NET DI |
| **AI 就绪** | 完整 XML 注释，AI 可根据协议文档自动生成接入代码 |

---

## 技术架构

### 分层设计

```
┌─────────────────────────────────────────────────────────────────────────┐
│  业务层（WinForm / Web / Service）                                      │
│  DeviceManager.GetOrCreateDevice<T>() → 直接调用业务方法               │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  设备层（Devices/）                                                    │
│  每个设备独立文件夹：Parser（协议解析）+ Proxy（设备代理）              │
│  新增设备只需添加文件，不改框架                                        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  核心调度层（Core/）                                                   │
│  ConnectionQueue：双队列调度、请求匹配、超时管理、主动上报路由          │
│  PendingOperation：防重入状态机                                        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  协议层（Parser/）                                                     │
│  IProtocolParser + ProtocolParserAttribute 特性驱动                    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  物理通道层（Channels/）                                               │
│  ICommChannel：Serial / Tcp / TcpServer / Udp                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 数据流

**发送路径：**

```
业务层 → DeviceProxy.BuildRequest() → ConnectionQueue.SendAsync()
       → 入队（双队列）→ 消费者线程出队 → ICommChannel.WriteAsync()
```

**接收路径：**

```
ICommChannel.DataReceived → FrameDecoder.Decode()
                         → IProtocolParser.GetFrameType() 分流
                              ├── Response → ExtractMatchKey() → 匹配字典 → 唤醒 Task
                              └── Report   → ExtractDeviceId() → 路由到 IDeviceProxy.HandleReport()
```

---

## 核心模块

| 模块 | 核心类 | 职责 |
|:---|:---|:---|
| **物理通道** | `ICommChannel`, `SerialChannel`, `TcpChannel`, `UdpChannel` | 串口/TCP/UDP 收发抽象 |
| **TCP服务端** | `ITcpServer`, `IClientSession` | 多客户端连接管理 |
| **协议解析** | `IProtocolParser`, `ProtocolParserAttribute` | 拆包、地址提取、匹配键、帧类型判断 |
| **设备代理** | `IDeviceProxy`, `DeviceProxyBase<T>` | 设备基类，提供发送能力 |
| **核心调度** | `ConnectionQueue`, `PendingOperation`, `FrameDecoder` | 队列、请求匹配、超时、粘包拆包 |
| **设备管理** | `DeviceManager` | 全局单例，连接池/设备池管理 |
| **配置工厂** | `ConnectionConfigParser`, `ConnectionConfig` | 连接字符串解析 |
| **DI扩展** | `ServiceCollectionExtensions` | `AddTransport()` 扩展方法 |

---

## 快速开始

### 安装

```bash
# 添加项目引用
dotnet add reference ../GeneralProject.Transport/GeneralProject.Transport.csproj

# 或通过 NuGet（待发布）
dotnet add package GeneralProject.Transport
```

### 基础使用

```csharp
using GeneralProject.Transport.Manager;
using GeneralProject.Transport.Devices.Renke;

// 1. 获取设备（一行代码完成通道+解析器+队列+设备创建）
var device = DeviceManager.Instance.GetOrCreateDevice<TemperatureHumidityProxy>(
    connectionId: "COM3_BUS",
    deviceId: "1",
    connectionConfig: "serial://COM3:9600"   // 或 "tcp://192.168.1.100:502"
);

// 2. 使用设备
float temp = await device.ReadTemperatureAsync();
float humidity = await device.ReadHumidityAsync();

// 3. 订阅主动上报（如设备支持）
device.PassiveReport += (data) => {
    Console.WriteLine($"上报数据: {data}");
};
```

### DI 集成

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder();
builder.Services.AddTransport();
var host = builder.Build();

// 业务层构造函数注入
public class MyService
{
    private readonly DeviceManager _deviceManager;
    public MyService(DeviceManager deviceManager) => _deviceManager = deviceManager;
}
```

### 连接配置格式

| 格式 | 示例 |
|:---|:---|
| **URI（推荐）** | `"tcp://192.168.1.100:502"` |
| **串口 URI** | `"serial://COM3:9600"` |
| **UDP URI** | `"udp://0.0.0.0:9999?remote=255.255.255.255:8888"` |
| **键值对** | `"type=tcp;host=192.168.1.100;port=502"` |
| **推断格式** | `"192.168.1.100:502"` 或 `"COM3:9600"` |

---

## 设备接入示例

### 步骤 1：定义协议解析器（AI 生成）

```csharp
using GeneralProject.Transport.Parser;

namespace GeneralProject.Transport.Devices.Renke
{
    public sealed class RenkeParser : IProtocolParser
    {
        public int TryParseFrame(byte[] buffer, int offset, int length, out byte[]? frame)
        {
            // 拆包逻辑：找帧头、读长度、校验 CRC
        }

        public string? ExtractDeviceId(byte[] frame) => frame[0].ToString();
        public ushort? ExtractMatchKey(byte[] frame) => (ushort)((frame[0] << 8) | frame[1]);
        public FrameType GetFrameType(byte[] frame) => FrameType.Response;
    }
}
```

### 步骤 2：定义设备代理（AI 生成）

```csharp
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Proxy;

namespace GeneralProject.Transport.Devices.Renke
{
    [ProtocolParser(typeof(RenkeParser))]
    public sealed class TemperatureHumidityProxy : DeviceProxyBase<float>
    {
        public TemperatureHumidityProxy(ConnectionQueue queue, string deviceId)
            : base(queue, deviceId) { }

        public async Task<float> ReadTemperatureAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0001);
            byte[] response = await SendAsync(request, matchKey: 0x03, timeoutMs);
            return ParseValue(response);
        }
    }
}
```

### 步骤 3：业务层使用

```csharp
var device = DeviceManager.Instance.GetOrCreateDevice<TemperatureHumidityProxy>(
    "COM3_BUS", "1", "serial://COM3:9600"
);

float temp = await device.ReadTemperatureAsync();
```

---

## AI 辅助开发工作流

框架设计阶段即考虑 AI 协作，形成标准化接入流程：

```
┌─────────────────────────────────────────────────────────────────────────┐
│  第 1 步：准备框架上下文（一次性）                                      │
│  └── 5 个核心接口/基类文件（IProtocolParser / DeviceProxyBase / ...）  │
├─────────────────────────────────────────────────────────────────────────┤
│  第 2 步：粘贴协议文档（每次新设备）                                    │
│  └── 厂家提供的协议说明（帧结构、命令列表、校验方式）                  │
├─────────────────────────────────────────────────────────────────────────┤
│  第 3 步：AI 生成代码（2-3 分钟）                                      │
│  └── XxxParser.cs + XxxProxy.cs + XxxData.cs                          │
├─────────────────────────────────────────────────────────────────────────┤
│  第 4 步：复制到项目（2 分钟）                                          │
│  └── 放入 Devices/[厂家名称]/ 目录                                    │
├─────────────────────────────────────────────────────────────────────────┤
│  第 5 步：编译运行                                                     │
│  └── 新设备接入完成                                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

> **实际效果**：温湿度变送器、空调控制器、微波探测器三个设备，AI 一次生成即可运行，无需人工修改。

---

## 已接入设备

| 设备 | 厂家 | 协议 | 功能 |
|:---|:---|:---|:---|
| 温湿度变送器 | 仁科（Renke） | ModBus RTU | 温度/湿度读取 |
| 空调控制器 | 仁科（Renke） | ModBus RTU | 温湿度读取、制冷/制热/关机控制、状态读取 |
| 微波探测器 | 仁科（Renke） | ModBus RTU | 报警状态、延时/持续时间配置 |

---

## 测试工具

### GeneralProject.UIDemo — WinForm 可视化测试工具

| 功能 | 说明 |
|:---|:---|
| **连接切换** | 串口（COM3）/ TCP（192.168.1.113:8234） |
| **三设备监控** | 温湿度变送器、空调控制器、微波探测器实时数据 |
| **轮询读取** | 勾选后自动循环，间隔 1-60 秒可调 |
| **手动刷新** | 单次读取所有设备状态 |
| **反馈** | 连接状态、更新时间、响应状态实时显示 |

---

## 技术栈

| 技术 | 用途 |
|:---|:---|
| **.NET 8** | 框架基础 |
| `System.IO.Ports` | 串口通信 |
| `System.Net.Sockets` | TCP/UDP 通信 |
| `Microsoft.Extensions.Hosting` | 依赖注入宿主 |
| `Microsoft.Extensions.DependencyInjection` | DI 容器 |
| **WinForms** | 测试工具 UI |

---

## 项目结构

```
GeneralProject.Transport/
│
├── Channels/                    # 【系统层】物理通道
│   ├── ICommChannel.cs
│   ├── SerialChannel.cs
│   ├── TcpChannel.cs
│   ├── TcpServerChannel.cs
│   └── UdpChannel.cs
│
├── Server/                      # 【系统层】TCP服务端
│   ├── ITcpServer.cs
│   ├── IClientSession.cs
│   ├── TcpServer.cs
│   └── ClientSession.cs
│
├── Parser/                      # 【系统层】协议接口
│   ├── IProtocolParser.cs
│   ├── ProtocolParserAttribute.cs
│   └── FrameType.cs
│
├── Proxy/                       # 【系统层】设备基类
│   ├── IDeviceProxy.cs
│   └── DeviceProxyBase.cs
│
├── Core/                        # 【系统层】核心调度
│   ├── ConnectionQueue.cs
│   ├── CommandPriority.cs
│   └── Internal/
│       ├── PendingOperation.cs
│       └── FrameDecoder.cs
│
├── Manager/                     # 【系统层】全局管理
│   └── DeviceManager.cs
│
├── Factory/                     # 【系统层】配置工厂
│   ├── ConnectionConfig.cs
│   ├── ConnectionConfigParser.cs
│   └── ConnectionConfigExtensions.cs
│
├── Extensions/                  # 【系统层】DI扩展
│   └── ServiceCollectionExtensions.cs
│
└── Devices/                     # 【应用层】具体设备实现
    └── Renke/                   # 仁科系列设备
        ├── RenkeParser.cs
        ├── TemperatureHumidityProxy.cs
        ├── AirConditionerControllerProxy.cs
        └── MicrowaveDetectorProxy.cs

GeneralProject.UIDemo/           # 测试工具
├── Program.cs
├── Form1.cs
└── Form1.Designer.cs
```

---

## 性能指标

| 指标 | 数值 |
|:---|:---|
| 框架代码量 | ~3000 行 |
| 核心接口数 | 6 个 |
| 设备接入时间（AI辅助） | **20-30 分钟** |
| 请求匹配精度 | 100%（基于 matchKey） |
| 支持物理通道类型 | 3 种（串口/TCP/UDP） |
| 已接入设备数 | 3 款 |

---

## 后续规划

| 方向 | 说明 |
|:---|:---|
| **更多协议支持** | Modbus TCP、DL/T 645、西门子 S7 |
| **心跳与自动重连** | 长连接保活机制 |
| **云端集成** | MQTT 桥接，接入 IoT 平台 |
| **性能优化** | 零拷贝、`Span<byte>` |
| **单元测试** | 核心组件测试覆盖 |
| **NuGet 发布** | 打包为可复用组件 |

---

<div align="center">

**Made with ❤️ for Industrial IoT**

</div>
