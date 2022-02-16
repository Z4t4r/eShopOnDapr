
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Identity.Web;

namespace Microsoft.eShopOnContainers.Services.Ordering.API.Infrastructure.Services
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
