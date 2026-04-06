using ACE.Server.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Security.Claims;
using ACE.Entity.Enum;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected log4net.ILog Log => log4net.LogManager.GetLogger(GetType());

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!ServerConfig.enable_web_portal.Value)
            {
                context.Result = new ObjectResult(new { message = "The Web Portal is currently disabled by the server administrator." })
                {
                    StatusCode = 503
                };
                return;
            }

            base.OnActionExecuting(context);
        }

        protected uint? CurrentAccountId
        {
            get
            {
                var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("AccountId")?.Value;
                if (uint.TryParse(idStr, out var id))
                    return id;

                return null;
            }
        }

        protected AccessLevel CurrentAccessLevel
        {
            get
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (Enum.TryParse<AccessLevel>(role, out var level))
                    return level;

                return AccessLevel.Player;
            }
        }

        protected bool IsAdmin => CurrentAccessLevel > AccessLevel.Player;

        protected bool IsAuthorizedForAccount(uint accountId)
        {
            return CurrentAccountId == accountId || IsAdmin;
        }
    }
}
