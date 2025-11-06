using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Views.Chat
{
    public class ThreadModel : PageModel
    {
        private readonly ChatService _chatService;

        public ChatThread Thread { get; set; }

        public ThreadModel(ChatService chatService)
        {
            _chatService = chatService;
        }

        public IActionResult OnGet(int threadId)
        {
            var userId = User.Identity.Name;
            Thread = _chatService.GetThreadById(threadId, userId);
            if (Thread == null) return NotFound();
            return Page();
        }
    }
}