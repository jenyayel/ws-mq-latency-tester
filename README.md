# IBM MQ message latency tester
Tests the latency of message from original queue until received by client by comparing header's timestamp and time when receieved on client.

### Usage

To run for local MQ manager with queue name "TestQueue" and single thread:
```
MQLatencyTester -h localhost -m MQManager -q TestQueue
```

For a full list of options:
```
MQLatencyTester --help
```
