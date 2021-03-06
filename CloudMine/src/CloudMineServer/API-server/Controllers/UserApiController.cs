using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CloudMineServer.Models;
using CloudMineServer.Interface;
using Microsoft.AspNetCore.Authorization;
using System.Text.Encodings.Web;
using CloudMineServer.API_server.Services;

namespace CloudMineServer.API_server.Controllers
{
    // TODO: Make new controller for regular users and make this one admin only
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/Users")]
    public class UserApiController : Controller
    {
        private static readonly int _defaultStorageSize = 100000000;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICloudMineDbService _cloudMineDbService;

        public UserApiController(UserManager<ApplicationUser> userManager, ICloudMineDbService cloudMineDbService)
        {
            _userManager = userManager;
            _cloudMineDbService = cloudMineDbService;
        }

        [HttpPost]
        public async Task<IActionResult> RegisterUser([FromBody]UserRegistrationModel userRegistration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser { Email = userRegistration.Email, UserName = userRegistration.Email };
            user.StorageSize = _defaultStorageSize;

            var result = await _userManager.CreateAsync(user, userRegistration.Password);
            if (result.Succeeded)
            {
                return CreatedAtAction("Get", new { userName = user.UserName }, new UserInfo { UserName = user.UserName, StorageSize = user.StorageSize });
            }

            return BadRequest(result.Errors.First().Description);
        }

        #region AdminActions

        [Authorize(Roles = "admin")]
        [HttpGet("{userEmail}")]
        public async Task<IActionResult> GetUserInfo([FromRoute]string userEmail)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            if(user == null)
                return BadRequest($"User {userEmail} not found");
            return Ok(await GetUserInfoAsync(user));
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<List<UserInfo>> GetUsersInfos()
        {
            var users = _userManager.Users.ToList();
            var allUserInfos = new List<UserInfo>();

            foreach (var user in users)
            {
                var userInfo = await GetUserInfoAsync(user);
                allUserInfos.Add(userInfo);
            }
            return allUserInfos;
        }


        // Change user settings, only storage for now
        [Authorize(Roles = "admin")]
        [HttpPut("{userEmail}")]
        public async Task<IActionResult> PutUserInfo([FromRoute]string userEmail, [FromBody]UserInfo userInfo)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            var oldUserInfo = await GetUserInfoAsync(user);

            if (oldUserInfo.UsedStorage > userInfo.StorageSize)
                return BadRequest("Cant shrink storage to less than your used storage!");

            user.StorageSize = userInfo.StorageSize;
            await _userManager.UpdateAsync(user);
            return Ok(userInfo);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{userEmail}")]
        public async Task<IActionResult> DeleteUser([FromRoute]string userEmail)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            var result = await _userManager.DeleteAsync(user);
            if (result == IdentityResult.Success)
                return Ok();
            return BadRequest($"Could not delete user: {userEmail}");
        }

        #endregion

        #region NonAdminActions

        [Authorize]
        [HttpGet]
        [Route("UserInfo")]
        public async Task<UserInfo> GetLoggedInUserInfo()
        {
            var user = await _userManager.FindByIdAsync(User.GetUserId());
            var userInfo = await GetUserInfoAsync(user);
            return userInfo;
        }

        [Route("Logout")]
        [Authorize]
        [HttpGet]
        public IActionResult LogoutUser()
        {
            if (Request.Cookies.ContainsKey("access_token"))
            {
                string cookieValue = HtmlEncoder.Default.Encode(Request.Cookies["access_token"]);
                Response.Cookies.Append(
                    "access_token",
                    cookieValue,
                    new CookieOptions
                    {
                        Path = "/",
                        HttpOnly = true,
                        Secure = false,
                        Expires = DateTime.Now.AddYears(-1)
                    }
                );
                return Ok();
            }
            return BadRequest("You must log in before you can logout");
        }

        [HttpGet("IsLoggedIn")]
        public bool GetLoginStatus() => User.Identity.IsAuthenticated;

        [Authorize]
        [HttpGet("LoginCode")]
        public IActionResult EmptyLoginCheck() => new OkResult();


        [Authorize]
        [HttpGet("IsAdmin")]
        public bool IsAdmin() => User.IsInRole("admin");
        
        #endregion

        private async Task<UserInfo> GetUserInfoAsync(ApplicationUser user)
        {
            var userInfo = new UserInfo();

            userInfo.UserName = user.UserName;

            var fileItems = await _cloudMineDbService.GetAllFilesUsingAPI(user.Id);
            userInfo.NumberFiles = fileItems.ListFileItems.Count;

            userInfo.StorageSize = user.StorageSize;

            userInfo.UsedStorage = 0;
            fileItems.ListFileItems.ForEach(f => userInfo.UsedStorage += f.FileSize);

            return userInfo;
        }
    }

    public class UserRegistrationModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class UserInfo
    {
        public string UserName { get; set; }
        public int StorageSize { get; set; }
        public int UsedStorage { get; set; }
        public int NumberFiles { get; set; }
    }
}