using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Views.Chat
{
    public class IndexModel : PageModel
    {
        private readonly ChatService _chatService;

        public List<ChatThread> Threads { get; set; }

        public IndexModel(ChatService chatService)
        {
            _chatService = chatService;
        }
        public void OnGet()
        {
            var userId = User.Identity.Name;
            Threads = _chatService.GetThreadsForUser(userId).ToList();
            
        }
    }
}