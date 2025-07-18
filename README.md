# RabbitmqPOC
## 1. Prerequisites
Install RabbitMQ on your machine.

## 2. How to Run the Projects
Start the Switch project.

Start one or more instances of the Uplink (Pos) project.

## 3. Project Overview
### Switch
Creates a queue named Switch_Pos_Incoming and binds it to the Pos_Incoming exchange.

Listens for incoming messages from uplinks (Pos instances).

Responds to each uplink by sending a message to the Pos_Outgoing exchange.

Uses the correlationId (received from the request) as the routing key to send the response back to the correct Pos instance.

###Pos (Uplink)
Each instance generates a unique instance ID.

Every second, it sends a request message to the Pos_Incoming exchange, using its instance ID as the correlationId.

It creates a temporary queue bound to the Pos_Outgoing exchange (type: direct), using its instance ID as the routing key.

Listens on this queue to receive responses from the Switch.

Note: When the Uplink shuts down, its temporary queue is automatically deleted by RabbitMQ.