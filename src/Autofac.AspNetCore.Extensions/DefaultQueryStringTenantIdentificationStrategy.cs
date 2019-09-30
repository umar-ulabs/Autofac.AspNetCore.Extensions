﻿using Autofac.Integration.AspNetCore.Multitenant;
using Autofac.Multitenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;

namespace Autofac.AspNetCore.Extensions
{
    public class DefaultQueryStringTenantIdentificationStrategy : ITenantIdentificationStrategy
    {
        private readonly ILogger<DefaultQueryStringTenantIdentificationStrategy> _logger;

        public DefaultQueryStringTenantIdentificationStrategy(IHttpContextAccessor accessor, ILogger<DefaultQueryStringTenantIdentificationStrategy> logger)
        {
            this.Accessor = accessor;
            this._logger = logger;
        }

        public IHttpContextAccessor Accessor { get; private set; }

        public bool TryIdentifyTenant(out object tenantId)
        {
            var context = this.Accessor.HttpContext;
            if (context == null)
            {
                // No current HttpContext. This happens during app startup
                // and isn't really an error, but is something to be aware of.
                tenantId = null;
                return false;
            }

            // Caching the value both speeds up tenant identification for
            // later and ensures we only see one log message indicating
            // relative success or failure for tenant ID.
            if (context.Items.TryGetValue("_tenantId", out tenantId))
            {
                // We've already identified the tenant at some point
                // so just return the cached value (even if the cached value
                // indicates we couldn't identify the tenant for this context).
                return tenantId != null;
            }

            var temp = MapToTenantId(context, GetValueFromRequest(context));
            if (temp != null)
            {
                tenantId = temp;
                context.Items["_tenantId"] = tenantId;
                this._logger.LogInformation("Identified tenant from query string: {tenant}", tenantId);
                return true;
            }

            this._logger.LogWarning("Unable to identify tenant from query string.");
            tenantId = null;
            context.Items["_tenantId"] = null;
            return false;
        }

        public virtual string GetValueFromRequest(HttpContext context)
        {
            StringValues tenantValues;
            if (context.Request.Query.TryGetValue("tenant", out tenantValues))
                return tenantValues[0];

            return null;
        }

        public virtual string MapToTenantId(HttpContext context, string value)
        {
            var mtc = context.RequestServices.GetRequiredService<IServiceProvider>().GetAutofacMultitenantRoot();
            return mtc.GetTenants().Any(t => t.Equals(value)) ? value : null;
        }

    }
}
