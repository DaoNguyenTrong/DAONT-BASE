---
title: Quản lý vai trò
description: 'Tạo, sửa và xoá vai trò, và chọn quyền cho từng vai trò.'
permission: organizations.roles.manage
---

# Quản lý vai trò

Vai trò là một nhóm quyền. Bạn giao vai trò cho thành viên thay vì giao từng quyền. Tạo vai trò riêng khi các vai trò có sẵn không khớp với cách nhóm bạn làm việc.

## Vai trò hệ thống

Mỗi tổ chức có sẵn ba vai trò có nhãn **Hệ thống**. Không sửa hay xoá được chúng.

| Vai trò    | Quyền                                              |
| ---------- | -------------------------------------------------- |
| **Owner**  | Chủ tổ chức, luôn có mọi quyền                     |
| **Admin**  | **Quản lý thành viên**                             |
| **Member** | Không có quyền quản lý; chỉ làm việc trong tổ chức |

## Các quyền

| Quyền                  | Cho phép                                                                |
| ---------------------- | ----------------------------------------------------------------------- |
| **Quản lý tổ chức**    | [Ngưng hoạt động tổ chức](./deactivate)                                 |
| **Quản lý thành viên** | [Thêm](./add-member), đổi vai trò và [xoá thành viên](./manage-members) |
| **Quản lý vai trò**    | Tạo, sửa và xoá vai trò (trang này)                                     |

::: warning
Quyền **Quản lý tổ chức** cho phép ngưng hoạt động tổ chức, việc không thể hoàn tác. Chỉ giao nó cho người thật sự cần.
:::

Các quyền **Quản lý tệp**, **Quản lý khoá API**, **Xem nhật ký hoạt động** và **Quản lý cài đặt** chỉ có tác dụng khi hệ thống của bạn có các chức năng tương ứng.

## Xem vai trò

1. Bấm **Tổ chức** ở thanh bên.
2. Ở dòng của tổ chức, bấm nút **Vai trò** (biểu tượng bánh răng).

<figure>

![Hộp thoại vai trò của tổ chức](/base/images/vi/organizations/roles.jpg)

<figcaption>Hộp thoại vai trò</figcaption>
</figure>

| #   | Thành phần                                     |
| --- | ---------------------------------------------- |
| 1   | Nhãn **Hệ thống**: vai trò không sửa, xoá được |
| 2   | Nút **Sửa vai trò**                            |
| 3   | Nút xoá vai trò                                |
| 4   | Nút **Tạo vai trò**                            |

## Tạo vai trò

1. Trong hộp thoại vai trò, bấm **Tạo vai trò**.
2. Nhập **Tên** của vai trò.
3. Ở mục **Quyền**, đánh dấu các quyền vai trò này được có.
4. Bấm **Lưu**.

<figure>

![Hộp thoại Tạo vai trò](/base/images/vi/organizations/role-form.jpg)

<figcaption>Hộp thoại Tạo vai trò</figcaption>
</figure>

| #   | Thành phần          |
| --- | ------------------- |
| 1   | Ô **Tên**           |
| 2   | Danh sách **Quyền** |
| 3   | Nút **Lưu**         |

Ứng dụng báo **Đã tạo vai trò**. Vai trò mới có thể chọn ngay khi thêm thành viên hoặc đổi vai trò.

## Sửa hoặc xoá vai trò

- **Sửa**: bấm nút **Sửa vai trò** (biểu tượng cây bút) ở dòng của vai trò, đổi tên hoặc quyền, rồi bấm **Lưu**.
- **Xoá**: bấm nút thùng rác ở dòng của vai trò, rồi bấm **Xác nhận**. Thành viên chỉ giữ vai trò này sẽ không còn vai trò nào; hãy [giao lại vai trò](./manage-members) cho họ.

## Sự cố thường gặp

- **Không thấy nút Vai trò ở dòng tổ chức**: bạn không có quyền **Quản lý vai trò** trong tổ chức đó.
- **Vai trò không có nút sửa, xoá**: đó là vai trò **Hệ thống**.
