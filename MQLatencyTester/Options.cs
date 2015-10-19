using CommandLine;
using CommandLine.Text;

namespace MQLatencyTester
{
    class Options
    {
        [Option('h', "host", Required = true, DefaultValue = "localhost", HelpText = "The host address of WebSphere MQ")]
        public string Host { get; set; }

        [Option('r', "port", Required = true, DefaultValue = 1414, HelpText = "The port of MQ manager listens to")]
        public int Port { get; set; }

        [Option('u', "user", HelpText = "User id property")]
        public string User { get; set; }

        [Option('p', "password", DefaultValue = "", HelpText = "Password property")]
        public string Password { get; set; }

        [Option('m', "manager", Required = true, HelpText = "The name of the queue manager")]
        public string QueueManagerName { get; set; }

        [Option('q', "queue", Required = true, HelpText = "The name of the queue")]
        public string QueueName { get; set; }

        [Option('t', "threads", DefaultValue = 1, HelpText = "Number of threads to read from queue")]
        public int ThreadsCount { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
