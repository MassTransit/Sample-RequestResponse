# Request/Response Sample

This sample shows a very simple service which handles a request and responds. The client is a console application that generates the request and using the request client to handle the request/response interaction.

## Requirements

This sample using .NET 4.5, MassTransit 3, and RabbitMQ.

The sample is configured having the following assumptions in mind:
 1. RabbitMQ is available on localhost
 2. Default RMQ credentials (guest/guest) can be used
 3. There is a virtual host `test`, to which the `guest` user has full access
