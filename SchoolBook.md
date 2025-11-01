# 🏫 SchoolBook Platform – Hệ thống Quản lý Bài đăng và Xác thực Đa lớp

## 1️⃣. Giới thiệu tổng quan

SchoolBook Platform là một hệ thống web nội bộ được xây dựng bằng ASP.NET Core MVC và SQL Server, nhằm quản lý người dùng, bài đăng, và quy trình xác thực nâng cao trong môi trường trường học.

Hệ thống phục vụ cho các vai trò:
- **HighAdmin**, **Admin**, **Moderator**, **Teacher**, **Student**
- Với các quyền truy cập và chức năng được phân định rõ ràng.

Ngoài cơ chế đăng nhập truyền thống (username/password), hệ thống tích hợp xác thực đa yếu tố (MFA) gồm:
- Nhận diện khuôn mặt (Face Verification bằng C# + Face API)
- Xác minh OTP qua SMS hoặc Email
- Quản lý token/cookie an toàn với Sliding Expiration (7 ngày) và khả năng hủy toàn bộ token khi cần

## 2️⃣. Mục tiêu hệ thống

- Quản lý người dùng nội bộ với nhiều cấp độ phân quyền
- Cung cấp nền tảng đăng bài, bình luận, và kiểm duyệt nội dung học thuật
- Bảo mật tối đa với xác thực đa lớp, ghi nhận hành vi đăng nhập
- Cho phép HighAdmin kiểm soát toàn bộ tài khoản và hoạt động
- Giữ người dùng đăng nhập liên tục trong 7 ngày với cơ chế Sliding Window

## 3️⃣. Các vai trò người dùng

### 1. HighAdmin
- Là tài khoản đầu tiên, cao nhất hệ thống
- **Quyền:**
  - Thêm/sửa/xóa bất kỳ user (mọi vai trò)
  - Thiết lập, reset mật khẩu, hoặc hủy token người dùng
  - Cấp quyền Admin/Moderator
  - Đăng nhập bằng username/password do hệ thống cấp thủ công

### 2. Admin
- Quản lý người dùng cấp dưới:
  - Thêm/sửa/xóa Moderator, Teacher, Student
  - Không được can thiệp vào HighAdmin hoặc Admin khác
  - Có quyền xem báo cáo và thống kê bài đăng

### 3. Moderator
- Xem tất cả bài đăng, kể cả bài không visible
- Xóa bài không phù hợp (soft delete)
- Tạo báo cáo (report) về bài đăng hoặc user vi phạm

### 4. Teacher
- Đăng bài và xóa bài của chính mình
- Có thể comment, upvote/downvote
- Chọn nhóm người xem bài (Admin, Teacher, Student)

### 5. Student
- Đăng/xóa bài của chính mình
- Comment, upvote/downvote bài đăng
- Không có quyền xem bài của nhóm riêng tư nếu không được cấp quyền

## 4️⃣. Tính năng xác thực (Authentication & Security)

### 1. Đăng nhập
- Đăng nhập bằng username/password
- Kiểm tra mật khẩu hash (ASP.NET Identity)
- Nếu là lần đầu → bắt buộc đổi mật khẩu
- Nếu tài khoản có FaceRegistered = TRUE → yêu cầu xác minh khuôn mặt
- Nếu đăng nhập từ thiết bị hoặc IP lạ → gửi OTP (SMS hoặc Email)

### 2. Đăng ký khuôn mặt (Face Enrollment)
- Có thể đăng ký trong Setting -> Xác thực đa nhân tố
- Ảnh được gửi đến dịch vụ Face Recognition (Azure Face Apis)
- Lưu FaceId vào bảng FaceProfiles

### 3. Xác minh khuôn mặt (Face Verification)
- Chụp ảnh khuôn mặt qua webcam
- Hệ thống so sánh với FaceId đã lưu
- Nếu độ tin cậy (confidence) ≥ ngưỡng (ví dụ 0.6) → cho phép đăng nhập
- Nếu thất bại → yêu cầu OTP

### 4. OTP Verification
- Người dùng chọn phương thức nhận OTP (SMS hoặc Email)
- OTP có thời hạn 3 phút, dùng 1 lần
- Xác minh trước khi hoàn tất đăng nhập

### 5. Token & Session Management
- Dùng Persistent Cookie Authentication (7 ngày)
- Sliding Expiration: Nếu người dùng tương tác trong 7 ngày → tự động gia hạn
- Revoke Token: HighAdmin/Admin có thể hủy toàn bộ session người dùng:
  - TokenVersion tăng lên → toàn bộ cookie cũ bị vô hiệu
  - UserTokens được đánh dấu IsRevoked = 1

## 5️⃣. Cấu trúc cơ sở dữ liệu (SQL Server)

### Các bảng chính

| Bảng | Mục đích |
|------|----------|
| **Users** | Lưu thông tin tài khoản |
| **Roles** | Danh sách vai trò |
| **UserRoles** | Liên kết Users–Roles |
| **FaceProfiles** | Dữ liệu nhận diện khuôn mặt |
| **OtpCodes** | Lưu OTP xác thực |
| **UserTokens** | Quản lý phiên đăng nhập và cookie |

*Chi tiết cấu trúc và script SQL được định nghĩa trong file SchoolBook_Auth.sql.*

## 6️⃣. Tính năng bài đăng (Post Management)

### 1. Tạo bài đăng
- **Các trường:** Title, Content, FileUpload (ảnh/video), VisibleTo (checkbox)
- Tự kiểm duyệt: kiểm tra nội dung theo danh sách từ cấm (Regex)
- Lưu file vào thư mục `/Uploads/`

### 2. Hiển thị bài đăng
- Hiển thị dựa theo quyền xem (VisibleTo và role)
- **Bộ lọc:**
  - **Newest:** Bài mới nhất (mặc định)
  - **Hot:** Bài có (Upvotes - Downvotes) cao nhất trong 24h
  - **Most Upvoted:** Bài được upvote nhiều nhất mọi thời gian

### 3. Upvote / Downvote
- Mỗi user chỉ được vote 1 lần/bài
- Nếu Downvotes > 1.5 * Upvotes (với tổng vote > 10) → tự động report

### 4. Comment
- Người dùng (Teacher/Student) có thể comment dưới bài đăng
- Hiển thị theo thứ tự thời gian tăng dần

### 5. Xóa bài
- Teacher/Student chỉ xóa bài của mình
- Moderator có thể soft delete bất kỳ bài nào

## 7️⃣. Kiểm duyệt (Moderation)

### 1. Tự kiểm duyệt
- Regex kiểm tra từ ngữ không phù hợp
- Nếu phát hiện → từ chối đăng bài, báo lỗi cho người dùng

### 2. Moderator kiểm duyệt
- Xem toàn bộ bài (bỏ qua VisibleTo)
- **Có thể:**
  - Xóa bài
  - Tạo Report về user/bài
  - Cập nhật Status của Report

## 8️⃣. Giao diện người dùng (UI)

### 1. Trang chủ
- Hiển thị danh sách bài đăng theo filter (Hot, Newest, Most Upvoted)
- Sử dụng Bootstrap 5 và Razor Views

### 2. Navbar
- **Menu tùy theo vai trò:**
  - HighAdmin/Admin: Manage Users
  - Moderator: Review Posts
  - Teacher/Student: My Posts
  - Tất cả: Home, Profile, Settings

### 3. Form tạo bài
- Title, Content, File Upload, VisibleTo (checkbox list)

### 4. Setting / Profile
- Đổi mật khẩu
- Đăng ký hoặc cập nhật khuôn mặt
- Cấu hình phương thức nhận OTP (SMS/Email)

## 9️⃣. Tính năng bổ sung bảo mật

| Tính năng | Mục đích |
|-----------|----------|
| HTTPS-only | Bảo vệ thông tin người dùng |
| Hash mật khẩu (ASP.NET Identity) | Chống rò rỉ |
| TokenVersion + Revoke | Hủy toàn bộ cookie khi cần |
| OTP + Face Verification | Xác thực đa lớp |
| Sliding Expiration (7 ngày) | Trải nghiệm liền mạch |
| Device/IP logging | Giám sát hoạt động đăng nhập |

## 🔟. Công nghệ sử dụng

| Thành phần | Công nghệ |
|------------|-----------|
| Backend | ASP.NET Core MVC 8.0 |
| Database | SQL Server 2022 |
| ORM | Entity Framework Core |
| Authentication | ASP.NET Identity + Cookie Auth |
| Face Recognition | C# + Azure Face API (hoặc OpenCV Service) |
| OTP Service | Twilio SMS / SendGrid Email |
| Frontend | Bootstrap 5, jQuery, Razor Views |
| Logging | Serilog + ILogger |
| Deployment | IIS hoặc Docker Container |

## 🧩 11️⃣. Quy trình đăng nhập tổng quát

1. User nhập username/password
2. Nếu lần đầu → yêu cầu đổi mật khẩu
3. Nếu có FaceID (đã bật trong setting) → xác minh khuôn mặt
4. Nếu thiết bị/IP lạ → gửi OTP
5. Khi thành công → tạo cookie 7 ngày (sliding window)
6. Ghi log session vào UserTokens

## 🏁 12️⃣. Tổng kết

Hệ thống SchoolBook Platform hướng đến:
- **Bảo mật cao** (Multi-factor Auth)
- **Phân quyền rõ ràng**
- **Trải nghiệm liền mạch** (7 ngày không logout)
- **Hỗ trợ AI** (Face Recognition)
- **Quản lý nội dung minh bạch, dễ mở rộng**