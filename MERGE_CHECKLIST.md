# Checklist Merge Branch - User Management Feature

## ✅ Trước khi Merge

- [ ] Backup code hiện tại (git stash hoặc commit)
- [ ] Pull code mới nhất từ main/master
- [ ] Kiểm tra branch hiện tại không có uncommitted changes

## 🔍 Files Cần Kiểm Tra Khi Merge

### 1. Program.cs
- [ ] Có conflict ở phần Services? → Giữ cả service cũ và `UserManagementService`
- [ ] Có conflict ở phần Policies? → Giữ cả policy cũ và `HighAdminOnly`
- [ ] Đảm bảo có dòng: `builder.Services.AddScoped<UserManagementService>();`

### 2. _Layout.cshtml  
- [ ] Có conflict ở navigation menu? → Giữ cả menu cũ và menu mới
- [ ] Đảm bảo menu "Quản lý người dùng" chỉ hiện cho HighAdmin/Admin
- [ ] Kiểm tra menu "Trang chủ" hiển thị đúng

### 3. appsettings.json
- [ ] Có conflict ở config? → Merge cả 2 sections (SendGrid + Twilio)
- [ ] **QUAN TRỌNG**: Giữ lại SendGrid API Key đã điền
- [ ] Đảm bảo không bị ghi đè config cũ (ConnectionStrings, AzureFace, etc.)

### 4. Files Mới
- [ ] Đảm bảo tất cả files mới đã được add:
  - ViewModels/UserListViewModel.cs
  - ViewModels/CreateUserViewModel.cs
  - ViewModels/EditUserViewModel.cs
  - Services/UserManagementService.cs
  - Controllers/UsersController.cs
  - Views/Users/Index.cshtml
  - Views/Users/Create.cshtml
  - Views/Users/Edit.cshtml
  - Views/Users/Delete.cshtml

## 🧪 Test Sau Khi Merge

### Authentication Test
- [ ] Đăng nhập với HighAdmin → Thấy menu "Quản lý người dùng"
- [ ] Đăng nhập với Admin → Thấy menu "Quản lý người dùng"
- [ ] Đăng nhập với Student → KHÔNG thấy menu "Quản lý người dùng"

### User Management Test (HighAdmin)
- [ ] Vào `/Users` → Thấy danh sách tất cả users
- [ ] Tạo user mới với role HighAdmin → Thành công
- [ ] Tạo user mới với role Admin → Thành công
- [ ] Tạo user mới với role Student → Thành công
- [ ] Edit user → Thành công
- [ ] Delete user → Thành công (soft delete)
- [ ] Reset password → Thành công
- [ ] Revoke tokens → Thành công

### User Management Test (Admin)
- [ ] Vào `/Users` → Chỉ thấy users có role Moderator/Teacher/Student
- [ ] Tạo user mới với role HighAdmin → **FAIL** (đúng như thiết kế)
- [ ] Tạo user mới với role Admin → **FAIL** (đúng như thiết kế)
- [ ] Tạo user mới với role Student → Thành công
- [ ] Edit user Moderator → Thành công
- [ ] Edit user HighAdmin → **FAIL** (đúng như thiết kế)

### Database Test
- [ ] Kiểm tra database có đủ 5 roles: HighAdmin, Admin, Moderator, Teacher, Student
- [ ] Kiểm tra có user HighAdmin trong database
- [ ] Test query users từ database → Không lỗi

## 🚨 Nếu Có Lỗi

### Lỗi: "UserManagementService not found"
- **Nguyên nhân**: Quên register service trong Program.cs
- **Giải pháp**: Thêm `builder.Services.AddScoped<UserManagementService>();`

### Lỗi: "Policy 'AdminOrHigher' not found"
- **Nguyên nhân**: Policy chưa được thêm vào Program.cs
- **Giải pháp**: Thêm policy trong `AddAuthorization`

### Lỗi: "SendGrid API Key chưa cấu hình"
- **Nguyên nhân**: appsettings.json bị conflict và mất config SendGrid
- **Giải pháp**: Thêm lại section SendGrid vào appsettings.json

### Lỗi: Menu không hiện
- **Nguyên nhân**: _Layout.cshtml bị conflict
- **Giải pháp**: Kiểm tra lại điều kiện `User.IsInRole("HighAdmin") || User.IsInRole("Admin")`

## 📝 Notes

- **Không sửa** các file: AuthenController, FeedsController, Models, AppDbContext
- **Chỉ thêm** tính năng mới, không thay đổi logic cũ
- **Giữ nguyên** tất cả config và service cũ

