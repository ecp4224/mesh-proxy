using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MeshProxy.Utils
{
    public static class AsyncShellCommand
    {
        public static async Task<string> ExecuteWithOutput(string command)
        {
            string file = command.Split(' ')[0];
            string args = command.Substring(file.Length + 1);

            Console.WriteLine("Executing " + command);

            using (Process proc = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = file,
                    Arguments = args
                }
            })
            {
                return await RunProcessAsyncWithOutput(proc);
            }
        }
        
        public static async Task<int> Execute(string command)
        {
            string file = command.Split(' ')[0];
            string args = command.Substring(file.Length + 1);

            Console.WriteLine("Executing " + command);

            using (Process proc = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = file,
                    Arguments = args
                }
            })
            {
                return await RunProcessAsync(proc);
            }
        }

        public static Task<int> RunProcessAsync(Process process)
        {
            var tcs = new TaskCompletionSource<int>();

            process.EnableRaisingEvents = true;
            process.Exited += (s, ea) => tcs.SetResult(process.ExitCode);
            process.OutputDataReceived += (s, ea) => Console.WriteLine(ea.Data);
            process.ErrorDataReceived += (s, ea) => Console.WriteLine("ERR: " + ea.Data);

            bool started = process.Start();
            if (!started)
            {
                //you may allow for the process to be re-used (started = false) 
                //but I'm not sure about the guarantees of the Exited event in such a case
                throw new InvalidOperationException("Could not start process: " + process);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
        
        public static Task<string> RunProcessAsyncWithOutput(Process process)
        {
            var tcs = new TaskCompletionSource<string>();

            string totalOutput = "";
            process.EnableRaisingEvents = true;
            process.Exited += (s, ea) => tcs.SetResult(totalOutput);
            process.OutputDataReceived += (s, ea) => totalOutput += ea.Data;
            process.ErrorDataReceived += (s, ea) => totalOutput += "ERR: " + ea.Data;

            bool started = process.Start();
            if (!started)
            {
                //you may allow for the process to be re-used (started = false) 
                //but I'm not sure about the guarantees of the Exited event in such a case
                throw new InvalidOperationException("Could not start process: " + process);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}