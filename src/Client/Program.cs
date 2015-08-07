namespace Client
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using log4net.Config;
    using MassTransit;
    using MassTransit.Context;
    using MassTransit.Log4NetIntegration.Logging;
    using MassTransit.Pipeline;
    using Sample.MessageTypes;


    class Program
    {
        static void Main()
        {
            ConfigureLogger();

            // MassTransit to use Log4Net
            Log4NetLogger.Use();

            IBusControl busControl = CreateBus();

            BusHandle busHandle = busControl.Start();

            try
            {
                IRequestClient<ISimpleRequest, ISimpleResponse> client = CreateRequestClient(busControl);

                for (;;)
                {
                    Console.Write("Enter customer id (quit exits): ");
                    string customerId = Console.ReadLine();
                    if (customerId == "quit")
                        break;

                    // this is run as a Task to avoid weird console application issues
                    Task.Run(async () =>
                    {
                        ISimpleResponse response = await client.Request(new SimpleRequest(customerId));

                        Console.WriteLine("Customer Name: {0}", response.CusomerName);
                    }).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception!!! OMG!!! {0}", ex);
            }
            finally
            {
                busHandle.Stop(TimeSpan.FromSeconds(30));
            }
        }


        static IRequestClient<ISimpleRequest, ISimpleResponse> CreateRequestClient(IBusControl busControl)
        {
            var serviceAddress = new Uri(ConfigurationManager.AppSettings["ServiceAddress"]);
            IRequestClient<ISimpleRequest, ISimpleResponse> client =
                new CustomRequestClient<ISimpleRequest, ISimpleResponse>(busControl, serviceAddress, TimeSpan.FromSeconds(10));

            return client;
        }

        static IBusControl CreateBus()
        {
            return
                Bus.Factory.CreateUsingRabbitMq(
                    x => x.Host(new Uri(ConfigurationManager.AppSettings["RabbitMQHost"]), h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    }));
        }

        static void ConfigureLogger()
        {
            const string logConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<log4net>
  <root>
    <level value=""INFO"" />
    <appender-ref ref=""console"" />
  </root>
  <appender name=""console"" type=""log4net.Appender.ColoredConsoleAppender"">
    <layout type=""log4net.Layout.PatternLayout"">
      <conversionPattern value=""%m%n"" />
    </layout>
  </appender>
</log4net>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(logConfig)))
            {
                XmlConfigurator.Configure(stream);
            }
        }


        class SimpleRequest :
            ISimpleRequest
        {
            readonly string _customerId;
            readonly DateTime _timestamp;

            public SimpleRequest(string customerId)
            {
                _customerId = customerId;
                _timestamp = DateTime.UtcNow;
            }

            public DateTime Timestamp
            {
                get { return _timestamp; }
            }

            public string CustomerId
            {
                get { return _customerId; }
            }
        }
    }


    class CustomRequestClient<TRequest, TResponse> :
        IRequestClient<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        readonly IBus _bus;
        readonly Lazy<Task<ISendEndpoint>> _requestEndpoint;
        readonly TimeSpan _timeout;

        /// <summary>
        /// Creates a message request client for the bus and endpoint specified
        /// </summary>
        /// <param name="bus">The bus instance</param>
        /// <param name="address">The service endpoint address</param>
        /// <param name="timeout">The request timeout</param>
        public CustomRequestClient(IBus bus, Uri address, TimeSpan timeout)
        {
            _bus = bus;
            _timeout = timeout;
            _requestEndpoint = new Lazy<Task<ISendEndpoint>>(async () => await _bus.GetSendEndpoint(address));
        }

        async Task<TResponse> IRequestClient<TRequest, TResponse>.Request(TRequest request,
            CancellationToken cancellationToken)
        {
            TaskScheduler taskScheduler = SynchronizationContext.Current == null
                ? TaskScheduler.Default
                : TaskScheduler.FromCurrentSynchronizationContext();

            Task<TResponse> responseTask = null;
            var pipe = new SendRequest<TRequest>(_bus, taskScheduler, x =>
            {
                x.Timeout = _timeout;

                responseTask = x.Handle<TResponse>();
            });

            var headerPipe = new HeaderPipe(pipe);

            ISendEndpoint endpoint = await _requestEndpoint.Value;

            await endpoint.Send(request, headerPipe, cancellationToken);

            return await responseTask;
        }


        class HeaderPipe :
            IPipe<SendContext<TRequest>>
        {
            readonly IPipe<SendContext<TRequest>> _pipe;

            public HeaderPipe(IPipe<SendContext<TRequest>> pipe)
            {
                _pipe = pipe;
            }

            public Task Send(SendContext<TRequest> context)
            {
                context.Headers.Set("Key", "MySecretKey");

                return _pipe.Send(context);
            }

            public void Probe(ProbeContext context)
            {
            }
        }
    }
}