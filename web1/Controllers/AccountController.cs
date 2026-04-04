// ================================================================
// AccountController - Quản lý tài khoản người dùng
// Chức năng: Đăng ký, đăng nhập, đăng xuất, hồ sơ, đổi mật khẩu
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web1.Models;

namespace web1.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        // GET: /Account/Login - Hiển thị form đăng nhập
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login - Xử lý đăng nhập
        // Tìm user theo email, kiểm tra password, đăng nhập qua SignInManager
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email và mật khẩu không được để trống.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View();
        }

        // GET: /Account/Register - Hiển thị form đăng ký
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Register - Xử lý đăng ký tài khoản mới
        // Tạo ApplicationUser mới, mã hóa password, đăng nhập tự động sau khi tạo thành công
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string email, string password, string confirmPassword, string fullName, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email và mật khẩu không được để trống.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email đã được sử dụng.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Đăng nhập tự động sau khi đăng ký
                await _signInManager.SignInAsync(user, isPersistent: false);

                TempData["SuccessMessage"] = "Đăng ký thành công! Chào mừng bạn đến với Fashion Store.";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // POST: /Account/Logout - Đăng xuất, xóa cookie đăng nhập
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile - Xem hồ sơ cá nhân (yêu cầu đăng nhập)
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        // POST: /Account/Profile - Cập nhật hồ sơ (FullName, Address, DateOfBirth)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string fullName, string? address, DateTime? dateOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            user.FullName = fullName;
            user.Address = address;
            user.DateOfBirth = dateOfBirth;
            user.UpdatedAt = DateTime.Now;

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";

            return RedirectToAction("Profile");
        }

        // GET: /Account/ChangePassword - Form đổi mật khẩu (yêu cầu đăng nhập)
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Account/ChangePassword - Xử lý đổi mật khẩu (CurrentPassword -> NewPassword)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ thông tin.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu mới xác nhận không khớp.");
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Profile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // GET: /Account/ForgotPassword - Form quên mật khẩu (nhập FullName + Email)
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword - Xác minh tài khoản qua FullName + Email
        // Lưu email vào TempData để chuyển sang trang ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                ViewData["Error"] = "Vui lòng nhập đầy đủ họ tên và email.";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ViewData["Error"] = "Không tìm thấy tài khoản với email này.";
                return View();
            }

            // Kiểm tra họ tên khớp
            if (string.IsNullOrEmpty(user.FullName) || !user.FullName.Equals(fullName.Trim(), StringComparison.Ordinal))
            {
                ViewData["Error"] = "Họ tên không khớp với tài khoản này.";
                return View();
            }

            // Lưu thông tin vào tempdata để chuyển sang trang reset
            TempData["ResetEmail"] = email;
            return RedirectToAction("ResetPassword");
        }

        // GET: /Account/ResetPassword - Form đặt lại mật khẩu (lấy email từ TempData)
        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = TempData["ResetEmail"] as string;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }
            ViewData["ResetEmail"] = email;
            return View();
        }

        // POST: /Account/ResetPassword - Xử lý đặt lại mật khẩu
        // Xóa password cũ bằng RemovePasswordAsync, tạo mới bằng AddPasswordAsync
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ViewData["Error"] = "Vui lòng nhập mật khẩu mới.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "Mật khẩu xác nhận không khớp.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ViewData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("ForgotPassword");
            }

            // Xóa password cũ + tạo password mới
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewData["ResetEmail"] = email;
            return View();
        }

        // GET: /Account/AccessDenied - Trang từ chối truy cập (khi không đủ quyền)
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ================================================================
        // AVATAR UPLOAD
        // ================================================================

        /// <summary>
        /// Upload & cập nhật avatar của user hiện tại.
        /// Lưu file vào wwwroot/uploads/avatars/ và cập nhật User.AvatarUrl.
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (avatar == null || avatar.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một ảnh.";
                return RedirectToAction("Profile");
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Chỉ chấp nhận ảnh: JPG, PNG, GIF, WEBP.";
                return RedirectToAction("Profile");
            }

            // Max 5MB
            if (avatar.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Ảnh không được vượt quá 5MB.";
                return RedirectToAction("Profile");
            }

            // Xóa avatar cũ nếu có
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            // Lưu avatar mới
            var avatarsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(avatarsDir);

            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(avatarsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await avatar.CopyToAsync(stream);

            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            user.UpdatedAt = DateTime.Now;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Cập nhật ảnh đại diện thành công!";
            return RedirectToAction("Profile");
        }
    }
}
