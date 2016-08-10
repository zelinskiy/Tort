﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tort.Models;
using Tort.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Tort.Controllers
{
    [Route("api/token")]
    public class TokenController : Controller
    {
        UserManager<ApplicationUser> _userManager;
        SignInManager<ApplicationUser> _signInManager;
        TokenProviderOptions _tokenOptions;

        public TokenController(
            SignInManager<ApplicationUser> signInManager,
            IOptions<TokenProviderOptions> tokenOptionsSource,
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _tokenOptions = tokenOptionsSource.Value;
            _signInManager = signInManager;
        }

        //api/token
        [HttpPost]
        public async Task<IActionResult> Token([FromBody] dynamic model)
        {
            string username = model.UserName;
            string password = model.Password;

            var responce = new Dictionary<string, string>();

            var identity = await _signInManager.PasswordSignInAsync(username, password, false, lockoutOnFailure: false);
            if (identity == null)
            {
                responce.Add("error", "user not found");
                return Json(responce);
            }
            else if (!identity.Succeeded)
            {
                responce.Add("error", "incorrect login data");
                return Json(responce);
            }
            var user = await _userManager.FindByNameAsync(username);


            var now = DateTime.UtcNow;
            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpochDate(now).ToString(), ClaimValueTypes.Integer64),
                new Claim("myClaimType", "myClaimValue")
            };

            foreach (var c in user.Claims)
            {
                claims.Add(c.ToClaim());
            }
            foreach (var r in await _userManager.GetRolesAsync(user))
            {
                claims.Add(new Claim("role", r));
            }


            var jwt = new JwtSecurityToken(
                issuer: _tokenOptions.Issuer,
                audience: _tokenOptions.Audience,
                claims: claims,
                notBefore: now,
                expires: now.Add(_tokenOptions.Expiration),
                signingCredentials: _tokenOptions.SigningCredentials);

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);


            responce.Add("token", encodedJwt);
            responce.Add("expires_in", ((int)_tokenOptions.Expiration.TotalSeconds).ToString());
            responce.Add("valid_to", jwt.ValidTo.ToString());

            return Json(responce);
        }


        public static long ToUnixEpochDate(DateTime date)
            => (long)Math.Round((date.ToUniversalTime() - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds);

    }
}
