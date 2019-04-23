using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Convey.WebApi.Requests
{
    public class RequestDispatcher : IRequestDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public RequestDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task<TResult> DispatchAsync<TRequest, TResult>(TRequest request) where TRequest : class, IRequest
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var handler = scope.ServiceProvider.GetService<IRequestHandler<TRequest, TResult>>();
                if (handler is null)
                {
                    throw new InvalidOperationException($"Request handler for: '{request}' was not found.");
                }

                return handler.HandleAsync(request);
            }
        }
    }
}