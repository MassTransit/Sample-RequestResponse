namespace RequestService
{
    using System;
    using System.Configuration;
    using MassTransit;
    using MassTransit.RabbitMqTransport;
    using Topshelf;
    using Topshelf.Logging;


    class RequestService :
        ServiceControl
    {
        readonly LogWriter _log = HostLogger.Get<RequestService>();

        IBusControl _busControl;
        BusHandle _busHandle;

        public bool Start(HostControl hostControl)
        {
            _log.Info("Creating bus...");

            _busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                IRabbitMqHost host = x.Host(new Uri(ConfigurationManager.AppSettings["RabbitMQHost"]), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                x.ReceiveEndpoint(host, ConfigurationManager.AppSettings["ServiceQueueName"],
                    e => { e.Consumer<RequestConsumer>(); });
            });

            _log.Info("Starting bus...");

            _busHandle = _busControl.Start();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _log.Info("Stopping bus...");

            if (_busHandle != null)
                _busHandle.Stop(TimeSpan.FromSeconds(30));

            return true;
        }
    }
}