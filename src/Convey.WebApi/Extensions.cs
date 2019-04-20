using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Convey.WebApi.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Convey.WebApi
{
    public static class Extensions
    {        
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private const string EmptyJsonObject = "{}";
        private const string LocationHeader = "Location";
        private const string JsonContentType = "application/json";

        public static IApplicationBuilder UseEndpoints(this IApplicationBuilder app, Action<IEndpointsBuilder> builder)
            => app.UseRouter(router => builder(new EndpointsBuilder(router)));
        
        public static IConveyBuilder AddWebApi(this IConveyBuilder builder)
        {
            builder.Services.AddRouting()
                .AddLogging()
                .AddMvcCore()
                .AddJsonFormatters();

            return builder;
        }
        
        public static Task Ok(this HttpResponse response, object data = null)
        {
            response.StatusCode = 200;
            if (!(data is null))
            {
                response.WriteJson(data);
            }

            return Task.CompletedTask;
        }

        public static Task Created(this HttpResponse response, string location = null)
        {
            response.StatusCode = 201;
            if (string.IsNullOrWhiteSpace(location))
            {
                return Task.CompletedTask;
            }

            if (!response.Headers.ContainsKey(LocationHeader))
            {
                response.Headers.Add(LocationHeader, location);
            }

            return Task.CompletedTask;
        }

        public static Task Accepted(this HttpResponse response)
        {
            response.StatusCode = 202;
            return Task.CompletedTask;
        }

        public static Task NoContent(this HttpResponse response)
        {
            response.StatusCode = 204;
            return Task.CompletedTask;
        }
        
        public static Task BadRequest(this HttpResponse response)
        {
            response.StatusCode = 400;
            return Task.CompletedTask;
        }
        
        public static Task NotFound(this HttpResponse response)
        {
            response.StatusCode = 404;
            return Task.CompletedTask;
        }
        
        public static void WriteJson<T>(this HttpResponse response, T obj)
        {
            response.ContentType = JsonContentType;
            using (var writer = new HttpResponseStreamWriter(response.Body, Encoding.UTF8))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.CloseOutput = false;
                    jsonWriter.AutoCompleteOnClose = false;
                    Serializer.Serialize(jsonWriter, obj);
                }
            }
        }

        public static T ReadJson<T>(this HttpContext httpContext)
        {
            using (var streamReader = new StreamReader(httpContext.Request.Body))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                var obj = Serializer.Deserialize<T>(jsonTextReader);
                var results = new List<ValidationResult>();
                if (Validator.TryValidateObject(obj, new ValidationContext(obj), results))
                {
                    return obj;
                }

                httpContext.Response.StatusCode = 400;
                httpContext.Response.WriteJson(results);

                return default(T);
            }
        }
        
        public static T ReadQuery<T>(this HttpContext context) where T : class
        {
            var request = context.Request;
            RouteValueDictionary values = null;
            if (HasRouteData(request))
            {
                values = request.HttpContext.GetRouteData().Values;
            }

            if (HasQueryString(request))
            {
                var queryString = HttpUtility.ParseQueryString(request.HttpContext.Request.QueryString.Value);
                values = values ?? new RouteValueDictionary();
                foreach (var key in queryString.AllKeys)
                {
                    values.TryAdd(key, queryString[key]);
                }
            }

            return values is null
                ? JsonConvert.DeserializeObject<T>(EmptyJsonObject)
                : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(values));
        }

        private static bool HasQueryString(this HttpRequest request)
            => request.Query.Any();

        private static bool HasRouteData(this HttpRequest request)
            => request.HttpContext.GetRouteData().Values.Any();
    }
}