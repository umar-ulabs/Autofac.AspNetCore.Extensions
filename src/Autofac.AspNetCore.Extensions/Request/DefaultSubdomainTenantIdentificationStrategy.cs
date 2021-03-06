﻿using Autofac.Integration.AspNetCore.Multitenant;
using Autofac.Multitenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace Autofac.AspNetCore.Extensions
{
    public class DefaultSubdomainTenantIdentificationStrategy : ITenantIdentificationStrategy
    {
        private readonly ILogger<DefaultSubdomainTenantIdentificationStrategy> _logger;
        private readonly AutofacMultitenantOptions _options;
        public DefaultSubdomainTenantIdentificationStrategy(IHttpContextAccessor accessor, ILogger<DefaultSubdomainTenantIdentificationStrategy> logger, AutofacMultitenantOptions options)
        {
            this.Accessor = accessor;
            this._logger = logger;
            _options = options;
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

            var tenantSlug = GetTenantSlugFromRequest(context);
            var temp = MapTenantSlugToTenantId(context, tenantSlug);
            if (temp != null)
            {
                context.Items["_requestTenantId"] = temp;

                tenantId = MapTenantIdToContainerId(context, temp);
                context.Items["_tenantId"] = tenantId;

                this._logger.LogInformation("Identified tenant from host subdomain: {tenant}", temp);
                return true;
            }

            this._logger.LogDebug("Unable to identify tenant from host subdomain.");
            tenantId = null;

            context.Items["_tenantId"] = null;

            return false;
        }

        public virtual string GetTenantSlugFromRequest(HttpContext context)
        {
            var host = context.Request.Host.Value.Replace("www.", "");
            var hostWithoutPort = host.Split(':')[0];
            var hostSplit = hostWithoutPort.Split('.');

            if ((hostSplit.Length == 3 && hostSplit[2] != "localhost") || (hostSplit.Length == 2 && hostSplit[1] == "localhost"))
            {
                return hostSplit[0];
            }

            return null;
        }

        public virtual string MapTenantSlugToTenantId(HttpContext context, string tenantSlug)
        {
            if (tenantSlug is null)
                return null;

            return tenantSlug.ToLowerInvariant();
        }

        public virtual string MapTenantIdToContainerId(HttpContext context, string tenantId)
        {
            //null > new DefaultTenantId()
            //Anything not null will get used/added

            if (tenantId == null)
                return null;

            var mtc = context.RequestServices.GetRequiredService<MultitenantContainer>();
            return mtc.GetTenants().ToList().Any(t => string.Equals(t as string, tenantId, StringComparison.OrdinalIgnoreCase)) ? tenantId : null;
        }
    }
}
