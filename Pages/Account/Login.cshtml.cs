using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NBADATA.Models;

namespace NBADATA.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signIn;

        public LoginModel(SignInManager<ApplicationUser> signIn) => _signIn = signIn;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; } = true; // persistencia
        }

        public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl ??= returnUrl ?? Url.Content("~/");
            if (!ModelState.IsValid) return Page();

            var result = await _signIn.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded) return LocalRedirect(ReturnUrl);

            ModelState.AddModelError(string.Empty, "Credenciales inválidas");
            return Page();
        }
    }
}

