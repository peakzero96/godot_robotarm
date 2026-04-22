# 机械臂抓取数字孪生 — 开发规格文档

> 版本：1.0
> 日期：2026-04-22
> 状态：待开发

---

## 1. 项目概述

### 1.1 项目目标

开发一个基于 Godot 4.6 的 3D 数字孪生可视化系统，接收控制端发送的机械臂控制信号，实时镜像显示机械臂运动状态和纸箱抓取过程。

### 1.2 核心功能

1. **机械臂可视化**：加载 URDF/DAE 模型，实时显示关节运动
2. **纸箱墙动态生成**：解析前端发送的纸箱位置数据，在场景中自动生成纸箱墙
3. **抓取循环可视化**：显示完整的抓取→搬运→卸下流程
4. **机械臂快速切换**：通过配置文件快速更换不同型号的机械臂
5. **嵌入集成**：支持独立运行和作为嵌入式组件（SubViewport / Spout）

### 1.3 运行流程

```
Godot 启动 → 自动 TCP 监听 → 控制端连接
  → 接收纸箱墙数据 → 生成纸箱墙
  → 循环：接收关节角度 + 状态信号 → 更新 3D 场景
         → 接收 BOX_GRAB → 纸箱附着夹爪
         → 接收 BOX_RELEASE → 纸箱放置到目标位置（短暂显示后消失）
  → 整面纸箱墙抓取完成 → 通知
```

### 1.4 技术栈

| 项 | 选型 |
|---|---|
| 引擎 | Godot 4.6 |
| 开发语言 | C# |
| 物理引擎 | 不使用（纯运动学） |
| 通信协议 | TCP + 自定义二进制协议 |
| 渲染 | Vulkan Forward+ |
| UI | Godot 内置 Control 节点 |

---

## 2. 项目文件结构

```
grasp/
├── project.godot                          # Godot 项目文件
├── dev_spec.md                            # 本文档
│
├── Config/                                # 配置文件
│   ├── app.json                           # 全局应用配置（端口、默认机械臂、日志级别等）
│   └── log.json                           # 日志配置
│
├── robot_arm/                             # 机械臂模型（目录名即型号标识）
│   ├── abb_irb4600_60_205/               # 示例：ABB IRB4600
│   │   ├── urdf/                         # URDF 文件
│   │   │   └── irb4600_60_205.urdf
│   │   └── meshes/
│   │       ├── visual/                   # DAE 可视化网格
│   │       │   ├── base_link.dae
│   │       │   ├── link_1.dae
│   │       │   └── ...
│   │       └── collision/                # STL 碰撞网格（本项目中可选，纯可视化不需要）
│   │           ├── base_link.stl
│   │           └── ...
│   └── <其他型号>/                        # 新增机械臂只需添加新目录
│       ├── urdf/
│       └── meshes/
│           ├── visual/
│       └── collision/
│
├── Scripts/                               # C# 脚本
│   ├── Main/                              # 入口与全局管理
│   │   ├── GameManager.cs                 # 全局管理器（初始化、生命周期）
│   │   └── AppConfig.cs                   # 读取 app.json 配置
│   │
│   ├── Network/                           # 通信层
│   │   ├── TcpServer.cs                   # TCP Server 监听与连接管理
│   │   ├── ProtocolParser.cs              # 二进制协议帧解析
│   │   ├── ProtocolTypes.cs               # 消息类型定义与常量
│   │   └── ConnectionState.cs             # 连接状态枚举与事件
│   │
│   ├── Robot/                             # 机械臂模块
│   │   ├── RobotLoader.cs                 # 通用加载器（解析 URDF + 加载网格 → 构建场景树）
│   │   ├── UrdfParser.cs                  # URDF XML 解析器（提取 link/joint 层级与参数）
│   │   ├── RobotController.cs             # 机械臂控制器（关节旋转更新、状态管理）
│   │   ├── JointPivot.cs                  # 单个关节节点组件
│   │   └── CoordinateConverter.cs         # 坐标系转换（预留，默认直通）
│   │
│   ├── BoxWall/                           # 纸箱墙模块
│   │   ├── BoxWallManager.cs              # 纸箱墙管理器（生成、状态追踪）
│   │   ├── BoxWallLoader.cs              # JSON 解析 → MultiMesh 实例化
│   │   └── BoxInstance.cs                 # 单个纸箱的状态（待抓取/抓取中/已放置）
│   │
│   ├── Grasp/                             # 抓取流程模块
│   │   ├── GraspStateMachine.cs           # 抓取状态机
│   │   └── GraspStates.cs                 # 状态定义与转换逻辑
│   │
│   ├── Camera/                            # 相机模块
│   │   ├── CameraManager.cs               # 多相机管理与切换
│   │   ├── FreeOrbitCamera.cs             # 自由轨道相机（鼠标拖拽/缩放）
│   │   ├── FixedCamera.cs                 # 固定监控相机
│   │   └── EndEffectorCamera.cs           # 末端跟随相机
│   │
│   ├── UI/                                # UI 模块
│   │   ├── UIManager.cs                   # UI 总管理器
│   │   ├── ConnectionPanel.cs             # 连接状态面板
│   │   ├── JointDisplayPanel.cs           # 关节角度实时显示
│   │   ├── GraspProgressPanel.cs          # 抓取进度面板
│   │   ├── ControlPanel.cs                # 控制按钮面板
│   │   └── EventLogPanel.cs               # 事件日志面板
│   │
│   ├── Logger/                            # 日志模块
│   │   ├── Logger.cs                      # 日志核心（写文件 + UI 输出）
│   │   └── LogLevel.cs                    # 日志级别定义
│   │
│   └── Utils/                             # 工具类
│       ├── ByteHelper.cs                  # 二进制读写辅助
│       └── MathHelper.cs                  # 数学辅助（角度转换等）
│
├── Scenes/                                # 场景文件
│   ├── Main.tscn                          # 主场景（3D 视口 + UI 布局）
│   ├── Robot/                             # 机械臂场景（由加载器动态生成，无需手动创建）
│   ├── Environment/                       # 环境场景
│   │   ├── FactoryFloor.tscn              # 简化工厂环境（地面 + 工作台 + 背景墙）
│   │   └── Lighting.tscn                  # 灯光预设
│   └── UI/                                # UI 场景
│       ├── ConnectionPanel.tscn
│       ├── JointDisplayPanel.tscn
│       ├── GraspProgressPanel.tscn
│       ├── ControlPanel.tscn
│       └── EventLogPanel.tscn
│
├── Resources/                             # 资源文件
│   ├── Materials/                         # 材质
│   │   ├── CardboardMaterial.tres         # 纸箱材质（瓦楞纸纹理 PBR）
│   │   ├── FloorMaterial.tres             # 地面材质
│   │   └── RobotMaterial.tres             # 机械臂默认材质
│   ├── Themes/                            # UI 主题
│   │   └── DefaultTheme.tres              # 默认暗色主题
│   └── Fonts/                             # 字体
│       └── Mono.ttf                       # 等宽字体（数据显示用）
│
├── Export/                                # 导出预设
│   └── Windows_Preset.tres                # Windows 桌面导出配置
│
└── Logs/                                  # 运行时日志输出目录
```

---

## 3. 全局配置文件

### 3.1 app.json

```json
{
  "version": "1.0",
  "network": {
    "listen_port": 5005,
    "max_connections": 1,
    "heartbeat_interval_ms": 1000,
    "reconnect_timeout_ms": 5000
  },
  "robot": {
    "default_path": "robot_arm/abb_irb4600_60_205",
    "update_rate_hz": 60
  },
  "scene": {
    "default_camera": "free_orbit",
    "environment": "Scenes/Environment/FactoryFloor.tscn"
  },
  "logging": {
    "level": "INFO",
    "file_enabled": true,
    "file_path": "Logs/grasp_{date}.log",
    "ui_max_lines": 500
  },
  "box_wall": {
    "default_color": "#C4A882",
    "grabbed_color": "#4CAF50",
    "fade_out_duration_ms": 2000
  }
}
```

---

## 4. 通信协议

### 4.1 概述

- 传输层：TCP
- 字节序：小端序（Little Endian）
- Godot 角色：TCP Server（监听等待控制端连接）
- 协议当前沿用可行性报告中的二进制帧框架，开发中根据实际需求调整

### 4.2 帧结构

```
┌──────────┬──────────┬──────────────┬──────────────────┐
│ Magic    │ Version  │ Message Type │ Payload Length    │
│ 2 bytes  │ 1 byte   │ 1 byte       │ 4 bytes (uint32) │
│ 0x47 0x52│ 0x01     │ 见消息类型表   │ N                │
├──────────┴──────────┴──────────────┴──────────────────┤
│                   Payload (N bytes)                    │
└───────────────────────────────────────────────────────┘
```

### 4.3 消息类型表

| Type ID | 名称 | 方向 | Payload 格式 | 说明 |
|---|---|---|---|---|
| `0x01` | `JOINT_STATE` | 控制端→Godot | `float32[N]`，N = URDF 解析出的 joint_count | 关节角度（弧度），按 URDF joint 拓扑顺序排列 |
| `0x02` | `GRIPPER_STATE` | 控制端→Godot | `float32` | 夹爪开合度（0.0=全开, 1.0=全合） |
| `0x03` | `BOX_GRAB` | 控制端→Godot | `uint32` + `float32[6]` | 抓取指定 ID 纸箱，附带 6D 位姿：x, y, z, rx, ry, rz |
| `0x04` | `BOX_RELEASE` | 控制端→Godot | `uint32` + `float32[6]` | 释放纸箱到指定 6D 位姿：x, y, z, rx, ry, rz |
| `0x05` | `TARGET_BOX` | 控制端→Godot | `uint32` + `float32[6]` | 高亮目标纸箱，附带 6D 位姿：x, y, z, rx, ry, rz |
| `0x06` | `BOX_WALL_DATA` | 控制端→Godot | JSON 字符串（UTF-8） | 纸箱墙位置数据 |
| `0x07` | `STATUS_REQUEST` | 双向 | 0 字节 | 请求状态 / 响应当前状态 |
| `0x08` | `SCENE_RESET` | 控制端→Godot | 0 字节 | 重置场景（清除纸箱、归位机械臂） |
| `0x09` | `LOAD_ROBOT` | 控制端→Godot | JSON 字符串（UTF-8） | 运行时切换机械臂配置路径 |
| `0x0A` | `HEARTBEAT` | 双向 | `int64` 时间戳 | 心跳保活 |

**6D 位姿说明**：`float32[6]` 依次为 `x, y, z, rx, ry, rz`，其中 x/y/z 为位置（米），rx/ry/rz 为欧拉角旋转（弧度），使用 Godot 原生坐标系（右手系，Y 朝上）。

### 4.4 协议解析规则

1. 读取 8 字节头部，校验 Magic（`0x47 0x52`）和 Version（`0x01`）
2. 根据 Message Type 和 Payload Length 读取完整 payload
3. 按 Type 分发到对应处理器
4. 收到未知 Type 时记录 WARN 日志，跳过该帧
5. payload 不完整时缓存到缓冲区，等待后续数据

### 4.5 Godot→控制端响应

`STATUS_REQUEST` 的响应帧：

```
Type: 0x07 (STATUS_REQUEST) — Godot→控制端 方向
Payload: JSON 字符串
{
  "connected": true,
  "robot_name": "ABB IRB4600-60/205",
  "joint_count": 6,
  "current_joints": [0.0, -0.5, 1.2, 0.0, 0.8, -0.3],
  "box_wall": {
    "total": 120,
    "grabbed": 45,
    "remaining": 75
  },
  "state": "MOVING_TO_BOX"
}
```

---

## 5. 机械臂配置与加载

### 5.1 目录约定

机械臂数据存放在项目根目录 `robot_arm/` 下，每个型号一个子目录，内部结构固定：

```
robot_arm/<型号名>/
├── urdf/
│   └── *.urdf              # 一个或多个 URDF 文件
└── meshes/
    ├── visual/              # DAE 可视化网格（文件名与 URDF link name 对应）
    │   ├── base_link.dae
    │   ├── link_1.dae
    │   └── ...
    └── collision/           # STL 碰撞网格（本项目不使用，保留目录结构）
```

**切换机械臂只需给出路径**，例如 `"robot_arm/abb_irb4600_60_205"` 即可加载该型号。无需额外配置文件。

### 5.2 URDF 解析器（UrdfParser.cs）

**输入**：URDF 文件路径（自动扫描 `robot_arm/<型号>/urdf/` 目录下第一个 `.urdf` 文件）

**输出**：结构化的 link/joint 数据模型

**需要从 URDF 中提取的信息**：

| URDF 元素 | 提取字段 | 用途 |
|---|---|---|
| `<robot name="...">` | `name` | 机械臂名称（显示在 UI 上） |
| `<link name="...">` | `name` | link 标识，用于匹配 visual mesh 文件名 |
| `<visual><geometry><mesh filename="..."/>` | mesh 路径 | 用于定位 DAE 文件（本项目不使用 URDF 内路径，改为按 link name 从 `meshes/visual/` 加载同名文件） |
| `<joint name="..." type="revolute">` | `name`, `type` | joint 标识和类型 |
| `<joint><parent link="..."/>` | `parent_link` | 关节父级 link，确定场景树层级 |
| `<joint><child link="..."/>` | `child_link` | 关节子级 link |
| `<joint><origin xyz="..." rpy="..."/>` | `origin` | 关节原点位置（xyz）和初始姿态（rpy），设置 joint_pivot 的 position 和 rotation |
| `<joint><axis xyz="..."/>` | `axis` | 关节旋转轴，设置 joint_pivot 的旋转轴 |
| `<joint><limit lower="..." upper="..."/>` | `lower`, `upper` | 关节限位角度（弧度），用于 UI 显示和校验 |

**解析流程**：

1. 扫描 `robot_arm/<型号>/urdf/` 目录，找到第一个 `.urdf` 文件
2. 使用 C# `XmlDocument` 解析 URDF XML
3. 提取 `<robot>` 下所有 `<link>` 和 `<joint>` 元素
4. 构建内部数据结构：
   ```
   RobotData:
     name: string
     links: LinkData[]        ← 所有 link
     joints: JointData[]      ← 所有 joint（按 parent-child 关系排序）
     joint_count: int         ← joint 数量

   LinkData:
     name: string

   JointData:
     name: string
     type: string             ← "revolute" / "prismatic" / ...
     parent_link: string
     child_link: string
     origin_xyz: Vector3
     origin_rpy: Vector3
     axis: Vector3
     lower: float
     upper: float
   ```
5. 按 parent-child 关系构建拓扑顺序（确保父节点先于子节点处理）

**URDF mesh 路径处理**：

URDF 中的 mesh filename 通常使用 ROS 包路径（如 `package://abb_irb4600_60_205/meshes/visual/base_link.dae`），本项目**不使用此路径**，而是根据 link name 直接从 `meshes/visual/` 目录加载同名 DAE 文件：
- link name = `base_link` → 加载 `robot_arm/<型号>/meshes/visual/base_link.dae`
- link name = `link_1` → 加载 `robot_arm/<型号>/meshes/visual/link_1.dae`

### 5.3 通用加载器（RobotLoader.cs）

**输入**：机械臂路径（如 `"robot_arm/abb_irb4600_60_205"`）

**输出**：构建完成的场景树 Node3D

**加载流程**：

1. 调用 UrdfParser 解析 `robot_arm/<型号>/urdf/*.urdf`，获取 RobotData
2. 创建根节点 `RobotRoot (Node3D)`
3. 找到 base link（没有 parent joint 的 link），加载其 DAE 网格 → 创建 `base_link (MeshInstance3D)`，挂在 RobotRoot 下
4. 遍历 joints[]（按拓扑顺序），对每个 joint：
   a. 在 parent_link 对应的节点下创建 `joint_pivot (Node3D)`
   b. 设置 `joint_pivot.Position = joint.origin_xyz`
   c. 设置 `joint_pivot.Rotation = new Vector3(joint.origin_rpy)` （Euler 角）
   d. 加载 child_link 的 DAE 网格 → 创建 `link_mesh (MeshInstance3D)`
   e. 将 link_mesh 添加到 joint_pivot 下
   f. 将 joint_pivot 引用存入 `joints[]` 数组
5. 在最后一个 link 的 joint_pivot 下创建 `gripper (Node3D)` 作为末端坐标延伸点
6. 设置默认材质（RobotMaterial.tres）
7. 返回 RobotRoot，同时返回 joint_count 供通信模块使用

**场景树结构**：

```
RobotRoot (Node3D)
  └── base_link (MeshInstance3D)
       └── joint_1_pivot (Node3D) ← joints[0]
            └── link_1 (MeshInstance3D)
                 └── joint_2_pivot (Node3D) ← joints[1]
                      └── link_2 (MeshInstance3D)
                           └── joint_3_pivot (Node3D) ← joints[2]
                                └── ...
                                     └── joint_N_pivot (Node3D) ← joints[N-1]
                                          └── gripper (Node3D) ← 末端延伸点
```

**DAE 网格加载**：

- 使用 Godot 的 `ResourceLoader.Load<PackedScene>("robot_arm/<型号>/meshes/visual/<link_name>.dae")` 加载
- DAE 文件导入 Godot 后自动转为 `.tres` 资源（Godot import 系统）
- 如果 DAE 加载失败，记录 ERROR 日志并跳过该 link（不阻塞整体加载）

### 5.4 切换机械臂

1. 移除当前 RobotRoot 节点（`QueueFree()`）
2. 调用 RobotLoader.Load("robot_arm/<新型号>") 构建新场景树
3. 添加到场景根节点
4. `joints[]` 数组和 `joint_count` 自动更新，通信模块无需改动
5. 触发 `RobotChanged` 事件通知 UI 更新

**新增一台机械臂的步骤**：

1. 在 `robot_arm/` 下创建新目录（如 `robot_arm/kuka_kr16/`）
2. 放入 URDF 文件到 `urdf/` 子目录
3. 放入 DAE 网格到 `meshes/visual/` 子目录（文件名需与 URDF link name 一致）
4. 完成——启动时指定路径或运行时发送 LOAD_ROBOT 即可加载

### 5.5 坐标系转换（CoordinateConverter.cs）

- 默认：Godot 原生坐标系（右手系，Y 朝上，1 单位 = 1 米），数据直通
- 预留转换函数接口：`ConvertPosition()`、`ConvertRotation()`
- 当控制端使用不同坐标系时，在此模块中实现转换逻辑

---

## 6. 纸箱墙模块

### 6.1 纸箱墙数据格式（JSON）

```json
{
  "version": "1.0",
  "wall_id": "wall_A",
  "boxes": [
    {
      "id": 0,
      "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "rotation_deg": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "size": { "x": 0.3, "y": 0.2, "z": 0.6 },
      "color": "#C4A882"
    },
    {
      "id": 1,
      "position": { "x": 0.31, "y": 0.0, "z": 0.0 },
      "rotation_deg": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "size": { "x": 0.3, "y": 0.2, "z": 0.6 },
      "color": "#C4A882"
    }
  ]
}
```

### 6.2 渲染方案

- 使用 `MultiMeshInstance3D` + `BoxMesh`，单次 draw call 渲染全部纸箱
- 每个纸箱通过 `set_instance_transform(index)` 设置位置/旋转/缩放
- 每个纸箱通过 `set_instance_color(index)` 设置颜色
- 材质使用 CardboardMaterial.tres（PBR 瓦楞纸效果）
- 预期规模：100-200 个纸箱，性能充裕

### 6.3 纸箱状态管理

每个纸箱维护一个 `BoxInstance` 状态对象：

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | int | 纸箱 ID |
| `position` | Vector3 | 在墙中的原始位置 |
| `size` | Vector3 | 尺寸 |
| `state` | enum | `WAITING` / `TARGETED` / `GRABBED` / `RELEASED` |
| `multiMeshIndex` | int | 在 MultiMesh 中的实例索引 |

### 6.4 纸箱视觉状态变化

| 状态 | 视觉表现 |
|---|---|
| `WAITING` | 默认颜色（牛皮纸色 #C4A882） |
| `TARGETED` | 高亮边框 / 发光（通过 instance color 变亮） |
| `GRABBED` | 从 MultiMesh 中隐藏（scale 设为 0），创建独立 MeshInstance3D 作为夹爪子节点跟随运动 |
| `RELEASED` | 在目标位置短暂显示（默认 2 秒），然后淡出消失（透明度动画 → 移除节点） |

### 6.5 抓取过渡效果

- BOX_GRAB 信号到达时：纸箱从墙中取出，创建独立 MeshInstance3D
- 过渡动画（~0.3 秒）：纸箱从原始位置平滑移动到夹爪当前位置（使用 `Tween` 插值）
- 附着到夹爪后：设为 gripper 节点的子节点，自动跟随

---

## 7. 抓取状态机

### 7.1 状态定义

```
IDLE → WALL_READY → MOVING_TO_BOX → GRIPPING → MOVING_TO_TARGET
  → RELEASING → RETURNING → (检查剩余纸箱) → MOVING_TO_BOX 或 COMPLETE
```

| 状态 | 触发条件 | 动作 |
|---|---|---|
| `IDLE` | 系统启动 | 等待纸箱墙数据 |
| `WALL_READY` | 收到 BOX_WALL_DATA | 生成纸箱墙，选择第一个目标 |
| `MOVING_TO_BOX` | 收到 TARGET_BOX | 高亮目标纸箱，机械臂运动（被动接收关节角度） |
| `GRIPPING` | 收到 BOX_GRAB | 纸箱附着夹爪，过渡动画 |
| `MOVING_TO_TARGET` | 抓取完成，关节角度持续更新 | 机械臂搬运中 |
| `RELEASING` | 收到 BOX_RELEASE | 纸箱放置到目标位置，淡出动画 |
| `RETURNING` | 纸箱已消失 | 检查剩余纸箱 |
| `COMPLETE` | 所有纸箱已处理 | 通知 UI，显示完成状态 |

### 7.2 状态机实现要求

- 单例模式，全局可访问
- 状态变更时触发事件（UI 订阅更新显示）
- 记录状态变更日志
- 支持 SCENE_RESET 指令重置到 IDLE

---

## 8. 相机系统

### 8.1 三种相机模式

| 模式 | 类 | 行为 |
|---|---|---|
| **自由轨道** | `FreeOrbitCamera.cs` | 鼠标左键拖拽旋转，滚轮缩放，右键平移，中键重置 |
| **固定监控** | `FixedCamera.cs` | 预设位置和朝向，观察全局场景 |
| **末端跟随** | `EndEffectorCamera.cs` | 绑定到 gripper 节点，跟随机械臂末端运动，可设置偏移 |

### 8.2 切换方式

- UI 下拉菜单或快捷键（1/2/3）切换
- 切换时平滑过渡（Tween 插值相机位置和朝向）
- `CameraManager.cs` 管理当前活跃相机，通过设置 `Current = true` 切换

---

## 9. UI 布局

### 9.1 主界面布局

```
┌──────────────────────────────────────────────────────────┐
│  顶部栏：项目标题 | 连接状态 | 当前机械臂 | 当前状态       │
├──────────────────────────────────┬───────────────────────┤
│                                  │  关节角度面板           │
│                                  │  ├─ Joint 1:  0.00°   │
│                                  │  ├─ Joint 2: -28.6°   │
│     3D 视口                      │  ├─ Joint 3:  68.8°   │
│  （SubViewportContainer）         │  ├─ Joint 4:   0.0°   │
│                                  │  ├─ Joint 5:  45.9°   │
│                                  │  └─ Joint 6: -17.2°   │
│                                  ├───────────────────────┤
│                                  │  抓取进度              │
│                                  │  [████████░░] 45/120  │
│                                  ├───────────────────────┤
│                                  │  控制面板              │
│                                  │  [重置场景] [切换相机]  │
│                                  │  [加载纸箱墙] [断开连接] │
│                                  ├───────────────────────┤
│                                  │  事件日志              │
│                                  │  10:23:01 连接已建立    │
│                                  │  10:23:02 纸箱墙已加载  │
│                                  │  10:23:05 抓取纸箱 #0   │
│                                  │  ...                   │
├──────────────────────────────────┴───────────────────────┤
│  底部状态栏：FPS | 网络延迟 | 纸箱数 | Godot 版本           │
└──────────────────────────────────────────────────────────┘
```

### 9.2 UI 面板清单

| 面板 | 场景文件 | 功能 |
|---|---|---|
| ConnectionPanel | `UI/ConnectionPanel.tscn` | 显示连接状态（IP、端口、已连接/断开/重连中）、手动断开按钮 |
| JointDisplayPanel | `UI/JointDisplayPanel.tscn` | 6+ 个关节角度实时数值显示，等宽字体，颜色编码（正常绿/异常红） |
| GraspProgressPanel | `UI/GraspProgressPanel.tscn` | 进度条 + 已抓取/总数计数 |
| ControlPanel | `UI/ControlPanel.tscn` | 重置场景、相机切换下拉、加载纸箱墙按钮、断开连接按钮 |
| EventLogPanel | `UI/EventLogPanel.tscn` | 滚动文本区域，显示带时间戳的事件日志，最多 500 行 |

### 9.3 UI 主题

- 使用 `DefaultTheme.tres` 统一管理
- 暗色主题为主
- 面板使用半透明背景（`StyleBoxFlat`，alpha 0.8）
- 等宽字体（Mono.ttf）用于数据显示

---

## 10. 日志系统

### 10.1 日志级别

| 级别 | 用途 |
|---|---|
| `DEBUG` | 协议帧收发详情、关节角度原始值、内部状态变更 |
| `INFO` | 连接/断开、纸箱墙加载、抓取完成、机械臂切换 |
| `WARN` | 未知消息类型、payload 不完整、坐标转换异常 |
| `ERROR` | 协议解析失败、模型加载失败、TCP 异常 |

### 10.2 输出

- **UI**：EventLogPanel 实时显示 INFO 及以上级别日志
- **文件**：`Logs/grasp_2026-04-22.log`，记录 DEBUG 及以上级别
- **Godot 控制台**：ERROR 级别输出到 GD.PrintErr

### 10.3 日志格式

```
[2026-04-22 10:23:05.123] [INFO ] [GraspSM] BOX_GRAB received, box_id=42
[2026-04-22 10:23:05.456] [DEBUG] [TcpServer] Frame received: type=0x01, len=24
[2026-04-22 10:23:06.789] [WARN ] [Protocol] Unknown message type 0xFF, skipped
```

---

## 11. 嵌入集成

### 11.1 独立运行模式

- 直接运行导出的 Windows 可执行文件
- 启动后自动 TCP 监听，显示完整 UI

### 11.2 SubViewport 嵌入模式（供其他 Godot 项目）

- 将主 3D 场景封装为 `RobotTwin.tscn`（SubViewportContainer + SubViewport）
- 其他 Godot 项目通过 `ResourceLoader.Load("path/to/RobotTwin.tscn").Instantiate()` 加载
- 提供 `StartServer(port)` 和 `StopServer()` 公开方法
- 通过信号（Signal）对外暴露状态变更事件

### 11.3 Spout 嵌入模式（供 WPF 应用）

- 将 3D 场景渲染到 SubViewport
- 通过 godot-spout GDExtension 插件发送纹理
- WPF 端使用 Spout.NET 接收并显示
- 此功能作为可选模块，需要时集成

---

## 12. 环境场景

### 12.1 简化工厂环境

`FactoryFloor.tscn` 包含：

| 元素 | 实现方式 | 说明 |
|---|---|---|
| 地面 | `MeshInstance3D` + `PlaneMesh` + FloorMaterial | 大面积地面，接受阴影 |
| 天空 | `WorldEnvironment` + `Sky` 资源 | 简单天空背景 |
| 灯光 | `DirectionalLight3D` + `WorldEnvironment` 环境光 | 主光源 + 环境填充光 |
| 工作台 | `CSGBox3D` 或简单网格 | 纸箱墙放置区域 |
| 背景墙 | `CSGBox3D` | 简单背景，不遮挡视线 |

### 12.2 灯光要求

- 1 个 DirectionalLight3D（主光源，启用阴影）
- 1 个 WorldEnvironment（环境光 + 天空）
- 可选：1-2 个 PointLight3D 补光

---

## 13. 开发阶段与任务拆分

### 阶段 1：场景加载（第 1-2 周）

> 目标：完成所有静态资源的加载能力——机械臂模型解析与显示、纸箱动态生成、基础场景搭建。本阶段不涉及通信，不涉及抓取逻辑。

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 1.1 | 创建 Godot 4.6 C# 项目，搭建文件结构 | project.godot + 目录结构 | 编译运行无报错 |
| 1.2 | 实现 AppConfig.cs，读取 app.json | 配置加载模块 | 能正确读取并打印所有配置项 |
| 1.3 | 实现 UrdfParser.cs URDF 解析器 | URDF 解析模块 | 能正确解析 IRB4600 的 URDF，提取所有 link/joint 参数（origin、axis、limits、parent-child 关系） |
| 1.4 | 实现 RobotLoader.cs 通用加载器 | 加载器模块 | 给定 "robot_arm/abb_irb4600_60_205" 路径后，自动解析 URDF + 加载 DAE 网格，场景树正确显示 |
| 1.5 | 实现 BoxWallLoader.cs 单个纸箱加载 | 纸箱生成 | 给定一个纸箱的 JSON 数据，能在场景中正确生成对应位置的方块 |
| 1.6 | 实现 BoxWallLoader.cs MultiMesh 批量生成 | 纸箱墙生成 | 给定纸箱墙 JSON 数据，使用 MultiMeshInstance3D 批量生成 100+ 纸箱 |
| 1.7 | 搭建简化工厂环境场景 | FactoryFloor.tscn | 地面、灯光、工作台正常显示 |
| 1.8 | 搭建主场景布局（3D 视口 + UI 容器占位） | Main.tscn | 3D 视口正常渲染环境 + 机械臂 + 纸箱 |

**阶段 1 完成标志**：启动项目后，场景中同时显示 IRB4600 机械臂和一面 100+ 纸箱墙，可通过编辑器手动调整关节角度验证视觉正确性。

### 阶段 2：简单功能实现——单个纸箱抓取全流程（第 2-3 周）

> 目标：实现单个纸箱的完整抓取→搬运→卸下流程。**不使用 TCP 通信**，机械臂运动由预先写好的关节角度序列驱动（硬编码关键帧），纸箱生成也通过硬编码数据。目的是在无通信依赖下验证抓取全流程。

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 2.1 | 实现 RobotController.cs 关节角度直接设置 | 关节驱动 | 调用接口设置 6 个关节角度，机械臂正确旋转到目标姿态 |
| 2.2 | 编写固定关节序列（关键帧插值） | 运动序列 | 定义一组关节角度关键帧序列，机械臂按序列平滑运动 |
| 2.3 | 实现关节序列播放器（Tween 插值） | 序列播放 | 按时间轴插值播放关键帧序列，运动平滑无跳变 |
| 2.4 | 实现单个纸箱的硬编码加载 | 单纸箱显示 | 场景中显示一个纸箱在指定位置 |
| 2.5 | 实现纸箱高亮效果（TARGET 模拟） | 目标指示 | 指定纸箱 ID 后颜色变亮 |
| 2.6 | 实现纸箱附着夹爪 + 过渡动画（BOX_GRAB 模拟） | 抓取可视化 | 机械臂到达纸箱位置后，纸箱从原位平滑移到夹爪并跟随 |
| 2.7 | 实现纸箱放置 + 淡出动画（BOX_RELEASE 模拟） | 释放可视化 | 夹爪打开后，纸箱在目标位置短暂显示然后淡出消失 |
| 2.8 | 串联全流程：移动→抓取→搬运→释放 | 完整单次抓取 | 点击"开始"后，机械臂按固定序列完成一次完整的抓取→搬运→释放流程 |

**阶段 2 完成标志**：场景中有一个纸箱，点击按钮后机械臂自动完成"移动到纸箱→抓取→搬运到目标位置→放下纸箱→纸箱消失"的完整流程。

### 阶段 3：两个纸箱的先后抓取（第 3 周）

> 目标：扩展到两个纸箱的顺序抓取，验证状态管理和纸箱状态追踪逻辑。

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 3.1 | 实现 GraspStateMachine.cs 基础状态机 | 状态机 | 状态枚举定义，IDLE / MOVING / GRIPPING / RELEASING / COMPLETE 等状态 |
| 3.2 | 实现 BoxInstance.cs 纸箱状态追踪 | 状态管理 | 每个纸箱有 WAITING / TARGETED / GRABBED / RELEASED 状态 |
| 3.3 | 实现 BoxWallManager.cs 纸箱列表管理 | 列表管理 | 维护纸箱列表，支持按 ID 查询、状态更新、完成计数 |
| 3.4 | 硬编码两个纸箱位置和两套关节序列 | 双纸箱数据 | 场景中显示两个纸箱 |
| 3.5 | 实现顺序抓取逻辑 | 顺序抓取 | 完成第一个纸箱抓取→回到初始位→抓取第二个纸箱→全部完成 |
| 3.6 | 实现抓取完成后纸箱从 MultiMesh 中隐藏 | 状态同步 | 已抓取的纸箱从墙中消失（MultiMesh instance scale = 0） |

**阶段 3 完成标志**：场景中有两个纸箱，自动依次完成两次完整的抓取→搬运→释放流程，每个纸箱被抓后从墙中消失。

### 阶段 4：实现通信层 + 通信驱动抓取（第 4-5 周）

> 目标：实现 TCP 通信协议，将阶段 2/3 的硬编码驱动替换为通信驱动。完成后能通过 TCP 控制单个和两个纸箱的抓取。

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 4.1 | 实现 ProtocolTypes.cs（消息类型常量定义） | 协议常量 | 所有 Type ID 和 Payload 结构正确定义 |
| 4.2 | 实现 ProtocolParser.cs（帧解析） | 协议解析器 | 能正确解析完整帧、处理半帧、校验 Magic、缓存不完整帧 |
| 4.3 | 实现 TcpServer.cs（监听、连接、接收） | TCP 服务器 | 启动监听、接受连接、持续接收数据流 |
| 4.4 | 实现 JOINT_STATE → 关节旋转更新 | 通信驱动关节 | 收到 JOINT_STATE 后机械臂实时旋转 |
| 4.5 | 实现 GRIPPER_STATE → 夹爪状态更新 | 通信驱动夹爪 | 收到 GRIPPER_STATE 后夹爪开合状态变化 |
| 4.6 | 实现 TARGET_BOX（含 6D 位姿）→ 纸箱高亮 | 通信驱动目标 | 收到 TARGET_BOX 后指定纸箱变色高亮 |
| 4.7 | 实现 BOX_GRAB（含 6D 位姿）→ 纸箱附着夹爪 | 通信驱动抓取 | 收到 BOX_GRAB 后纸箱在指定位姿附着到夹爪 |
| 4.8 | 实现 BOX_RELEASE（含 6D 位姿）→ 纸箱放置 + 淡出 | 通信驱动释放 | 收到 BOX_RELEASE 后纸箱在 6D 位姿处放置并淡出 |
| 4.9 | 实现心跳保活与断线重连 | 连接管理 | 心跳超时检测、断线日志、自动重连 |
| 4.10 | 实现 BOX_WALL_DATA → 纸箱墙生成 | 通信驱动纸箱墙 | 收到 JSON 后正确生成 MultiMesh 纸箱墙 |
| 4.11 | 实现通信驱动单纸箱抓取 | 集成测试 | 通过 TCP 发送指令序列，完成单纸箱抓取全流程 |
| 4.12 | 实现通信驱动双纸箱抓取 | 集成测试 | 通过 TCP 发送指令序列，完成两纸箱顺序抓取全流程 |

**阶段 4 完成标志**：Godot 启动后自动 TCP 监听，外部程序通过 TCP 发送 JOINT_STATE / TARGET_BOX / BOX_GRAB / BOX_RELEASE 等指令，Godot 端实时响应，完成单纸箱和双纸箱抓取。

### 阶段 5：整面纸箱墙抓取（第 5-6 周）

> 目标：将抓取逻辑扩展到整面纸箱墙（100-200 个），验证大规模场景下的性能和状态管理。

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 5.1 | 实现整面墙的纸箱状态管理（100+ 实例） | 大规模状态管理 | 100+ 纸箱的 WAITING/TARGETED/GRABBED/RELEASED 状态正确切换 |
| 5.2 | 实现批量纸箱的 MultiMesh 隐藏/恢复 | 批量更新 | 抓取后从墙中隐藏、释放后创建独立实例的性能正常 |
| 5.3 | 实现抓取进度追踪（已完成/总数） | 进度管理 | 实时统计并可通过接口查询抓取进度 |
| 5.4 | 实现整面墙抓取完成检测 | 完成检测 | 所有纸箱处理完毕后状态机转到 COMPLETE |
| 5.5 | 实现 SCENE_RESET → 场景完全重置 | 重置功能 | 所有纸箱清除，机械臂归零，状态回到 IDLE |
| 5.6 | 性能测试与优化 | 性能报告 | 100-200 纸箱场景下 ≥ 60 FPS，通信处理延迟 < 1ms |
| 5.7 | 整面墙抓取端到端测试 | 集成测试 | TCP 驱动完成 100+ 纸箱的完整抓取流程 |

**阶段 5 完成标志**：通过 TCP 发送纸箱墙数据 + 指令序列，Godot 端完成整面纸箱墙的逐个抓取→搬运→释放，全程流畅无卡顿。

### 阶段 6：UI 与交互（第 6-7 周）

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 6.1 | 实现 Logger.cs 日志系统 | 日志模块 | UI 输出 + 文件输出 + 级别过滤（DEBUG/INFO/WARN/ERROR） |
| 6.2 | 实现 DefaultTheme.tres | UI 主题 | 暗色主题，面板样式统一 |
| 6.3 | 实现 ConnectionPanel | 连接面板 | 显示连接状态（IP、端口、已连接/断开），手动断开按钮 |
| 6.4 | 实现 JointDisplayPanel | 关节面板 | N 个关节角度实时更新显示（N 由 URDF 决定），等宽字体 |
| 6.5 | 实现 GraspProgressPanel | 进度面板 | 进度条 + 已抓取/总数计数实时更新 |
| 6.6 | 实现 ControlPanel | 控制面板 | 重置场景、相机切换、加载纸箱墙、断开连接等按钮 |
| 6.7 | 实现 EventLogPanel | 日志面板 | 带时间戳的事件流滚动显示，最多 500 行 |

### 阶段 7：相机与环境（第 7 周）

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 7.1 | 实现 FreeOrbitCamera.cs | 自由轨道相机 | 鼠标左键拖拽旋转、滚轮缩放、右键平移、中键重置 |
| 7.2 | 实现 FixedCamera.cs | 固定相机 | 预设位置和朝向正确 |
| 7.3 | 实现 EndEffectorCamera.cs | 末端相机 | 绑定 gripper 节点，跟随末端运动 |
| 7.4 | 实现 CameraManager.cs 切换逻辑 | 相机管理 | 下拉菜单 / 快捷键（1/2/3）切换，平滑过渡 |
| 7.5 | 完善工厂环境（材质、阴影、光照） | 环境优化 | PBR 材质、阴影正常、视觉舒适 |

### 阶段 8：高级功能与集成（第 8 周）

| # | 任务 | 产出 | 验收标准 |
|---|---|---|---|
| 8.1 | 实现 LOAD_ROBOT 运行时切换机械臂 | 切换功能 | TCP 发送 LOAD_ROBOT 后场景正确更换，关节数自适应 |
| 8.2 | 实现 CoordinateConverter.cs | 坐标转换 | 默认直通，转换函数接口可用 |
| 8.3 | 实现 STATUS_REQUEST 响应 | 状态查询 | 控制端请求后收到完整状态 JSON |
| 8.4 | 实现 SubViewport 嵌入封装 | 嵌入模块 | RobotTwin.tscn 可被其他 Godot 项目加载，暴露公开方法 |
| 8.5 | Windows 导出配置与打包 | 可执行文件 | 导出 .exe 独立运行，无额外依赖 |
| 8.6 | 端到端联调测试 | 测试报告 | 使用真实控制端完成全流程测试，输出测试报告 |

---

## 14. 关键约束与约定

### 14.1 编码约定

- 命名空间：按模块划分（`Grasp.Robot`、`Grasp.Network`、`Grasp.UI` 等）
- C# 脚本使用 Godot 4.6 的 C# API（非 GDScript）
- 事件使用 Godot Signal 机制（`[Signal]` 属性）
- 单例管理器通过 `AutoLoad` 注册

### 14.2 性能约束

| 指标 | 目标 |
|---|---|
| 渲染帧率 | ≥ 60 FPS（纸箱墙 100-200 个 + 机械臂） |
| 协议处理延迟 | < 1 ms（单帧处理所有待处理消息） |
| 内存占用 | < 500 MB |
| 启动时间 | < 5 秒 |

### 14.3 架构约束

- **无物理引擎**：所有运动均为运动学，不使用 RigidBody / HingeJoint
- **被动渲染**：Godot 不发送控制指令，只接收并显示
- **配置驱动**：机械臂通过目录约定加载（无额外配置文件），端口、日志级别等通过 app.json 控制
- **模块解耦**：Network、Robot、BoxWall、Grasp、Camera、UI 各模块通过信号/事件通信，不直接调用

---

## 15. 风险与应对

| 风险 | 影响 | 应对 |
|---|---|---|
| DAE 网格导入后材质/缩放异常 | 视觉不正确 | 预留阶段 1.4 的调试时间，准备 Blender 中转方案 |
| TCP 高频数据导致卡顿 | 关节运动不流畅 | 协议解析在独立线程中缓冲，`_Process` 中批量应用 |
| 坐标系不一致 | 位置/角度错位 | CoordinateConverter 预留接口，按需实现转换 |
| 协议设计需要调整 | 开发返工 | 协议解析器与业务逻辑解耦，消息处理独立注册 |
| Spout 嵌入不可用 | WPF 集成受阻 | 作为可选功能，不影响核心流程 |
