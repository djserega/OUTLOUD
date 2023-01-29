using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Outloud.Rss.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {

        [HttpGet]
        public string GetToken()
        {
            SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes("45bfb043-27cd-4019-a88a-0effbc4df195"));
            SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new(
                expires: DateTime.Now.AddDays(14),
                signingCredentials: credentials);

            return $"{{\"bearerToken\": \"{new JwtSecurityTokenHandler().WriteToken(token)}\"}}";
        }
    }
}
