# Unity_2final

一个基于 Unity 的 3D 等距视角动作场景课程项目，当前版本围绕角色移动、战斗交互、NPC 对话、背包界面和暂停菜单进行了整合，并使用 URP 完成整体渲染配置。

## Project Overview

本项目以第三人称等距视角探索为核心，搭建了一个可操作的场景原型。玩家可以在场景中移动、奔跑、攻击，并与 NPC 进行近距离互动。项目中还包含基础 UI 系统，用于背包展示、暂停控制和对话呈现。

## Main Features

- 等距相机跟随与角色朝向同步
- 角色移动、奔跑与近战攻击
- NPC 交互提示与多段对话流程
- 背包界面与角色预览窗口
- 暂停菜单、重新开始与退出逻辑
- 基于 URP 的项目渲染与像素化后处理尝试

## Controls

- `W A S D` / 方向输入：移动角色
- `Left Shift`：奔跑
- `鼠标左键`：攻击
- `F`：与 NPC 交互
- `Tab`：打开或关闭背包
- `Esc`：打开或关闭暂停菜单

## Project Structure

- `Assets/Scenes`：Unity 场景资源
- `Assets/Rendering/Toon2D/Scripts/Runtime`：核心运行时脚本
- `Assets/Art`：角色、地形与环境美术资源
- `Packages`：Unity 包依赖配置
- `ProjectSettings`：项目设置与渲染设置

## Tech Stack

- Unity
- Universal Render Pipeline (URP)
- C#

## Current Focus

当前仓库内容主要集中在以下方向：

- 调整场景资源与地形内容
- 整理 Toon2D 运行时脚本
- 增加交互式 UI 与 NPC 对话体验
- 更新渲染相关设置与包依赖

## Notes

建议使用 Unity Hub 打开本项目，并等待依赖与缓存自动导入完成后再运行场景。`Library`、`Temp`、`Logs` 等目录已通过 `.gitignore` 排除，不建议提交生成文件。
