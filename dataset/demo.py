import numpy as np
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection


# ===================== 四元数与旋转矩阵（与 Godot C# 一致） =====================

def quat_multiply(q1, q2):
    """四元数乘法 (x,y,z,w)，与 Godot Quaternion * 一致"""
    x1, y1, z1, w1 = q1
    x2, y2, z2, w2 = q2
    return np.array([
        w1*x2 + x1*w2 + y1*z2 - z1*y2,
        w1*y2 - x1*z2 + y1*w2 + z1*x2,
        w1*z2 + x1*y2 - y1*x2 + z1*w2,
        w1*w2 - x1*x2 - y1*y2 - z1*z2,
    ])


def quat_to_rotation_matrix(q):
    """四元数 (x,y,z,w) → 旋转矩阵，与 Godot Basis(Quaternion) 一致"""
    q = q / np.linalg.norm(q)
    x, y, z, w = q
    xx, yy, zz, ww = x*x, y*y, z*z, w*w
    xy, xz, yz = x*y, x*z, y*z
    wx, wy, wz = w*x, w*y, w*z
    return np.array([
        [ww + xx - yy - zz, 2*(xy - wz),       2*(xz + wy)],
        [2*(xy + wz),       ww - xx + yy - zz, 2*(yz - wx)],
        [2*(xz - wy),       2*(yz + wx),       ww - xx - yy + zz]
    ])


def rotation_to_euler_xyz(R):
    """XYZ 固有欧拉角 (弧度)，与 Godot Basis.GetEuler() 默认一致
    重建: R = Rx(a) * Ry(b) * Rz(c)"""
    sy = np.clip(R[0, 2], -1, 1)
    b = np.arcsin(sy)
    if abs(np.cos(b)) > 1e-6:
        a = np.arctan2(-R[1, 2], R[2, 2])
        c = np.arctan2(-R[0, 1], R[0, 0])
    else:
        a = np.arctan2(R[2, 1], R[2, 2])
        c = 0.0
    return np.array([a, b, c])


def rotation_to_euler_yxz(R):
    """YXZ 固有欧拉角 (弧度)，与 Godot Basis.GetEuler(EulerOrder.Yxz) 一致
    重建: R = Ry(b) * Rx(a) * Rz(c)"""
    sa = np.clip(-R[1, 2], -1, 1)
    a = np.arcsin(sa)
    if abs(np.cos(a)) > 1e-6:
        b = np.arctan2(R[0, 2], R[2, 2])
        c = np.arctan2(R[1, 0], R[1, 1])
    else:
        b = np.arctan2(-R[2, 0], R[0, 0])
        c = 0.0
    return np.array([a, b, c])


def euler_xyz_to_rotation(euler_rad):
    """XYZ 固有欧拉角 (弧度) → 旋转矩阵: R = Rx(a) * Ry(b) * Rz(c)"""
    a, b, c = euler_rad
    Rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]])
    Ry = np.array([[np.cos(b), 0, np.sin(b)], [0, 1, 0], [-np.sin(b), 0, np.cos(b)]])
    Rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
    return Rx @ Ry @ Rz


def euler_yxz_to_rotation(euler_rad):
    """YXZ 固有欧拉角 (弧度) → 旋转矩阵: R = Ry(b) * Rx(a) * Rz(c)
    与 Godot Node3D.Rotation 重建一致"""
    a, b, c = euler_rad
    Rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]])
    Ry = np.array([[np.cos(b), 0, np.sin(b)], [0, 1, 0], [-np.sin(b), 0, np.cos(b)]])
    Rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz


# ===================== 数据解析 =====================

def parse_box_data(filepath):
    with open(filepath, 'r') as f:
        line = f.readline().strip()
    parts = line.split(';')
    box_count = int(parts[1])
    boxes = []
    for i in range(2, 2 + box_count):
        vals = parts[i].split(',')
        x, y, z = float(vals[0]) / 1000, -float(vals[1]) / 1000, -float(vals[2]) / 1000
        qa, qb, qc, qd = float(vals[3]), float(vals[4]), float(vals[5]), float(vals[6])
        w, h = float(vals[7]), float(vals[8])
        d = 0.2
        boxes.append({
            'pos': np.array([x, y, z]),
            'raw_quat': np.array([qa, qb, qc, qd]),
            'size': np.array([w, h, d]),
        })
    return boxes


# ===================== 抓取面计算 =====================

def find_grab_surface(box_pos, R, box_size, std_R):
    """与 C# CalculateGrabTransform 算法一致"""
    std_x = std_R[:, 0]
    std_y = std_R[:, 1]

    dots = [abs(R[:, k] @ std_x) for k in range(3)]
    k = int(np.argmax(dots))

    half = box_size[k] / 2.0
    pos_plus = box_pos + half * R[:, k]
    pos_minus = box_pos - half * R[:, k]

    if np.linalg.norm(pos_plus) < np.linalg.norm(pos_minus):
        surface_pos = pos_plus
        sign = 1
    else:
        surface_pos = pos_minus
        sign = -1

    grab_x = -sign * R[:, k]
    grab_x /= np.linalg.norm(grab_x)

    remaining = [i for i in range(3) if i != k]
    dots_y = [abs(R[:, r] @ std_y) for r in remaining]
    best_r = remaining[int(np.argmax(dots_y))]
    grab_y = R[:, best_r].copy()
    grab_y = grab_y - np.dot(grab_y, grab_x) * grab_x
    grab_y /= np.linalg.norm(grab_y)

    grab_z = np.cross(grab_x, grab_y)
    grab_z /= np.linalg.norm(grab_z)

    R_grab = np.column_stack([grab_x, grab_y, grab_z])
    return surface_pos, R_grab, k


def cal_lift_hmat(surface_pos, R_grab, lift_distance):
    """沿抓取面 -X 方向（向外）移动 lift_distance"""
    grab_x = R_grab[:, 0]
    lift_pos = surface_pos - lift_distance * grab_x
    R_lift = R_grab
    return lift_pos, R_lift


# ===================== 可视化工具 =====================

def get_box_faces(center, R, size):
    half = size / 2.0
    local_verts = np.array([
        [-1, -1, -1], [+1, -1, -1], [+1, +1, -1], [-1, +1, -1],
        [-1, -1, +1], [+1, -1, +1], [+1, +1, +1], [-1, +1, +1],
    ], dtype=float) * half
    verts = (R @ local_verts.T).T + center
    faces = [
        [verts[0], verts[1], verts[2], verts[3]],
        [verts[4], verts[5], verts[6], verts[7]],
        [verts[0], verts[1], verts[5], verts[4]],
        [verts[2], verts[3], verts[7], verts[6]],
        [verts[0], verts[3], verts[7], verts[4]],
        [verts[1], verts[2], verts[6], verts[5]],
    ]
    return faces


def draw_frame(ax, pos, R, length=0.5, label="", lw=1.5, colors=None):
    if colors is None:
        colors = ['#e74c3c', '#2ecc71', '#3498db']
    for j in range(3):
        ax.quiver(pos[0], pos[1], pos[2],
                  R[0, j] * length, R[1, j] * length, R[2, j] * length,
                  color=colors[j], linewidth=lw, arrow_length_ratio=0.15)
    if label:
        ax.text(pos[0], pos[1], pos[2] + length * 0.3, label,
                fontsize=7, ha='center', va='bottom', fontweight='bold')


# ===================== 主程序 =====================

BOX_SIZE = np.array([0.587, 0.452, 0.2])
boxes = parse_box_data('box_position.txt')

i = 0
box = boxes[i]

# --- 帧旋转：frameQuat * rawQuat（与 C# Quaternion.FromEuler(PI,0,0) * rawQuat 一致） ---
frame_quat = np.array([1.0, 0.0, 0.0, 0.0])  # (x,y,z,w) = 180° around X
raw_quat = box['raw_quat'] / np.linalg.norm(box['raw_quat'])
combined_quat = quat_multiply(frame_quat, raw_quat)
combined_quat /= np.linalg.norm(combined_quat)
box['quat'] = combined_quat

# --- 旋转矩阵：直接从四元数（与 C# new Basis(quat) 一致） ---
box['R'] = quat_to_rotation_matrix(combined_quat)

# --- 欧拉角：XYZ 固有（与 C# Basis.GetEuler() 默认一致） ---
box['euler_xyz'] = np.degrees(rotation_to_euler_xyz(box['R']))

# --- End effector 旋转：YXZ 固有（与 C# Node3D.Rotation 一致） ---
ee_euler_yxz = np.radians(np.array([-90.0, 0.0, 0.0]))
ee_R = euler_yxz_to_rotation(ee_euler_yxz)

# --- 抓取面计算 ---
surface_pos, R_grab, normal_axis = find_grab_surface(
    box['pos'], box['R'], box['size'], ee_R
)
box['grab_pos'] = surface_pos
box['grab_R'] = R_grab
box['grab_euler_xyz'] = np.degrees(rotation_to_euler_xyz(R_grab))
box['normal_axis'] = normal_axis

# --- Lift 位姿 ---
box_lift_distance = box['size'][0] / 2
lift_pos, R_lift = cal_lift_hmat(surface_pos, R_grab, lift_distance=box_lift_distance)
box['lift_pose'] = lift_pos
box['lift_R'] = R_lift

# --- 打印（与 C# LogPose 输出格式一致） ---
print(f"Box {i}: pos=({box['pos'][0]:7.3f}, {box['pos'][1]:7.3f}, {box['pos'][2]:7.3f}) "
      f"euler(deg)=({box['euler_xyz'][0]:6.1f}, {box['euler_xyz'][1]:6.1f}, {box['euler_xyz'][2]:6.1f})")
print(f"Grab: pos=({box['grab_pos'][0]:7.3f}, {box['grab_pos'][1]:7.3f}, {box['grab_pos'][2]:7.3f}) "
      f"euler(deg)=({box['grab_euler_xyz'][0]:6.1f}, {box['grab_euler_xyz'][1]:6.1f}, {box['grab_euler_xyz'][2]:6.1f})")
print(f"Lift: pos=({box['lift_pose'][0]:7.3f}, {box['lift_pose'][1]:7.3f}, {box['lift_pose'][2]:7.3f}) "
      f"euler(deg)=({np.degrees(rotation_to_euler_xyz(R_lift))[0]:6.1f}, {np.degrees(rotation_to_euler_xyz(R_lift))[1]:6.1f}, {np.degrees(rotation_to_euler_xyz(R_lift))[2]:6.1f})")
print(f"NormalAxis: {normal_axis}  ({'XYZ'[normal_axis]})")

# --- 可视化 ---
fig = plt.figure(figsize=(14, 8))
ax = fig.add_subplot(111, projection='3d')

box_colors = plt.cm.tab20(np.linspace(0, 1, len(boxes)))

faces = get_box_faces(box['pos'], box['R'], box['size'])
poly = Poly3DCollection(faces, alpha=0.5, facecolor=box_colors[0],
                        edgecolor='k', linewidth=0.5)
ax.add_collection3d(poly)

draw_frame(ax, box['pos'], box['R'], length=0.3, label=str(0), lw=1.2)

grab_colors = ['#ff6b35', '#ffc857', '#2ec4b6']
draw_frame(ax, box['grab_pos'], box['grab_R'], length=0.4,
            label=f'G{0}', lw=1.0, colors=grab_colors)

ax.plot3D(*zip(box['pos'], box['grab_pos']),
            ':', color='gray', linewidth=0.6, alpha=0.5)

lift_colors = ['#9b59b6', '#e84393', '#6c5ce7']
draw_frame(ax, box['lift_pose'], box['lift_R'], length=0.4,
            label='L0', lw=1.0, colors=lift_colors)

ax.plot3D(*zip(box['grab_pos'], box['lift_pose']),
            '-', color='#9b59b6', linewidth=1.0, alpha=0.7)

all_pos = np.array([box['pos']])
margin = 1.0
ax.set_xlim(all_pos[:, 0].min() - margin, all_pos[:, 0].max() + margin)
ax.set_ylim(all_pos[:, 1].min() - margin, all_pos[:, 1].max() + margin)
ax.set_zlim(all_pos[:, 2].min() - margin, all_pos[:, 2].max() + margin)

ax.set_xlabel('X (m)')
ax.set_ylabel('Y (m)')
ax.set_zlabel('Z (m)')
ax.set_title(f'Box 0 — Grab/Lift Visualization')
ax.set_box_aspect([1, 1, 1])

plt.tight_layout()
plt.show()
