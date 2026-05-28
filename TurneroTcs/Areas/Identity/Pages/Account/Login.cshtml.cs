// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace TurneroTcs.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            public string Ultimatix { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null, string rateLimitMessage = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
            else if (!string.IsNullOrWhiteSpace(rateLimitMessage))
            {
                ModelState.AddModelError(string.Empty, rateLimitMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(Input.Ultimatix, Input.Password, Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                var user = await _userManager.FindByNameAsync(Input.Ultimatix);
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    ModelState.AddModelError(string.Empty, await BuildLockedOutMessageAsync(user));
                    return Page();
                }
                else
                {
                    ModelState.AddModelError(string.Empty, await BuildInvalidLoginMessageAsync(user));
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private async Task<string> BuildInvalidLoginMessageAsync(IdentityUser user)
        {
            if (user == null)
            {
                return "Ultimatix o contraseña inválidos.";
            }

            var failedCount = await _userManager.GetAccessFailedCountAsync(user);
            var maxFailed = _userManager.Options.Lockout.MaxFailedAccessAttempts;
            var remainingAttempts = Math.Max(0, maxFailed - failedCount);

            if (remainingAttempts <= 0)
            {
                return await BuildLockedOutMessageAsync(user);
            }

            var intentosTexto = remainingAttempts == 1 ? "intento" : "intentos";
            return $"Ultimatix o contraseña inválidos. Te quedan {remainingAttempts} {intentosTexto} antes del bloqueo temporal.";
        }

        private async Task<string> BuildLockedOutMessageAsync(IdentityUser user)
        {
            if (user == null)
            {
                return "Tu cuenta ha sido bloqueada temporalmente por demasiados intentos fallidos.";
            }

            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            if (lockoutEnd == null)
            {
                return "Tu cuenta ha sido bloqueada temporalmente por demasiados intentos fallidos.";
            }

            var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return "Tu cuenta ha sido bloqueada temporalmente por demasiados intentos fallidos.";
            }

            if (remaining.TotalMinutes >= 1)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                var minutosTexto = minutes == 1 ? "minuto" : "minutos";
                return $"Tu cuenta está bloqueada temporalmente. Intenta nuevamente en {minutes} {minutosTexto}.";
            }

            var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            var segundosTexto = seconds == 1 ? "segundo" : "segundos";
            return $"Tu cuenta está bloqueada temporalmente. Intenta nuevamente en {seconds} {segundosTexto}.";
        }
    }
}
