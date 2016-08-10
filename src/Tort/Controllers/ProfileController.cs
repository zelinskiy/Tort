using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tort.Models.ProfileJsonModels;
using Microsoft.AspNetCore.Identity;
using Tort.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Tort.Controllers
{
    [Route("api/profile")]
    [Authorize(ActiveAuthenticationSchemes = "Bearer")]
    public class ProfileController : Controller
    {
        readonly UserManager<ApplicationUser> _userManager;
        readonly SignInManager<ApplicationUser> _signInManager;
        readonly ILogger _logger;
        readonly ApplicationRoleManager _roleManager;

        string[] myRoles = new string[] { "admin" };

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            ApplicationRoleManager roleManager
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = loggerFactory.CreateLogger<ProfileController>();
            _roleManager = roleManager;
        }

        private ApplicationUser Me
        {
            get
            {
                var username = User.Claims.First(c => c.Type.EndsWith("nameidentifier")).Value;
                var user = _userManager.Users.First(u => u.UserName == username);
                return user;
            }
        }


        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<JsonResult> Register([FromBody]RegisterJsonModel model)
        {
            var responce = new Dictionary<string, string>();
            
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    responce.Add("status", "success");
                }
                else
                {
                    responce.Add("errors", JsonConvert.SerializeObject(result.Errors));
                    responce.Add("status", "fail");
                }
            }
            else
            {
                responce.Add("errors", string.Join(",",ModelState.Root.Errors.Select(e=>e.Exception+":"+e.ErrorMessage)));
                responce.Add("status", "fail");
            }

            return Json(responce);
        }

        [HttpGet("claims/my")]
        public IActionResult MyClaims()
        {
            var responce = new Dictionary<string, string>();

            responce.Add("claims", string.Join(";", User.Claims.Select(
                c => c.Type + ":" + c.Value.Substring(0, c.Value.Length > 10 ? 10 : c.Value.Length)
                )));

            return Json(responce);
        }
        

        [HttpPost("roles/my/togglerole/{roleName}")]
        public async Task<IActionResult> ToggleMyRole(string roleName)
        {
            await createRoles();
            var responce = new Dictionary<string, string>();
            var user = Me;
            
            if (roleName == null)
            {
                responce.Add("status", $"RoleName not set");
                return Json(responce);
            }
            else if (!await _roleManager.RoleExistsAsync(roleName))
            {
                responce.Add("status", $"Role {roleName} not found");
                return Json(responce);
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
                responce.Add("status", $"User {user.UserName} added to role {roleName} succesfully");
            }
            else
            {
                await _userManager.RemoveFromRoleAsync(user, roleName);
                responce.Add("status", $"User {user.UserName} removed from role {roleName}");
            }

            return Json(responce);
        }


        private async Task createRoles()
        {
            foreach (var r in myRoles)
            {
                if (!await _roleManager.RoleExistsAsync(r))
                {
                    await _roleManager.CreateAsync(new IdentityRole(r));
                }
            }
        }


    }
}
