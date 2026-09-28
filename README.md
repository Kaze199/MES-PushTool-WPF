# MES-PushTool-WPF (SMT 读码设备绑定 SN 推送 MES 系统)

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.5%2B-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Win%20XP%20%7C%20Win7%20%7C%20Win10-lightgrey.svg)](https://www.microsoft.com/windows)

本项目是一款基于 C# WPF 架构开发的工业现场上位机数据采集与推送工具。专门用于连接 SMT 产线条码扫描设备（串口/网口），实现条码数据的实时接收、格式校验拆分、JSON 报文组装，并通过 Web Service HTTP POST 接口自动推送到工厂 MES（防错料系统）。

---

## 📄 目录
- [背景与应用场景](#-背景与应用场景)
- [核心功能特性](#-核心功能特性)
- [软件架构与业务流程](#-软件架构与业务流程)
- [接口与数据规范](#-接口与数据规范)
- [环境要求与技术栈](#-环境要求与技术栈)
- [安装与运行指南](#-安装与运行指南)
- [单机模拟测试说明](#-单机模拟测试说明)
- [项目目录结构](#-项目目录结构)
- [常见问题 (FAQ)](#-常见问题-faq)
- [开源协议](#-开源协议)

---

## 🎯 背景与应用场景

在 SMT（表面贴装技术）电子制造产线中，连板（Panel）通过读码设备时会一次性返回多个小板（PCB）的二维码。本工具作为工控上位机中间件，充当设备端与 MES 系统的通信桥梁：
1. **硬件兼容**：向下兼容串口扫码枪及 TCP/IP 网口读码器。
2. **格式转换**：将扫描得到的字符串自动提取提取出组 SN（GSN）和小板 SN 列表。
3. **数据上报**：按标准 JSON 格式安全推送至 MES Web Service 接口，保障生产数据的追溯性。

---

## 💡 核心功能特性

* **双通道硬件通讯**：支持串口（Serial Port）与 TCP 服务端（TCP Listener）两种接收模式（运行时可自由切换）。
* **自动化数据解析**：自动解析设备通过 `@` 分隔上传的拼板条码，支持单码与多码混合校验。
* **参数持久化配置**：接口地址、Token、工单号（WORKORDER）、波特率等参数一次设置，自动保存（App.config/JSON），启动即用。
* **实时监控与可视交互**：界面直观显示原始接收数据、拼接后的 POST 报文以及 MES 的实时 Response 响应。
* **完整日志追溯**：本地文本日志自动按日期归档，提供软件界面历史日志检索与导出功能。
* **高兼容性设计**：针对工业老旧工控机优化，无缝运行于 Windows XP、Win7、Win10 等多操作系统平台。

---

## 🔄 软件架构与业务流程

### 数据流向示意图：
