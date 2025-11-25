using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SchoolBookPlatform.Models.ViewModels
{
    public class CreatePostViewModel
    {
        [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
        [MaxLength(300, ErrorMessage = "Tiêu đề không được vượt quá 300 ký tự")]
        [Display(Name = "Tiêu đề")]
        public string? Title { get; set; }

        [Display(Name = "Nội dung")]
        public string? Content { get; set; }

        [Display(Name = "Hiển thị cho")]
        public string VisibleToRoles { get; set; } = "All";

        [Display(Name = "Tệp đính kèm")]
        public List<IFormFile> Attachments { get; set; } = new List<IFormFile>();

        // Options for VisibleToRoles dropdown
        public Dictionary<string, string> VisibleToOptions { get; } = new Dictionary<string, string>
        {
            { "All", "Tất cả mọi người" },
            { "Student", "Học sinh" },
            { "Teacher", "Giáo viên" },
            { "Admin", "Quản trị viên" }
        };
    }
}