using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using API.DTOs;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace API.Controllers {
  [Route("[controller]")]
  [ApiController]
  public class UserController : ControllerBase {
    private readonly IConfiguration _configuration;
    private readonly ITokenService _tokenService;

    public UserController(IConfiguration configuration, ITokenService tokenService) {
      _configuration = configuration;
      _tokenService = tokenService;
    }

    //TODO: remove after users creation
    [HttpPost("register")]
    public async Task<IActionResult> Register(LoginDto loginDto) {
      using var hmac = new HMACSHA512();
      var passwordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password)));      
      var passwordSalt = Convert.ToBase64String(hmac.Key);
      var client = new AmazonDynamoDBClient();
      var tableName = _configuration.GetValue<string>("users-table-name");
      DynamoDBContext context = new DynamoDBContext(client);
      var item = new Data.User()
      {
        UserName = loginDto.UserName,
        Password = passwordHash,
        Salt = passwordSalt
      };
      await context.SaveAsync(item);
      
      return NoContent();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto) {
      var client = new AmazonDynamoDBClient();
      var tableName = _configuration.GetValue<string>("users-table-name");
      DynamoDBContext context = new DynamoDBContext(client);
      var user = await context.LoadAsync<Data.User>(loginDto.UserName);
      if(user == null) {
        return NotFound("User not found");
      }
      var salt = Convert.FromBase64String(user.Salt);
      var passwordHash = Convert.FromBase64String(user.Password);
      using var hmac = new HMACSHA512(salt);
      var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));
      for (int i = 0; i < computedHash.Length; i++) {
        if (computedHash[i] != passwordHash[i]) { return Unauthorized("Wrong password or user name"); };
      }

      return Ok(new UserDto
      {
        UserName = loginDto.UserName,
        Token = _tokenService.CreateToken(user)
      });
    }

    [HttpPost("changePassword")]
    public async Task<IActionResult> ChangePassword (ChangePasswordDto changePasswordDto) {
      var client = new AmazonDynamoDBClient();
      DynamoDBContext context = new DynamoDBContext(client);
      var user = await context.LoadAsync<Data.User>(changePasswordDto.UserName);
      if (user == null) {
        return Unauthorized();
      }
      var salt = Convert.FromBase64String(user.Salt);
      var passwordHash = Convert.FromBase64String(user.Password);
      using var hmac = new HMACSHA512(salt);
      var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(changePasswordDto.CurrentPassword));
      for (int i = 0; i < computedHash.Length; i++) {
        if (computedHash[i] != passwordHash[i]) { return Unauthorized(); };
      }
      if(!string.Equals(changePasswordDto.NewPassword, changePasswordDto.NewPasswordConfirm)) {
        return BadRequest("Passwords are not matched");
      }
      using var newhmac = new HMACSHA512();
      var newPasswordHash = Convert.ToBase64String(newhmac.ComputeHash(Encoding.UTF8.GetBytes(changePasswordDto.NewPassword)));
      var passwordSalt = Convert.ToBase64String(newhmac.Key);
      user.Salt = passwordSalt;
      user.Password = newPasswordHash;
      await context.SaveAsync(user);

      return Ok("Password succesfully updated");
    }
  }
}
