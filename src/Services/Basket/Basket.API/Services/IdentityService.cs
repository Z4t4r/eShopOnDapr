
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Services
{
    public class IdentityService : IIdentityService
    {
        private IHttpContextAccessor _context; 

        public IdentityService(IHttpContextAccessor context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string GetUserIdentity()
        {
            return _context.HttpContext.User.FindFirstValue(ClaimConstants.ObjectId);
        }
        public string GetUserName()
        {
            return _context.HttpContext.User.FindFirstValue(ClaimConstants.Name);
        }
    }
}
