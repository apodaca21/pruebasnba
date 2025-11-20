using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NBADATA.Models;

namespace NBADATA.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;

        public RegisterModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
            => (_users, _signIn) = (users, signIn);

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, DataType(DataType.Password), MinLength(6)]
            public string Password { get; set; } = string.Empty;

            [Required, DataType(DataType.Password), Compare(nameof(Password))]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };
            var result = await _users.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                await _signIn.SignInAsync(user, isPersistent: true);
                return RedirectToPage("/Index");
            }
            foreach (var err in result.Errors) ModelState.AddModelError(string.Empty, err.Description);
            return Page();
        }
    }
}
