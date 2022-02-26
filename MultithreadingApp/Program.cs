using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultithreadingApp
{
    class Program
    {
        public class TaskResult
        {
            public string Message { get; set; }
            public int ResourceNo { get; set; }
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("Processor Amount: " + Environment.ProcessorCount);

            int SHARED_COUNTER = 0;
            ConcurrentDictionary<int, int> BEING_USED_RESOURCE = new ConcurrentDictionary<int, int>();

            try
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();

                using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        concurrencySemaphore.Wait();

                        int threadNo = Interlocked.Increment(ref SHARED_COUNTER);

                        int resourceNo = threadNo % 2;
                        if (BEING_USED_RESOURCE.TryAdd(resourceNo, resourceNo))
                        {
                            // Starting a new thread
                            var t1 = Task.Factory.StartNew(() => RunMethod(threadNo, resourceNo, tokenSource.Token), TaskCreationOptions.AttachedToParent);

                            // Displaying result when success
                            t1.ContinueWith(x => {
                                Console.WriteLine(x.Result.Message);

                                if (x.Result.ResourceNo > -1)
                                {
                                    int tempVal;
                                    BEING_USED_RESOURCE.TryRemove(x.Result.ResourceNo, out tempVal);
                                }

                                try
                                {
                                    concurrencySemaphore.Release();
                                }
                                catch { }
                            }, TaskContinuationOptions.OnlyOnRanToCompletion);

                            // Displaying result when error
                            t1.ContinueWith(x => {
                                Console.WriteLine($"Task#{threadNo} has an error. Error => {t1.Exception.Message}");

                                try
                                {
                                    concurrencySemaphore.Release();
                                }
                                catch { }
                            }, TaskContinuationOptions.OnlyOnFaulted);
                        }
                        else
                        {
                            Console.WriteLine($"Task #{threadNo} CANNOT access resource #{resourceNo}");

                            try
                            {
                                Thread.Sleep(1000);
                                concurrencySemaphore.Release();
                            }
                            catch { }
                        }
                    }
                }

                // Version 1
                /*using(SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(2))
                {
                    int threadAmt = 5;
                    for (int i = 0; i < threadAmt; i++)
                    {
                        concurrencySemaphore.Wait();

                        int threadNo = i + 1;

                        // Starting a new thread
                        var t1 = Task.Factory.StartNew(() => RunMethod(threadNo, tokenSource.Token), TaskCreationOptions.AttachedToParent);

                        // Displaying result when success
                        t1.ContinueWith(x => {
                            Console.WriteLine(x.Result);

                            try
                            {
                                concurrencySemaphore.Release();
                            }
                            catch { }
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);

                        // Displaying result when error
                        t1.ContinueWith(x => {
                            Console.WriteLine($"Task#{threadNo} has an error. Error => {t1.Exception.Message}");

                            try
                            {
                                concurrencySemaphore.Release();
                            }
                            catch { }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }*/
            }
            catch (Exception ex)
            {
                string error = GetExceptionMessages(ex);
                Console.WriteLine(error);
            }

            Console.ReadKey();
        }

        private static T RunTask<T>(Func<T> function, CancellationTokenSource tokenSource, int timeoutInSecs = 0)
        {
            timeoutInSecs = timeoutInSecs == 0 ? 10 : timeoutInSecs;

            Task<T> task = Task.Run(function);
            if (task.Wait(TimeSpan.FromSeconds(timeoutInSecs)))
            {
                return task.Result;
            }
            else
            {
                tokenSource.Cancel();
                return default(T);
            }
        }

        private static string RunMethodV1(int taskNo, CancellationToken token)
        {
            int sec = 500;
            int sleepingTime = taskNo * sec;
            int loopAmt = taskNo * 5;

            try
            {
                for (int i = 0; i < loopAmt; i++)
                {
                    Thread.Sleep(sec);
                    ProcessTaskNo(taskNo, i + 1, token);

                    // *** Simulating an error
                    //if (i == 10) throw new Exception("ERROR!!!");
                }

                return "Task #" + taskNo + " is done.";
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine("Killing task #" + taskNo);
                return string.Empty;
            }
        }

        private static TaskResult RunMethod(int taskNo, int resourceNo, CancellationToken token)
        {
            try
            {
                Thread.Sleep(1000);
                return new TaskResult() {
                    Message = $"Task #{taskNo} is using resource #{resourceNo}",
                    ResourceNo = resourceNo
                };
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine("Killing task #" + taskNo);
                return new TaskResult() { Message = ex.Message, ResourceNo = -1 };
            }
        }

        private static void ProcessTaskNo(int taskNo, int loopNo, CancellationToken token)
        {
            CheckCancellationRequest(token);

            Console.WriteLine($"Task #{taskNo}.{loopNo} is processing.");
        }

        private static void CheckCancellationRequest(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
        }

        private static string GetExceptionMessages(Exception exception, int exceptionNo = 1)
        {
            try
            {
                StringBuilder msg = new StringBuilder();
                msg.Append("Exception #" + exceptionNo + " => " + exception.Message + "\r\n");

                for (int i = 1; i < exceptionNo; i++)
                    msg.Append("\t");
                msg.Append("Stacktrace #" + exceptionNo + " => " + exception.StackTrace.Replace(Environment.NewLine, "\t"));

                if (exception.InnerException != null)
                {
                    msg.Append("\r\n");
                    for (int i = 0; i < exceptionNo; i++)
                        msg.Append("\t");

                    msg.Append("Inner ");
                    msg.Append(GetExceptionMessages(exception.InnerException, exceptionNo + 1));
                }

                return msg.ToString();
            }
            catch (Exception)
            {
                return "Failed getting exception messages.";
            }
        }
    }
}
