# Hướng Dẫn Lấy SendGrid API Key

## Bước 1: Đăng ký tài khoản SendGrid

1. Truy cập: https://sendgrid.com/
2. Click **"Start for Free"** hoặc **"Sign Up"**
3. Điền thông tin:
   - Email
   - Mật khẩu
   - Tên công ty (optional)
4. Verify email của bạn qua link trong hộp thư

## Bước 2: Verify Sender Identity (Email người gửi)

1. Sau khi đăng nhập, vào **Settings** > **Sender Authentication**
2. Chọn **Verify a Single Sender** (cho testing) hoặc **Authenticate Your Domain** (cho production)
3. **Verify a Single Sender** (Dễ nhất):
   - Click **Create a Sender**
   - Điền thông tin:
     - **From Email**: Email bạn muốn dùng để gửi (ví dụ: `noreply@yourdomain.com`)
     - **From Name**: Tên hiển thị (ví dụ: `SchoolBook Platform`)
     - **Reply To**: Email nhận reply (có thể giống From Email)
   - Verify email bằng cách click link trong email SendGrid gửi cho bạn

## Bước 3: Tạo API Key

1. Vào **Settings** > **API Keys** (hoặc click vào icon Profile > **API Keys**)
2. Click **Create API Key**
3. Đặt tên cho API Key (ví dụ: `SchoolBook-OTP-Key`)
4. Chọn quyền: **Full Access** (hoặc **Restricted Access** với chỉ quyền gửi email)
5. Click **Create & View**
6. **QUAN TRỌNG**: Copy API Key ngay lập tức (chỉ hiện 1 lần duy nhất!)
   - Format: `SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`

## Bước 4: Cập nhật appsettings.json

Mở file `SchoolBookPlatform/appsettings.json` và thay thế:

```json
"SendGrid": {
  "ApiKey": "SG.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "FromEmail": "noreply@yourdomain.com",
  "FromName": "SchoolBook Platform"
}
```

**Lưu ý:**
- Thay `YOUR_SENDGRID_API_KEY_HERE` bằng API Key bạn vừa copy
- Thay `your-email@example.com` bằng email đã verify ở Bước 2
- Thay `SchoolBook Platform` bằng tên bạn muốn hiển thị

## Bước 5: Kiểm tra

1. Chạy lại ứng dụng
2. Thử đăng nhập và nhận OTP qua email
3. Kiểm tra email inbox (có thể vào thư mục Spam)

## Gói miễn phí SendGrid

- **100 emails/ngày** miễn phí vĩnh viễn
- Đủ cho môi trường development và testing
- Không cần thẻ tín dụng để đăng ký

## Troubleshooting

### Lỗi: "API Key chưa cấu hình"
- Kiểm tra lại `appsettings.json` đã có section `SendGrid` chưa
- Kiểm tra API Key đã copy đúng chưa (không có khoảng trắng thừa)

### Lỗi: "SendGrid lỗi: 403"
- API Key không đúng hoặc đã bị revoke
- Tạo API Key mới và cập nhật lại

### Lỗi: "SendGrid lỗi: 400"
- Email sender chưa được verify
- Quay lại Bước 2 và verify email

### Email không nhận được
- Kiểm tra thư mục Spam
- Kiểm tra email đã được verify trong SendGrid chưa
- Kiểm tra Activity trong SendGrid dashboard để xem email có được gửi không

## Link hữu ích

- SendGrid Dashboard: https://app.sendgrid.com/
- SendGrid Documentation: https://docs.sendgrid.com/
- SendGrid API Keys: https://app.sendgrid.com/settings/api_keys

