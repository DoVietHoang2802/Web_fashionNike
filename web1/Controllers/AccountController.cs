// ================================================================
// AccountController - Quản lý tài khoản người dùng
// Chức năng: Đăng ký, đăng nhập, đăng xuất, hồ sơ, đổi mật khẩu
// Sử dụng ASP.NET Core Identity để xác thực và quản lý user
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web1.Models;

namespace web1.Controllers
{
    /// <summary>
    /// Controller xử lý tất cả các thao tác liên quan đến tài khoản người dùng.
    /// Sử dụng UserManager để quản lý user (tạo, cập nhật, đổi pass...)
    /// Sử dụng SignInManager để đăng nhập / đăng xuất (quản lý cookie auth)
    /// </summary>
    public class AccountController : Controller
    {
        // UserManager<T>: quản lý CRUD user trong Identity system
        // Dùng để: FindByEmailAsync, CreateAsync, UpdateAsync, ChangePasswordAsync...
        private readonly UserManager<ApplicationUser> _userManager;

        // SignInManager<T>: quản lý trạng thái đăng nhập (cookie, session)
        // Dùng để: PasswordSignInAsync, SignInAsync, SignOutAsync
        private readonly SignInManager<ApplicationUser> _signInManager;

        // IWebHostEnvironment: truy cập thư mục gốc của web (wwwroot)
        // Dùng để: xây dựng đường dẫn lưu file upload (avatar)
        private readonly IWebHostEnvironment _env;

        // ================================================================
        // CONSTRUCTOR - Tiêm dependency (DI - Dependency Injection)
        // ASP.NET Core tự động inject UserManager, SignInManager, IWebHostEnvironment
        // khi controller được khởi tạo
        // ================================================================
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        // ================================================================
        // LOGIN (Đăng nhập)
        // ================================================================

        // GET: /Account/Login - Hiển thị form đăng nhập
        // returnUrl dùng để sau khi đăng nhập thành công sẽ quay lại trang trước đó
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;  // Truyền returnUrl sang View để form gửi kèm
            return View();
        }

        // POST: /Account/Login - Xử lý đăng nhập
        // Luồng xử lý:
        //   1. Validate email/password không trống
        //   2. Tìm user theo email (FindByEmailAsync)
        //   3. Kiểm tra password (PasswordSignInAsync)
        //   4. Nếu thành công: chuyển hướng về returnUrl hoặc Home
        //   5. Nếu thất bại: báo lỗi và trả lại form
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống tấn công CSRF - bắt buộc cho form POST
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Bước 1: Kiểm tra dữ liệu đầu vào không trống
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email và mật khẩu không được để trống.");
                return View();
            }

            // Bước 2: Tìm user trong database qua email
            // Nếu không tìm thấy -> user chưa đăng ký hoặc nhập sai email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
                return View();
            }

            // Bước 3: Kiểm tra password đã nhập có khớp với password đã hash trong DB
            // isPersistent: false = KHÔNG ghi nhớ đăng nhập (cookie session hết khi đóng trình duyệt)
            // lockoutOnFailure: false = KHÔNG khóa tài khoản khi nhập sai nhiều lần
            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);

            // Bước 4: Xử lý kết quả đăng nhập
            if (result.Succeeded)
            {
                // Chuyển hướng về trang trước đó nếu returnUrl hợp lệ (chống open redirect attack)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
                // Ngược lại về trang chủ
                return RedirectToAction("Index", "Home");
            }

            // Bước 5: Đăng nhập thất bại -> báo lỗi chung (không tiết lộ email có tồn tại hay không)
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View();
        }

        // ================================================================
        // REGISTER (Đăng ký)
        // ================================================================

        // GET: /Account/Register - Hiển thị form đăng ký
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Register - Xử lý đăng ký tài khoản mới
        // Luồng xử lý:
        //   1. Validate email, password, confirmPassword không trống
        //   2. Kiểm tra password và confirmPassword khớp nhau
        //   3. Kiểm tra email chưa được sử dụng (FindByEmailAsync)
        //   4. Tạo ApplicationUser mới -> UserManager.CreateAsync (tự hash password)
        //   5. Nếu thành công: đăng nhập tự động + chuyển hướng
        //   6. Nếu thất bại: hiển thị danh sách lỗi từ Identity (password yếu, email trùng...)
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống tấn công CSRF
        public async Task<IActionResult> Register(string email, string password, string confirmPassword, string fullName, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Bước 1: Validate đầu vào không trống
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email và mật khẩu không được để trống.");
                return View();
            }

            // Bước 2: Kiểm tra mật khẩu xác nhận khớp
            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View();
            }

            // Bước 3: Kiểm tra email đã tồn tại trong hệ thống chưa
            // Việc kiểm tra này giúp tránh trùng lặp trước khi gọi CreateAsync
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Email đã được sử dụng.");
                return View();
            }

            // Bước 4: Tạo đối tượng ApplicationUser mới
            // UserName = email (theo mặc định Identity dùng UserName làm unique identifier)
            // Password sẽ được UserManager tự động HASH trước khi lưu vào DB (KHÔNG lưu plain text)
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                CreatedAt = DateTime.Now  // Thời điểm tạo tài khoản
            };

            // CreateAsync: tạo user + hash password + lưu vào AspNetUsers table
            var result = await _userManager.CreateAsync(user, password);

            // Bước 5: Xử lý kết quả
            if (result.Succeeded)
            {
                // Đăng nhập tự động ngay sau khi đăng ký thành công
                // isPersistent: false = session cookie (hết hiệu lực khi đóng trình duyệt)
                await _signInManager.SignInAsync(user, isPersistent: false);

                TempData["SuccessMessage"] = "Đăng ký thành công! Chào mừng bạn đến với Fashion Store.";

                // Chuyển hướng về returnUrl hoặc trang chủ
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            // Bước 6: Đăng ký thất bại -> hiển thị lỗi chi tiết từ Identity
            // Ví dụ: "Passwords must have at least one uppercase ('A')"
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // ================================================================
        // LOGOUT (Đăng xuất)
        // ================================================================

        // POST: /Account/Logout - Đăng xuất, xóa cookie đăng nhập
        // Chỉ hỗ trợ POST (không phải GET) để tránh bị gọi nhầm qua link / bot
        [HttpPost]
        [ValidateAntiForgeryToken]  // Bắt buộc - chống CSRF attack khi logout
        public async Task<IActionResult> Logout()
        {
            // SignOutAsync: xóa authentication cookie khỏi trình duyệt user
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
            return RedirectToAction("Index", "Home");
        }

        // ================================================================
        // PROFILE (Hồ sơ)
        // ================================================================

        // GET: /Account/Profile - Xem hồ sơ cá nhân
        // [Authorize]: chỉ user đã đăng nhập mới được truy cập
        // Lấy thông tin user hiện tại từ Claims (User) qua UserManager
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            // GetUserAsync(User): lấy ApplicationUser từ HttpContext.User (claims trong cookie)
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            return View(user);  // Truyền user model sang View để hiển thị
        }

        // POST: /Account/Profile - Cập nhật hồ sơ cá nhân
        // Cho phép cập nhật: FullName, Address, DateOfBirth
        // Không cho cập nhật: Email, UserName (để bảo mật)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string fullName, string? address, DateTime? dateOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // Cập nhật các trường thông tin cá nhân
            user.FullName = fullName;
            user.Address = address;
            user.DateOfBirth = dateOfBirth;
            user.UpdatedAt = DateTime.Now;  // Ghi nhận thời điểm cập nhật cuối

            // UpdateAsync: lưu thay đổi vào AspNetUsers table
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";

            return RedirectToAction("Profile");
        }

        // ================================================================
        // CHANGE PASSWORD (Đổi mật khẩu)
        // ================================================================

        // GET: /Account/ChangePassword - Form đổi mật khẩu
        [Authorize]  // Yêu cầu đăng nhập để đổi pass
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Account/ChangePassword - Xử lý đổi mật khẩu
        // Luồng:
        //   1. Validate: currentPassword, newPassword, confirmPassword không trống
        //   2. Kiểm tra newPassword == confirmPassword
        //   3. Gọi UserManager.ChangePasswordAsync (tự động hash password mới)
        //   4. Nếu thành công -> chuyển về Profile
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

            // ChangePasswordAsync: xác minh currentPassword trước,
            // nếu đúng -> hash và lưu password mới vào DB
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Profile");
            }

            // Lỗi có thể: currentPassword sai, password mới không đủ mạnh...
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // ================================================================
        // FORGOT PASSWORD (Quên mật khẩu) - Bước 1: Xác minh tài khoản
        // ================================================================

        // GET: /Account/ForgotPassword - Form nhập FullName + Email để xác minh
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword - Xác minh tài khoản qua FullName + Email
        // Vì không có email service thực tế (SendGrid, SMTP...), hệ thống dùng
        // TempData để chuyển email sang trang ResetPassword trực tiếp.
        // ⚠️ Lưu ý: Đây là cách đơn giản - trong production nên gửi email chứa
        // token reset (UserManager.GeneratePasswordResetTokenAsync)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                ViewData["Error"] = "Vui lòng nhập đầy đủ họ tên và email.";
                return View();
            }

            // Tìm user theo email trong DB
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Vẫn hiển thị thông báo chung để không tiết lộ email có tồn tại hay không
                ViewData["Error"] = "Không tìm thấy tài khoản với email này.";
                return View();
            }

            // Kiểm tra họ tên khớp với tài khoản (tăng độ bảo mật)
            // StringComparison.Ordinal: so sánh chính xác, phân biệt hoa/thường
            if (string.IsNullOrEmpty(user.FullName) || !user.FullName.Equals(fullName.Trim(), StringComparison.Ordinal))
            {
                ViewData["Error"] = "Họ tên không khớp với tài khoản này.";
                return View();
            }

            // Lưu email vào TempData để trang ResetPassword nhận được
            // TempData: lưu tạm qua 1 request (chuyển hướng), tự động xóa sau khi đọc
            TempData["ResetEmail"] = email;
            return RedirectToAction("ResetPassword");
        }

        // ================================================================
        // RESET PASSWORD (Đặt lại mật khẩu) - Bước 2: Nhận email + đặt pass mới
        // ================================================================

        // GET: /Account/ResetPassword - Form đặt lại mật khẩu
        // Lấy email từ TempData (đã được lưu ở bước ForgotPassword)
        // Nếu TempData trống (user vào thẳng URL) -> chuyển về ForgotPassword
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
        // Cách thực hiện: RemovePasswordAsync (xóa pass cũ) + AddPasswordAsync (tạo pass mới)
        // ⚠️ Lưu ý: Cách này KHÔNG xác minh mật khẩu cũ vì user đã quên.
        // Cách bảo mật hơn: dùng UserManager.GeneratePasswordResetTokenAsync + ResetPasswordAsync
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            // Validate email hợp lệ
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            // Validate mật khẩu mới không trống
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ViewData["Error"] = "Vui lòng nhập mật khẩu mới.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            // Validate mật khẩu xác nhận khớp
            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "Mật khẩu xác nhận không khớp.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            // Validate độ dài tối thiểu (Identity mặc định yêu cầu 6 ký tự)
            if (newPassword.Length < 6)
            {
                ViewData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự.";
                ViewData["ResetEmail"] = email;
                return View();
            }

            // Tìm user theo email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ViewData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("ForgotPassword");
            }

            // Bước đặt lại password:
            // RemovePasswordAsync: xóa password hash cũ khỏi user
            // AddPasswordAsync: hash password mới và lưu vào DB
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay.";
                return RedirectToAction("Login");
            }

            // Hiển thị lỗi (ví dụ: password không đủ mạnh theo chính sách Identity)
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewData["ResetEmail"] = email;
            return View();
        }

        // ================================================================
        // ACCESS DENIED (Từ chối truy cập)
        // ================================================================

        // GET: /Account/AccessDenied - Trang báo không có quyền truy cập
        // Được gọi tự động bởi Identity khi user cố truy cập tài nguyên có
        // [Authorize] nhưng không đủ role/claim cần thiết
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ================================================================
        // AVATAR UPLOAD (Tải lên ảnh đại diện)
        // ================================================================

        /// <summary>
        /// Upload & cập nhật avatar của user hiện tại.
        /// Quy trình:
        ///   1. Validate file: có chọn ảnh, đúng định dạng, kích thước <= 5MB
        ///   2. Xóa avatar cũ (nếu có) để tránh rác file
        ///   3. Lưu avatar mới vào wwwroot/uploads/avatars/
        ///   4. Cập nhật User.AvatarUrl vào DB
        /// Đường dẫn lưu: wwwroot/uploads/avatars/{userId}.{ext}
        /// </summary>
        /// <param name="avatar">File ảnh từ form (type=file)</param>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // Bước 1: Validate - kiểm tra có file được chọn chưa
            if (avatar == null || avatar.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một ảnh.";
                return RedirectToAction("Profile");
            }

            // Bước 2: Validate định dạng file (chỉ chấp nhận ảnh)
            // GetExtension: lấy phần mở rộng file (".jpg", ".png"...)
            // ToLowerInvariant: chuẩn hóa thành chữ thường để so sánh
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Chỉ chấp nhận ảnh: JPG, PNG, GIF, WEBP.";
                return RedirectToAction("Profile");
            }

            // Bước 3: Validate kích thước file (max 5MB)
            // avatar.Length: kích thước tính bằng bytes (5MB = 5 * 1024 * 1024 bytes)
            if (avatar.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Ảnh không được vượt quá 5MB.";
                return RedirectToAction("Profile");
            }

            // Bước 4: Xóa avatar cũ (nếu có) để giải phóng dung lượng
            // AvatarUrl lưu dạng "/uploads/avatars/xxx.jpg" -> cắt '/' đầu để thành đường dẫn tuyệt đối
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldPath = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);  // Xóa file vật lý khỏi thư mục
            }

            // Bước 5: Lưu avatar mới
            // Tạo thư mục nếu chưa tồn tại
            var avatarsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(avatarsDir);

            // Đặt tên file = user.Id + extension (đảm bảo duy nhất, không trùng)
            // Ví dụ: "abc123-xyz.jpg"
            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(avatarsDir, fileName);

            // Copy file vào đường dẫn đích (FileStream: stream ghi file vật lý)
            using var stream = new FileStream(filePath, FileMode.Create);
            await avatar.CopyToAsync(stream);

            // Bước 6: Cập nhật AvatarUrl vào DB để hiển thị trên giao diện
            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            user.UpdatedAt = DateTime.Now;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Cập nhật ảnh đại diện thành công!";
            return RedirectToAction("Profile");
        }
    }
}
