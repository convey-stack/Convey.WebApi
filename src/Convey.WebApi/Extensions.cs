using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Convey.WebApi.Builders;
using Convey.WebApi.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Convey.WebApi
{
    public static class Extensions
    {        
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private const string SectionName = "webapi";
        private const string RegistryName = "webapi";
        private const string EmptyJsonObject = "{}";
        private const string LocationHeader = "Location";
        private const string JsonContentType = "application/json";

        public static IApplicationBuilder UseEndpoints(this IApplicationBuilder app, Action<IEndpointsBuilder> builder)
            => app.UseRouter(router => builder(new EndpointsBuilder(router)));
        
        public static IConveyBuilder AddWebApi(this IConveyBuilder builder, string sectionName = SectionName)
        {
            if (!builder.TryRegister(RegistryName))
            {
                return builder;
            }

            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.AddRouting()
                .AddLogging()
                .AddMvcCore()
                .AddJsonFormatters()
                .AddDataAnnotations()
                .AddApiExplorer()
                .AddDefaultJsonOptions()
                .AddAuthorization();

            return builder;
        }
        
        private static IMvcCoreBuilder AddDefaultJsonOptions(this IMvcCoreBuilder builder)
            => builder.AddJsonOptions(o =>
            {
                o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                o.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                o.SerializerSettings.DateParseHandling = DateParseHandling.DateTimeOffset;
                o.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.None;
                o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                o.SerializerSettings.Formatting = Formatting.Indented;
                o.SerializerSettings.Converters.Add(new StringEnumConverter());
            });
        
        public static IApplicationBuilder UseErrorHandler(this IApplicationBuilder builder)
            => builder.UseMiddleware<ErrorHandlerMiddleware>();

        public static IApplicationBuilder UseAllForwardedHeaders(this IApplicationBuilder builder)
            => builder.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });
        
        public static T Bind<T>(this T model, Expression<Func<T, object>> expression, object value)
            => model.Bind<T, object>(expression, value);

        public static T BindId<T>(this T model, Expression<Func<T, Guid>> expression)
            => model.Bind(expression, Guid.NewGuid());
        
        public static T BindId<T>(this T model, Expression<Func<T, string>> expression)
            => model.Bind(expression, Guid.NewGuid().ToString("N"));

        private static TModel Bind<TModel, TProperty>(this TModel model, Expression<Func<TModel, TProperty>> expression,
            object value)
        {
            if (!(expression.Body is MemberExpression memberExpression))
            {
                memberExpression = ((UnaryExpression) expression.Body).Operand as MemberExpression;
            }

            if (memberExpression is null)
            {
                throw new InvalidOperationException("Invalid member expression.");
            }

            var propertyName = memberExpression.Member.Name.ToLowerInvariant();
            var modelType = model.GetType();
            var field = modelType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .SingleOrDefault(x => x.Name.ToLowerInvariant().StartsWith($"<{propertyName}>"));
            if (field == null)
            {
                return model;
            }

            field.SetValue(model, value);

            return model;
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
        
        public static Task InternalServerError(this HttpResponse response)
        {
            response.StatusCode = 500;
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