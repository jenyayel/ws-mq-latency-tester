using IBM.WMQ;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQLatencyTester
{
    class Program
    {

        private const int WAIT_TIMEOUT = 100;

        static void Main(string[] args)
        {
            var stopper = new Stopwatch();
            stopper.Start();


            AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
            var options = new Options();

            if (!CommandLine.Parser.Default.ParseArguments(args, options))
                return;

            var _prop = new Hashtable();
            _prop.Add(MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED);
            _prop.Add(MQC.HOST_NAME_PROPERTY, options.Host);
            _prop.Add(MQC.PORT_PROPERTY, options.Port);
            _prop.Add(MQC.CHANNEL_PROPERTY, "SYSTEM.DEF.SVRCONN");

            if (!String.IsNullOrEmpty(options.User))
            {
                _prop.Add(MQC.USER_ID_PROPERTY, options.User);
                _prop.Add(MQC.PASSWORD_PROPERTY, options.Password);
            }

            MQQueueManager manager = null;
            try
            {
                Console.Write("Connecting to {0}:{1}...", _prop[MQC.HOST_NAME_PROPERTY], _prop[MQC.PORT_PROPERTY]);
                manager = new MQQueueManager(options.QueueManagerName, _prop);
                Console.WriteLine(" connected");

                Console.WriteLine("Starting {0} threads...", options.ThreadsCount);
                var tasks = new List<Task>();
                var cancelToken = new CancellationTokenSource();

                // threads creation
                for (int i = 0; i < options.ThreadsCount; i++)
                    tasks.Add(startTask(manager, options.QueueName, i + 1, cancelToken));

                Console.WriteLine("You can hit any key now to exit");
                Console.ReadKey();
                Console.WriteLine("Stopping...");

                // abort work on tasks
                cleanUp(tasks, cancelToken);

            }
            catch (MQException ex)
            {
                Console.WriteLine("MQException error code: {0} message: {0}", ex.ReasonCode, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("General error {0}", ex.Message);
            }
            finally
            {
                if (manager != null)
                    manager.Disconnect();
            }

            stopper.Stop();

            Console.WriteLine("\nDone. All in all was up: {0}", stopper.Elapsed);
        }

        private static Task startTask(MQQueueManager manager, string queueName, int taskNumber, CancellationTokenSource token)
        {
            long messageCount = 0;
            TimeSpan maxLatencyTime = TimeSpan.MinValue;
            var options = new MQGetMessageOptions() { WaitInterval = WAIT_TIMEOUT };

            return Task.Factory.StartNew(() =>
            {
                Console.WriteLine("#{0}:\tTask started", taskNumber);

                while (!token.IsCancellationRequested)
                {
                    var message = new MQMessage();

                    try
                    {
                        // the actual reading of message
                        manager
                            .AccessQueue(queueName, MQC.MQOO_INPUT_AS_Q_DEF + MQC.MQOO_FAIL_IF_QUIESCING)
                            .Get(message, options);
                        messageCount++;
                    }
                    catch (MQException ex)
                    {
                        if (ex.ReasonCode != 2033)
                            // unless there is no message - code 2033
                            Console.WriteLine("#{0}:\tError reading message code: {1} message: {2}",
                                taskNumber, ex.ReasonCode, ex.Message);
                        continue; 
                    }

                    // decode timestamp of message when it was putted in source queue
                    var timestamp = DateTime.ParseExact(
                        ASCIIEncoding.ASCII.GetString(message.MQMD.PutDate) +
                        ASCIIEncoding.ASCII.GetString(message.MQMD.PutTime),
                        "yyyyMMddHHmmssff", CultureInfo.InvariantCulture);

                    var latency = DateTime.UtcNow - timestamp;

                    if (latency > maxLatencyTime || messageCount % 100 == 0)
                    {
                        // will print only on each 100 messages or when the larger latency detected
                        if (latency > maxLatencyTime)
                            maxLatencyTime = latency;

                        Console.WriteLine("#{0}:\tMax latency time after {1} messages is {2}",
                            taskNumber, messageCount, maxLatencyTime);
                    }
                }

            }, token.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private static void cleanUp(List<Task> tasks, CancellationTokenSource cancelToken)
        {
            // cancel tasks
            cancelToken.Cancel();

            try
            {
                // wait until all tasks are finished 
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException e)
            {
                foreach (var inner in e.InnerExceptions)
                {
                    if (inner is TaskCanceledException)
                        continue;
                    else
                        Console.WriteLine("Exception while stopping: {0} {1}", inner.GetType().Name, inner.Message);
                }
            }
            finally
            {
                tasks = null;
                cancelToken.Dispose();
            }
        }
    }
}
