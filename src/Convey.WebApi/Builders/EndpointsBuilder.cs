using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Convey.WebApi.Builders
{
    public class EndpointsBuilder : IEndpointsBuilder
    {
        private readonly IRouteBuilder _routeBuilder;

        public EndpointsBuilder(IRouteBuilder routeBuilder)
        {
            _routeBuilder = routeBuilder;
        }

        public IEndpointsBuilder Get(string path, Func<HttpContext, Task> context = null)
        {
            _routeBuilder.MapGet(path, (req, res, data) => context?.Invoke(req.HttpContext));

            return this;
        }

        public IEndpointsBuilder Get<T>(string path, Func<T, HttpContext, Task> context = null) where T : class
        {
            _routeBuilder.MapGet(path, (req, res, data) => BuildQueryContext(req, context));

            return this;
        }

        public IEndpointsBuilder Post(string path, Func<HttpContext, Task> context = null)
        {
            _routeBuilder.MapPost(path, (req, res, data) => context?.Invoke(req.HttpContext));

            return this;
        }

        public IEndpointsBuilder Post<T>(string path, Func<T, HttpContext, Task> context = null) where T : class
        {
            _routeBuilder.MapPost(path, (req, res, data) => BuildRequestContext(req, context));

            return this;
        }

        public IEndpointsBuilder Put(string path, Func<HttpContext, Task> context = null)
        {
            _routeBuilder.MapPut(path, (req, res, data) => context?.Invoke(req.HttpContext));

            return this;
        }

        public IEndpointsBuilder Put<T>(string path, Func<T, HttpContext, Task> context = null) where T : class
        {
            _routeBuilder.MapPut(path, (req, res, data) => BuildRequestContext(req, context));

            return this;
        }

        public IEndpointsBuilder Delete(string path, Func<HttpContext, Task> context = null)
        {
            _routeBuilder.MapDelete(path, (req, res, data) => context?.Invoke(req.HttpContext));

            return this;
        }

        public IEndpointsBuilder Delete<T>(string path, Func<T, HttpContext, Task> context = null) where T : class
        {
            _routeBuilder.MapDelete(path, (req, res, data) => BuildQueryContext(req, context));

            return this;
        }

        private static Task BuildRequestContext<T>(HttpRequest req, Func<T, HttpContext, Task> context = null)
            where T : class
        {
            var httpContext = req.HttpContext;
            var request = httpContext.ReadJson<T>();

            return context?.Invoke(request, httpContext);
        }

        private static Task BuildQueryContext<T>(HttpRequest req, Func<T, HttpContext, Task> context = null)
            where T : class
        {
            var httpContext = req.HttpContext;
            var request = httpContext.ReadQuery<T>();

            return context?.Invoke(request, httpContext);
        }
    }
}