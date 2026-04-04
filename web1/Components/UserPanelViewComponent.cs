// ================================================================
// UserPanelViewComponent - Lấy thông tin user cho _Layout
// Trả về avatar URL để hiển thị trong navbar dropdown
// ================================================================
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web1.Models;

namespace web1.Components
{
    public class UserPanelViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserPanelViewComponent(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null) return Content("");

            return View(viewName: "", model: new UserPanelViewModel
            {
                AvatarUrl  = user.AvatarUrl,
                FullName   = user.FullName,
                Email      = user.Email ?? "",
                IsAdmin    = User.IsInRole("Admin")
            });
        }
    }

    public class UserPanelViewModel
    {
        public string? AvatarUrl  { get; set; }
        public string? FullName   { get; set; }
        public string Email        { get; set; } = "";
        public bool   IsAdmin     { get; set; }
    }
}
